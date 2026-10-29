using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace kok {
    public partial class MainWindow {
        readonly List<PreviewWindow> previewWindows=new List<PreviewWindow>();
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
        PreviewWindow PreviewFor(object sender){return previewWindows.FirstOrDefault(w=>!w.IsDisposed&&w.Browser.CoreWebView2==sender);}
        object PreviewList(){return previewWindows.Where(w=>!w.IsDisposed&&w.SessionLibrary==library).Select(w=>new{id=w.ItemId,name=w.Text,handle=w.Handle.ToInt64(),behind=w.SentBehind,ready=w.Ready,opacity=w.Opacity,presenting=w.Presenting,state=w.WindowState.ToString(),border=w.FormBorderStyle.ToString(),fullScreen=w.FullScreenState,bounds=new{x=w.Left,y=w.Top,width=w.Width,height=w.Height}}).ToArray();}
        void PreviewListChanged(){Send(new{eventName="previewWindowsChanged",data=PreviewList()});}
        async Task<object> OpenPreview(Dictionary<string,object> args){
            var item=RequireEntry(args);var existing=previewWindows.FirstOrDefault(w=>!w.IsDisposed&&w.SessionLibrary==library&&w.ItemId==item.id);
            if(existing!=null){existing.RestorePreview();return new{handle=existing.Handle.ToInt64()};}
            var preview=new PreviewWindow(this,item,library);previewWindows.Add(preview);
            if(args.ContainsKey("origin")&&args["origin"] is Dictionary<string,object>){var origin=(Dictionary<string,object>)args["origin"];double x=CreativeEdits.Number(origin,"x",.5,-2,3),y=CreativeEdits.Number(origin,"y",.5,-2,3),width=CreativeEdits.Number(origin,"width",.2,.01,2),height=CreativeEdits.Number(origin,"height",.2,.01,2);var center=web.PointToScreen(new Point((int)(x*web.ClientSize.Width),(int)(y*web.ClientSize.Height)));preview.LaunchOrigin=new Rectangle(center.X-(int)(width*web.ClientSize.Width/2),center.Y-(int)(height*web.ClientSize.Height/2),(int)(width*web.ClientSize.Width),(int)(height*web.ClientSize.Height));}
            preview.Activated+=delegate{if(preview.Ready&&preview.Browser.CoreWebView2!=null)SendTo(preview.Browser.CoreWebView2,new{eventName="workspaceFocus",data=new{}});};
            preview.FormClosed+=delegate{CancelSpeechOwner(preview);previewWindows.Remove(preview);PreviewListChanged();};
            try{preview.Show();await preview.Initialize(!args.ContainsKey("play")||Convert.ToBoolean(args["play"]));PreviewListChanged();return new{handle=preview.Handle.ToInt64()};}
            catch{preview.CloseWithoutSaving();throw;}
        }
        async Task SaveWebSession(WebView2 browser,Library target){
            if(browser.IsDisposed||browser.CoreWebView2==null)return;
            string encoded=await SessionPersistence.ReadSnapshot(browser.CoreWebView2.ExecuteScriptAsync("window.kokSession ? JSON.stringify(window.kokSession()) : '[]'"),5000);
            string json=Library.Json.Deserialize<string>(encoded);if(json==null)throw new InvalidDataException("窗口内容尚未准备好，请稍后重试关闭。");
            foreach(var edit in Library.Json.Deserialize<List<Dictionary<string,object>>>(json)){
                string id=StringArg(edit,"id");var entry=target.Find(id);var patch=(Dictionary<string,object>)edit["patch"];if(entry!=null){if(entry.ownedDocument&&!target.IsPrivate&&patch.ContainsKey("editDraft")){var draft=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(patch["editDraft"]));target.SaveDocumentSession(id,draft);patch.Remove("editDraft");}if(patch.Count>0)target.Update(id,patch);}
                if(target==normalLibrary&&target.Find(id)!=null&&edit.ContainsKey("translationView"))await HandleReadingSessionAction("saveTranslationView",new Dictionary<string,object>{{"id",id},{"view",edit["translationView"]}});
            }
        }
        async Task SavePreviewSessions(){foreach(var preview in previewWindows.ToArray())if(!preview.IsDisposed&&preview.SessionLibrary==library)await SaveWebSession(preview.Browser,preview.SessionLibrary);}
        void ClosePreviewWindows(){foreach(var preview in previewWindows.ToArray())preview.CloseWithoutSaving();}
        void SendTo(CoreWebView2 target,object value){if(target==null||IsDisposed)return;try{target.PostWebMessageAsJson(Library.Json.Serialize(value));}catch(Exception ex){RuntimeLog.Write("preview-message-error",ex);}}
        async void CheckPrivateDeparture(){
            if(nativeFileDialogOpen)return;await Task.Delay(180);
            if(IsDisposed||nativeFileDialogOpen)return;uint foregroundProcess;GetWindowThreadProcessId(GetForegroundWindow(),out foregroundProcess);
            if(foregroundProcess==(uint)System.Diagnostics.Process.GetCurrentProcess().Id)return;
            privateDepartureGeneration++;
            if(library.IsPrivate&&!privateGate)try{await LockPrivateVault();}catch(Exception ex){RuntimeLog.Write("private-lock-error",ex);}
        }
        sealed class PreviewWindow : Form {
            readonly MainWindow host;
            internal readonly WebView2 Browser=new WebView2();
            internal readonly Library SessionLibrary;
            internal string ItemId;
            internal bool SentBehind,Ready;
            internal Rectangle LaunchOrigin;
            readonly WindowInteraction interaction;
            System.Windows.Forms.Timer presentationTimer;
            internal bool Presenting {get{return presentationTimer!=null;}}
            bool closeReady,closing,fullScreen;
            internal bool FullScreenState {get{return fullScreen;}}
            Rectangle restoreBounds;FormWindowState restoreState;
            internal PreviewWindow(MainWindow host,Entry item,Library session){
                this.host=host;SessionLibrary=session;ItemId=item.id;Text=item.name;Icon=host.Icon;Opacity=0;
                interaction=new WindowInteraction(this,(active,resizing)=>host.SendTo(Browser.CoreWebView2,new{eventName="windowInteraction",data=new{active=active,resizing=resizing}}));
                BackColor=host.chromeColor;Browser.DefaultBackgroundColor=BackColor;
                MinimumSize=new Size(520,380);Size=new Size(item.kind=="photo"&&item.height>item.width?660:1050,item.kind=="photo"&&item.height>item.width?820:740);
                StartPosition=FormStartPosition.Manual;var area=Screen.FromControl(host).WorkingArea;int offset=28*(host.previewWindows.Count%6);
                Width=Math.Min(Width,area.Width);Height=Math.Min(Height,area.Height);Location=new Point(Math.Max(area.Left,Math.Min(area.Right-Width,host.Left+80+offset)),Math.Max(area.Top,Math.Min(area.Bottom-Height,host.Top+60+offset)));
                ShowInTaskbar=true;Browser.AllowExternalDrop=true;Controls.Add(Browser);Resize+=delegate{interaction.SizeChanged();LayoutBrowser();};DpiChanged+=delegate{LayoutBrowser();};
                Activated+=delegate{SentBehind=false;host.PreviewListChanged();};Deactivate+=delegate{interaction.End();host.CheckPrivateDeparture();};
                FormClosing+=async delegate(object sender,FormClosingEventArgs e){
                    if(closeReady)return;e.Cancel=true;if(closing)return;closing=true;
                    try{if(SessionLibrary==host.library){await host.SaveWebSession(Browser,SessionLibrary);host.Send(new{eventName="libraryChanged",data=(object)null});}closeReady=true;Close();}
                    catch(Exception ex){
                        // A crashed or unresponsive page must not keep the window open forever; let the user choose.
                        RuntimeLog.Write("preview-session-save-error",ex);closing=false;
                        if(MessageBox.Show(this,host.L(ex.Message)+"\n\n"+host.L("仍要关闭这个窗口吗？未保存的修改会丢失。"),"kok",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes){closeReady=true;Close();}
                    }
                };
                FormClosed+=delegate{FinishPresentation();Browser.Dispose();};LayoutBrowser();
            }
            internal async Task Initialize(bool play){
                // Share the installed engine and user-data environment, not another bundled runtime.
                await Browser.EnsureCoreWebView2Async(host.web.CoreWebView2.Environment);
                if(IsDisposed||SessionLibrary!=host.library)return;
                var core=Browser.CoreWebView2;core.Settings.AreDevToolsEnabled=host.debug;core.Settings.IsStatusBarEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
                try{core.Settings.IsNonClientRegionSupportEnabled=true;await core.AddScriptToExecuteOnDocumentCreatedAsync("window.kokNativeDragSupported=true;");}catch(NotImplementedException){}catch(System.Runtime.InteropServices.COMException){}
                core.SetVirtualHostNameToFolderMapping("app.kok.local",Path.Combine(host.appDir,"ui"),CoreWebView2HostResourceAccessKind.DenyCors);
                core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);core.WebResourceRequested+=host.Resource;core.WebMessageReceived+=host.Message;
                core.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){if(!e.Uri.StartsWith("https://app.kok.local/",StringComparison.OrdinalIgnoreCase))e.Cancel=true;};
                core.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;};core.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e){e.State=CoreWebView2PermissionState.Deny;};
                core.ContainsFullScreenElementChanged+=delegate{SetFullScreen(core.ContainsFullScreenElement);};
                core.ProcessFailed+=delegate(object sender,CoreWebView2ProcessFailedEventArgs e){if(e.ProcessFailedKind==CoreWebView2ProcessFailedKind.RenderProcessExited){Ready=false;try{core.Reload();}catch{CloseWithoutSaving();}}};
                string origin="";if(LaunchOrigin.Width>0){var point=Browser.PointToScreen(Point.Empty);Func<double,string> number=value=>value.ToString("0.######",System.Globalization.CultureInfo.InvariantCulture);origin="&ox="+number((LaunchOrigin.Left+LaunchOrigin.Width/2.0-point.X)/Math.Max(1,Browser.ClientSize.Width))+"&oy="+number((LaunchOrigin.Top+LaunchOrigin.Height/2.0-point.Y)/Math.Max(1,Browser.ClientSize.Height))+"&ow="+number(LaunchOrigin.Width/(double)Math.Max(1,Browser.ClientSize.Width))+"&oh="+number(LaunchOrigin.Height/(double)Math.Max(1,Browser.ClientSize.Height));}
                core.Navigate("https://app.kok.local/index.html?preview="+Uri.EscapeDataString(ItemId)+"&play="+(play?"1":"0")+origin+"&v=0.8.1-modules");
            }
            internal void Appearance(Color color){BackColor=color;Browser.DefaultBackgroundColor=color;try{int dark=color.R*.2126+color.G*.7152+color.B*.0722<145?1:0;DwmSetWindowAttribute(Handle,20,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){} }
            internal void Present(bool reduceMotion){
                Ready=true;if(reduceMotion||Opacity>=1){FinishPresentation();return;}if(presentationTimer!=null)return;
                var elapsed=System.Diagnostics.Stopwatch.StartNew();presentationTimer=new System.Windows.Forms.Timer{Interval=16};presentationTimer.Tick+=delegate{if(IsDisposed){FinishPresentation();return;}double t=Math.Min(1,elapsed.Elapsed.TotalMilliseconds/160);Opacity=1-Math.Pow(1-t,3);if(t>=1)FinishPresentation();};presentationTimer.Start();
            }
            void FinishPresentation(){if(presentationTimer!=null){presentationTimer.Stop();presentationTimer.Dispose();presentationTimer=null;}if(!IsDisposed&&IsHandleCreated)Opacity=1;}
            internal void RestorePreview(){if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;SentBehind=false;Show();BringToFront();Activate();host.PreviewListChanged();}
            internal void SendBehind(){if(fullScreen)return;host.Show();if(host.WindowState==FormWindowState.Minimized)host.WindowState=FormWindowState.Normal;host.BringToFront();host.Activate();SetWindowPos(Handle,host.Handle,0,0,0,0,0x0013);SentBehind=true;host.PreviewListChanged();}
            internal void DragPreview(){if(fullScreen)return;ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}
            internal void ResizePreview(int edge){if(fullScreen||WindowState!=FormWindowState.Normal||edge<10||edge>17)return;ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(edge),IntPtr.Zero);}
            internal void ToggleMaximize(){if(!fullScreen)WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
            internal void CloseWithoutSaving(){closeReady=true;Close();}
            void SetFullScreen(bool enabled){if(enabled==fullScreen)return;if(enabled){var screen=Screen.FromControl(this).Bounds;restoreBounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;restoreState=WindowState;WindowState=FormWindowState.Normal;fullScreen=true;FormBorderStyle=FormBorderStyle.None;Bounds=screen;}else{fullScreen=false;FormBorderStyle=FormBorderStyle.Sizable;Bounds=restoreBounds;WindowState=restoreState;}LayoutBrowser();}
            void LayoutBrowser(){int edge=fullScreen||WindowState==FormWindowState.Maximized?0:5;var bounds=new Rectangle(edge,edge,Math.Max(0,ClientSize.Width-edge*2),Math.Max(0,ClientSize.Height-edge*2));if(Browser.Bounds!=bounds)Browser.Bounds=bounds;}
            protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);try{var margins=new ChromeMargins{left=1,right=1,top=1,bottom=1};DwmExtendFrameIntoClientArea(Handle,ref margins);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){} }
            protected override void WndProc(ref Message message){
                if(interaction!=null)interaction.Process(message.Msg);
                if(!fullScreen){
                    if(message.Msg==0x83&&message.WParam!=IntPtr.Zero){if(IsZoomed(Handle)){var rect=(ChromeRect)Marshal.PtrToStructure(message.LParam,typeof(ChromeRect));var info=new ChromeMonitor{size=Marshal.SizeOf(typeof(ChromeMonitor))};if(GetMonitorInfo(MonitorFromRect(ref rect,2),ref info))Marshal.StructureToPtr(info.work,message.LParam,false);}message.Result=IntPtr.Zero;return;}
                    if(message.Msg==0x84&&WindowState==FormWindowState.Normal){long packed=message.LParam.ToInt64();var p=PointToClient(new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff)));bool l=p.X<5,r=p.X>=ClientSize.Width-5,t=p.Y<5,b=p.Y>=ClientSize.Height-5;int hit=t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:0;if(hit!=0){message.Result=new IntPtr(hit);return;}}
                }
                base.WndProc(ref message);
            }
        }
    }
}
