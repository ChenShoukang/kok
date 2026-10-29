using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace kok {
 internal static class RecipePlans {
  internal sealed class PlanStep {public string title,module;public bool writes,ai;}
  internal sealed class Plan {public string id,name,version;public int aiCalls,maxInputTokens,maxOutputTokens;public List<string> problems=new List<string>();public List<PlanStep> steps=new List<PlanStep>();}
  public static Plan Preview(ModuleContext c,string id){
   var recipe=((WorkspaceState)c.Library.WorkspaceSnapshot()).recipes.FirstOrDefault(r=>r.id==id);if(recipe==null)throw new InvalidDataException("配方内容无效。");
   var plan=new Plan{id=id,name=recipe.name};var inputs=new List<object>();
   for(int n=0;n<recipe.steps.Count;n++){
    var step=recipe.steps[n];var cap=ModuleRegistry.Builtins.Capabilities.FirstOrDefault(k=>k.id==step.capability&&k.surfaces.Contains("recipe"));
    if(cap==null){plan.problems.Add("配方包含不可用能力。");continue;}
    var action=cap.actions.First(a=>a.UiCommand==null);bool ai=(cap.requires&Need.Ai)!=0;
    plan.steps.Add(new PlanStep{title=cap.title,module=cap.module,writes=cap.kind=="write",ai=ai});
    try{c.Policy.Ensure(cap.requires,cap.module);}catch(InvalidOperationException e){plan.problems.Add(e.Message);}
    if(ai){plan.aiCalls++;plan.maxOutputTokens+=1200;object text;plan.maxInputTokens+=step.args.TryGetValue("text",out text)&&text is string&&!((string)text).StartsWith("$previous.",StringComparison.Ordinal)?System.Text.Encoding.UTF8.GetByteCount(RecipeText.Excerpt((string)text))+4096:164096;}
    foreach(var pair in step.args){if(pair.Value is string&&((string)pair.Value).StartsWith("$previous.",StringComparison.Ordinal)&&n==0)plan.problems.Add("第一步不能引用上一步结果。");}
    object input;if(step.args.TryGetValue("id",out input)&&input is string&&!((string)input).StartsWith("$previous.",StringComparison.Ordinal)){
     var item=c.Library.Find((string)input);if(item==null)plan.problems.Add("资料已移除。");else{var file=new FileInfo(item.path);inputs.Add(new{id=item.id,notes=item.notes,modified=item.modified,exists=file.Exists,size=file.Exists?file.Length:0,ticks=file.Exists?file.LastWriteTimeUtc.Ticks:0});}
    }
    // Static fields are checked before any step runs. Result references are checked
    // against the real schema immediately before dispatch, never evaluated as code.
    try{CapabilitySchema.Check(action.Schema,Arguments(step.args,action.Schema,n));}catch(InvalidDataException e){plan.problems.Add(e.Message);}
   }
   plan.problems=plan.problems.Distinct().ToList();plan.version=Library.StudyHash(new{recipe,inputs});return plan;
  }
  static bool HasReference(object value){var text=value as string;if(text!=null)return text.StartsWith("$previous.",StringComparison.Ordinal);var obj=value as Dictionary<string,object>;if(obj!=null)return obj.Values.Any(HasReference);var list=value as System.Collections.IEnumerable;if(list!=null)return list.Cast<object>().Any(HasReference);return false;}
  static object Arguments(object value,Dictionary<string,object> schema,int step){
   var text=value as string;if(text!=null&&text.StartsWith("$previous.",StringComparison.Ordinal)){if(step==0)throw new InvalidDataException("第一步不能引用上一步结果。");string type=schema.ContainsKey("type")?Convert.ToString(schema["type"]):"string";if(type=="string")return schema.ContainsKey("enum")?((System.Collections.IEnumerable)schema["enum"]).Cast<object>().First():"x";if(type=="number"||type=="integer")return schema.ContainsKey("minimum")?schema["minimum"]:0;if(type=="array")return new object[0];if(type=="boolean")return false;return new Dictionary<string,object>();}
   var obj=value as Dictionary<string,object>;if(obj!=null){var props=schema.ContainsKey("properties")?schema["properties"] as Dictionary<string,object>:null;return obj.ToDictionary(p=>p.Key,p=>Arguments(p.Value,props!=null&&props.ContainsKey(p.Key)?(Dictionary<string,object>)props[p.Key]:CapabilitySchema.Obj(additional:true),step));}
   var list=value as System.Collections.IEnumerable;if(list!=null&&!(value is string))return list.Cast<object>().Select(v=>Arguments(v,schema.ContainsKey("items")?(Dictionary<string,object>)schema["items"]:CapabilitySchema.Obj(additional:true),step)).ToArray();return value;
  }
 }
}
