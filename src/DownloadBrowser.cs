using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace kok {
    public partial class MainWindow {
        async Task<object> BrowserDownloadPage(string input,string quality,CancellationToken token){
            await Task.Yield();
            string url=MediaResolver.NormalizeInput(input),site=DownloadSessions.Site(url);if(site!="bilibili"&&site!="douyin")throw new InvalidDataException("网页辅助解析目前用于 B 站和抖音，请输入对应的视频页面。");
            var page=new MediaPage{url=url,provider=site,fromPage=true};var match=Regex.Match(url,@"/(?:video|note)/(\d+)");string expectedId=match.Success?match.Groups[1].Value:"";bool accepted=false;
            using(var stop=CancellationTokenSource.CreateLinkedTokenSource(token))using(var dialog=new Form{Text=L("网页辅助解析")+" · "+site,Width=1080,Height=800,MinimumSize=new Size(760,560),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false})using(var browser=new WebView2{Dock=DockStyle.Fill}){
                var note=new Label{Dock=DockStyle.Top,Height=48,Padding=new Padding(16,12,16,6),Text=L("请在官方页面打开目标视频，必要时手动登录或完成验证；页面始终静音。")};
                var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=56,Padding=new Padding(12),FlowDirection=FlowDirection.RightToLeft};var use=new Button{Text=L("使用检测到的资源"),Enabled=false,AutoSize=true};var close=new Button{Text=L("取消"),AutoSize=true};actions.Controls.Add(use);actions.Controls.Add(close);dialog.Controls.Add(browser);dialog.Controls.Add(note);dialog.Controls.Add(actions);
                Action update=()=>{if(dialog.IsDisposed)return;use.Enabled=page.candidates.Count>0;if(use.Enabled)note.Text=L("已检测到当前视频的资源，可返回 kok 选择画质并下载。");};
                close.Click+=delegate{dialog.Close();};dialog.FormClosed+=delegate{stop.Cancel();};
                use.Click+=async delegate{use.Enabled=false;try{var cookies=await browser.CoreWebView2.CookieManager.GetCookiesAsync(DownloadSessions.Home(site));downloads.Sessions.Store(site,cookies.Select(c=>new DownloadCookie{name=c.Name,value=c.Value,domain=c.Domain,path=c.Path,secure=c.IsSecure,httpOnly=c.IsHttpOnly,expires=c.IsSession?0:(c.Expires.ToUniversalTime()-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds}),browser.CoreWebView2.Settings.UserAgent);if(page.title=="")page.title=browser.CoreWebView2.DocumentTitle;page.url=MediaResolver.NormalizeInput(browser.CoreWebView2.Source);accepted=true;dialog.Close();}catch{note.Text=L("资源读取失败，请重试。");update();}};
                dialog.Shown+=async delegate{try{
                    var options=web.CoreWebView2.Environment.CreateCoreWebView2ControllerOptions();options.ProfileName="site-assist-"+site;options.IsInPrivateModeEnabled=true;await browser.EnsureCoreWebView2Async(web.CoreWebView2.Environment,options);var core=browser.CoreWebView2;
                    core.Settings.IsWebMessageEnabled=false;core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;core.IsMuted=true;
                    foreach(System.Net.Cookie cookie in downloads.Sessions.Jar(site).GetCookies(new Uri(DownloadSessions.Home(site)))){var saved=core.CookieManager.CreateCookie(cookie.Name,cookie.Value,cookie.Domain,cookie.Path);saved.IsSecure=cookie.Secure;saved.IsHttpOnly=cookie.HttpOnly;if(cookie.Expires!=DateTime.MinValue)saved.Expires=cookie.Expires;core.CookieManager.AddOrUpdateCookie(saved);}
                    core.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){if(!DownloadSessions.LoginUrl(site,e.Uri)){e.Cancel=true;return;}var target=Regex.Match(e.Uri,@"/(?:video|note)/(\d+)");if(site=="douyin"&&target.Success&&target.Groups[1].Value!=expectedId){expectedId=target.Groups[1].Value;page.candidates.Clear();page.title="";update();}};
                    core.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;if(DownloadSessions.LoginUrl(site,e.Uri))core.Navigate(e.Uri);};
                    core.DownloadStarting+=delegate(object sender,CoreWebView2DownloadStartingEventArgs e){e.Cancel=true;};core.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e){e.State=CoreWebView2PermissionState.Deny;};
                    core.WebResourceResponseReceived+=async delegate(object sender,CoreWebView2WebResourceResponseReceivedEventArgs e){
                        if(stop.IsCancellationRequested||DownloadSessions.Site(e.Request.Uri)!=site)return;string path=new Uri(e.Request.Uri).AbsolutePath;if(site=="douyin"&&!path.Contains("/aweme/"))return;if(site=="bilibili"&&!path.Contains("playurl"))return;
                        try{using(var stream=await e.Response.GetContentAsync())using(var buffer=new MemoryStream()){if(stream==null)return;var bytes=new byte[16384];int count;while((count=await stream.ReadAsync(bytes,0,bytes.Length,stop.Token))>0){if(buffer.Length+count>4*1024*1024)return;buffer.Write(bytes,0,count);}if(stop.IsCancellationRequested)return;SiteAdapters.ParseResponse(page,Encoding.UTF8.GetString(buffer.ToArray()),site,expectedId);update();}}catch(Exception){}
                    };
                    core.DOMContentLoaded+=async delegate{try{string encoded=await core.ExecuteScriptAsync("document.documentElement.outerHTML.slice(0,2097152)");if(stop.IsCancellationRequested)return;string html=Library.Json.Deserialize<string>(encoded);SiteAdapters.ParseEmbedded(page,core.Source,html);update();}catch(Exception){}};
                    core.Navigate(url);
                }catch{note.Text=L("无法打开网页辅助解析，请检查 WebView2 环境。");}};
                using(token.Register(()=>{try{if(!dialog.IsDisposed&&dialog.IsHandleCreated)dialog.BeginInvoke((Action)(()=>dialog.Close()));}catch(InvalidOperationException){}}))dialog.ShowDialog(this);
            }
            if(!accepted)return new{cancelled=true};return await downloads.RegisterBrowserPage(page,quality,token);
        }
    }
}
