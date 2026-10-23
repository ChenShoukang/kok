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
        [STAThread] static void Main(string[] args) {
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new MainWindow(args)); }
            catch(Exception ex) { MessageBox.Show(Localization.Translate(ex.Message,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="en"),Localization.Translate("kok · 无法启动",System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="en"),MessageBoxButtons.OK,MessageBoxIcon.Error); }
        }
    }
    public class MainWindow : Form {
        readonly WebView2 web = new WebView2();
        readonly Library library;
        readonly TranslationService translations;
        readonly Dictionary<string,CancellationTokenSource> translationJobs=new Dictionary<string,CancellationTokenSource>();
        readonly string appDir = AppDomain.CurrentDomain.BaseDirectory;
        bool importing;
        readonly bool debug;
        readonly FileStream databaseLock;
        bool closeReady,closingPending;
        public MainWindow(string[] args) {
            Text="kok"; Width=1440; Height=940; MinimumSize=new Size(960,640);
            StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(248,249,251);
            Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            debug=args.Contains("--debug");
            string dataDir=Path.Combine(appDir,"data");
            int dataIndex=Array.IndexOf(args,"--data-dir");
            if(dataIndex>=0 && dataIndex+1<args.Length) dataDir=Path.GetFullPath(args[dataIndex+1]);
            Directory.CreateDirectory(dataDir);
            try { databaseLock=new FileStream(Path.Combine(dataDir,".kok.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) { throw new InvalidOperationException("这个资料库已经在另一个 kok 窗口中打开，请使用原窗口。\n如需独立窗口，请通过 --data-dir 指定其他资料库。"); }
            library=new Library(dataDir);
            translations=new TranslationService(dataDir);
            FormClosed+=delegate { databaseLock.Dispose(); };
            FormClosing+=HandleWindowClosing;
            web.DefaultBackgroundColor=BackColor; web.Dock=DockStyle.Fill; Controls.Add(web);
            Shown+=async delegate { await Init(); };
        }
        async void HandleWindowClosing(object sender,FormClosingEventArgs e) {
            if(closeReady||web.CoreWebView2==null)return;
            e.Cancel=true;if(closingPending)return;
            if(importing){MessageBox.Show(this,L("正在导入文件，请等待本次整理完成后再退出。"),"kok");return;}
            closingPending=true;
            foreach(var job in translationJobs.Values)job.Cancel();
            try {
                string encoded=await web.CoreWebView2.ExecuteScriptAsync("window.kokSession ? JSON.stringify(window.kokSession()) : '[]'");
                string json=Library.Json.Deserialize<string>(encoded);
                if(json!=null){var edits=Library.Json.Deserialize<List<Dictionary<string,object>>>(json);foreach(var edit in edits){string id=StringArg(edit,"id");if(library.Find(id)!=null)library.Update(id,(Dictionary<string,object>)edit["patch"]);}}
            }catch(Exception ex){MessageBox.Show(this,L("退出前保存失败："+ex.Message+"\n此前自动保存的资料仍然保留。"),L("kok · 保存提示"));}
            closeReady=true;Close();
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
                var options=new CoreWebView2EnvironmentOptions(debug ? "--remote-debugging-port=9225" : "");
                options.Language=library.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(library.Db.settings["uiLanguage"])=="en"?"en-US":"zh-CN";
                var env=await CoreWebView2Environment.CreateAsync(browser,Path.Combine(library.DataDir,"WebView2"),options);
                await web.EnsureCoreWebView2Async(env);
                web.CoreWebView2.Settings.AreDevToolsEnabled=debug;
                web.CoreWebView2.Settings.IsStatusBarEnabled=false;
                web.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
                web.CoreWebView2.SetVirtualHostNameToFolderMapping("app.kok.local",Path.Combine(appDir,"ui"),CoreWebView2HostResourceAccessKind.DenyCors);
                web.CoreWebView2.AddWebResourceRequestedFilter("https://media.kok.local/*",CoreWebView2WebResourceContext.All);
                web.CoreWebView2.WebResourceRequested+=Resource;
                web.CoreWebView2.WebMessageReceived+=Message;
                web.CoreWebView2.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){ e.Handled=true; };
                web.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){ if(!e.Uri.StartsWith("https://app.kok.local/",StringComparison.OrdinalIgnoreCase)) e.Cancel=true; };
                web.CoreWebView2.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e){ e.State=CoreWebView2PermissionState.Deny; };
                web.CoreWebView2.Navigate("https://app.kok.local/index.html");
            } catch(Exception ex) { MessageBox.Show(this,L(ex.Message),L("kok · 初始化失败"),MessageBoxButtons.OK,MessageBoxIcon.Error); Close(); }
        }
        void Send(object value) { if(IsDisposed || web.CoreWebView2==null) return; web.CoreWebView2.PostWebMessageAsJson(Library.Json.Serialize(value)); }
        void Notify(string name,object value) { Send(new { eventName=name, data=value }); }
        string L(string value) { return Localization.Translate(value,library!=null&&library.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(library.Db.settings["uiLanguage"])=="en"); }
        static string StringArg(Dictionary<string,object> a,string key) { return a.ContainsKey(key) ? Convert.ToString(a[key]) : ""; }
        async void Message(object sender,CoreWebView2WebMessageReceivedEventArgs e) {
            if(!e.Source.StartsWith("https://app.kok.local/",StringComparison.OrdinalIgnoreCase)) return;
            string requestId="";
            try {
                var message=Library.Json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);
                requestId=StringArg(message,"requestId"); string action=StringArg(message,"action");
                var a=message.ContainsKey("args") ? message["args"] as Dictionary<string,object> : null;
                if(a==null) a=new Dictionary<string,object>();
                object result=null;
                switch(action) {
                    case "library": result=Library.Json.DeserializeObject(library.Snapshot()); break;
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
                    case "importFiles": case "importFolder":
                        if(importing) throw new InvalidOperationException("正在导入，请稍候。");
                        string[] paths;
                        if(action=="importFiles") {
                            using(var dialog=new OpenFileDialog { Multiselect=true, Title=L("选择视频、照片、音乐、文档或电子书"), Filter=L("所有文件|*.*") }) { if(dialog.ShowDialog(this)!=DialogResult.OK) { result=new { cancelled=true }; break; } paths=dialog.FileNames; }
                        } else {
                            using(var dialog=new FolderBrowserDialog { Description=L("选择资料文件夹（包含子文件夹）"), ShowNewFolderButton=false }) { if(dialog.ShowDialog(this)!=DialogResult.OK) { result=new { cancelled=true }; break; } paths=new[]{dialog.SelectedPath}; }
                        }
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
                    case "settings": library.Settings(a); result=true; break;
                    case "aiConfig": result=translations.PublicConfig();break;
                    case "saveAiConfig": result=translations.SaveConfig(a);break;
                    case "splitTranslationText": result=TranslationService.SplitText(StringArg(a,"text"));break;
                    case "cancelTranslation": {CancellationTokenSource job;if(translationJobs.TryGetValue(StringArg(a,"jobId"),out job))job.Cancel();result=true;break;}
                    case "translate": {
                        string jobId=StringArg(a,"jobId");if(jobId.Length<1||jobId.Length>80||translationJobs.ContainsKey(jobId)||translationJobs.Count>=4)throw new InvalidOperationException("翻译任务正在切换，请稍后重试。");
                        var parts=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(a["parts"]));
                        using(var cancel=new CancellationTokenSource()){
                            translationJobs.Add(jobId,cancel);
                            try{result=await translations.TranslateAsync(parts,cancel.Token,(done,total)=>{if(!IsDisposed)Notify("translationProgress",new{jobId=jobId,completed=done,total=total});},!a.ContainsKey("test")||!Convert.ToBoolean(a["test"]));}
                            catch(OperationCanceledException){throw new InvalidOperationException("翻译已取消。");}
                            finally{translationJobs.Remove(jobId);}
                        }break;
                    }
                    case "exportTranslation": {
                        string text=StringArg(a,"text"),format=StringArg(a,"format");if(text.Length>16*1024*1024)throw new InvalidDataException("译文过大。");
                        string extension=format=="srt"?"srt":"txt";
                        using(var dialog=new SaveFileDialog{Title=L("导出译文"),Filter=L(extension=="srt"?"SRT 字幕|*.srt":"文本译文|*.txt"),FileName=StringArg(a,"name")+L("-译文")+"."+extension}){
                            if(dialog.ShowDialog(this)==DialogResult.OK){string destination=Path.GetFullPath(dialog.FileName);if(library.Db.items.Any(item=>String.Equals(Path.GetFullPath(item.path),destination,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("请选择新的文件名，避免覆盖已收录的原文件。");File.WriteAllText(destination,text,new UTF8Encoding(false));result=true;}
                        }break;
                    }
                    case "addCollection": library.AddCollection(StringArg(a,"name"));result=true;break;
                    case "deleteCollection": library.DeleteCollection(StringArg(a,"name"));result=true;break;
                    case "importFont": {
                        using(var dialog=new OpenFileDialog{Title=L("导入你有权使用的字体"),Filter=L("字体文件|*.ttf;*.otf")}) { if(dialog.ShowDialog(this)==DialogResult.OK)result=library.ImportFont(dialog.FileName); }
                        break;
                    }
                    case "subtitles": result=await Task.Run(()=>Subtitles.Discover(RequireEntry(a).path));break;
                    case "chooseSubtitle": {
                        using(var dialog=new OpenFileDialog{Title=L("载入字幕"),Filter=L("文字字幕|*.srt;*.vtt;*.ass;*.ssa;*.sub;*.sbv;*.lrc;*.ttml;*.dfxp;*.smi|所有文件|*.*")}) { if(dialog.ShowDialog(this)==DialogResult.OK)result=Subtitles.Read(dialog.FileName); }
                        break;
                    }
                    case "savePhoto": {
                        Entry original=RequireEntry(a);
                        string encoded=StringArg(a,"image");if(!encoded.StartsWith("data:image/png;base64,")||encoded.Length>48*1024*1024)throw new InvalidDataException("图片数据无效或过大。");
                        using(var dialog=new SaveFileDialog{Title=L("另存编辑后的照片"),Filter=L("PNG 图片|*.png|JPEG 图片|*.jpg"),FileName=StringArg(a,"name")+L("-编辑")+".png"}) {
                            if(dialog.ShowDialog(this)==DialogResult.OK) {
                                byte[] bytes=Convert.FromBase64String(encoded.Substring(22));
                                library.SaveEditedPhoto(original.path,dialog.FileName,bytes);result=true;
                            }
                        }break;
                    }
                    case "exportReading": {
                        string html=StringArg(a,"html");if(html.Length>32*1024*1024)throw new InvalidDataException("导出内容过大。");
                        using(var dialog=new SaveFileDialog{Title=L("导出带标记的阅读副本"),Filter=L("HTML 阅读副本|*.html"),FileName=StringArg(a,"name")+L("-批注")+".html"}) { if(dialog.ShowDialog(this)==DialogResult.OK){File.WriteAllText(dialog.FileName,html,new UTF8Encoding(false));result=true;} }break;
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
                        using(var dialog=new SaveFileDialog { Title=L("导出资料库索引与笔记"), Filter=L("JSON 资料库|*.json"), FileName="kok-library-"+DateTime.Now.ToString("yyyyMMdd")+".json" }) {
                            if(dialog.ShowDialog(this)==DialogResult.OK) { File.WriteAllText(dialog.FileName,library.Snapshot(),new UTF8Encoding(false)); result=true; }
                        } break;
                    }
                    case "diagnostics": result=new { engine=web.CoreWebView2.Environment.BrowserVersionString, count=library.Db.items.Count, dataDir=library.DataDir, version="0.1.4" }; break;
                    default: throw new InvalidOperationException("未知操作。");
                }
                Send(new { requestId=requestId, result=result });
            } catch(Exception ex) { Send(new { requestId=requestId, error=ex.Message }); }
        }
        Entry RequireEntry(Dictionary<string,object> a) { Entry item=library.Find(StringArg(a,"id")); if(item==null || !File.Exists(item.path)) throw new FileNotFoundException("找不到原文件，文件可能已移动。请从资料库移除后重新导入。"); return item; }
        void Resource(object sender,CoreWebView2WebResourceRequestedEventArgs e) {
            try {
                Uri uri=new Uri(e.Request.Uri); string[] parts=uri.AbsolutePath.Trim('/').Split('/');
                if(parts.Length!=2) { RespondError(e,404,"Not Found"); return; }
                if(parts[0]=="font") {
                    var font=library.Db.fonts.FirstOrDefault(f=>f.id==parts[1]);if(font==null||!File.Exists(font.path)){RespondError(e,404,"Not Found");return;}
                    e.Response=web.CoreWebView2.Environment.CreateWebResourceResponse(File.OpenRead(font.path),200,"OK","Content-Type: font/ttf\r\nAccess-Control-Allow-Origin: https://app.kok.local\r\n");return;
                }
                Entry item=library.Find(parts[1]); if(item==null) { RespondError(e,404,"Not Found"); return; }
                string path=parts[0]=="thumb" ? library.ThumbnailPath(item) : parts[0]=="file" ? item.path : null;
                if(path==null || !File.Exists(path)) { RespondError(e,404,"Not Found"); return; }
                var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                long size=file.Length,start=0,end=size-1;
                string mime=parts[0]=="thumb" ? "image/jpeg" : Mime(item.ext);
                string headers="Content-Type: "+mime+"\r\nAccept-Ranges: bytes\r\nAccess-Control-Allow-Origin: https://app.kok.local\r\nAccess-Control-Expose-Headers: Content-Range, Accept-Ranges, Content-Length\r\nX-Content-Type-Options: nosniff\r\nCache-Control: no-cache\r\n";
                string range=e.Request.Headers.Contains("Range") ? e.Request.Headers.GetHeader("Range") : null;
                bool partial=!String.IsNullOrEmpty(range);
                if(partial && !TryRange(range,size,out start,out end)) { file.Dispose(); e.Response=web.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(),416,"Range Not Satisfiable",headers+"Content-Range: bytes */"+size+"\r\n"); return; }
                if(partial) headers+="Content-Range: bytes "+start+"-"+end+"/"+size+"\r\n";
                long count=Math.Max(0,end-start+1); headers+="Content-Length: "+count+"\r\n";
                e.Response=web.CoreWebView2.Environment.CreateWebResourceResponse(new SliceStream(file,start,count),partial?206:200,partial?"Partial Content":"OK",headers);
            } catch { RespondError(e,500,"Read Error"); }
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
