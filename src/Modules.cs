using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

namespace kok {
    // Every bridge action is registered, including the compatibility module.
    public sealed class ActionSpec {
        public string Name,Module,CapabilityId,Title,Kind,UiCommand,When="";
        public Need? Requires;public bool? Writes;public bool Emergency;public string[] Surfaces=new string[0];
        public Dictionary<string,object> Schema;
        public Action<Dictionary<string,object>> Validate;
        public Func<ModuleContext,Dictionary<string,object>,Task<object>> Execute;
    }
    public sealed class Capability {public string id,module,title,kind,uiCommand,when;public Need requires;public string[] surfaces;public ActionSpec[] actions;public AgentToolSpec tool;}
    public sealed class FacetSpec {public string Name;public string[] Fields=new string[0];public int Version=1;}
    public interface IKokModule {
        string Name {get;} string[] Kinds {get;}
        IEnumerable<ActionSpec> Actions {get;}
        IEnumerable<AgentToolSpec> AgentTools {get;}
        IEnumerable<FacetSpec> Facets {get;}
        bool Required {get;}bool Ui {get;}
    }
    public sealed class KokModule:IKokModule {
        public string Name {get;set;} public string[] Kinds {get;set;}
        public IEnumerable<ActionSpec> Actions {get;set;}
        public IEnumerable<AgentToolSpec> AgentTools {get;set;}
        public IEnumerable<FacetSpec> Facets {get;set;}
        public bool Required {get;set;}public bool Ui {get;set;}
    }
    // Built-in modules only. Registration does not load DLLs, scripts or external code.
    public sealed class ModuleRegistry {
        readonly Dictionary<string,IKokModule> modules=new Dictionary<string,IKokModule>();
        readonly Dictionary<string,ActionSpec> actions=new Dictionary<string,ActionSpec>();
        readonly Dictionary<string,AgentToolSpec> tools=new Dictionary<string,AgentToolSpec>();
        public IEnumerable<IKokModule> Modules {get{return modules.Values;}}
        public AgentToolSpec[] Tools {get{return tools.Values.ToArray();}}
        public IEnumerable<Capability> Capabilities {get{
            foreach(var id in actions.Values.Select(a=>a.CapabilityId).Concat(tools.Values.Select(t=>t.CapabilityId)).Distinct()){
                var aa=actions.Values.Where(a=>a.CapabilityId==id).ToArray();var t=tools.Values.FirstOrDefault(x=>x.CapabilityId==id);var a=aa.FirstOrDefault(x=>x.UiCommand!=null)??aa.FirstOrDefault();
                yield return new Capability{id=id,module=a!=null?a.Module:t.Module,title=a!=null?a.Title:t.Title,kind=aa.Any(x=>x.Writes==true)||t!=null&&t.Write?"write":a!=null?a.Kind:"read",requires=a!=null?a.Requires.Value:t.Requires.Value,surfaces=aa.SelectMany(x=>x.Surfaces).Concat(t==null?new string[0]:new[]{"agent"}).Distinct().ToArray(),actions=aa,tool=t,when=a==null?"":a.When,uiCommand=aa.Select(x=>x.UiCommand).FirstOrDefault(x=>x!=null)};
            }
        }}
        public void Register(IKokModule module){
            if(module==null||String.IsNullOrWhiteSpace(module.Name)||modules.ContainsKey(module.Name))throw new InvalidDataException("模块注册无效。");
            var aa=(module.Actions??Enumerable.Empty<ActionSpec>()).ToArray();var tt=(module.AgentTools??Enumerable.Empty<AgentToolSpec>()).ToArray();
            var fields=(module.Facets??Enumerable.Empty<FacetSpec>()).SelectMany(f=>f.Fields??new string[0]).ToArray();if(fields.Distinct().Count()!=fields.Length||fields.Any(f=>FieldOwner(f)!=null))throw new InvalidDataException("字段已由其他模块管理。");
            if(aa.Where(a=>a!=null&&a.CapabilityId!=null).Select(a=>a.CapabilityId).Concat(tt.Where(t=>t!=null&&t.CapabilityId!=null).Select(t=>t.CapabilityId)).Any(id=>Capabilities.Any(c=>c.id==id)))throw new InvalidDataException("能力已由其他模块管理。");
            if(aa.Any(a=>a==null||String.IsNullOrEmpty(a.Name)||a.Execute==null||a.Schema==null||!a.Requires.HasValue||!a.Writes.HasValue||!new[]{"read","write","open"}.Contains(a.Kind))||aa.Select(a=>a.Name).Distinct().Count()!=aa.Length||aa.Any(a=>actions.ContainsKey(a.Name))||tt.Any(t=>t==null||String.IsNullOrEmpty(t.Name)||t.Properties==null||t.Required==null||!t.Requires.HasValue)||tt.Select(t=>t.Name).Distinct().Count()!=tt.Length||tt.Any(t=>tools.ContainsKey(t.Name)))throw new InvalidDataException("模块声明缺少校验、权限或存在重复动作。");
            modules.Add(module.Name,module);foreach(var a in aa){a.Module=module.Name;a.CapabilityId=a.CapabilityId??module.Name+"."+a.Name;a.Title=a.Title??a.Name;actions.Add(a.Name,a);}foreach(var t in tt){t.Module=module.Name;t.CapabilityId=t.CapabilityId??module.Name+"."+t.Name;t.Title=t.Title??t.Name;tools.Add(t.Name,t);}
        }
        public bool Handles(string action){return actions.ContainsKey(action);}
        public string FieldOwner(string field){return Modules.Where(m=>(m.Facets??Enumerable.Empty<FacetSpec>()).Any(f=>(f.Fields??new string[0]).Contains(field))).Select(m=>m.Name).FirstOrDefault();}
        public async Task<object> Dispatch(ModuleContext context,string name,Dictionary<string,object> args){ActionSpec a;if(!actions.TryGetValue(name,out a))throw new InvalidDataException("未知模块动作。");context.Token.ThrowIfCancellationRequested();if(name=="openPreview"&&args.ContainsKey("id")){var item=context.Library.Find(Convert.ToString(args["id"]));if(item!=null)context.Policy.Ensure(Need.None,item.ext=="PPTX"?"presentations":item.kind=="document"?"documents":item.kind=="book"?"reading":item.kind);}context.Policy.Ensure(a.Requires.Value,a.Module,a.Emergency);CapabilitySchema.Check(a.Schema,args);if(context.Jobs!=null&&name.StartsWith("cancel",StringComparison.Ordinal))context.Jobs.RequestCancellation(name,args);if(a.Validate!=null)a.Validate(args);var result=context.Jobs==null?await a.Execute(context,args):await context.Jobs.RunAction(context,a,args);if(context.Jobs!=null)context.Jobs.RecordResult(result);return result;}
        public bool ToolEnabled(string name,Library library){AgentToolSpec t;return tools.TryGetValue(name,out t)&&library.ModuleEnabled(t.Module);}
        public object Catalog(ModuleContext ctx){return new{contractVersion=2,modules=Modules.Select(m=>new{id=m.Name,kinds=m.Kinds??new string[0],actions=(m.Actions??Enumerable.Empty<ActionSpec>()).Select(a=>a.Name).ToArray(),facets=m.Facets,required=m.Required,ui=m.Ui,enabled=ctx.Library.ModuleEnabled(m.Name)}),capabilities=Capabilities.Select(c=>new{id=c.id,module=c.module,title=c.title,kind=c.kind,when=c.when,requires=c.requires.ToString(),surfaces=c.surfaces,uiCommand=c.uiCommand,available=ctx.Policy.Allows(c.requires,c.module),actions=c.actions.Select(a=>new{name=a.Name,schema=a.Schema,requires=a.Requires.ToString(),writes=a.Writes}),agent=c.tool==null?null:new{name=c.tool.Name,schema=c.tool.Schema()}}),tools=Tools.Where(t=>ToolEnabled(t.Name,ctx.Library)).Select(t=>new{name=t.Name,title=t.Title,module=t.Module,write=t.Write}),legacyActions=Modules.Where(m=>m.Name=="legacy").SelectMany(m=>m.Actions).Count()};}
        static readonly Lazy<ModuleRegistry> builtins=new Lazy<ModuleRegistry>(ModuleDefinitions.Create);
        public static ModuleRegistry Builtins {get{return builtins.Value;}}
    }
    public partial class Library {
        public bool ModuleEnabled(string id){lock(gate){object value;return !Db.settings.TryGetValue("disabledModules",out value)||!Json.Deserialize<List<string>>(Json.Serialize(value)).Contains(id);}}
        public void SetModuleEnabled(string id,bool enabled){var m=ModuleRegistry.Builtins.Modules.FirstOrDefault(x=>x.Name==id);if(m==null||m.Required)throw new InvalidDataException("此模块不能停用。");lock(gate){object value;var list=Db.settings.TryGetValue("disabledModules",out value)?Json.Deserialize<List<string>>(Json.Serialize(value)):new List<string>();if(enabled)list.Remove(id);else if(!list.Contains(id))list.Add(id);Db.settings["disabledModules"]=list;Save();}}
    }
}
