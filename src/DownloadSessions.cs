using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Collections.Generic;

namespace kok {
    public class DownloadCookie {public string name,value,domain,path;public bool secure,httpOnly;public double expires;}
    public class DownloadAccount {public string agent="";public List<DownloadCookie> cookies=new List<DownloadCookie>();}
    public sealed class DownloadSessions {
        readonly object gate=new object();readonly string file;
        Dictionary<string,DownloadAccount> accounts=new Dictionary<string,DownloadAccount>();
        static readonly byte[] Entropy=Encoding.UTF8.GetBytes("kok-download-accounts-v1");
        public static readonly string[] Sites={"bilibili","youtube","douyin"};
        public DownloadSessions(string directory){file=Path.Combine(directory,"download-accounts.sealed");if(File.Exists(file))try{accounts=Library.Json.Deserialize<Dictionary<string,DownloadAccount>>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),Entropy,DataProtectionScope.CurrentUser)))??accounts;}catch{accounts=new Dictionary<string,DownloadAccount>();}}
        public static string Site(string url){Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri))return "";string host=uri.Host.ToLowerInvariant();if(Domain(host,"bilibili.com")||host=="b23.tv")return "bilibili";if(Domain(host,"youtube.com")||host=="youtu.be"||Domain(host,"youtube-nocookie.com")||Domain(host,"googlevideo.com"))return "youtube";if(Domain(host,"douyin.com")||Domain(host,"iesdouyin.com")||Domain(host,"douyinvod.com")||Domain(host,"douyinpic.com"))return "douyin";return "";}
        static bool Domain(string host,string root){return host==root||host.EndsWith("."+root,StringComparison.OrdinalIgnoreCase);}
        public static string Home(string site){switch(site){case "bilibili":return "https://www.bilibili.com/";case "youtube":return "https://www.youtube.com/";case "douyin":return "https://www.douyin.com/";default:throw new InvalidDataException("请选择支持的下载站点。");}}
        public static bool CookieDomain(string site,string domain){domain=(domain??"").TrimStart('.').ToLowerInvariant();switch(site){case "bilibili":return Domain(domain,"bilibili.com");case "youtube":return Domain(domain,"youtube.com");case "douyin":return Domain(domain,"douyin.com")||Domain(domain,"iesdouyin.com");default:return false;}}
        public static bool LoginUrl(string site,string url){Uri uri;return Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&String.IsNullOrEmpty(uri.UserInfo)&&(Site(url)==site||site=="youtube"&&(Domain(uri.Host,"google.com")||Domain(uri.Host,"googleusercontent.com")));}
        public void Store(string site,IEnumerable<DownloadCookie> cookies,string agent){Home(site);var values=cookies.Where(c=>c!=null&&CookieDomain(site,c.domain)&&!String.IsNullOrEmpty(c.name)&&c.name.Length<=128&&(c.value??"").Length<=16384).Take(150).ToList();lock(gate){accounts[site]=new DownloadAccount{agent=agent??"",cookies=values};Save();}}
        public void Clear(string site){Home(site);lock(gate){accounts.Remove(site);Save();}}
        public void Import(string site,string text){Home(site);if(text.Length>1024*1024)throw new InvalidDataException("登录信息文件过大，请只导出所选站点的 cookies.txt。");var cookies=new List<DownloadCookie>();foreach(string row in text.Split('\n')){string line=row.TrimEnd('\r');bool httpOnly=line.StartsWith("#HttpOnly_",StringComparison.Ordinal);if(httpOnly)line=line.Substring(10);else if(line.StartsWith("#"))continue;var values=line.Split('\t');double expires;if(values.Length!=7||!CookieDomain(site,values[0])||!Double.TryParse(values[4],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out expires))continue;cookies.Add(new DownloadCookie{domain=values[0],path=values[2],secure=values[3]=="TRUE",expires=expires,name=values[5],value=values[6],httpOnly=httpOnly});}if(cookies.Count==0)throw new InvalidDataException("没有找到所选站点的登录信息，请使用 Netscape cookies.txt 格式。");Store(site,cookies,"");}
        void Save(){byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Library.Json.Serialize(accounts)),Entropy,DataProtectionScope.CurrentUser);string temp=file+".tmp";File.WriteAllBytes(temp,bytes);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
        public CookieContainer Jar(string site){var jar=new CookieContainer(300,150,20000);lock(gate){DownloadAccount account;if(accounts.TryGetValue(site,out account)&&account!=null&&account.cookies!=null)foreach(var c in account.cookies)try{if(!CookieDomain(site,c.domain))continue;var cookie=new Cookie(c.name,c.value,String.IsNullOrEmpty(c.path)?"/":c.path,c.domain){Secure=c.secure,HttpOnly=c.httpOnly};if(c.expires>0)cookie.Expires=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(c.expires);if(!cookie.Expired)jar.Add(cookie);}catch(CookieException){}catch(ArgumentException){}}return jar;}
        public string Agent(string site){lock(gate){DownloadAccount account;return accounts.TryGetValue(site,out account)&&account!=null&&!String.IsNullOrWhiteSpace(account.agent)?account.agent:"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36";}}
        public object[] Status(){lock(gate)return Sites.Select(site=>{DownloadAccount account;bool saved=accounts.TryGetValue(site,out account)&&account!=null&&account.cookies!=null&&account.cookies.Any(c=>CookieDomain(site,c.domain)&&(c.expires<=0||c.expires>(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds));return (object)new{id=site,hasLoginInfo=saved};}).ToArray();}
    }
    public static class DownloadNetwork {
        sealed class Context {public DownloadSessions sessions;public Dictionary<string,CookieContainer> jars=new Dictionary<string,CookieContainer>();}
        static readonly AsyncLocal<Context> Current=new AsyncLocal<Context>();
        sealed class Scope : IDisposable {readonly Context previous;public Scope(DownloadSessions sessions){previous=Current.Value;Current.Value=new Context{sessions=sessions};}public void Dispose(){Current.Value=previous;}}
        public static IDisposable Use(DownloadSessions sessions){return new Scope(sessions);}
        public static CookieContainer Cookies(string site){var context=Current.Value;if(context==null||context.sessions==null)return new CookieContainer();lock(context.jars){CookieContainer jar;if(!context.jars.TryGetValue(site,out jar))context.jars[site]=jar=context.sessions.Jar(site);return jar;}}
        public static string Agent(string url){string site=DownloadSessions.Site(url);var context=Current.Value;return context!=null&&context.sessions!=null&&site!=""?context.sessions.Agent(site):"kok/0.4.1";}
        public static HttpClient Client(string url,bool redirects=false){string site=DownloadSessions.Site(url);var handler=new HttpClientHandler{AllowAutoRedirect=redirects,UseCookies=site!="",UseDefaultCredentials=false,AutomaticDecompression=DecompressionMethods.None};if(site!="")handler.CookieContainer=Cookies(site);if(new Uri(url).IsLoopback)handler.UseProxy=false;return new HttpClient(handler){Timeout=TimeSpan.FromSeconds(60)};}
    }
}
