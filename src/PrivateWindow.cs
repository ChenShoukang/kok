using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace kok {
    public partial class MainWindow {
        VaultSession privateSession;
        bool privateGate,privateOpening,nativeFileDialogOpen;
        int backgroundLibraryWrites,failedVaultAttempts,privateDepartureGeneration;
        DateTime lastPrivateActivity=DateTime.UtcNow,nextVaultAttempt=DateTime.MinValue;
        readonly System.Windows.Forms.Timer privateTimer=new System.Windows.Forms.Timer{Interval=1000};
        string VaultDirectory {get{return Path.Combine(normalLibrary.DataDir,"private-vault");}}
        void EnsureExportDestination(string destination){
            string full=Path.GetFullPath(destination),data=Path.GetFullPath(normalLibrary.DataDir).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(full.StartsWith(data,StringComparison.OrdinalIgnoreCase)||normalLibrary.Db.items.Concat(library.Db.items).Any(item=>String.Equals(Path.GetFullPath(item.path),full,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("请选择新的文件名，避免覆盖已收录的原文件。");
        }
        void ExportPrivateBackup(string destination){
            EnsureExportDestination(destination);
            library.Save();using(var file=File.Create(destination))using(var archive=new System.IO.Compression.ZipArchive(file,System.IO.Compression.ZipArchiveMode.Create)){
                foreach(var name in new[]{"vault-info.json","library.vault"}){var entry=archive.CreateEntry(name);using(var input=File.OpenRead(Path.Combine(VaultDirectory,name)))using(var output=entry.Open())input.CopyTo(output);}
                foreach(var path in Directory.GetFiles(Path.Combine(VaultDirectory,"thumbs"),"*.sealed")){var entry=archive.CreateEntry("thumbs/"+Path.GetFileName(path));using(var input=File.OpenRead(path))using(var output=entry.Open())input.CopyTo(output);}
            }
        }
        void InitializePrivateWindow(){
            privateTimer.Tick+=async delegate{if(library.IsPrivate&&!privateGate&&DateTime.UtcNow-lastPrivateActivity>TimeSpan.FromMinutes(5))try{await LockPrivateVault();}catch(Exception ex){RuntimeLog.Write("private-lock-error",ex);}};
            privateTimer.Start();
            Deactivate+=delegate{CheckPrivateDeparture();};
            Resize+=async delegate{if(WindowState==FormWindowState.Minimized&&library.IsPrivate&&!privateGate&&!nativeFileDialogOpen)try{await LockPrivateVault();}catch(Exception ex){RuntimeLog.Write("private-lock-error",ex);}};
            FormClosed+=delegate{privateTimer.Dispose();if(privateSession!=null)privateSession.Dispose();};
        }
        async Task SaveCurrentSession(){
            await SavePreviewSessions();
            if(web.CoreWebView2!=null&&!engineUnavailable)await SaveWebSession(web,library);
        }
        async Task StopAiForPrivate(){
            CancelCreativeJobs();CancelSpeechJobs();if(musicLookupCancellation!=null)musicLookupCancellation.Cancel();
            PauseNetworkDownloads();
            foreach(var job in translationJobs.Values.ToArray())job.Cancel();if(fileChatCancellation!=null)fileChatCancellation.Cancel();if(organizationCancellation!=null)organizationCancellation.Cancel();if(modelListCancellation!=null)modelListCancellation.Cancel();organizationPreview=null;
            for(int n=0;n<200&&(translationJobs.Count>0||fileChatCancellation!=null||organizationCancellation!=null||modelListCancellation!=null||backgroundLibraryWrites>0);n++)await Task.Delay(25);
            if(translationJobs.Count>0||fileChatCancellation!=null||organizationCancellation!=null||modelListCancellation!=null||backgroundLibraryWrites>0)throw new InvalidOperationException("后台任务正在停止，请稍后重试。");
        }
        async Task<object> OpenPrivateVault(string password,bool create){
            if(DateTime.UtcNow<nextVaultAttempt)throw new InvalidOperationException("密码尝试过于频繁，请稍后重试。");
            if(library.IsPrivate){if(privateGate){try{using(var verify=await Task.Run(()=>VaultSession.Open(VaultDirectory,password))){}failedVaultAttempts=0;privateGate=false;lastPrivateActivity=DateTime.UtcNow;Notify("librarySwitched",new{isPrivate=true});}catch{RegisterVaultFailure();throw;}}return true;}if(privateOpening)throw new InvalidOperationException("私密文件夹正在打开，请稍候。");if(importing)throw new InvalidOperationException("正在导入，请稍候。");privateOpening=true;int departure=privateDepartureGeneration;VaultSession session=null;
            try{await StopAiForPrivate();await SaveCurrentSession();session=await Task.Run(()=>create?VaultSession.Create(VaultDirectory,password):VaultSession.Open(VaultDirectory,password));var target=await Task.Run(()=>new Library(VaultDirectory,session));
                foreach(string name in new[]{"theme","uiLanguage","fontFamily","appMode","smartModeNoticeSeen"}.Concat(Library.AppearanceKeys))if(normalLibrary.Db.settings.ContainsKey(name)){if(name!="fontFamily"||new[]{"system","yahei","song","kai"}.Contains(Convert.ToString(normalLibrary.Db.settings[name])))target.Db.settings[name]=normalLibrary.Db.settings[name];}
                target.Save();if(privateDepartureGeneration!=departure){session.Dispose();session=null;failedVaultAttempts=0;return false;}library=target;privateSession=session;session=null;lastPrivateActivity=DateTime.UtcNow;failedVaultAttempts=0;Notify("librarySwitched",new{isPrivate=true});return true;
            }catch{if(session!=null)session.Dispose();RegisterVaultFailure();throw;}finally{privateOpening=false;}
        }
        void RegisterVaultFailure(){failedVaultAttempts++;if(failedVaultAttempts>=3)nextVaultAttempt=DateTime.UtcNow.AddSeconds(Math.Min(30,Math.Pow(2,Math.Min(8,failedVaultAttempts-3))));}
        async Task LockPrivateVault(){
            if(!library.IsPrivate||privateGate)return;privateGate=true;CancelCreativeJobs();if(musicLookupCancellation!=null)musicLookupCancellation.Cancel();Notify("privateLocking",null);
            try{if(folderCancellation!=null)folderCancellation.Cancel();while(importing||backgroundLibraryWrites>0)await Task.Delay(50);await SaveCurrentSession();library.Save();SetMediaFullScreen(false);library=normalLibrary;var session=privateSession;privateSession=null;if(session!=null)session.Dispose();privateGate=false;Notify("librarySwitched",new{isPrivate=false});
                if(web.CoreWebView2!=null)try{await web.CoreWebView2.Profile.ClearBrowsingDataAsync(Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.DiskCache);}catch(Exception ex){RuntimeLog.Write("cache-clear-error",ex);}
            }catch{privateGate=library.IsPrivate;Notify("privateLockFailed",null);throw;}
        }
    }
}
