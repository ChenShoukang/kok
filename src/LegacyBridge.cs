using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
namespace kok {public partial class MainWindow {
 internal static readonly string[] LegacyDirectActions={"setWindowAppearance","openPreview","previewWindows","focusPreview","previewReady","previewItemChanged","sendPreviewBehind","dragPreview","resizePreview","maximizePreview","minimizePreview","closePreview","appModeState","setAppMode","library","knowledgeIndex","runVisualEdit","knowledgeAiPreview","runKnowledgeAnalysis","vaultStatus","createPrivate","unlockPrivate","lockPrivate","vaultActivity","readHelp","importSamples","importDropped","importFiles","importFolder","update","remove","settings","saveVideoCover","fileProperties","aiConfig","runStudyTask","generateVideoOutline","fileDates","bulkClassify","fileChatHistory","clearFileChat","cancelFileChat","chatWithFile","sources","addSource","changeSource","scanSources","cancelSourceScan","organizeFiles","cancelOrganization","applyOrganization","editOrganization","saveAiConfig","aiModels","splitTranslationText","cancelTranslation","translate","exportTranslation","addCollection","deleteCollection","importFont","subtitles","chooseSubtitle","savePhoto","exportReadingNotes","exportReading","read","openExternal","reveal","export","diagnostics","formats"};
 internal static IEnumerable<string> LegacyNames(){return LegacyDirectActions.Concat(ReadingSessionActions).Concat(SpeechActions).Concat(DocumentTranslationActions).Concat(MusicActions).Concat(CreativeActions).Concat(DownloadActions);}
 delegate Task<object> ShellAction(MainWindow host,string action,Dictionary<string,object> args,Form owner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs message);
 static readonly Dictionary<string,ShellAction> ShellActions=new Dictionary<string,ShellAction>{{"setWindowAppearance",(host,action,args,owner,preview,message)=>host.ShellAction0(action,args,owner,preview,message)},
{"openPreview",(host,action,args,owner,preview,message)=>host.ShellAction1(action,args,owner,preview,message)},
{"previewWindows",(host,action,args,owner,preview,message)=>host.ShellAction2(action,args,owner,preview,message)},
{"focusPreview",(host,action,args,owner,preview,message)=>host.ShellAction3(action,args,owner,preview,message)},
{"previewReady",(host,action,args,owner,preview,message)=>host.ShellAction4(action,args,owner,preview,message)},
{"previewItemChanged",(host,action,args,owner,preview,message)=>host.ShellAction5(action,args,owner,preview,message)},
{"sendPreviewBehind",(host,action,args,owner,preview,message)=>host.ShellAction6(action,args,owner,preview,message)},
{"dragPreview",(host,action,args,owner,preview,message)=>host.ShellAction7(action,args,owner,preview,message)},
{"resizePreview",(host,action,args,owner,preview,message)=>host.ShellAction8(action,args,owner,preview,message)},
{"maximizePreview",(host,action,args,owner,preview,message)=>host.ShellAction9(action,args,owner,preview,message)},
{"minimizePreview",(host,action,args,owner,preview,message)=>host.ShellAction10(action,args,owner,preview,message)},
{"closePreview",(host,action,args,owner,preview,message)=>host.ShellAction11(action,args,owner,preview,message)},
{"appModeState",(host,action,args,owner,preview,message)=>host.ShellAction12(action,args,owner,preview,message)},
{"setAppMode",(host,action,args,owner,preview,message)=>host.ShellAction13(action,args,owner,preview,message)},
{"library",(host,action,args,owner,preview,message)=>host.ShellAction14(action,args,owner,preview,message)},
{"knowledgeIndex",(host,action,args,owner,preview,message)=>host.ShellAction15(action,args,owner,preview,message)},
{"runVisualEdit",(host,action,args,owner,preview,message)=>host.ShellAction16(action,args,owner,preview,message)},
{"knowledgeAiPreview",(host,action,args,owner,preview,message)=>host.ShellAction17(action,args,owner,preview,message)},
{"runKnowledgeAnalysis",(host,action,args,owner,preview,message)=>host.ShellAction18(action,args,owner,preview,message)},
{"vaultStatus",(host,action,args,owner,preview,message)=>host.ShellAction19(action,args,owner,preview,message)},
{"createPrivate",(host,action,args,owner,preview,message)=>host.ShellAction20(action,args,owner,preview,message)},
{"unlockPrivate",(host,action,args,owner,preview,message)=>host.ShellAction21(action,args,owner,preview,message)},
{"lockPrivate",(host,action,args,owner,preview,message)=>host.ShellAction22(action,args,owner,preview,message)},
{"vaultActivity",(host,action,args,owner,preview,message)=>host.ShellAction23(action,args,owner,preview,message)},
{"readHelp",(host,action,args,owner,preview,message)=>host.ShellAction24(action,args,owner,preview,message)},
{"importSamples",(host,action,args,owner,preview,message)=>host.ShellAction25(action,args,owner,preview,message)},
{"importDropped",(host,action,args,owner,preview,message)=>host.ShellAction26(action,args,owner,preview,message)},
{"importFiles",(host,action,args,owner,preview,message)=>host.ShellAction27(action,args,owner,preview,message)},
{"importFolder",(host,action,args,owner,preview,message)=>host.ShellAction27(action,args,owner,preview,message)},
{"update",(host,action,args,owner,preview,message)=>host.ShellAction28(action,args,owner,preview,message)},
{"remove",(host,action,args,owner,preview,message)=>host.ShellAction29(action,args,owner,preview,message)},
{"settings",(host,action,args,owner,preview,message)=>host.ShellAction30(action,args,owner,preview,message)},
{"saveVideoCover",(host,action,args,owner,preview,message)=>host.ShellAction31(action,args,owner,preview,message)},
{"fileProperties",(host,action,args,owner,preview,message)=>host.ShellAction32(action,args,owner,preview,message)},
{"aiConfig",(host,action,args,owner,preview,message)=>host.ShellAction33(action,args,owner,preview,message)},
{"runStudyTask",(host,action,args,owner,preview,message)=>host.ShellAction34(action,args,owner,preview,message)},
{"generateVideoOutline",(host,action,args,owner,preview,message)=>host.ShellAction35(action,args,owner,preview,message)},
{"fileDates",(host,action,args,owner,preview,message)=>host.ShellAction36(action,args,owner,preview,message)},
{"bulkClassify",(host,action,args,owner,preview,message)=>host.ShellAction37(action,args,owner,preview,message)},
{"fileChatHistory",(host,action,args,owner,preview,message)=>host.ShellAction38(action,args,owner,preview,message)},
{"clearFileChat",(host,action,args,owner,preview,message)=>host.ShellAction39(action,args,owner,preview,message)},
{"cancelFileChat",(host,action,args,owner,preview,message)=>host.ShellAction40(action,args,owner,preview,message)},
{"chatWithFile",(host,action,args,owner,preview,message)=>host.ShellAction41(action,args,owner,preview,message)},
{"sources",(host,action,args,owner,preview,message)=>host.ShellAction42(action,args,owner,preview,message)},
{"addSource",(host,action,args,owner,preview,message)=>host.ShellAction43(action,args,owner,preview,message)},
{"changeSource",(host,action,args,owner,preview,message)=>host.ShellAction44(action,args,owner,preview,message)},
{"scanSources",(host,action,args,owner,preview,message)=>host.ShellAction45(action,args,owner,preview,message)},
{"cancelSourceScan",(host,action,args,owner,preview,message)=>host.ShellAction46(action,args,owner,preview,message)},
{"organizeFiles",(host,action,args,owner,preview,message)=>host.ShellAction47(action,args,owner,preview,message)},
{"cancelOrganization",(host,action,args,owner,preview,message)=>host.ShellAction48(action,args,owner,preview,message)},
{"applyOrganization",(host,action,args,owner,preview,message)=>host.ShellAction49(action,args,owner,preview,message)},
{"editOrganization",(host,action,args,owner,preview,message)=>host.ShellAction50(action,args,owner,preview,message)},
{"saveAiConfig",(host,action,args,owner,preview,message)=>host.ShellAction51(action,args,owner,preview,message)},
{"aiModels",(host,action,args,owner,preview,message)=>host.ShellAction52(action,args,owner,preview,message)},
{"splitTranslationText",(host,action,args,owner,preview,message)=>host.ShellAction53(action,args,owner,preview,message)},
{"cancelTranslation",(host,action,args,owner,preview,message)=>host.ShellAction54(action,args,owner,preview,message)},
{"translate",(host,action,args,owner,preview,message)=>host.ShellAction55(action,args,owner,preview,message)},
{"exportTranslation",(host,action,args,owner,preview,message)=>host.ShellAction56(action,args,owner,preview,message)},
{"addCollection",(host,action,args,owner,preview,message)=>host.ShellAction57(action,args,owner,preview,message)},
{"deleteCollection",(host,action,args,owner,preview,message)=>host.ShellAction58(action,args,owner,preview,message)},
{"importFont",(host,action,args,owner,preview,message)=>host.ShellAction59(action,args,owner,preview,message)},
{"subtitles",(host,action,args,owner,preview,message)=>host.ShellAction60(action,args,owner,preview,message)},
{"chooseSubtitle",(host,action,args,owner,preview,message)=>host.ShellAction61(action,args,owner,preview,message)},
{"savePhoto",(host,action,args,owner,preview,message)=>host.ShellAction62(action,args,owner,preview,message)},
{"exportReadingNotes",(host,action,args,owner,preview,message)=>host.ShellAction63(action,args,owner,preview,message)},
{"exportReading",(host,action,args,owner,preview,message)=>host.ShellAction64(action,args,owner,preview,message)},
{"read",(host,action,args,owner,preview,message)=>host.ShellAction65(action,args,owner,preview,message)},
{"openExternal",(host,action,args,owner,preview,message)=>host.ShellAction66(action,args,owner,preview,message)},
{"reveal",(host,action,args,owner,preview,message)=>host.ShellAction67(action,args,owner,preview,message)},
{"export",(host,action,args,owner,preview,message)=>host.ShellAction68(action,args,owner,preview,message)},
{"diagnostics",(host,action,args,owner,preview,message)=>host.ShellAction69(action,args,owner,preview,message)},
{"formats",(host,action,args,owner,preview,message)=>host.ShellAction70(action,args,owner,preview,message)}};
 async Task<object> ExecuteLegacy(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){
  ShellAction handler;if(ShellActions.TryGetValue(action,out handler))return await handler(this,action,a,dialogOwner,preview,e);
  if(ReadingSessionActions.Contains(action))return await HandleReadingSessionAction(action,a);
  if(SpeechActions.Contains(action))return await HandleSpeechAction(action,a,dialogOwner);
  if(DocumentTranslationActions.Contains(action))return await HandleDocumentTranslationAction(action,a,dialogOwner);
  if(MusicActions.Contains(action))return await HandleMusicAction(action,a);
  if(CreativeActions.Contains(action))return await HandleCreativeAction(action,a,dialogOwner);
  if(DownloadActions.Contains(action))return await HandleDownloadAction(action,a);
  throw new InvalidOperationException("未知操作。");
 }
 async Task<object> ShellAction0(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {Color color;if(!TryChromeColor(StringArg(a,"color"),out color))throw new InvalidDataException("窗口颜色无效。");if(preview!=null)preview.Appearance(color);else ApplyWindowAppearance(color,a.ContainsKey("reduceMotion")&&Convert.ToBoolean(a["reduceMotion"]));result=true;break;}
                    
}while(false);return result;}
 async Task<object> ShellAction1(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await OpenPreview(a);break;
                    
}while(false);return result;}
 async Task<object> ShellAction2(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=PreviewList();break;
                    
}while(false);return result;}
 async Task<object> ShellAction3(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {var target=previewWindows.FirstOrDefault(w=>w.ItemId==StringArg(a,"id")&&w.SessionLibrary==library);if(target!=null)target.RestorePreview();result=true;break;}
                    
}while(false);return result;}
 async Task<object> ShellAction4(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(preview!=null){preview.Present(a.ContainsKey("reduceMotion")&&Convert.ToBoolean(a["reduceMotion"]));PreviewListChanged();}result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction5(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(preview!=null){var item=RequireEntry(a);preview.ItemId=item.id;preview.Text=item.name;PreviewListChanged();}result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction6(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(preview!=null)preview.SendBehind();result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction7(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(preview!=null)preview.DragPreview();result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction8(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(preview!=null)preview.ResizePreview(Convert.ToInt32(a["edge"]));result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction9(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(preview!=null)preview.ToggleMaximize();result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction10(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(preview!=null)preview.WindowState=FormWindowState.Minimized;result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction11(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(preview!=null)preview.BeginInvoke((Action)(()=>preview.Close()));result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction12(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=library.ModeState();break;
                    
}while(false);return result;}
 async Task<object> ShellAction13(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        library.SetAppMode(StringArg(a,"mode"),a.ContainsKey("acknowledged")&&Convert.ToBoolean(a["acknowledged"]));
                        if(!library.AiEnabled){if(jobs!=null)jobs.StopScope();CancelAgentRun();CancelSpeechJobs();PauseNetworkDownloads();if(musicLookupCancellation!=null)musicLookupCancellation.Cancel();foreach(var job in translationJobs.Values.ToArray())job.Cancel();if(fileChatCancellation!=null)fileChatCancellation.Cancel();if(organizationCancellation!=null)organizationCancellation.Cancel();if(modelListCancellation!=null)modelListCancellation.Cancel();if(folderCancellation!=null)folderCancellation.Cancel();organizationPreview=null;}
                        result=library.ModeState();break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction14(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {var snapshot=Library.Json.Deserialize<Dictionary<string,object>>(library.Snapshot(false));snapshot["isPrivate"]=library.IsPrivate;if(library.IsPrivate)snapshot["navigation"]=new{items=normalLibrary.Db.items.Select(item=>new{kind=item.kind,collection=item.collection}).ToArray(),collections=normalLibrary.Db.collections};result=snapshot;break;}
                    
}while(false);return result;}
 async Task<object> ShellAction15(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await ReadKnowledgeIndex();break;

                    
}while(false);return result;}
 async Task<object> ShellAction16(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await RunVisualEdit(a);break;
                    
}while(false);return result;}
 async Task<object> ShellAction17(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await KnowledgePreview(a);break;
                    
}while(false);return result;}
 async Task<object> ShellAction18(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await RunKnowledgeAnalysis(a);break;
                    
}while(false);return result;}
 async Task<object> ShellAction19(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=new{exists=File.Exists(Path.Combine(VaultDirectory,"vault-info.json")),unlocked=library.IsPrivate,autoLockMinutes=5};break;
                    
}while(false);return result;}
 async Task<object> ShellAction20(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await OpenPrivateVault(StringArg(a,"password"),true);break;
                    
}while(false);return result;}
 async Task<object> ShellAction21(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await OpenPrivateVault(StringArg(a,"password"),false);break;
                    
}while(false);return result;}
 async Task<object> ShellAction22(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 await LockPrivateVault();result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction23(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 lastPrivateActivity=DateTime.UtcNow;result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction24(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=File.ReadAllText(Path.Combine(appDir,L("README.md")),Encoding.UTF8);break;
                    
}while(false);return result;}
 async Task<object> ShellAction25(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        if(importing) throw new InvalidOperationException("正在导入，请稍候。");
                        string samples=Path.Combine(appDir,"samples");
                        if(!Directory.Exists(samples)) throw new DirectoryNotFoundException("示例目录不存在。");
                        importing=true; Notify("importing",new { count=0 });
                        try { result=await Task.Run(()=>library.Import(Directory.GetFiles(samples),null,jobs.CurrentToken)); }
                        finally { importing=false; Notify("imported",null); }
                        break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction26(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        if(importing) throw new InvalidOperationException("正在导入，请稍候。");
                        // Use native-backed DOM files only. Never accept a path supplied in page JSON.
                        var objects=e.AdditionalObjects;
                        string[] dropped=objects==null ? new string[0] : objects.OfType<CoreWebView2File>().Select(f=>f.Path).Where(p=>!String.IsNullOrWhiteSpace(p)).ToArray();
                        if(dropped.Length==0) throw new InvalidDataException("未收到本地文件，请从文件资源管理器拖入，或点击导入文件。");
                        importing=true; Notify("importing",new { count=0 });
                        try { result=await Task.Run(()=>library.ImportDropped(dropped,n=>BeginInvoke((Action)(()=>Notify("importing",new{count=n}))),jobs.CurrentToken)); }
                        finally { importing=false; Notify("imported",null); }
                        break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction27(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {

                        if(importing) throw new InvalidOperationException("正在导入，请稍候。");
                        string[] paths;
                        nativeFileDialogOpen=true;
                        try{if(action=="importFiles") {
                            using(var dialog=new OpenFileDialog { Multiselect=true, Title=L("选择视频、照片、音乐、文档或电子书"), Filter=L("所有文件|*.*") }) { if(dialog.ShowDialog(dialogOwner)!=DialogResult.OK) { result=new { cancelled=true }; break; } paths=dialog.FileNames; }
                        } else {
                            using(var dialog=new FolderBrowserDialog { Description=L("选择资料文件夹（包含子文件夹）"), ShowNewFolderButton=false }) { if(dialog.ShowDialog(dialogOwner)!=DialogResult.OK) { result=new { cancelled=true }; break; } paths=new[]{dialog.SelectedPath}; }
                        }}finally{nativeFileDialogOpen=false;}
                        importing=true; Notify("importing",new { count=0 });
                        try {
                            bool folder=action=="importFolder";
                            result=await Task.Run(()=> {
                                var errors=new List<string>();
                                IEnumerable<string> input=folder ? Library.Walk(paths[0],errors) : paths;
                                ImportResult report=library.Import(input,n=>BeginInvoke((Action)(()=>Notify("importing",new{count=n}))),jobs.CurrentToken);
                                report.errors.AddRange(errors); return report;
                            });
                        } finally { importing=false; Notify("imported",null); }
                        break;
                    
}while(false);return result;}
 async Task<object> ShellAction28(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 library.Update(StringArg(a,"id"),(Dictionary<string,object>)a["patch"]); result=true; break;
                    
}while(false);return result;}
 async Task<object> ShellAction29(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 library.Remove(StringArg(a,"id")); result=true; break;
                    
}while(false);return result;}
 async Task<object> ShellAction30(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 library.Settings(a);if(library.IsPrivate){var shared=a.Where(pair=>pair.Key=="theme"||pair.Key=="uiLanguage"||Library.AppearanceKeys.Contains(pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Value);if(shared.Count>0)normalLibrary.Settings(shared);} result=true; break;
                    
}while(false);return result;}
 async Task<object> ShellAction31(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {var targetLibrary=library;backgroundLibraryWrites++;try{result=await Task.Run(()=>targetLibrary.SaveVideoCover(StringArg(a,"id"),StringArg(a,"image")));}finally{backgroundLibraryWrites--;}break;}
                    
}while(false);return result;}
 async Task<object> ShellAction32(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        var item=RequireEntry(a);var info=new FileInfo(item.path);
                        result=new { name=info.Name,path=info.FullName,size=info.Length,extension=item.ext,modified=info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") };break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction33(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=translations.PublicConfig();break;
                    
}while(false);return result;}
 async Task<object> ShellAction34(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await RunStudyTask(a);break;
                    
}while(false);return result;}
 async Task<object> ShellAction35(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await GenerateVideoOutline(a);break;
                    
}while(false);return result;}
 async Task<object> ShellAction36(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 await Task.Run(()=>library.FillFileDates());result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction37(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 library.BulkClassify(Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["ids"])),StringArg(a,"collection"));result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction38(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=library.ChatHistory(StringArg(a,"id"));break;
                    
}while(false);return result;}
 async Task<object> ShellAction39(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(fileChatCancellation!=null)throw new InvalidOperationException("请先停止当前回答。");library.ClearChat(StringArg(a,"id"));result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction40(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(fileChatCancellation!=null&&fileChatJob==StringArg(a,"jobId"))fileChatCancellation.Cancel();result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction41(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        if(fileChatCancellation!=null)throw new InvalidOperationException("请先停止当前回答。");var item=RequireEntry(a);string jobId=StringArg(a,"jobId"),question=StringArg(a,"question"),source=StringArg(a,"source");if(jobId.Length<1||jobId.Length>80)throw new InvalidDataException("提问或引用内容过长，请缩小范围。");
                        var references=Library.ReadChatReferences(a);
                        using(var cancel=ModuleCancellation()){fileChatCancellation=cancel;fileChatJob=jobId;
                            try{var answer=await translations.ChatAsync(item.name,StringArg(a,"context"),source,question,library.ChatHistory(item.id),StringArg(a,"image"),library.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(library.Db.settings["uiLanguage"])=="en",cancel.Token,delta=>Notify("fileChatDelta",new{jobId=jobId,text=delta}));cancel.Token.ThrowIfCancellationRequested();library.AddChat(item.id,question,answer,source,references);result=library.ChatHistory(item.id);}
                            catch(OperationCanceledException){throw new InvalidOperationException("AI 回答已停止。");}finally{fileChatCancellation=null;fileChatJob="";}
                        }break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction42(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=new {folders=library.Folders(),busy=importing};break;
                    
}while(false);return result;}
 async Task<object> ShellAction43(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        if(importing)throw new InvalidOperationException("正在导入，请稍候。");
                        using(var dialog=new FolderBrowserDialog{Description=L("选择自动扫描的文件夹"),ShowNewFolderButton=false}){if(dialog.ShowDialog(dialogOwner)==DialogResult.OK)result=library.AddFolder(dialog.SelectedPath);else result=new{cancelled=true};}break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction44(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 library.ChangeFolder(StringArg(a,"id"),a.ContainsKey("enabled")?(bool?)Convert.ToBoolean(a["enabled"]):null);result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction45(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=await ScanSources();break;
                    
}while(false);return result;}
 async Task<object> ShellAction46(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(folderCancellation!=null)folderCancellation.Cancel();result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction47(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        if(organizationCancellation!=null)throw new InvalidOperationException("AI 正在整理，请等待或取消当前任务。");
                        var ids=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["ids"]));bool content=a.ContainsKey("content")&&Convert.ToBoolean(a["content"]);
                        using(var cancel=ModuleCancellation()){organizationCancellation=cancel;organizationPreview=null;
                            try{var inputs=await Task.Run(()=>library.OrganizeInputs(ids,content));cancel.Token.ThrowIfCancellationRequested();var rows=await translations.OrganizeAsync(inputs,content,library.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(library.Db.settings["uiLanguage"])=="en",cancel.Token,(done,total)=>Notify("organizationProgress",new{done=done,total=total}));cancel.Token.ThrowIfCancellationRequested();organizationPreview=rows;result=rows;}
                            catch(OperationCanceledException){throw new InvalidOperationException("AI 整理已取消。");}finally{organizationCancellation=null;}
                        }break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction48(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(organizationCancellation!=null)organizationCancellation.Cancel();result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction49(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 if(organizationPreview==null)throw new InvalidDataException("整理结果已失效，请重新分析。");result=library.ProposeOrganization(organizationPreview,Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["ids"])));organizationPreview=null;break;
                    
}while(false);return result;}
 async Task<object> ShellAction50(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 library.EditOrganization(StringArg(a,"id"),StringArg(a,"category"),Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["tags"])),StringArg(a,"summary"));result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction51(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=translations.SaveConfig(a);break;
                    
}while(false);return result;}
 async Task<object> ShellAction52(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {if(modelListCancellation!=null)throw new InvalidOperationException("正在读取模型列表，请稍候。");using(var cancel=ModuleCancellation()){modelListCancellation=cancel;try{result=await translations.ModelsAsync(a,cancel.Token);}finally{modelListCancellation=null;}}break;}
                    
}while(false);return result;}
 async Task<object> ShellAction53(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=TranslationService.SplitText(StringArg(a,"text"));break;
                    
}while(false);return result;}
 async Task<object> ShellAction54(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {CancellationTokenSource job;if(translationJobs.TryGetValue(StringArg(a,"jobId"),out job))job.Cancel();result=true;break;}
                    
}while(false);return result;}
 async Task<object> ShellAction55(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        string jobId=StringArg(a,"jobId");if(jobId.Length<1||jobId.Length>80||translationJobs.ContainsKey(jobId)||translationJobs.Count>=4)throw new InvalidOperationException("翻译任务正在切换，请稍后重试。");
                        var parts=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["parts"]));
                        using(var cancel=ModuleCancellation()){
                            translationJobs.Add(jobId,cancel);
                            try{result=await translations.TranslateAsync(parts,cancel.Token,(done,total)=>{if(!IsDisposed)Notify("translationProgress",new{jobId=jobId,completed=done,total=total});},!a.ContainsKey("test")||!Convert.ToBoolean(a["test"]),a.ContainsKey("language")?StringArg(a,"language"):null,(start,output)=>{if(!IsDisposed)Notify("translationBatch",new{jobId=jobId,start=start,parts=output});},a.ContainsKey("fast")&&Convert.ToBoolean(a["fast"]));}
                            catch(OperationCanceledException){throw new InvalidOperationException("翻译已取消。");}
                            finally{translationJobs.Remove(jobId);}
                        }break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction56(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        string text=StringArg(a,"text"),format=StringArg(a,"format");if(text.Length>16*1024*1024)throw new InvalidDataException("译文过大。");
                        string extension=format=="srt"?"srt":"txt";
                        using(var dialog=new SaveFileDialog{Title=L("导出译文"),Filter=L(extension=="srt"?"SRT 字幕|*.srt":"文本译文|*.txt"),FileName=StringArg(a,"name")+L("-译文")+"."+extension}){
                            if(dialog.ShowDialog(dialogOwner)==DialogResult.OK){EnsureExportDestination(dialog.FileName);File.WriteAllText(dialog.FileName,text,new UTF8Encoding(false));result=true;}
                        }break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction57(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 library.AddCollection(StringArg(a,"name"));result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction58(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 library.DeleteCollection(StringArg(a,"name"));result=true;break;
                    
}while(false);return result;}
 async Task<object> ShellAction59(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        using(var dialog=new OpenFileDialog{Title=L("导入你有权使用的字体"),Filter=L("字体文件|*.ttf;*.otf")}) { if(dialog.ShowDialog(dialogOwner)==DialogResult.OK)result=library.ImportFont(dialog.FileName); }
                        break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction60(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {var entry=RequireEntry(a);var tracks=await Task.Run(()=>Subtitles.Discover(entry.path));if(entry.generatedSubtitles!=null&&entry.generatedSubtitles.cues.Count>0)tracks.Insert(0,entry.generatedSubtitles);result=tracks;break;}
                    
}while(false);return result;}
 async Task<object> ShellAction61(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        using(var dialog=new OpenFileDialog{Title=L("载入字幕"),Filter=L("文字字幕|*.srt;*.vtt;*.ass;*.ssa;*.sub;*.sbv;*.lrc;*.ttml;*.dfxp;*.smi|所有文件|*.*")}) { if(dialog.ShowDialog(dialogOwner)==DialogResult.OK)result=Subtitles.Read(dialog.FileName); }
                        break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction62(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        Entry original=RequireEntry(a);
                        string encoded=StringArg(a,"image");if(!encoded.StartsWith("data:image/png;base64,")||encoded.Length>48*1024*1024)throw new InvalidDataException("图片数据无效或过大。");
                        using(var dialog=new SaveFileDialog{Title=L("另存编辑后的照片"),Filter=L("PNG 图片|*.png|JPEG 图片|*.jpg"),FileName=StringArg(a,"name")+L("-编辑")+".png"}) {
                            if(dialog.ShowDialog(dialogOwner)==DialogResult.OK) {
                                EnsureExportDestination(dialog.FileName);
                                byte[] bytes=Convert.FromBase64String(encoded.Substring(22));
                                library.SaveEditedPhoto(original.path,dialog.FileName,bytes);result=true;
                            }
                        }break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction63(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=ExportReadingNotes(a);break;
                    
}while(false);return result;}
 async Task<object> ShellAction64(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        string html=StringArg(a,"html");if(html.Length>32*1024*1024)throw new InvalidDataException("导出内容过大。");
                        using(var dialog=new SaveFileDialog{Title=L("导出带标记的阅读副本"),Filter=L("HTML 阅读副本|*.html"),FileName=StringArg(a,"name")+L("-批注")+".html"}) { if(dialog.ShowDialog(dialogOwner)==DialogResult.OK){EnsureExportDestination(dialog.FileName);File.WriteAllText(dialog.FileName,html,new UTF8Encoding(false));result=true;} }break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction65(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        Entry item=RequireEntry(a); result=await Task.Run(()=>Library.ReadBook(item)); break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction66(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        Entry item=RequireEntry(a); Process.Start(new ProcessStartInfo(item.path){UseShellExecute=true}); result=true; break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction67(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        Entry item=RequireEntry(a); Process.Start("explorer.exe","/select,\""+item.path+"\""); result=true; break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction68(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 {
                        if(library.IsPrivate){using(var privateDialog=new SaveFileDialog{Title=L("导出加密私密备份"),Filter=L("私密资料库备份|*.kokvault"),FileName="kok-private-"+DateTime.Now.ToString("yyyyMMdd")+".kokvault"}){if(privateDialog.ShowDialog(dialogOwner)==DialogResult.OK){ExportPrivateBackup(privateDialog.FileName);result=true;}}break;}
                        using(var dialog=new SaveFileDialog { Title=L("导出资料库索引与笔记"), Filter=L("JSON 资料库|*.json"), FileName="kok-library-"+DateTime.Now.ToString("yyyyMMdd")+".json" }) {
                            if(dialog.ShowDialog(dialogOwner)==DialogResult.OK) { EnsureExportDestination(dialog.FileName);File.WriteAllText(dialog.FileName,library.Snapshot(),new UTF8Encoding(false)); result=true; }
                        } break;
                    }
                    
}while(false);return result;}
 async Task<object> ShellAction69(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=new { engine=web.CoreWebView2.Environment.BrowserVersionString, count=library.Db.items.Count, dataDir=library.DataDir, version="0.8.1", mediaResponseLimit=MediaResponseLimit, logFile=RuntimeLog.FilePath, silentTest=silentTest, window=new { handle=Handle.ToInt64(),fullScreen=mediaFullScreen, border=FormBorderStyle.ToString(), state=WindowState.ToString(), bounds=new { x=Left,y=Top,width=Width,height=Height }, screen=Screen.FromControl(this).Bounds,workArea=Screen.FromControl(this).WorkingArea,clientScreen=RectangleToScreen(ClientRectangle), chrome=new { custom=customChrome,color=ColorTranslator.ToHtml(chromeColor),height=ChromeHeight,edge=ChromeEdge,handle=windowChrome.Handle.ToInt64(),content=web.Bounds,buttons=new[]{new {name="minimize",handle=minimizeWindow.Handle.ToInt64(),bounds=minimizeWindow.Bounds},new {name="maximize",handle=maximizeWindow.Handle.ToInt64(),bounds=maximizeWindow.Bounds},new {name="close",handle=closeWindow.Handle.ToInt64(),bounds=closeWindow.Bounds}}} } }; break;
                    
}while(false);return result;}
 async Task<object> ShellAction70(string action,Dictionary<string,object> a,Form dialogOwner,PreviewWindow preview,CoreWebView2WebMessageReceivedEventArgs e){object result=null;do {
 result=Compatibility.Groups; break;

}while(false);return result;}
}}
