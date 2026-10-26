using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace kok {
    static class Program {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [STAThread] static void Main(string[] args) {
            AppDomain.CurrentDomain.UnhandledException+=delegate(object sender,UnhandledExceptionEventArgs e) { var error=e.ExceptionObject as Exception;if(error!=null)RuntimeLog.Write("unhandled",error); };
            try{if(!SetProcessDpiAwarenessContext(new IntPtr(-4)))SetProcessDPIAware();}catch(EntryPointNotFoundException){SetProcessDPIAware();} Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new MainWindow(args)); }
            catch(Exception ex) { RuntimeLog.Write("host-error",ex);MessageBox.Show(Localization.Translate(ex.Message,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="en"),Localization.Translate("kok · 无法启动",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="en"),MessageBoxButtons.OK,MessageBoxIcon.Error); }
        }
    }
    public partial class MainWindow : Form {
        readonly WebView2 web = new WebView2();
        Library library;
        readonly Library normalLibrary;
        readonly TranslationService translations;
        readonly System.Windows.Forms.Timer folderTimer=new System.Windows.Forms.Timer { Interval=5000 };
        CancellationTokenSource folderCancellation,organizationCancellation;
        List<OrganizeSuggestion> organizationPreview;
        CancellationTokenSource fileChatCancellation,modelListCancellation;
        string fileChatJob="";
        readonly Dictionary<string,CancellationTokenSource> translationJobs=new Dictionary<string,CancellationTokenSource>();
        readonly string appDir = AppDomain.CurrentDomain.BaseDirectory;
        bool importing;
        readonly bool debug,silentTest;
        readonly FileStream databaseLock;
        bool closeReady,closingPending;
        bool engineUnavailable,recoveryNotice;
        Panel engineErrorPanel;
        string lastMediaId;
        bool mediaFullScreen;
        Rectangle windowedBounds;
        FormWindowState windowedState;
        FormBorderStyle windowedBorder;
        Size windowedMinimum;
        public const int MediaResponseLimit=4*1024*1024;
        public MainWindow(string[] args) {
            Text="kok"; Width=1440; Height=940; MinimumSize=new Size(760,520);
            StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(248,249,251);
            string iconFile=Path.Combine(appDir,"ui","kok.ico");
            Icon=File.Exists(iconFile)?new Icon(iconFile):Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            debug=args.Contains("--debug");
            silentTest=debug&&args.Contains("--test-silent");
            string dataDir=Path.Combine(appDir,"data");
            int dataIndex=Array.IndexOf(args,"--data-dir");
            if(dataIndex>=0 && dataIndex+1<args.Length) dataDir=Path.GetFullPath(args[dataIndex+1]);
            Directory.CreateDirectory(dataDir);
            RuntimeLog.Configure(dataDir);RuntimeLog.Write("startup","version=0.5.1 process="+Process.GetCurrentProcess().Id);
            try { databaseLock=new FileStream(Path.Combine(dataDir,".kok.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) { throw new InvalidOperationException("这个资料库已经在另一个 kok 窗口中打开，请使用原窗口。\n如需独立窗口，请通过 --data-dir 指定其他资料库。"); }
            library=new Library(dataDir);
            normalLibrary=library;InitializePrivateWindow();
            translations=new TranslationService(dataDir);
            InitializeDownloads();
            folderTimer.Tick+=async delegate {folderTimer.Interval=45000;if(library.AiEnabled&&!privateOpening&&!importing&&!closingPending&&library.HasEnabledFolders())try{await ScanSources();}catch(Exception ex){RuntimeLog.Write("folder-scan-error",ex.GetType().Name);}};
            folderTimer.Start();
            FormClosed+=delegate { ClosePreviewWindows();folderTimer.Dispose(); databaseLock.Dispose(); };
            FormClosing+=HandleWindowClosing;
            Activated+=delegate{if(web.CoreWebView2!=null&&!engineUnavailable)Send(new{eventName="workspaceFocus",data=new{}});};
            web.DefaultBackgroundColor=BackColor; web.Dock=DockStyle.Fill; web.AllowExternalDrop=true; Controls.Add(web);
            InitializeWindowChrome();
            Load+=async delegate { await Init(); };
        }
        async void HandleWindowClosing(object sender,FormClosingEventArgs e) {
            if(closeReady||engineUnavailable||web.CoreWebView2==null)return;
            e.Cancel=true;if(closingPending)return;
            if(importing){MessageBox.Show(this,L("正在导入文件，请等待本次整理完成后再退出。"),"kok");return;}
            closingPending=true;
            folderTimer.Stop();if(folderCancellation!=null)folderCancellation.Cancel();if(organizationCancellation!=null)organizationCancellation.Cancel();
            if(fileChatCancellation!=null)fileChatCancellation.Cancel();if(modelListCancellation!=null)modelListCancellation.Cancel();
            foreach(var job in translationJobs.Values)job.Cancel();CancelSpeechJobs();
            CancelCreativeJobs();if(musicLookupCancellation!=null)musicLookupCancellation.Cancel();
            try {
                await SaveCurrentSession();
            }catch(Exception ex){MessageBox.Show(this,L("退出前保存失败："+ex.Message+"\n此前自动保存的资料仍然保留。"),L("kok · 保存提示"));}
            ClosePreviewWindows();closeReady=true;Close();
        }
        async Task Init() {
            try {
                string browser=null;
                try { CoreWebView2Environment.GetAvailableBrowserVersionString(); }
                catch {
                    // An installed Microsoft Edge can also supply the browser engine.
                    string[] candidates={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft","Edge","Application"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft","Edge","Application")};
                    browser=candidates.FirstOrDefault(x=>File.Exists(Path.Combine(x,"msedge.exe")));
                    if(browser==null) throw new InvalidOperationException("请安装微软 WebView2 Evergreen Runtime，然后重新打开 kok。\n下载：https://developer.microsoft.com/microsoft-edge/webview2/");
                }
                var options=new CoreWebView2EnvironmentOptions((debug ? "--remote-debugging-port=9225" : "")+(silentTest ? " --mute-audio" : ""));
                options.Language=library.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(library.Db.settings["uiLanguage"])=="en"?"en-US":"zh-CN";
                var env=await CoreWebView2Environment.CreateAsync(browser,Path.Combine(library.DataDir,"WebView2"),options);
                await web.EnsureCoreWebView2Async(env);
                web.CoreWebView2.Settings.AreDevToolsEnabled=debug;
                web.CoreWebView2.Settings.IsStatusBarEnabled=false;
                web.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
                web.CoreWebView2.Settings.IsPasswordAutosaveEnabled=false;
                web.CoreWebView2.Settings.IsGeneralAutofillEnabled=false;
                web.CoreWebView2.SetVirtualHostNameToFolderMapping("app.kok.local",Path.Combine(appDir,"ui"),CoreWebView2HostResourceAccessKind.DenyCors);
                web.CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
                web.CoreWebView2.WebResourceRequested+=Resource;
                web.CoreWebView2.WebMessageReceived+=Message;
                web.CoreWebView2.ProcessFailed+=HandleProcessFailed;
                web.CoreWebView2.ContainsFullScreenElementChanged+=delegate { SetMediaFullScreen(web.CoreWebView2.ContainsFullScreenElement); };
                web.CoreWebView2.NavigationCompleted+=delegate(object sender,CoreWebView2NavigationCompletedEventArgs e) {
                    if(e.IsSuccess&&recoveryNotice) { engineUnavailable=false;recoveryNotice=false;Notify("engineRecovered",null); }
                    else if(!e.IsSuccess&&recoveryNotice) { engineUnavailable=true;ShowEngineError(); }
                };
                web.CoreWebView2.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){ e.Handled=true; };
                web.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){ if(!e.Uri.StartsWith("https://app.kok.local/",StringComparison.OrdinalIgnoreCase)) e.Cancel=true; };
                web.CoreWebView2.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e){ e.State=CoreWebView2PermissionState.Deny; };
                web.CoreWebView2.Navigate("https://app.kok.local/index.html?v=0.5.1-refined");
            } catch(Exception ex) { RuntimeLog.Write("engine-init-error",ex);MessageBox.Show(this,L(ex.Message),L("kok · 初始化失败"),MessageBoxButtons.OK,MessageBoxIcon.Error); Close(); }
        }
        void HandleProcessFailed(object sender,CoreWebView2ProcessFailedEventArgs e) {
            var kind=e.ProcessFailedKind;
            RuntimeLog.Write("engine-failure","kind="+kind+" reason="+e.Reason+" exit="+e.ExitCode);
            if(kind!=CoreWebView2ProcessFailedKind.RenderProcessExited&&kind!=CoreWebView2ProcessFailedKind.BrowserProcessExited)return;
            engineUnavailable=true;
            foreach(var job in translationJobs.Values)job.Cancel();
            if(IsDisposed||Disposing||!IsHandleCreated)return;
            BeginInvoke((Action)(async ()=>{
                if(IsDisposed||Disposing)return;
                if(library.IsPrivate)try{await LockPrivateVault();}catch(Exception error){RuntimeLog.Write("private-recovery-lock-error",error);ShowEngineError();return;}
                SetMediaFullScreen(false);
                if(kind==CoreWebView2ProcessFailedKind.RenderProcessExited) {
                    try { recoveryNotice=true;engineUnavailable=false;web.CoreWebView2.Reload();return; }
                    catch(Exception error) { engineUnavailable=true;RuntimeLog.Write("engine-reload-error",error); }
                }
                ShowEngineError();
            }));
        }
        void SetMediaFullScreen(bool enabled) {
            if(enabled==mediaFullScreen||IsDisposed)return;
            SuspendLayout();
            try {
                if(enabled) {
                    var screen=Screen.FromControl(this).Bounds;
                    windowedState=WindowState;windowedBorder=FormBorderStyle;windowedMinimum=MinimumSize;
                    windowedBounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
                    mediaFullScreen=true;MinimumSize=Size.Empty;WindowState=FormWindowState.Normal;
                    FormBorderStyle=FormBorderStyle.None;Bounds=screen;
                } else {
                    mediaFullScreen=false;WindowState=FormWindowState.Normal;FormBorderStyle=windowedBorder;
                    MinimumSize=windowedMinimum;Bounds=windowedBounds;WindowState=windowedState;
                }
            } finally { LayoutWindowChrome();ResumeLayout(true); }
        }
        void ShowEngineError() {
            if(engineErrorPanel!=null)return;
            engineErrorPanel=new Panel { Dock=DockStyle.Fill,BackColor=BackColor,Padding=new Padding(48) };
            var layout=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true };
            layout.Controls.Add(new Label { AutoSize=true,Text=L("播放内核已停止，窗口和资料库已保留。"),Font=new Font("Segoe UI",15),Margin=new Padding(0,35,0,20) });
            layout.Controls.Add(new Label { AutoSize=true,Text=L("请关闭窗口并重新打开 kok。故障日志位于 data/logs/kok.log。"),MaximumSize=new Size(700,0),Margin=new Padding(0,0,0,25) });
            var item=lastMediaId==null?null:library.Find(lastMediaId);
            if(item!=null&&File.Exists(item.path)) {
                var external=new Button { AutoSize=true,Padding=new Padding(15,8,15,8),Text=L("系统播放器打开") };
                external.Click+=delegate { try{Process.Start(new ProcessStartInfo(item.path){UseShellExecute=true});}catch(Exception error){RuntimeLog.Write("external-open-error",error);} };layout.Controls.Add(external);
            }
            var close=new Button { AutoSize=true,Padding=new Padding(15,8,15,8),Text=L("关闭窗口"),Margin=new Padding(0,18,0,0) };close.Click+=delegate{closeReady=true;Close();};layout.Controls.Add(close);
            engineErrorPanel.Controls.Add(layout);Controls.Add(engineErrorPanel);engineErrorPanel.BringToFront();
            LayoutWindowChrome();
        }
        void Send(object value) {
            if(IsDisposed||engineUnavailable||web.CoreWebView2==null)return;
            try { web.CoreWebView2.PostWebMessageAsJson(Library.Json.Serialize(value)); }
            catch(Exception error) { RuntimeLog.Write("message-delivery-error",error); }
        }
        void Notify(string name,object value) {
            if(name=="privateLocking")foreach(var preview in previewWindows.ToArray())preview.Hide();
            if(name=="librarySwitched")ClosePreviewWindows();
            var message=new {eventName=name,data=value};Send(message);
            foreach(var preview in previewWindows.ToArray())if(!preview.IsDisposed&&preview.SessionLibrary==library)SendTo(preview.Browser.CoreWebView2,message);
        }
        string L(string value) { return Localization.Translate(value,library!=null&&library.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(library.Db.settings["uiLanguage"])=="en"); }
        static string StringArg(Dictionary<string,object> a,string key) { return a.ContainsKey(key) ? Convert.ToString(a[key]) : ""; }
        async void Message(object sender,CoreWebView2WebMessageReceivedEventArgs e) {
            if(!e.Source.StartsWith("https://app.kok.local/",StringComparison.OrdinalIgnoreCase)) return;
            var replyCore=sender as CoreWebView2;var preview=PreviewFor(sender);
            if(replyCore!=web.CoreWebView2&&preview==null)return;
            string requestId="";bool nativeDialogScope=false;Form dialogOwner=preview==null?(Form)this:preview;
            try {
                var message=Library.Json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);
                requestId=StringArg(message,"requestId"); string action=StringArg(message,"action");
                if(preview!=null&&preview.SessionLibrary!=library)throw new InvalidOperationException("私密资料库正在锁定，请稍候。");
                var a=message.ContainsKey("args") ? message["args"] as Dictionary<string,object> : null;
                if(a==null) a=new Dictionary<string,object>();
                if(privateOpening&&!new[]{"diagnostics","vaultStatus","vaultActivity","cancelTranslation","cancelOrganization","cancelFileChat"}.Contains(action))throw new InvalidOperationException("私密文件夹正在打开，请稍候。");
                nativeDialogScope=new[]{"importFiles","importFolder","importFont","chooseSubtitle","savePhoto","exportTranslation","exportReading","exportReadingNotes","chooseDownloadFolder","chooseFfmpeg","export","loginDownloadSite","importDownloadLogin","browserProbeDownload"}.Contains(action);if(nativeDialogScope)nativeFileDialogOpen=true;
                if(privateGate&&library.IsPrivate&&!new[]{"lockPrivate","unlockPrivate","vaultStatus","vaultActivity","cancelTranslation","cancelOrganization","cancelFileChat"}.Contains(action))throw new InvalidOperationException("私密资料库正在锁定，请稍候。");
                if(new[]{"aiConfig","saveAiConfig","aiModels","translate","organizeFiles","chatWithFile","applyOrganization","runStudyTask","generateVideoOutline"}.Contains(action))library.RequireAiMode();
                object result=null;
                switch(action) {
                    case "setWindowAppearance": {Color color;if(!TryChromeColor(StringArg(a,"color"),out color))throw new InvalidDataException("窗口颜色无效。");if(preview!=null)preview.Appearance(color);else ApplyWindowAppearance(color,a.ContainsKey("reduceMotion")&&Convert.ToBoolean(a["reduceMotion"]));result=true;break;}
                    case "openPreview": result=await OpenPreview(a);break;
                    case "previewWindows": result=PreviewList();break;
                    case "focusPreview": {var target=previewWindows.FirstOrDefault(w=>w.ItemId==StringArg(a,"id")&&w.SessionLibrary==library);if(target!=null)target.RestorePreview();result=true;break;}
                    case "previewReady": if(preview!=null){preview.Present(a.ContainsKey("reduceMotion")&&Convert.ToBoolean(a["reduceMotion"]));PreviewListChanged();}result=true;break;
                    case "previewItemChanged": if(preview!=null){var item=RequireEntry(a);preview.ItemId=item.id;preview.Text=item.name;PreviewListChanged();}result=true;break;
                    case "sendPreviewBehind": if(preview!=null)preview.SendBehind();result=true;break;
                    case "dragPreview": if(preview!=null)preview.DragPreview();result=true;break;
                    case "resizePreview": if(preview!=null)preview.ResizePreview(Convert.ToInt32(a["edge"]));result=true;break;
                    case "maximizePreview": if(preview!=null)preview.ToggleMaximize();result=true;break;
                    case "minimizePreview": if(preview!=null)preview.WindowState=FormWindowState.Minimized;result=true;break;
                    case "closePreview": if(preview!=null)preview.BeginInvoke((Action)(()=>preview.Close()));result=true;break;
                    case "appModeState": result=library.ModeState();break;
                    case "setAppMode": {
                        library.SetAppMode(StringArg(a,"mode"),a.ContainsKey("acknowledged")&&Convert.ToBoolean(a["acknowledged"]));
                        if(!library.AiEnabled){CancelSpeechJobs();PauseNetworkDownloads();if(musicLookupCancellation!=null)musicLookupCancellation.Cancel();foreach(var job in translationJobs.Values.ToArray())job.Cancel();if(fileChatCancellation!=null)fileChatCancellation.Cancel();if(organizationCancellation!=null)organizationCancellation.Cancel();if(modelListCancellation!=null)modelListCancellation.Cancel();if(folderCancellation!=null)folderCancellation.Cancel();organizationPreview=null;}
                        result=library.ModeState();break;
                    }
                    case "library": {var snapshot=Library.Json.Deserialize<Dictionary<string,object>>(library.Snapshot());snapshot["isPrivate"]=library.IsPrivate;if(library.IsPrivate)snapshot["navigation"]=new{items=normalLibrary.Db.items.Select(item=>new{kind=item.kind,collection=item.collection}).ToArray(),collections=normalLibrary.Db.collections};result=snapshot;break;}
                    case "vaultStatus": result=new{exists=File.Exists(Path.Combine(VaultDirectory,"vault-info.json")),unlocked=library.IsPrivate,autoLockMinutes=5};break;
                    case "createPrivate": result=await OpenPrivateVault(StringArg(a,"password"),true);break;
                    case "unlockPrivate": result=await OpenPrivateVault(StringArg(a,"password"),false);break;
                    case "lockPrivate": await LockPrivateVault();result=true;break;
                    case "vaultActivity": lastPrivateActivity=DateTime.UtcNow;result=true;break;
                    case "readHelp": result=File.ReadAllText(Path.Combine(appDir,L("README.md")),Encoding.UTF8);break;
                    case "importSamples": {
                        if(importing) throw new InvalidOperationException("正在导入，请稍候。");
                        string samples=Path.Combine(appDir,"samples");
                        if(!Directory.Exists(samples)) throw new DirectoryNotFoundException("示例目录不存在。");
                        importing=true; Notify("importing",new { count=0 });
                        try { result=await Task.Run(()=>library.Import(Directory.GetFiles(samples),null)); }
                        finally { importing=false; Notify("imported",null); }
                        break;
                    }
                    case "importDropped": {
                        if(importing) throw new InvalidOperationException("正在导入，请稍候。");
                        // Use native-backed DOM files only. Never accept a path supplied in page JSON.
                        var objects=e.AdditionalObjects;
                        string[] dropped=objects==null ? new string[0] : objects.OfType<CoreWebView2File>().Select(f=>f.Path).Where(p=>!String.IsNullOrWhiteSpace(p)).ToArray();
                        if(dropped.Length==0) throw new InvalidDataException("未收到本地文件，请从文件资源管理器拖入，或点击导入文件。");
                        importing=true; Notify("importing",new { count=0 });
                        try { result=await Task.Run(()=>library.ImportDropped(dropped,n=>BeginInvoke((Action)(()=>Notify("importing",new{count=n}))))); }
                        finally { importing=false; Notify("imported",null); }
                        break;
                    }
                    case "importFiles": case "importFolder":
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
                                ImportResult report=library.Import(input,n=>BeginInvoke((Action)(()=>Notify("importing",new{count=n}))));
                                report.errors.AddRange(errors); return report;
                            });
                        } finally { importing=false; Notify("imported",null); }
                        break;
                    case "update": library.Update(StringArg(a,"id"),(Dictionary<string,object>)a["patch"]); result=true; break;
                    case "remove": library.Remove(StringArg(a,"id")); result=true; break;
                    case "settings": library.Settings(a);if(library.IsPrivate){var shared=a.Where(pair=>pair.Key=="theme"||pair.Key=="uiLanguage"||Library.AppearanceKeys.Contains(pair.Key)).ToDictionary(pair=>pair.Key,pair=>pair.Value);if(shared.Count>0)normalLibrary.Settings(shared);} result=true; break;
                    case "saveVideoCover": {var targetLibrary=library;backgroundLibraryWrites++;try{result=await Task.Run(()=>targetLibrary.SaveVideoCover(StringArg(a,"id"),StringArg(a,"image")));}finally{backgroundLibraryWrites--;}break;}
                    case "fileProperties": {
                        var item=RequireEntry(a);var info=new FileInfo(item.path);
                        result=new { name=info.Name,path=info.FullName,size=info.Length,extension=item.ext,modified=info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") };break;
                    }
                    case "aiConfig": result=translations.PublicConfig();break;
                    case "runStudyTask": result=await RunStudyTask(a);break;
                    case "generateVideoOutline": result=await GenerateVideoOutline(a);break;
                    case "fileDates": await Task.Run(()=>library.FillFileDates());result=true;break;
                    case "bulkClassify": library.BulkClassify(Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["ids"])),StringArg(a,"collection"));result=true;break;
                    case "fileChatHistory": result=library.ChatHistory(StringArg(a,"id"));break;
                    case "clearFileChat": if(fileChatCancellation!=null)throw new InvalidOperationException("请先停止当前回答。");library.ClearChat(StringArg(a,"id"));result=true;break;
                    case "cancelFileChat": if(fileChatCancellation!=null&&fileChatJob==StringArg(a,"jobId"))fileChatCancellation.Cancel();result=true;break;
                    case "chatWithFile": {
                        if(fileChatCancellation!=null)throw new InvalidOperationException("请先停止当前回答。");var item=RequireEntry(a);string jobId=StringArg(a,"jobId"),question=StringArg(a,"question"),source=StringArg(a,"source");if(jobId.Length<1||jobId.Length>80)throw new InvalidDataException("提问或引用内容过长，请缩小范围。");
                        var references=Library.ReadChatReferences(a);
                        using(var cancel=new CancellationTokenSource()){fileChatCancellation=cancel;fileChatJob=jobId;
                            try{var answer=await translations.ChatAsync(item.name,StringArg(a,"context"),source,question,library.ChatHistory(item.id),StringArg(a,"image"),library.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(library.Db.settings["uiLanguage"])=="en",cancel.Token,delta=>Notify("fileChatDelta",new{jobId=jobId,text=delta}));cancel.Token.ThrowIfCancellationRequested();library.AddChat(item.id,question,answer,source,references);result=library.ChatHistory(item.id);}
                            catch(OperationCanceledException){throw new InvalidOperationException("AI 回答已停止。");}finally{fileChatCancellation=null;fileChatJob="";}
                        }break;
                    }
                    case "sources": result=new {folders=library.Folders(),busy=importing};break;
                    case "addSource": {
                        if(importing)throw new InvalidOperationException("正在导入，请稍候。");
                        using(var dialog=new FolderBrowserDialog{Description=L("选择自动扫描的文件夹"),ShowNewFolderButton=false}){if(dialog.ShowDialog(dialogOwner)==DialogResult.OK)result=library.AddFolder(dialog.SelectedPath);else result=new{cancelled=true};}break;
                    }
                    case "changeSource": library.ChangeFolder(StringArg(a,"id"),a.ContainsKey("enabled")?(bool?)Convert.ToBoolean(a["enabled"]):null);result=true;break;
                    case "scanSources": result=await ScanSources();break;
                    case "cancelSourceScan": if(folderCancellation!=null)folderCancellation.Cancel();result=true;break;
                    case "organizeFiles": {
                        if(organizationCancellation!=null)throw new InvalidOperationException("AI 正在整理，请等待或取消当前任务。");
                        var ids=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["ids"]));bool content=a.ContainsKey("content")&&Convert.ToBoolean(a["content"]);
                        using(var cancel=new CancellationTokenSource()){organizationCancellation=cancel;organizationPreview=null;
                            try{var inputs=await Task.Run(()=>library.OrganizeInputs(ids,content));cancel.Token.ThrowIfCancellationRequested();var rows=await translations.OrganizeAsync(inputs,content,library.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(library.Db.settings["uiLanguage"])=="en",cancel.Token,(done,total)=>Notify("organizationProgress",new{done=done,total=total}));cancel.Token.ThrowIfCancellationRequested();organizationPreview=rows;result=rows;}
                            catch(OperationCanceledException){throw new InvalidOperationException("AI 整理已取消。");}finally{organizationCancellation=null;}
                        }break;
                    }
                    case "cancelOrganization": if(organizationCancellation!=null)organizationCancellation.Cancel();result=true;break;
                    case "applyOrganization": if(organizationPreview==null)throw new InvalidDataException("整理结果已失效，请重新分析。");result=library.ApplyOrganization(organizationPreview,Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["ids"])));organizationPreview=null;break;
                    case "editOrganization": library.EditOrganization(StringArg(a,"id"),StringArg(a,"category"),Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["tags"])),StringArg(a,"summary"));result=true;break;
                    case "saveAiConfig": result=translations.SaveConfig(a);break;
                    case "aiModels": {if(modelListCancellation!=null)throw new InvalidOperationException("正在读取模型列表，请稍候。");using(var cancel=new CancellationTokenSource()){modelListCancellation=cancel;try{result=await translations.ModelsAsync(a,cancel.Token);}finally{modelListCancellation=null;}}break;}
                    case "splitTranslationText": result=TranslationService.SplitText(StringArg(a,"text"));break;
                    case "cancelTranslation": {CancellationTokenSource job;if(translationJobs.TryGetValue(StringArg(a,"jobId"),out job))job.Cancel();result=true;break;}
                    case "translate": {
                        string jobId=StringArg(a,"jobId");if(jobId.Length<1||jobId.Length>80||translationJobs.ContainsKey(jobId)||translationJobs.Count>=4)throw new InvalidOperationException("翻译任务正在切换，请稍后重试。");
                        var parts=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["parts"]));
                        using(var cancel=new CancellationTokenSource()){
                            translationJobs.Add(jobId,cancel);
                            try{result=await translations.TranslateAsync(parts,cancel.Token,(done,total)=>{if(!IsDisposed)Notify("translationProgress",new{jobId=jobId,completed=done,total=total});},!a.ContainsKey("test")||!Convert.ToBoolean(a["test"]),a.ContainsKey("language")?StringArg(a,"language"):null,(start,output)=>{if(!IsDisposed)Notify("translationBatch",new{jobId=jobId,start=start,parts=output});},a.ContainsKey("fast")&&Convert.ToBoolean(a["fast"]));}
                            catch(OperationCanceledException){throw new InvalidOperationException("翻译已取消。");}
                            finally{translationJobs.Remove(jobId);}
                        }break;
                    }
                    case "exportTranslation": {
                        string text=StringArg(a,"text"),format=StringArg(a,"format");if(text.Length>16*1024*1024)throw new InvalidDataException("译文过大。");
                        string extension=format=="srt"?"srt":"txt";
                        using(var dialog=new SaveFileDialog{Title=L("导出译文"),Filter=L(extension=="srt"?"SRT 字幕|*.srt":"文本译文|*.txt"),FileName=StringArg(a,"name")+L("-译文")+"."+extension}){
                            if(dialog.ShowDialog(dialogOwner)==DialogResult.OK){EnsureExportDestination(dialog.FileName);File.WriteAllText(dialog.FileName,text,new UTF8Encoding(false));result=true;}
                        }break;
                    }
                    case "addCollection": library.AddCollection(StringArg(a,"name"));result=true;break;
                    case "deleteCollection": library.DeleteCollection(StringArg(a,"name"));result=true;break;
                    case "importFont": {
                        using(var dialog=new OpenFileDialog{Title=L("导入你有权使用的字体"),Filter=L("字体文件|*.ttf;*.otf")}) { if(dialog.ShowDialog(dialogOwner)==DialogResult.OK)result=library.ImportFont(dialog.FileName); }
                        break;
                    }
                    case "subtitles": {var entry=RequireEntry(a);var tracks=await Task.Run(()=>Subtitles.Discover(entry.path));if(entry.generatedSubtitles!=null&&entry.generatedSubtitles.cues.Count>0)tracks.Insert(0,entry.generatedSubtitles);result=tracks;break;}
                    case "chooseSubtitle": {
                        using(var dialog=new OpenFileDialog{Title=L("载入字幕"),Filter=L("文字字幕|*.srt;*.vtt;*.ass;*.ssa;*.sub;*.sbv;*.lrc;*.ttml;*.dfxp;*.smi|所有文件|*.*")}) { if(dialog.ShowDialog(dialogOwner)==DialogResult.OK)result=Subtitles.Read(dialog.FileName); }
                        break;
                    }
                    case "savePhoto": {
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
                    case "exportReadingNotes": result=ExportReadingNotes(a);break;
                    case "exportReading": {
                        string html=StringArg(a,"html");if(html.Length>32*1024*1024)throw new InvalidDataException("导出内容过大。");
                        using(var dialog=new SaveFileDialog{Title=L("导出带标记的阅读副本"),Filter=L("HTML 阅读副本|*.html"),FileName=StringArg(a,"name")+L("-批注")+".html"}) { if(dialog.ShowDialog(dialogOwner)==DialogResult.OK){EnsureExportDestination(dialog.FileName);File.WriteAllText(dialog.FileName,html,new UTF8Encoding(false));result=true;} }break;
                    }
                    case "read": {
                        Entry item=RequireEntry(a); result=await Task.Run(()=>Library.ReadBook(item)); break;
                    }
                    case "openExternal": {
                        Entry item=RequireEntry(a); Process.Start(new ProcessStartInfo(item.path){UseShellExecute=true}); result=true; break;
                    }
                    case "reveal": {
                        Entry item=RequireEntry(a); Process.Start("explorer.exe","/select,\""+item.path+"\""); result=true; break;
                    }
                    case "export": {
                        if(library.IsPrivate){using(var privateDialog=new SaveFileDialog{Title=L("导出加密私密备份"),Filter=L("私密资料库备份|*.kokvault"),FileName="kok-private-"+DateTime.Now.ToString("yyyyMMdd")+".kokvault"}){if(privateDialog.ShowDialog(dialogOwner)==DialogResult.OK){ExportPrivateBackup(privateDialog.FileName);result=true;}}break;}
                        using(var dialog=new SaveFileDialog { Title=L("导出资料库索引与笔记"), Filter=L("JSON 资料库|*.json"), FileName="kok-library-"+DateTime.Now.ToString("yyyyMMdd")+".json" }) {
                            if(dialog.ShowDialog(dialogOwner)==DialogResult.OK) { EnsureExportDestination(dialog.FileName);File.WriteAllText(dialog.FileName,library.Snapshot(),new UTF8Encoding(false)); result=true; }
                        } break;
                    }
                    case "diagnostics": result=new { engine=web.CoreWebView2.Environment.BrowserVersionString, count=library.Db.items.Count, dataDir=library.DataDir, version="0.5.1", mediaResponseLimit=MediaResponseLimit, logFile=RuntimeLog.FilePath, silentTest=silentTest, window=new { handle=Handle.ToInt64(),fullScreen=mediaFullScreen, border=FormBorderStyle.ToString(), state=WindowState.ToString(), bounds=new { x=Left,y=Top,width=Width,height=Height }, screen=Screen.FromControl(this).Bounds,workArea=Screen.FromControl(this).WorkingArea,clientScreen=RectangleToScreen(ClientRectangle), chrome=new { custom=customChrome,color=ColorTranslator.ToHtml(chromeColor),height=ChromeHeight,edge=ChromeEdge,handle=windowChrome.Handle.ToInt64(),content=web.Bounds,buttons=new[]{new {name="minimize",handle=minimizeWindow.Handle.ToInt64(),bounds=minimizeWindow.Bounds},new {name="maximize",handle=maximizeWindow.Handle.ToInt64(),bounds=maximizeWindow.Bounds},new {name="close",handle=closeWindow.Handle.ToInt64(),bounds=closeWindow.Bounds}}} } }; break;
                    case "formats": result=Compatibility.Groups; break;
                    default: if(ReadingSessionActions.Contains(action))result=await HandleReadingSessionAction(action,a);else if(SpeechActions.Contains(action))result=await HandleSpeechAction(action,a,dialogOwner);else if(DocumentTranslationActions.Contains(action))result=await HandleDocumentTranslationAction(action,a,dialogOwner);else if(MusicActions.Contains(action))result=await HandleMusicAction(action,a);else if(CreativeActions.Contains(action))result=await HandleCreativeAction(action,a,dialogOwner);else if(DownloadActions.Contains(action))result=await HandleDownloadAction(action,a);else throw new InvalidOperationException("未知操作。");break;
                }
                SendTo(replyCore,new { requestId=requestId, result=result });
                if(preview!=null&&new[]{"update","remove","savePhoto","importFont"}.Contains(action))Send(new{eventName="libraryChanged",data=(object)null});
                if(action=="settings"||action=="setAppMode")Notify("previewPreferencesChanged",null);
            } catch(Exception ex) { SendTo(replyCore,new { requestId=requestId, error=ex.Message }); }finally{if(nativeDialogScope)nativeFileDialogOpen=false;}
        }
        Entry RequireEntry(Dictionary<string,object> a) { Entry item=library.Find(StringArg(a,"id")); if(item==null || !File.Exists(item.path)) throw new FileNotFoundException("找不到原文件，文件可能已移动。请从资料库移除后重新导入。"); return item; }
        async Task<ImportResult> ScanSources() {
            if(importing)throw new InvalidOperationException("正在导入，请稍候。");
            importing=true;Notify("sourceScan",new{busy=true});
            using(var cancel=new CancellationTokenSource()){folderCancellation=cancel;
                try{var report=await Task.Run(()=>library.ScanFolders(appDir,cancel.Token,n=>BeginInvoke((Action)(()=>Notify("sourceScan",new{busy=true,count=n})))));Notify("sourceScan",new{busy=false,added=report.added,errors=report.errors});if(report.added>0)Notify("libraryChanged",null);return report;}
                catch(OperationCanceledException){library.Save();Notify("sourceScan",new{busy=false,cancelled=true});Notify("libraryChanged",null);return new ImportResult();}
                catch{Notify("sourceScan",new{busy=false});throw;}
                finally{importing=false;folderCancellation=null;}
            }
        }
        void Resource(object sender,CoreWebView2WebResourceRequestedEventArgs e) {
            try {
                var preview=PreviewFor(sender);if(preview!=null&&preview.SessionLibrary!=library){RespondError(e,403,"Library Changed");return;}
                var requestUri=new Uri(e.Request.Uri);
                if(privateGate&&requestUri.Host.Equals("media.kok.local",StringComparison.OrdinalIgnoreCase)){RespondError(e,403,"Vault Locked");return;}
                if(!requestUri.Host.Equals("media.kok.local",StringComparison.OrdinalIgnoreCase)){
                    if(!library.AiEnabled&&(requestUri.Scheme=="http"||requestUri.Scheme=="https")&&!requestUri.Host.Equals("app.kok.local",StringComparison.OrdinalIgnoreCase))RespondError(e,403,"Offline Mode");
                    return;
                }
                if(e.Request.Method=="OPTIONS") {
                    e.Response=web.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(),204,"No Content","Content-Length: 0\r\nAccess-Control-Allow-Origin: https://app.kok.local\r\nAccess-Control-Allow-Methods: GET, HEAD, OPTIONS\r\nAccess-Control-Allow-Headers: Range\r\nAccess-Control-Max-Age: 600\r\n");return;
                }
                bool head=e.Request.Method=="HEAD";
                if(e.Request.Method!="GET"&&!head){RespondError(e,405,"Method Not Allowed");return;}
                Uri uri=new Uri(e.Request.Uri); string[] parts=uri.AbsolutePath.Trim('/').Split('/');
                if(parts.Length!=2) { RespondError(e,404,"Not Found"); return; }
                if(parts[0]=="font") {
                    var font=library.Db.fonts.FirstOrDefault(f=>f.id==parts[1]);if(font==null||!File.Exists(font.path)){RespondError(e,404,"Not Found");return;}
                    e.Response=web.CoreWebView2.Environment.CreateWebResourceResponse(File.OpenRead(font.path),200,"OK","Content-Type: font/ttf\r\nAccess-Control-Allow-Origin: https://app.kok.local\r\n");return;
                }
                Entry item=library.Find(parts[1]); if(item==null) { RespondError(e,404,"Not Found"); return; }
                if(parts[0]=="file"&&(item.kind=="video"||item.kind=="music")&&lastMediaId!=item.id) {
                    lastMediaId=item.id;RuntimeLog.Write("media-open","id="+item.id+" type="+item.ext+" bytes="+item.size);
                }
                string path=parts[0]=="thumb" ? library.ThumbnailPath(item) : parts[0]=="file" ? item.path : null;
                if(path==null || !File.Exists(path)) { RespondError(e,404,"Not Found"); return; }
                Stream file=parts[0]=="thumb"?library.OpenThumbnail(item):(Stream)new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                long size=file.Length,start=0,end=size-1;
                string mime=parts[0]=="thumb" ? "image/jpeg" : Mime(item.ext);
                string headers="Content-Type: "+mime+"\r\nAccept-Ranges: bytes\r\nAccess-Control-Allow-Origin: https://app.kok.local\r\nAccess-Control-Expose-Headers: Content-Range, Accept-Ranges, Content-Length\r\nX-Content-Type-Options: nosniff\r\nCache-Control: no-store\r\n";
                string range=e.Request.Headers.Contains("Range") ? e.Request.Headers.GetHeader("Range") : null;
                bool partial=!String.IsNullOrEmpty(range);
                if(partial && !TryRange(range,size,out start,out end)) { file.Dispose(); e.Response=web.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(),416,"Range Not Satisfiable",headers+"Content-Range: bytes */"+size+"\r\n"); return; }
                // The WebView2 response bridge can buffer the response even when the .NET
                // source is a FileStream. Never hand it an entire multi-gigabyte movie.
                long count=Math.Max(0,end-start+1);
                if(!head&&parts[0]=="file"&&(item.kind=="video"||item.kind=="music")&&count>MediaResponseLimit) {
                    if(!partial) { file.Dispose(); RespondError(e,413,"Range Required"); return; }
                    count=MediaResponseLimit;end=start+count-1;
                }
                if(partial) headers+="Content-Range: bytes "+start+"-"+end+"/"+size+"\r\n";
                headers+="Content-Length: "+count+"\r\n";
                Stream body;
                if(head){file.Dispose();body=new MemoryStream();}else body=new SliceStream(file,start,count);
                e.Response=web.CoreWebView2.Environment.CreateWebResourceResponse(body,partial?206:200,partial?"Partial Content":"OK",headers);
            } catch(Exception error) { RuntimeLog.Write("resource-error",error);try{RespondError(e,500,"Read Error");}catch(Exception responseError){RuntimeLog.Write("resource-response-error",responseError);} }
        }
        void RespondError(CoreWebView2WebResourceRequestedEventArgs e,int code,string reason) { e.Response=web.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(),code,reason,"Content-Length: 0\r\nAccess-Control-Allow-Origin: https://app.kok.local\r\n"); }
        public static bool TryRange(string range,long size,out long start,out long end) {
            start=0; end=size-1; if(size==0 || !range.StartsWith("bytes=") || range.Contains(",")) return false;
            string[] bits=range.Substring(6).Split('-'); if(bits.Length!=2) return false;
            long number;
            if(bits[0]=="") { if(!Int64.TryParse(bits[1],out number) || number<=0) return false; start=Math.Max(0,size-number); }
            else { if(!Int64.TryParse(bits[0],out start) || start<0 || start>=size) return false; if(bits[1]!="") { if(!Int64.TryParse(bits[1],out number) || number<start) return false; end=Math.Min(size-1,number); } }
            return true;
        }
        static string Mime(string ext) {
            switch(ext.ToLowerInvariant()) {
                case "mp4": case "m4v": return "video/mp4"; case "webm": return "video/webm"; case "mov": return "video/quicktime";
                case "mp3": return "audio/mpeg"; case "m4a": return "audio/mp4"; case "aac": return "audio/aac"; case "flac": return "audio/flac"; case "wav": return "audio/wav"; case "ogg": case "oga": case "opus": return "audio/ogg";
                case "jpg": case "jpeg": return "image/jpeg"; case "png": return "image/png"; case "gif": return "image/gif"; case "webp": return "image/webp"; case "avif": return "image/avif"; case "bmp": return "image/bmp";
                case "pdf": return "application/pdf"; default: return "application/octet-stream";
            }
        }
    }
    // WebView2 reads this seekable view without buffering a whole video in memory.
    public class SliceStream : Stream {
        readonly Stream inner; readonly long start,length; long position;
        public SliceStream(Stream source,long offset,long count) { inner=source; start=offset; length=count; inner.Position=start; }
        public override bool CanRead { get{return true;} } public override bool CanSeek { get{return true;} } public override bool CanWrite { get{return false;} }
        public override long Length { get{return length;} }
        public override long Position { get{return position;} set{Seek(value,SeekOrigin.Begin);} }
        public override int Read(byte[] buffer,int offset,int count) { int n=inner.Read(buffer,offset,(int)Math.Min(count,length-position)); position+=n; return n; }
        public override long Seek(long offset,SeekOrigin origin) { long p=origin==SeekOrigin.Begin?offset:origin==SeekOrigin.Current?position+offset:length+offset; if(p<0 || p>length) throw new IOException(); inner.Seek(start+p,SeekOrigin.Begin); position=p; return p; }
        public override void Flush() { } public override void SetLength(long value){throw new NotSupportedException();} public override void Write(byte[] b,int o,int c){throw new NotSupportedException();}
        protected override void Dispose(bool disposing) { if(disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
