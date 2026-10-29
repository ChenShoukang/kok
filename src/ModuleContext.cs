using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;
namespace kok {
 [Flags] public enum Need {None=0,Ai=1,Network=2,NormalLibrary=4}
 public sealed class ModulePolicy {
  readonly Library library;readonly Func<bool> current,transition;
  public ModulePolicy(Library library,Func<bool> current=null,Func<bool> transition=null){this.library=library;this.current=current??(()=>true);this.transition=transition??(()=>false);}
  public void Ensure(Need need,string module,bool emergency=false){
   if(!emergency&&(!current()||transition()))throw new InvalidOperationException("资料库正在切换，请稍候。");
   if(!emergency&&!library.ModuleEnabled(module))throw new InvalidOperationException("此模块已停用，请在设置中启用。");
   if((need&Need.NormalLibrary)!=0&&library.IsPrivate)throw new InvalidOperationException("私密文件夹不提供此功能。");
   if((need&(Need.Ai|Need.Network))!=0)library.RequireAiMode();
   if((need&Need.Ai)!=0&&!library.ModuleEnabled("ai"))throw new InvalidOperationException("此模块已停用，请在设置中启用。");
  }
  public bool Allows(Need need,string module){try{Ensure(need,module);return true;}catch(InvalidOperationException){return false;}}
 }
 // No host/window controller is exposed to modules; Owner is only a dialog owner.
 public sealed class ModuleContext {
  public readonly Library Library;public readonly TranslationService Ai;public readonly ModulePolicy Policy;
  public readonly Form Owner;public readonly CancellationToken Token;public readonly Action<string,object> Notify;
  public readonly Operations Ops;public readonly Jobs Jobs;public readonly AgentService Agent;
  public ModulePorts Ports {get;internal set;}
  public readonly Func<IDisposable> BeginWrite;public readonly Func<Dictionary<string,object>,Task<object>> OpenItem;
  public readonly Func<Entry,string,string,string> ChooseDestination;public readonly string AppDirectory;
  internal Func<string,Dictionary<string,object>,Task<object>> Legacy;internal Action<string> StopModule;
  public Func<string,Dictionary<string,object>,Task<object>> Invoke {get;internal set;}
  public ModuleContext(Library library,TranslationService ai,ModulePolicy policy,Form owner,CancellationToken token,Action<string,object> notify,Operations ops,Jobs jobs,AgentService agent=null,Func<IDisposable> beginWrite=null,Func<Dictionary<string,object>,Task<object>> openItem=null,Func<Entry,string,string,string> choose=null,string appDirectory=""){
   Library=library;Ai=ai;Policy=policy;Owner=owner;Token=token;Notify=notify??((n,v)=>{});Ops=ops;Jobs=jobs;Agent=agent;BeginWrite=beginWrite??(()=>new ModuleLease(()=>{}));OpenItem=openItem;ChooseDestination=choose;AppDirectory=appDirectory;
  }
  public static string Text(Dictionary<string,object> a,string key){return a.ContainsKey(key)?Convert.ToString(a[key])??"":"";}
 }
 public sealed class ModuleLease:IDisposable {Action release;public ModuleLease(Action release){this.release=release;}public void Dispose(){var r=Interlocked.Exchange(ref release,null);if(r!=null)r();}}
}
