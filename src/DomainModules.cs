using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace kok {
 // Ports are composed by the native shell. A module receives only its own action port.
 public sealed class ModulePorts {
  readonly Dictionary<string,Func<string,Dictionary<string,object>,Task<object>>> ports=new Dictionary<string,Func<string,Dictionary<string,object>,Task<object>>>();
  public void Bind(string module,Func<string,Dictionary<string,object>,Task<object>> port){ports.Add(module,port);}
  public Task<object> Call(string module,string action,Dictionary<string,object> args){Func<string,Dictionary<string,object>,Task<object>> port;if(!ports.TryGetValue(module,out port)||!DomainModules.Groups().First(g=>g.Key==module).Value.Contains(action))throw new InvalidOperationException("模块服务未连接。");return port(action,args);}
 }
 internal static class DomainModules {
  public static Dictionary<string,string[]> Groups(){return new Dictionary<string,string[]>{
   {"video",new[]{"saveVideoCover","subtitles","chooseSubtitle","generateVideoOutline"}},
   {"photo",new[]{"savePhoto"}},
   {"music",MainWindow.MusicModuleNames()},
   {"translation",MainWindow.TranslationModuleNames().Concat(new[]{"splitTranslationText","cancelTranslation","translate","exportTranslation"}).ToArray()},
   {"speech",MainWindow.SpeechModuleNames()},
   {"downloads",MainWindow.DownloadModuleNames()},
   {"editing",MainWindow.EditingModuleNames()},
   {"reading",MainWindow.ReadingModuleNames().Concat(new[]{"exportReadingNotes","exportReading"}).ToArray()},
   {"knowledge",new[]{"knowledgeIndex","knowledgeAiPreview","runKnowledgeAnalysis"}},
   {"organizer",new[]{"sources","addSource","changeSource","scanSources","cancelSourceScan","organizeFiles","cancelOrganization","applyOrganization","fileDates"}},
   {"ai",new[]{"aiConfig","saveAiConfig","aiModels","fileChatHistory","clearFileChat","cancelFileChat","chatWithFile","runStudyTask","runVisualEdit"}}
  };}
  static string[] Fields(string module){if(module=="music")return new[]{"album","artist","audio"};if(module=="editing")return new[]{"editDraft"};if(module=="reading")return new[]{"chapterTexts","bookmarks","annotations"};if(module=="ai")return new[]{"aiChat","aiStudies"};if(module=="video")return new[]{"videoOutline","generatedSubtitles"};if(module=="organizer")return new[]{"collection","tags","aiCategory","aiSummary","aiFingerprint"};return new string[0];}
  public static IEnumerable<IKokModule> Create(){foreach(var g in Groups()){string module=g.Key;yield return new KokModule{Name=module,Ui=true,AgentTools=module=="speech"?SpeechToolDefinitions.Tools():new AgentToolSpec[0],Kinds=module=="video"?new[]{"video"}:module=="photo"?new[]{"photo"}:module=="music"?new[]{"music"}:module=="speech"||module=="downloads"?new[]{"video","music"}:module=="reading"||module=="knowledge"?new[]{"document","book"}:new[]{"video","photo","music","document","book"},Actions=g.Value.Select(name=>{var a=ModuleDefinitions.Action(name,LegacyRequirements.For(name),LegacyRequirements.Writes(name),(c,args)=>c.Ports.Call(module,name,args));a.Emergency=LegacyRequirements.Emergency(name);return a;}).ToArray(),Facets=new[]{new FacetSpec{Name=module+"-data",Fields=Fields(module)}}};}}
 }
 public partial class MainWindow {
  internal static string[] MusicModuleNames(){return MusicActions;}internal static string[] TranslationModuleNames(){return DocumentTranslationActions;}internal static string[] SpeechModuleNames(){return SpeechActions;}internal static string[] DownloadModuleNames(){return DownloadActions;}internal static string[] EditingModuleNames(){return CreativeActions;}internal static string[] ReadingModuleNames(){return ReadingSessionActions;}
  ModulePorts Ports(System.Windows.Forms.Form owner,PreviewWindow preview,Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs message){var ports=new ModulePorts();foreach(var group in DomainModules.Groups()){string key=group.Key;ports.Bind(key,(action,args)=>ExecuteLegacy(action,args,owner,preview,message));}return ports;}
 }
}
