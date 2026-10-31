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
            if(args.Contains("--mcp")){McpAccessHost.Run(args);return;}
            LiveEvaluationBudget.Initialize(args);
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
            RuntimeLog.Configure(dataDir);RuntimeLog.Write("startup","version="+AppVersion.Current+" process="+Process.GetCurrentProcess().Id);
            try { databaseLock=new FileStream(Path.Combine(dataDir,".kok.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) { throw new InvalidOperationException("这个资料库已经在另一个 kok 窗口中打开，请使用原窗口。\n如需独立窗口，请通过 --data-dir 指定其他资料库。"); }
            library=new Library(dataDir);
            normalLibrary=library;InitializePrivateWindow();
            translations=new TranslationService(dataDir);
            InitializeDownloads();
            folderTimer.Tick+=async delegate {folderTimer.Interval=45000;if(library.AiEnabled&&!privateOpening&&!importing&&!closingPending&&library.HasEnabledFolders())try{await ScanSources();}catch(Exception ex){RuntimeLog.Write("folder-scan-error",ex.GetType().Name);}};
            folderTimer.Start();
            FormClosed+=delegate { normalLibrary.StopConnections();ClosePreviewWindows();folderTimer.Dispose(); databaseLock.Dispose(); };
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
            closingPending=true;CancelAgentRun();
            folderTimer.Stop();if(folderCancellation!=null)folderCancellation.Cancel();if(organizationCancellation!=null)organizationCancellation.Cancel();
            if(fileChatCancellation!=null)fileChatCancellation.Cancel();if(modelListCancellation!=null)modelListCancellation.Cancel();
            foreach(var job in translationJobs.Values)job.Cancel();CancelSpeechJobs();
            CancelCreativeJobs();if(musicLookupCancellation!=null)musicLookupCancellation.Cancel();
            try {
                await SaveCurrentSession();
            }catch(Exception ex){
                string message=L("退出前保存失败："+ex.Message+"\n此前自动保存的资料仍然保留。")+"\n\n"+L("仍要关闭这个窗口吗？未保存的修改会丢失。");
                if(MessageBox.Show(this,message,L("kok · 保存提示"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes){closingPending=false;folderTimer.Start();return;}
            }
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
                web.CoreWebView2.Navigate("https://app.kok.local/index.html?v=0.8.2-modules");
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
            if(jobs!=null)jobs.Observe(name,value);
            if(name=="privateLocking"&&jobs!=null)jobs.StopScope();
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
            string requestId="",bridgeAction="";bool nativeDialogScope=false;Form dialogOwner=preview==null?(Form)this:preview;
            try {
                var message=Library.Json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);
                requestId=StringArg(message,"requestId"); string action=StringArg(message,"action");bridgeAction=action;
                if(preview!=null&&preview.SessionLibrary!=library)throw new InvalidOperationException("私密资料库正在锁定，请稍候。");
                var a=message.ContainsKey("args") ? message["args"] as Dictionary<string,object> : null;
                if(a==null) a=new Dictionary<string,object>();
                if(privateOpening&&!new[]{"diagnostics","vaultStatus","vaultActivity","cancelTranslation","cancelOrganization","cancelFileChat"}.Contains(action))throw new InvalidOperationException("私密文件夹正在打开，请稍候。");
                nativeDialogScope=new[]{"importFiles","importFolder","importFont","chooseSubtitle","savePhoto","exportTranslation","exportReading","exportReadingNotes","chooseDownloadFolder","chooseFfmpeg","exportKnowledge","restoreKnowledge","relinkSource","export","loginDownloadSite","importDownloadLogin","browserProbeDownload"}.Contains(action);if(nativeDialogScope)nativeFileDialogOpen=true;
                if(privateGate&&library.IsPrivate&&!new[]{"lockPrivate","unlockPrivate","vaultStatus","vaultActivity","cancelTranslation","cancelOrganization","cancelFileChat"}.Contains(action))throw new InvalidOperationException("私密资料库正在锁定，请稍候。");
                if(new[]{"aiConfig","saveAiConfig","aiModels","translate","organizeFiles","chatWithFile","applyOrganization","runStudyTask","generateVideoOutline","knowledgeAiPreview","runKnowledgeAnalysis","runVisualEdit"}.Contains(action))library.RequireAiMode();
                object result=null;
                var context=CreateModuleContext(dialogOwner);
                context.Ports=Ports(dialogOwner,preview,e);context.Legacy=(name,args)=>ExecuteLegacy(name,args,dialogOwner,preview,e);
                result=await ModuleRegistry.Builtins.Dispatch(context,action,a);
                SendTo(replyCore,new { requestId=requestId, result=result });
                if(preview!=null&&new[]{"update","remove","savePhoto","importFont"}.Contains(action))Send(new{eventName="libraryChanged",data=(object)null});
                if(action=="settings"||action=="setAppMode")Notify("previewPreferencesChanged",new{policyChanged=action=="setAppMode"});
            } catch(Exception ex) { RuntimeLog.Write("bridge-error",bridgeAction+" "+ex.GetType().FullName+" HRESULT="+ex.HResult.ToString("X8"));SendTo(replyCore,new { requestId=requestId, error=ex.Message }); }finally{if(nativeDialogScope)nativeFileDialogOpen=false;}
        }
        Entry RequireEntry(Dictionary<string,object> a) { Entry item=library.Find(StringArg(a,"id")); if(item==null || !File.Exists(item.path)) throw new FileNotFoundException("找不到原文件，文件可能已移动。请从资料库移除后重新导入。"); return item; }
        async Task<ImportResult> ScanSources() {
            if(importing)throw new InvalidOperationException("正在导入，请稍候。");
            importing=true;Notify("sourceScan",new{busy=true});
            using(var cancel=ModuleCancellation()){folderCancellation=cancel;
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
                Entry item=library.Find(parts[1]); if(item==null) { if(parts[0]=="thumb"){NoThumbnail(e);return;}RespondError(e,404,"Not Found"); return; }
                if(parts[0]=="file"&&(item.kind=="video"||item.kind=="music")&&lastMediaId!=item.id) {
                    lastMediaId=item.id;RuntimeLog.Write("media-open","id="+item.id+" type="+item.ext+" bytes="+item.size);
                }
                string path=parts[0]=="thumb" ? library.ThumbnailPath(item) : parts[0]=="file" ? item.path : null;
                if(path==null || !File.Exists(path)) { if(parts[0]=="thumb"){NoThumbnail(e);return;}RespondError(e,404,"Not Found"); return; }
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
        // Covers are optional and lazy. A late request after removal must fall back without a network error.
        // No old thumbnail is returned for an entry that is no longer in this library.
        void NoThumbnail(CoreWebView2WebResourceRequestedEventArgs e){e.Response=web.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(),204,"No Content","Content-Length: 0\r\nAccess-Control-Allow-Origin: https://app.kok.local\r\nCache-Control: no-store\r\n");}
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
