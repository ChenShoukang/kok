using System;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace kok {
    public partial class MainWindow {
        async Task<object> LoginDownloadSite(string site){
            await Task.Yield();
            string address=DownloadSessions.Home(site);var saved=false;
            using(var dialog=new Form{Text=L("站点登录")+" · "+site,Width=1000,Height=760,MinimumSize=new Size(720,540),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,BackColor=Color.FromArgb(248,248,248)})
            using(var browser=new WebView2{Dock=DockStyle.Fill}){
                var description=new Label{Dock=DockStyle.Top,Height=48,Padding=new Padding(18,12,18,8),Text=L("请在站点官方页面手动登录，完成后点击保存登录信息。"),Font=new Font("Microsoft YaHei UI",9)};
                var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,Padding=new Padding(12),FlowDirection=FlowDirection.RightToLeft};
                var save=new Button{Text=L("保存登录信息"),AutoSize=true,Enabled=false,FlatStyle=FlatStyle.Flat,Padding=new Padding(12,3,12,3)};
                var cancel=new Button{Text=L("取消"),AutoSize=true,FlatStyle=FlatStyle.Flat,Padding=new Padding(12,3,12,3)};buttons.Controls.Add(save);buttons.Controls.Add(cancel);dialog.Controls.Add(browser);dialog.Controls.Add(description);dialog.Controls.Add(buttons);
                cancel.Click+=delegate{dialog.Close();};
                save.Click+=async delegate{save.Enabled=false;try{var values=await browser.CoreWebView2.CookieManager.GetCookiesAsync(address);var cookies=values.Select(c=>new DownloadCookie{name=c.Name,value=c.Value,domain=c.Domain,path=c.Path,secure=c.IsSecure,httpOnly=c.IsHttpOnly,expires=c.IsSession?0:(c.Expires.ToUniversalTime()-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds}).ToArray();downloads.Sessions.Store(site,cookies,browser.CoreWebView2.Settings.UserAgent);saved=true;dialog.Close();}catch{description.Text=L("登录信息保存失败，请重试或导入 cookies.txt。");save.Enabled=true;}};
                dialog.Shown+=async delegate{try{
                    var options=web.CoreWebView2.Environment.CreateCoreWebView2ControllerOptions();options.ProfileName="site-"+site;options.IsInPrivateModeEnabled=true;
                    await browser.EnsureCoreWebView2Async(web.CoreWebView2.Environment,options);
                    var core=browser.CoreWebView2;core.Settings.IsWebMessageEnabled=false;core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;core.IsMuted=true;
                    core.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){if(!DownloadSessions.LoginUrl(site,e.Uri))e.Cancel=true;};
                    core.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;if(DownloadSessions.LoginUrl(site,e.Uri))core.Navigate(e.Uri);};
                    core.DownloadStarting+=delegate(object sender,CoreWebView2DownloadStartingEventArgs e){e.Cancel=true;};
                    core.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e){e.State=CoreWebView2PermissionState.Deny;};
                    core.Navigate(address);save.Enabled=true;
                }catch{description.Text=L("无法打开站点登录，请使用 cookies.txt 导入。 ");}};
                dialog.ShowDialog(this);
            }
            await Task.Yield();return new{saved=saved};
        }
    }
}
