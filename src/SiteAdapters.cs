using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    // Protocol/layout references: iawia002/lux (MIT). No external extractor process.
    public static class SiteAdapters {
        public static Dictionary<string,object> Map(object value){return value as Dictionary<string,object>??new Dictionary<string,object>();}
        public static object Value(object value,string key){object result;return Map(value).TryGetValue(key,out result)?result:null;}
        public static string Text(object value,string key){return Convert.ToString(Value(value,key),CultureInfo.InvariantCulture)??"";}
        public static double Number(object value,string key){double number;return Double.TryParse(Text(value,key),NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&!Double.IsInfinity(number)&&!Double.IsNaN(number)?number:0;}
        public static IEnumerable<object> Items(object value){return value is IEnumerable&&!(value is string)&&!(value is IDictionary)?((IEnumerable)value).Cast<object>():Enumerable.Empty<object>();}
        public static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").Substring(0,16).ToLowerInvariant();}
        static string SafeUrl(string page,string value){try{if(String.IsNullOrWhiteSpace(value))return null;return DownloadManager.ValidateUrl(new Uri(new Uri(page),value).AbsoluteUri);}catch{return null;}}
        static string FirstUrl(object value){return Items(Value(value,"url_list")).Concat(Items(Value(value,"urlList"))).Select(Convert.ToString).FirstOrDefault(u=>u.StartsWith("http",StringComparison.OrdinalIgnoreCase))??(Text(value,"url")!=""?Text(value,"url"):Text(value,"src"));}
        public static MediaCandidate Candidate(string page,string url,string kind,string label,int height,string key){url=SafeUrl(page,url);if(url==null)return null;return new MediaCandidate{id=Hash(key),url=url,kind=kind,label=label,height=height,referer=new Uri(page).GetLeftPart(UriPartial.Path)};}
        static void Add(MediaPage page,MediaCandidate candidate){if(candidate!=null&&page.candidates.Count<60&&!page.candidates.Any(c=>c.id==candidate.id))page.candidates.Add(candidate);}
        // Scan balanced JSON assignments without executing any supplied JavaScript.
        public static object Assignment(string text,string name){
            int start=text.IndexOf(name,StringComparison.Ordinal);if(start<0)return null;start=text.IndexOf('=',start+name.Length);if(start<0)return null;while(++start<text.Length&&Char.IsWhiteSpace(text[start])){}if(start>=text.Length||text[start]!='{')return null;
            int depth=0;bool quoted=false,escape=false;for(int n=start;n<text.Length&&n-start<4*1024*1024;n++){char c=text[n];if(quoted){if(escape)escape=false;else if(c=='\\')escape=true;else if(c=='"')quoted=false;continue;}if(c=='"')quoted=true;else if(c=='{')depth++;else if(c=='}'&&--depth==0){try{return Library.Json.DeserializeObject(text.Substring(start,n-start+1));}catch{return null;}}}return null;
        }
        static void BiliStreams(MediaPage page,object root,string identity){
            object data=Value(root,"data")??Value(root,"result")??root;object dash=Value(data,"dash");
            var audios=Items(Value(dash,"audio")).OrderByDescending(a=>Number(a,"bandwidth")).ToList();var audio=audios.FirstOrDefault();string audioUrl=Text(audio,"baseUrl");if(audioUrl=="")audioUrl=Text(audio,"base_url");
            foreach(var video in Items(Value(dash,"video")).OrderByDescending(v=>Text(v,"codecs").StartsWith("avc",StringComparison.OrdinalIgnoreCase))){
                string url=Text(video,"baseUrl");if(url=="")url=Text(video,"base_url");int height=(int)Number(video,"height");string key=identity+"/v/"+Text(video,"id")+"/"+Text(video,"codecid");var candidate=Candidate(page.url,url,"video",height+"p · "+Text(video,"codecs"),height,key);if(candidate==null)continue;candidate.provider="bilibili";candidate.audioUrl=SafeUrl(page.url,audioUrl);candidate.filenameHint="video.mp4";candidate.duration=Number(data,"timelength")/1000;Add(page,candidate);
            }
            if(audio!=null){var candidate=Candidate(page.url,audioUrl,"music","Audio",0,identity+"/a/"+Text(audio,"id"));if(candidate!=null){candidate.provider="bilibili";candidate.filenameHint="audio.m4a";Add(page,candidate);}}
            var combined=Items(Value(data,"durl")).ToList();if(combined.Count==1){var candidate=Candidate(page.url,Text(combined[0],"url"),"video",Text(data,"quality")+" · MP4",0,identity+"/combined/"+Text(data,"quality"));if(candidate!=null){candidate.provider="bilibili";candidate.filenameHint="video.mp4";Add(page,candidate);}}
        }
        static void DouyinObject(MediaPage page,object value,int depth,ref int budget,string expectedId=""){
            if(depth>20||budget--<=0||value==null)return;var map=value as Dictionary<string,object>;
            if(map!=null){object video=Value(map,"video");if(video!=null&&(expectedId==""||Text(map,"aweme_id")==expectedId||Text(map,"awemeId")==expectedId)&&(Value(map,"aweme_id")!=null||Value(map,"awemeId")!=null||Value(video,"play_addr")!=null||Value(video,"playAddr")!=null)){
                    if(page.title=="")page.title=Text(map,"desc");string identity=Text(map,"aweme_id");if(identity=="")identity=Text(map,"awemeId");
                    var sources=Items(Value(video,"bit_rate")).Concat(Items(Value(video,"bitRateList"))).Select(b=>Value(b,"play_addr")??Value(b,"playAddr")).Where(b=>b!=null).ToList();sources.Add(Value(video,"play_addr")??Value(video,"playAddr"));
                    foreach(var source in sources.Where(v=>v!=null)){string url=source is string?(string)source:FirstUrl(source);int height=(int)Number(source,"height");var candidate=Candidate(page.url,url,"video",height>0?height+"p":"MP4",height,identity+"/"+url);if(candidate==null)continue;candidate.provider="douyin";candidate.filenameHint="video.mp4";candidate.duration=Number(video,"duration")/1000;Add(page,candidate);}
                }
                foreach(var item in map.Values)DouyinObject(page,item,depth+1,ref budget,expectedId);
            }else foreach(var item in Items(value))DouyinObject(page,item,depth+1,ref budget);
        }
        public static void ParseEmbedded(MediaPage page,string url,string html){
            object play=Assignment(html,"__playinfo__");if(play!=null){page.fromPage=true;page.provider="bilibili";BiliStreams(page,play,url);}
            var expected=Regex.Match(url,@"/(?:video|note)/(\d+)");string expectedId=expected.Success?expected.Groups[1].Value:"";
            object router=Assignment(html,"_ROUTER_DATA");if(router!=null){int budget=10000;DouyinObject(page,router,0,ref budget,expectedId);}
            foreach(Match match in Regex.Matches(html,@"<script[^>]*id\s*=\s*[""'](?:RENDER_DATA|__UNIVERSAL_DATA_FOR_REHYDRATION__)[""'][^>]*>(.*?)</script>",RegexOptions.Singleline|RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2)))try{string json=Uri.UnescapeDataString(WebUtility.HtmlDecode(match.Groups[1].Value));int budget=10000;DouyinObject(page,Library.Json.DeserializeObject(json),0,ref budget,expectedId);}catch(ArgumentException){}catch(InvalidOperationException){}
        }
        public static void ParseResponse(MediaPage page,string json,string site,string expectedId){
            object root;try{root=Library.Json.DeserializeObject(json);}catch{return;}
            if(site=="bilibili"){BiliStreams(page,root,page.url);page.provider=site;}
            else if(site=="douyin"){int budget=10000;DouyinObject(page,root,0,ref budget,expectedId);page.provider=site;}
        }
        static async Task<Tuple<string,string>> Fetch(string url,CancellationToken token){using(var client=DirectDownloads.Client(url))using(var response=await DirectDownloads.Open(client,url,null,false,null,token,DownloadSessions.Site(url)=="bilibili"?DownloadSessions.Home("bilibili"):null)){
                DirectDownloads.RequireSuccess(response);using(var input=await response.Content.ReadAsStreamAsync())using(var output=new MemoryStream()){byte[] buffer=new byte[16384];int count;while((count=await DirectDownloads.Read(input,buffer,buffer.Length,response,token))>0){if(output.Length+count>6*1024*1024)throw new InvalidDataException("站点返回的页面过大，请稍后重试。");output.Write(buffer,0,count);}return Tuple.Create(response.RequestMessage.RequestUri.AbsoluteUri,Encoding.UTF8.GetString(output.ToArray()));}
            }}
        static async Task<object> Api(string url,CancellationToken token){var data=await Fetch(url,token);try{return Library.Json.DeserializeObject(data.Item2);}catch{throw new InvalidDataException("站点返回了无效信息，请稍后重试。");}}
        // Bilibili's public WBI request-signing layout; no external signing service.
        public static string SignBili(IDictionary<string,string> values,string keys,long timestamp){
            int[] order={46,47,18,2,53,8,23,32,15,50,10,31,58,3,45,35,27,43,5,49,33,9,42,19,29,28,14,39,12,38,41,13};if(keys==null||keys.Length<64)throw new InvalidDataException("B 站签名信息暂不可用，请稍后重试。");
            string mix=new string(order.Select(n=>keys[n]).ToArray());var sorted=new SortedDictionary<string,string>(values,StringComparer.Ordinal);sorted["wts"]=timestamp.ToString(CultureInfo.InvariantCulture);
            string query=String.Join("&",sorted.Select(pair=>Uri.EscapeDataString(pair.Key)+"="+Uri.EscapeDataString(new string((pair.Value??"").Where(c=>!"!'()*".Contains(c)).ToArray()))));
            using(var md5=MD5.Create())return query+"&w_rid="+BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(query+mix))).Replace("-","").ToLowerInvariant();
        }
        static async Task<object> BiliPlay(string parameter,string cid,CancellationToken token){
            object response=null;try{var nav=await Api("https://api.bilibili.com/x/web-interface/nav",token);var image=Value(Value(nav,"data"),"wbi_img");string keys=Path.GetFileNameWithoutExtension(new Uri(Text(image,"img_url")).AbsolutePath)+Path.GetFileNameWithoutExtension(new Uri(Text(image,"sub_url")).AbsolutePath);var values=new Dictionary<string,string>{{parameter.Split('=')[0],parameter.Split('=')[1]},{"cid",cid},{"qn","127"},{"fnval","4048"},{"fnver","0"},{"fourk","1"}};string signed=SignBili(values,keys,(long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds);response=await Api("https://api.bilibili.com/x/player/wbi/playurl?"+signed,token);}catch(OperationCanceledException){throw;}catch(Exception){}
            if(response!=null&&Number(response,"code")==0)return response;return await Api("https://api.bilibili.com/x/player/playurl?"+parameter+"&cid="+cid+"&qn=127&fnval=4048&fnver=0&fourk=1",token);
        }
        public static async Task<MediaPage> Resolve(string input,string site,CancellationToken token){
            var page=new MediaPage{url=input,provider=site,fromPage=true};string html="";try{var fetched=await Fetch(input,token);page.url=fetched.Item1;html=fetched.Item2;ParseEmbedded(page,page.url,html);}catch(OperationCanceledException){throw;}catch(Exception){if(site!="bilibili")throw;}
            if(site=="bilibili"){
                var id=Regex.Match(page.url,@"/(BV[0-9A-Za-z]{10}|av\d+)",RegexOptions.IgnoreCase);if(!id.Success)throw new InvalidDataException("请使用 B 站普通视频或 b23.tv 分享链接，暂不支持番剧与付费内容。");
                string identity=id.Groups[1].Value,parameter=identity.StartsWith("av",StringComparison.OrdinalIgnoreCase)?"aid="+identity.Substring(2):"bvid="+identity;
                if(page.candidates.Count==0){var view=await Api("https://api.bilibili.com/x/web-interface/view?"+parameter,token);if(Number(view,"code")!=0)throw new InvalidDataException("B 站未返回可用视频，请检查链接、登录状态或访问权限。");var data=Value(view,"data");page.title=Text(data,"title");int index=1;var p=Regex.Match(new Uri(page.url).Query,@"(?:^|[?&])p=(\d+)");if(p.Success)Int32.TryParse(p.Groups[1].Value,out index);var parts=Items(Value(data,"pages")).ToList();if(index<1||index>parts.Count)throw new InvalidDataException("这个 B 站分 P 不存在。");string cid=Text(parts[index-1],"cid");var play=await BiliPlay(parameter,cid,token);BiliStreams(page,play,identity+"/"+cid);
                    try{var captions=await Api("https://api.bilibili.com/x/player/v2?"+parameter+"&cid="+cid,token);foreach(var caption in Items(Value(Value(Value(captions,"data"),"subtitle"),"subtitles"))){string url=SafeUrl(page.url,Text(caption,"subtitle_url"));if(url!=null)page.captions.Add(new MediaCaption{url=url,language=Text(caption,"lan"),format="bilibili-json"});}}catch(OperationCanceledException){throw;}catch(Exception){}
                }
            }else if(site=="douyin"&&page.candidates.Count==0){var id=Regex.Match(page.url,@"/(?:video|note)/(\d+)");if(id.Success){var shared=await Fetch("https://www.iesdouyin.com/share/video/"+id.Groups[1].Value+"/",token);ParseEmbedded(page,shared.Item1,shared.Item2);}}
            if(page.title==""){var title=Regex.Match(html,@"<title[^>]*>(.*?)</title>",RegexOptions.IgnoreCase|RegexOptions.Singleline);page.title=title.Success?WebUtility.HtmlDecode(title.Groups[1].Value):site;}
            if(page.candidates.Count==0)throw new InvalidDataException("站点暂未返回可下载资源，请先保存站点登录信息；风控或权限限制无法通过登录保证解决。");return page;
        }
    }
}
