using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    public class MediaCandidate {public string id,url,label,kind,referer,provider="",audioUrl="",filenameHint="";public int height;public double duration;public long bytes;internal object videoStream,audioStream,muxedStream;internal MediaCandidate Copy(){return (MediaCandidate)MemberwiseClone();}}
    public class MediaCaption {public string url,language,format="";}
    public class MediaPage {public string title="",url,provider="";public bool fromPage;public List<MediaCandidate> candidates=new List<MediaCandidate>();public List<MediaCaption> captions=new List<MediaCaption>();}
    // Public static page metadata only: no script execution, browser cookies or remote extractor.
    public static class MediaResolver {
        const string NotFound="这个网页暂未找到公开音视频。可尝试媒体直链、M3U8 或 MPD；需要登录或复杂脚本的站点尚未适配。";
        static MatchCollection Matches(string text,string pattern){return Regex.Matches(text,pattern,RegexOptions.IgnoreCase|RegexOptions.Singleline,TimeSpan.FromSeconds(2));}
        static string Decode(string value){
            value=WebUtility.HtmlDecode(value??"").Replace("\\/","/");
            return Regex.Replace(value,@"\\u([0-9a-fA-F]{4})",m=>((char)Int32.Parse(m.Groups[1].Value,NumberStyles.HexNumber)).ToString());
        }
        static string Url(string page,string value){try{value=Decode(value).Trim();if(value.Length==0||value.StartsWith("#"))return null;return DownloadManager.ValidateUrl(new Uri(new Uri(page),value).AbsoluteUri);}catch(ArgumentException){return null;}catch(UriFormatException){return null;}catch(InvalidDataException){return null;}}
        static string Kind(string url){string ext=Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();if(ext==".m3u8"||ext==".mpd")return "video";string kind=Library.Classify(new Uri(url).AbsolutePath);return kind=="video"||kind=="music"?kind:null;}
        static Dictionary<string,string> Attributes(string tag){
            var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(Match match in Matches(tag,@"([\w:-]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))"))result[match.Groups[1].Value]=Decode(match.Groups[2].Success?match.Groups[2].Value:match.Groups[3].Success?match.Groups[3].Value:match.Groups[4].Value);
            return result;
        }
        static string Get(Dictionary<string,string> values,string key){string value;return values.TryGetValue(key,out value)?value:"";}
        static string Id(string url){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-","").Substring(0,16).ToLowerInvariant();}
        static int Height(string label){var match=Regex.Match(label??"",@"(?:^|\D)([1-8]?\d{3})\s*p?\b",RegexOptions.IgnoreCase);int n;return match.Success&&Int32.TryParse(match.Groups[1].Value,out n)?n:0;}
        static void Add(MediaPage page,string baseUrl,string value,string kind,string label,int height){
            string url=Url(baseUrl,value);if(url==null||page.candidates.Count>=40)return;kind=Kind(url)??kind;if(kind!="video"&&kind!="music")return;
            var previous=page.candidates.FirstOrDefault(c=>c.url==url);if(previous!=null){if(previous.height<height)previous.height=height;return;}
            if(String.IsNullOrWhiteSpace(label))label=Uri.UnescapeDataString(Path.GetFileName(new Uri(url).AbsolutePath));if(label.Length>160)label=label.Substring(0,160);
            page.candidates.Add(new MediaCandidate{id=Id(url),url=url,kind=kind,label=label,height=height>0?height:Height(label),referer=new Uri(baseUrl).GetLeftPart(UriPartial.Path)});
        }
        static void Caption(MediaPage page,string baseUrl,string value,string language){string url=Url(baseUrl,value);if(url!=null&&page.captions.Count<20&&!page.captions.Any(c=>c.url==url))page.captions.Add(new MediaCaption{url=url,language=(language??"").Substring(0,Math.Min(24,(language??"").Length))});}
        static void Json(MediaPage page,string baseUrl,object data,int depth,ref int budget){
            if(data==null||depth>16||budget--<=0)return;
            var map=data as Dictionary<string,object>;if(map!=null){
                Func<string,string> text=key=>map.ContainsKey(key)&&map[key] is string?(string)map[key]:"";
                string type=text("@type")+" "+text("mimeType")+" "+text("type"),kind=type.IndexOf("audio",StringComparison.OrdinalIgnoreCase)>=0?"music":"video";
                string label=text("label");if(label=="")label=text("name");int height=Height(text("quality")+" "+label);object number;if(map.TryGetValue("height",out number))Int32.TryParse(Convert.ToString(number,CultureInfo.InvariantCulture),out height);
                string language=text("srclang");if(language=="")language=text("languageCode");if(language=="")language=text("language");
                foreach(string key in new[]{"contentUrl","videoUrl","playUrl","streamUrl","file","src","url","baseUrl"}){
                    string value=text(key);if(value=="")continue;string resolved=Url(baseUrl,value);if(resolved==null)continue;
                    bool caption=new[]{".vtt",".srt"}.Contains(Path.GetExtension(new Uri(resolved).AbsolutePath).ToLowerInvariant())||type.IndexOf("caption",StringComparison.OrdinalIgnoreCase)>=0||type.IndexOf("subtitle",StringComparison.OrdinalIgnoreCase)>=0||key=="baseUrl"&&language!="";
                    if(caption)Caption(page,baseUrl,value,language);else if(Kind(resolved)!=null||key=="contentUrl"||type.IndexOf("video",StringComparison.OrdinalIgnoreCase)>=0||type.IndexOf("audio",StringComparison.OrdinalIgnoreCase)>=0)Add(page,baseUrl,value,kind,label,height);
                }
                foreach(var item in map.Values)Json(page,baseUrl,item,depth+1,ref budget);
            }else{var array=data as System.Collections.IEnumerable;if(array!=null&&!(data is string))foreach(var item in array)Json(page,baseUrl,item,depth+1,ref budget);}
        }
        static List<string> Parse(MediaPage page,string baseUrl,string html){
            var frames=new List<string>();string document=Regex.Replace(html,@"<!--[\s\S]*?-->","",RegexOptions.None,TimeSpan.FromSeconds(2));
            SiteAdapters.ParseEmbedded(page,baseUrl,document);
            foreach(Match match in Matches(document,@"<meta\b[^>]*>")){
                var a=Attributes(match.Value);string property=Get(a,"property");if(property=="")property=Get(a,"name");string value=Get(a,"content");
                if(property.Equals("og:title",StringComparison.OrdinalIgnoreCase)&&page.title=="")page.title=value;
                if(new[]{"og:video","og:video:url","og:video:secure_url","twitter:player:stream","og:audio","og:audio:url","og:audio:secure_url"}.Contains(property.ToLowerInvariant()))Add(page,baseUrl,value,property.Contains("audio")?"music":"video","",0);
            }
            if(page.title==""){var title=Matches(document,@"<title\b[^>]*>(.*?)</title>").Cast<Match>().FirstOrDefault();if(title!=null)page.title=Decode(Regex.Replace(title.Groups[1].Value,"<[^>]+>",""));}
            foreach(Match match in Matches(document,@"<(video|audio|source|track|iframe|a)\b[^>]*>")){
                string tag=match.Groups[1].Value.ToLowerInvariant();var a=Attributes(match.Value);string value=Get(a,tag=="a"?"href":"src"),url=Url(baseUrl,value);if(url==null)continue;
                if(tag=="track"){if(Get(a,"kind")==""||new[]{"captions","subtitles"}.Contains(Get(a,"kind")))Caption(page,baseUrl,value,Get(a,"srclang"));continue;}
                if(tag=="iframe"){if(frames.Count<3)frames.Add(url);continue;}
                if(tag=="a"&&Kind(url)==null)continue;
                string type=Get(a,"type"),label=Get(a,"label");if(label=="")label=Get(a,"data-quality");if(label=="")label=Get(a,"res");
                Add(page,baseUrl,value,tag=="audio"||type.StartsWith("audio/",StringComparison.OrdinalIgnoreCase)?"music":"video",label,Height(label));
            }
            foreach(Match match in Matches(document,@"<script\b[^>]*>(.*?)</script>")){
                string json=match.Groups[1].Value.Trim();if(json.Length>512*1024)continue;
                if(json.StartsWith("{")||json.StartsWith("["))try{int budget=3000;Json(page,baseUrl,Library.Json.DeserializeObject(json),0,ref budget);}catch(ArgumentException){}catch(InvalidOperationException){}
                foreach(Match pair in Matches(json,@"(?:""|'|\b)(?:contentUrl|videoUrl|playUrl|streamUrl|file|src)(?:""|')?\s*[:=]\s*(?:""((?:\\.|[^""\\])*)""|'((?:\\.|[^'\\])*)')")){
                    string value=Decode(pair.Groups[1].Success?pair.Groups[1].Value:pair.Groups[2].Value),url=Url(baseUrl,value);if(url!=null&&Kind(url)!=null)Add(page,baseUrl,value,Kind(url),"",0);
                }
            }
            return frames;
        }
        static async Task ResolveInto(MediaPage result,string input,HashSet<string> visited,int depth,CancellationToken token){
            if(depth>2||visited.Count>=5||!visited.Add(input))return;
            using(var client=DirectDownloads.Client(input))using(var response=await DirectDownloads.Open(client,input,null,false,null,token).ConfigureAwait(false)){
                DirectDownloads.RequireSuccess(response);string url=response.RequestMessage.RequestUri.AbsoluteUri,mime=response.Content.Headers.ContentType==null?"":response.Content.Headers.ContentType.MediaType.ToLowerInvariant();
                bool html=mime.Contains("html"),json=mime.Contains("json");
                if(!html&&!json){Add(result,url,url,mime.StartsWith("audio/")?"music":"video","",0);return;}
                if(response.Content.Headers.ContentLength>4*1024*1024)throw new InvalidDataException("网页内容过大，暂不支持自动查找媒体。");
                using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var buffer=new MemoryStream()){
                    byte[] bytes=new byte[16384];int count;while((count=await DirectDownloads.Read(stream,bytes,bytes.Length,response,token).ConfigureAwait(false))>0){if(buffer.Length+count>4*1024*1024)throw new InvalidDataException("网页内容过大，暂不支持自动查找媒体。");buffer.Write(bytes,0,count);}
                    Encoding encoding=Encoding.UTF8;try{if(response.Content.Headers.ContentType!=null&&!String.IsNullOrEmpty(response.Content.Headers.ContentType.CharSet))encoding=Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet.Trim('"'));}catch(ArgumentException){}
                    string text=encoding.GetString(buffer.ToArray()).TrimStart('\uFEFF');result.fromPage=true;
                    if(json){try{int budget=3000;Json(result,url,Library.Json.DeserializeObject(text),0,ref budget);}catch(ArgumentException){}catch(InvalidOperationException){}return;}
                    var frames=Parse(result,url,text);if(result.candidates.Count==0)foreach(string frame in frames)await ResolveInto(result,frame,visited,depth+1,token).ConfigureAwait(false);
                }
            }
        }
        public static async Task<MediaPage> Resolve(string input,CancellationToken token){
            string site=DownloadSessions.Site(input);string host=new Uri(input).Host;if(host.EndsWith("googlevideo.com",StringComparison.OrdinalIgnoreCase)||host.EndsWith("douyinvod.com",StringComparison.OrdinalIgnoreCase)||host.EndsWith("douyinpic.com",StringComparison.OrdinalIgnoreCase))site="";if(site=="youtube")return await YoutubeSite.Resolve(input,token);if(site=="bilibili"||site=="douyin")return await SiteAdapters.Resolve(input,site,token);
            var result=new MediaPage{url=input};try{await ResolveInto(result,input,new HashSet<string>(StringComparer.Ordinal),0,token).ConfigureAwait(false);}catch(RegexMatchTimeoutException){throw new InvalidDataException("网页媒体信息过于复杂，请使用媒体直链。");}
            if(result.candidates.Count==0)throw new InvalidDataException(NotFound);if(result.title.Length>200)result.title=result.title.Substring(0,200);return result;
        }
        public static string NormalizeInput(string input){string value=(input??"").Trim();if(!value.StartsWith("http://",StringComparison.OrdinalIgnoreCase)&&!value.StartsWith("https://",StringComparison.OrdinalIgnoreCase)){var match=Regex.Match(value,@"https?://[^\s<>""，。]+",RegexOptions.IgnoreCase);if(match.Success)value=match.Value.TrimEnd(')',']','》','】');}return DownloadManager.ValidateUrl(value);}
        public static MediaCandidate Select(MediaPage page,string quality,string selection){return SelectCore(page,quality,selection).Copy();}
        static MediaCandidate SelectCore(MediaPage page,string quality,string selection){
            if(!String.IsNullOrEmpty(selection)){var chosen=page.candidates.FirstOrDefault(c=>c.id==selection);if(chosen==null)throw new InvalidDataException("所选资源已变化，请重新解析链接后选择。");return chosen;}
            var options=page.candidates.AsEnumerable();if(quality=="audio"&&options.Any(c=>c.kind=="music"))options=options.Where(c=>c.kind=="music");else if(options.Any(c=>c.kind=="video"))options=options.Where(c=>c.kind=="video");
            int max;if(Int32.TryParse(quality,out max)){var limited=options.Where(c=>c.height>0&&c.height<=max);if(limited.Any())options=limited;else if(options.Any(c=>c.height>0))return options.OrderBy(c=>c.height==0?Int32.MaxValue:c.height).First();}
            return options.OrderByDescending(c=>c.height).First();
        }
        public static async Task SaveCaptions(MediaPage page,string file,CancellationToken token){
            if(page.captions.Any(c=>c.format=="youtube")){await YoutubeSite.SaveCaptions(page,file,token);return;}
            int index=0;foreach(var caption in page.captions){
                using(var client=DirectDownloads.Client(caption.url))using(var response=await DirectDownloads.Open(client,caption.url,null,false,null,token,new Uri(page.url).GetLeftPart(UriPartial.Path)).ConfigureAwait(false)){
                    DirectDownloads.RequireSuccess(response);using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var output=new MemoryStream()){
                        byte[] buffer=new byte[16384];int count;while((count=await DirectDownloads.Read(input,buffer,buffer.Length,response,token).ConfigureAwait(false))>0){if(output.Length+count>8*1024*1024)throw new InvalidDataException("字幕文件过大，暂不支持保存。");output.Write(buffer,0,count);}
                        string text=Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF');if(caption.format=="bilibili-json"){var rows=SiteAdapters.Items(SiteAdapters.Value(Library.Json.DeserializeObject(text),"body"));var converted=new StringBuilder();int number=0;foreach(var row in rows){double from=SiteAdapters.Number(row,"from"),to=SiteAdapters.Number(row,"to");if(from<0||to<from||to>7*24*60*60)continue;converted.Append(++number).Append('\n').Append(CaptionTime(from)).Append(" --> ").Append(CaptionTime(to)).Append('\n').Append(SiteAdapters.Text(row,"content")).Append("\n\n");}text=converted.ToString();}
                        bool vtt=text.TrimStart().StartsWith("WEBVTT",StringComparison.Ordinal),srt=Regex.IsMatch(text,@"\d{2}:\d{2}:\d{2},\d{3}\s*-->");if(!vtt&&!srt)throw new InvalidDataException("网页字幕不是支持的 SRT 或 VTT 格式。");
                        string language=Regex.Replace(caption.language??"",@"[^a-zA-Z0-9_-]","");if(language=="")language="sub";string target=Path.Combine(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file)+"."+language+"."+(++index)+(vtt?".vtt":".srt"));if(!File.Exists(target))File.WriteAllText(target,text,new UTF8Encoding(false));
                    }
                }
            }
        }
        static string CaptionTime(double seconds){var value=TimeSpan.FromMilliseconds(Math.Round(seconds*1000));return ((int)value.TotalHours).ToString("00")+":"+value.Minutes.ToString("00")+":"+value.Seconds.ToString("00")+","+value.Milliseconds.ToString("000");}
    }
}
