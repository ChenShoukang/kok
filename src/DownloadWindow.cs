using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

namespace kok {
    public partial class MainWindow {
        DownloadManager downloads;
        CancellationTokenSource componentInstall,downloadProbe;
        ComponentProgress componentProgress=new ComponentProgress();
        string componentError="";
        static readonly string[] DownloadActions={"downloadState","installDownloadTools","cancelDownloadTools","chooseDownloadFolder","chooseFfmpeg","probeDownload","cancelDownloadProbe","startDownloads","pauseDownload","resumeDownload","cancelDownload","importDownload","revealDownload","clearDownloadHistory","loginDownloadSite","clearDownloadLogin","importDownloadLogin","browserProbeDownload"};
        void InitializeDownloads(){
            downloads=new DownloadManager(normalLibrary.DataDir,appDir);
            downloads.Changed=()=>{if(IsDisposed||Disposing||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(jobs!=null&&!library.IsPrivate)jobs.Changed();}));}catch(InvalidOperationException){}};
            downloads.Completed=delegate(VideoDownload job){
                if(IsDisposed||Disposing||!IsHandleCreated)return;
                try{BeginInvoke((Action)(()=>{
                    if(IsDisposed||library.IsPrivate||!library.AiEnabled)return;
                    try{normalLibrary.ImportDownloaded(downloads.Files(job.id),downloads.IsAudio(job.id));downloads.MarkImported(job.id);Notify("downloadComplete",new{id=job.id});}catch(Exception error){RuntimeLog.Write("download-import-error",error.GetType().Name);}
                }));}catch(InvalidOperationException){}
            };
            FormClosed+=delegate{PauseNetworkDownloads();downloads.Dispose();};
        }
        void PauseNetworkDownloads(){if(downloads!=null)downloads.PauseAll();if(componentInstall!=null)componentInstall.Cancel();if(downloadProbe!=null)downloadProbe.Cancel();}
        void RequireDownloadAccess(){if(library.IsPrivate)throw new InvalidOperationException("私密文件夹不提供联网下载。");if(!library.AiEnabled)throw new InvalidOperationException("请先切换至智能模式，再使用联网下载。");}
        async Task<object> HandleDownloadAction(string action,Dictionary<string,object> args){
            if(action=="pauseDownload"||action=="cancelDownload"){if(library.IsPrivate)throw new InvalidOperationException("私密文件夹不提供联网下载。");downloads.Stop(StringArg(args,"id"),action=="pauseDownload");return true;}
            if(action=="cancelDownloadTools"){if(componentInstall!=null)componentInstall.Cancel();return true;}
            if(action=="cancelDownloadProbe"){if(downloadProbe!=null)downloadProbe.Cancel();return true;}
            RequireDownloadAccess();
            switch(action){
                case "downloadState": return new{downloads=await Task.Run(()=>downloads.Snapshot()),installation=componentProgress,installationError=componentError};
                case "loginDownloadSite": return await LoginDownloadSite(StringArg(args,"site"));
                case "browserProbeDownload": {if(downloadProbe!=null)downloadProbe.Cancel();var cancel=new CancellationTokenSource();downloadProbe=cancel;try{return await BrowserDownloadPage(StringArg(args,"url"),args.ContainsKey("quality")?StringArg(args,"quality"):"best",cancel.Token);}catch(OperationCanceledException){return new{cancelled=true};}finally{if(downloadProbe==cancel)downloadProbe=null;cancel.Dispose();}}
                case "clearDownloadLogin": downloads.Sessions.Clear(StringArg(args,"site"));return true;
                case "importDownloadLogin": {string site=StringArg(args,"site");DownloadSessions.Home(site);using(var dialog=new OpenFileDialog{Title=L("导入所选站点的 cookies.txt"),Filter="Netscape cookies (*.txt)|*.txt",Multiselect=false}){if(dialog.ShowDialog(this)!=DialogResult.OK)return new{cancelled=true};if(new FileInfo(dialog.FileName).Length>1024*1024)throw new InvalidDataException("登录信息文件过大，请只导出所选站点的 cookies.txt。");downloads.Sessions.Import(site,File.ReadAllText(dialog.FileName));return true;}}
                case "installDownloadTools": {
                    if(componentInstall!=null)throw new InvalidOperationException("下载组件正在安装，请稍候。");
                    componentInstall=new CancellationTokenSource();componentError="";componentProgress=new ComponentProgress{status="starting"};
                    try{await Task.Run(()=>downloads.Tools.Install(p=>componentProgress=p,componentInstall.Token));componentProgress=new ComponentProgress{status="completed"};return true;}
                    catch(OperationCanceledException){componentProgress=new ComponentProgress{status="cancelled"};return new{cancelled=true};}
                    catch{componentError="组件安装失败，请检查网络后重试。";componentProgress=new ComponentProgress{status="error"};throw new InvalidOperationException(componentError);}
                    finally{componentInstall.Dispose();componentInstall=null;}
                }
                case "chooseDownloadFolder": using(var dialog=new FolderBrowserDialog{Description=L("选择下载保存位置"),SelectedPath=Convert.ToString(normalLibrary.Db.settings.ContainsKey("lastDownloadFolder")?normalLibrary.Db.settings["lastDownloadFolder"]:""),ShowNewFolderButton=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return new{cancelled=true};downloads.SetFolder(dialog.SelectedPath);return new{path=dialog.SelectedPath};}
                case "chooseFfmpeg": using(var dialog=new FolderBrowserDialog{Description=L("选择包含 ffmpeg.exe 的文件夹"),ShowNewFolderButton=false}){if(dialog.ShowDialog(this)!=DialogResult.OK)return new{cancelled=true};downloads.SetFfmpeg(dialog.SelectedPath);return true;}
                case "probeDownload": {
                    if(downloadProbe!=null)downloadProbe.Cancel();var cancellation=new CancellationTokenSource();downloadProbe=cancellation;cancellation.CancelAfter(TimeSpan.FromSeconds(70));
                    try{return await downloads.Probe(StringArg(args,"url"),cancellation.Token,args.ContainsKey("quality")?StringArg(args,"quality"):"best",StringArg(args,"selection"));}catch(OperationCanceledException){return new{cancelled=true};}finally{if(downloadProbe==cancellation)downloadProbe=null;cancellation.Dispose();}
                }
                case "startDownloads": {
                    var urls=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(args.ContainsKey("urls")?args["urls"]:new object[0]));
                    downloads.Start(urls??new List<string>(),StringArg(args,"quality"),args.ContainsKey("subtitles")&&Convert.ToBoolean(args["subtitles"]),StringArg(args,"selection"),StringArg(args,"browserToken"));return true;
                }
                case "pauseDownload": downloads.Stop(StringArg(args,"id"),true);return true;
                case "cancelDownload": downloads.Stop(StringArg(args,"id"),false);return true;
                case "resumeDownload": downloads.Resume(StringArg(args,"id"));return true;
                case "clearDownloadHistory": downloads.ClearFinished();return true;
                case "revealDownload": {var files=downloads.Files(StringArg(args,"id"));if(files.Length>0)Process.Start("explorer.exe","/select,"+DownloadTools.QuoteArgument(files[0]));return true;}
                case "importDownload": {string id=StringArg(args,"id");normalLibrary.ImportDownloaded(downloads.Files(id),downloads.IsAudio(id));downloads.MarkImported(id);return true;}
            }
            throw new InvalidOperationException("未知操作。");
        }
    }
}
