using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Globalization;

namespace kok {
    public class DirectMedia {
        public string filename,mime,etag,lastModified,sourceHash;
        public long total;
        public bool audio,resumeSupported;
    }
    // Native HTTP transfers do not load site scripts, components, cookies or AI credentials.
    public static class DirectDownloads {
        const string NotMedia="没有找到可直接下载的音视频，请提供媒体链接或包含公开媒体的网页。";
        const string Restart="服务器不支持安全续传或文件已变化，已从头下载。";
        internal static HttpClient Client(string url){
            var client=DownloadNetwork.Client(url);client.Timeout=Timeout.InfiniteTimeSpan;return client;
        }
        static string Hash(string value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
        internal static async Task<HttpResponseMessage> Open(HttpClient client,string url,long? from,bool probe,string validator,CancellationToken token,string referer=null){
            for(int redirects=0;redirects<=8;redirects++){
                using(var request=new HttpRequestMessage(HttpMethod.Get,DownloadManager.ValidateUrl(url)))
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
                    request.Headers.UserAgent.ParseAdd(DownloadNetwork.Agent(url));request.Headers.TryAddWithoutValidation("Accept-Encoding","identity");
                    if(!String.IsNullOrEmpty(referer))request.Headers.Referrer=new Uri(referer);
                    if(from.HasValue)request.Headers.Range=new RangeHeaderValue(from.Value,probe?(long?)511:null);
                    if(!String.IsNullOrEmpty(validator))request.Headers.TryAddWithoutValidation("If-Range",validator);
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));HttpResponseMessage response;
                    try{response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);}
                    catch(OperationCanceledException){token.ThrowIfCancellationRequested();throw new InvalidOperationException("下载连接超时，请稍后重试。");}
                    int status=(int)response.StatusCode;
                    if(new[]{301,302,303,307,308}.Contains(status)){
                        var location=response.Headers.Location;response.Dispose();if(location==null||redirects==8)throw new InvalidDataException("下载链接重定向无效或次数过多。");
                        url=DownloadManager.ValidateUrl(new Uri(new Uri(url),location).AbsoluteUri);continue;
                    }
                    return response;
                }
            }
            throw new InvalidDataException("下载链接重定向无效或次数过多。");
        }
        internal static void RequireSuccess(HttpResponseMessage response){
            if(response.StatusCode!=HttpStatusCode.OK&&response.StatusCode!=HttpStatusCode.PartialContent)throw new InvalidOperationException("服务器拒绝下载，请检查链接是否过期或需要登录。");
            if(response.Content.Headers.ContentEncoding.Any(e=>!e.Equals("identity",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("服务器返回了不支持的压缩传输格式。");
        }
        internal static async Task<int> Read(Stream stream,byte[] buffer,int count,HttpResponseMessage response,CancellationToken token){
            using(var stall=CancellationTokenSource.CreateLinkedTokenSource(token)){
                stall.CancelAfter(TimeSpan.FromSeconds(30));
                using(stall.Token.Register(()=>response.Dispose())){
                    try{return await stream.ReadAsync(buffer,0,count,stall.Token).ConfigureAwait(false);}
                    catch(Exception){token.ThrowIfCancellationRequested();if(stall.IsCancellationRequested)throw new InvalidOperationException("下载连接超时，请稍后重试。");throw;}
                }
            }
        }
        internal static string SafeName(string value,string fallback){
            value=(value??"").Trim().Trim('"');try{value=Uri.UnescapeDataString(value);}catch(UriFormatException){}
            // Strip both path separator styles before sanitizing the leaf name.
            value=value.Replace('\\','/').Split('/').Last().Trim().TrimEnd('.',' ');
            value=new string(value.Where(c=>!Char.IsControl(c)&&!Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim().TrimEnd('.',' ');
            if(String.IsNullOrWhiteSpace(value))value=fallback;
            string stem=Path.GetFileNameWithoutExtension(value),ext=Path.GetExtension(value);
            if(new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem.ToUpperInvariant()))stem="_"+stem;
            if(ext.Length>12)ext="";if(stem.Length>130)stem=stem.Substring(0,130);return stem+ext;
        }
        static DirectMedia Metadata(HttpResponseMessage response,string url,string filenameHint=null){
            var h=response.Content.Headers;string mime=h.ContentType==null?"":h.ContentType.MediaType.ToLowerInvariant();
            string kindHint=StreamDownloads.Kind(response.RequestMessage.RequestUri.AbsoluteUri,mime);if(kindHint!=null)throw new StreamLinkException(kindHint);
            if(mime.StartsWith("text/")||mime.Contains("html")||mime.Contains("json")||mime.Contains("xml")||mime.Contains("mpegurl")||mime.Contains("dash"))throw new InvalidDataException(NotMedia);
            string name=h.ContentDisposition==null?null:h.ContentDisposition.FileNameStar??h.ContentDisposition.FileName;
            name=SafeName(name??new Uri(url).AbsolutePath,"media");
            string kind=Library.Classify(name),extension="";
            if(kind!="video"&&kind!="music"&&!String.IsNullOrEmpty(filenameHint)){string hinted=Library.Classify(filenameHint);if(hinted=="video"||hinted=="music"){name=SafeName(filenameHint,"media.mp4");kind=hinted;}}
            switch(mime){case "video/mp4":extension=".mp4";break;case "audio/mp4":extension=".m4a";break;case "video/webm":case "audio/webm":extension=".webm";break;case "video/x-matroska":extension=".mkv";break;case "video/quicktime":extension=".mov";break;case "video/x-msvideo":extension=".avi";break;case "audio/mpeg":extension=".mp3";break;case "audio/wav":case "audio/x-wav":extension=".wav";break;case "audio/flac":case "audio/x-flac":extension=".flac";break;case "audio/ogg":extension=".ogg";break;case "audio/aac":extension=".aac";break;}
            if(kind!="video"&&kind!="music"){
                if(extension=="")throw new InvalidDataException(NotMedia);
                name=SafeName(Path.GetFileNameWithoutExtension(name)+extension,"media"+extension);
            }
            var range=h.ContentRange;long total=range!=null&&range.Length.HasValue?range.Length.Value:h.ContentLength??0;
            return new DirectMedia{filename=name,mime=mime,total=total,audio=mime.StartsWith("audio/")||Library.Classify(name)=="music",etag=response.Headers.ETag!=null&&!response.Headers.ETag.IsWeak?response.Headers.ETag.ToString():null,lastModified=h.LastModified.HasValue?h.LastModified.Value.ToString("R",CultureInfo.InvariantCulture):null,sourceHash=Hash(url),resumeSupported=response.StatusCode==HttpStatusCode.PartialContent||response.Headers.AcceptRanges.Contains("bytes")};
        }
        static void CheckManifestHeaders(HttpResponseMessage response){string mime=response.Content.Headers.ContentType==null?"":response.Content.Headers.ContentType.MediaType.ToLowerInvariant();string kind=StreamDownloads.Kind(response.RequestMessage.RequestUri.AbsoluteUri,mime);if(kind!=null)throw new StreamLinkException(kind);}
        static void CheckPrefix(byte[] bytes,int length){
            string start=Encoding.UTF8.GetString(bytes,0,Math.Min(length,512)).TrimStart('\uFEFF',' ','\r','\n','\t').ToLowerInvariant();
            if(start.StartsWith("#extm3u"))throw new StreamLinkException("hls");
            if(System.Text.RegularExpressions.Regex.IsMatch(start,@"<(?:[a-z0-9_]+:)?mpd(?:\s|>)"))throw new StreamLinkException("dash");
            if(length==0||start.StartsWith("<")||start.StartsWith("{")||start.StartsWith("[")||start.StartsWith("#extm3u"))throw new InvalidDataException(NotMedia);
        }
        static string Validator(DirectMedia media){return media==null?null:media.etag??media.lastModified;}
        static bool SameEntity(DirectMedia before,DirectMedia after){return before!=null&&(before.etag==null||after.etag==null||before.etag==after.etag)&&(before.lastModified==null||after.lastModified==null||before.lastModified==after.lastModified)&&(before.total==0||after.total==0||before.total==after.total);}
        static void SaveMetadata(string file,DirectMedia media){string temp=file+".tmp";File.WriteAllText(temp,Library.Json.Serialize(media),new UTF8Encoding(false));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
        public static async Task<object> Probe(string url,CancellationToken token,string referer=null,string filenameHint=null){
            using(var client=Client(url))using(var response=await Open(client,url,0,true,null,token,referer).ConfigureAwait(false)){
                RequireSuccess(response);CheckManifestHeaders(response);
                if(response.StatusCode==HttpStatusCode.PartialContent&&(response.Content.Headers.ContentRange==null||response.Content.Headers.ContentRange.From!=0))throw new InvalidDataException("服务器返回了无效的下载范围。");
                using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false)){byte[] prefix=new byte[512];int count=await Read(stream,prefix,prefix.Length,response,token).ConfigureAwait(false);CheckPrefix(prefix,count);}
                var info=Metadata(response,url,filenameHint);
                return new{backend="native",url=url,title=info.filename,filename=info.filename,source=new Uri(url).Host,duration=0,bytes=info.total,kind=info.audio?"music":"video",resumeSupported=info.resumeSupported&&Validator(info)!=null};
            }
        }
        public static async Task<string> Download(string url,string folder,Action<DirectMedia,string> metadata,Action<long,long,double> progress,CancellationToken token,string referer=null,string filenameHint=null){
            Directory.CreateDirectory(folder);string part=Path.Combine(folder,"native-media.part"),record=Path.Combine(folder,"native-resume.json");DirectMedia old=null;
            if(File.Exists(record))try{old=Library.Json.Deserialize<DirectMedia>(File.ReadAllText(record,Encoding.UTF8));}catch{}
            long offset=File.Exists(part)?new FileInfo(part).Length:0;string notice="";
            if(offset>0&&(old==null||old.sourceHash!=Hash(url)||Validator(old)==null||old.total>0&&offset>old.total)){offset=0;notice=Restart;}
            using(var client=Client(url)){
                for(int attempt=0;attempt<2;attempt++){
                    using(var response=await Open(client,url,offset>0?(long?)offset:null,false,offset>0?Validator(old):null,token,referer).ConfigureAwait(false)){
                        if(response.StatusCode==HttpStatusCode.RequestedRangeNotSatisfiable&&offset>0){offset=0;notice=Restart;continue;}
                        RequireSuccess(response);CheckManifestHeaders(response);
                        using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false)){
                        byte[] buffer=new byte[65536];int first=await Read(stream,buffer,buffer.Length,response,token).ConfigureAwait(false);
                        if(offset==0)CheckPrefix(buffer,first);
                        var info=Metadata(response,url,filenameHint);var range=response.Content.Headers.ContentRange;
                        if(response.StatusCode==HttpStatusCode.PartialContent){
                            bool valid=range!=null&&range.Unit=="bytes"&&range.From==offset&&range.To.HasValue&&range.Length.HasValue&&range.To.Value==range.Length.Value-1&&range.To.Value>=offset&&(response.Content.Headers.ContentLength==null||response.Content.Headers.ContentLength==range.To.Value-offset+1)&&(offset==0||SameEntity(old,info));
                            if(!valid){if(offset>0){offset=0;notice=Restart;continue;}throw new InvalidDataException("服务器返回了无效的下载范围。");}
                        }else if(offset>0){offset=0;notice=Restart;}
                        // Preserve validators omitted by a valid partial response.
                        if(offset>0){info.etag=info.etag??old.etag;info.lastModified=info.lastModified??old.lastModified;}
                            SaveMetadata(record,info);metadata(info,notice);
                            long received=offset,session=0;var watch=Stopwatch.StartNew();
                            using(var output=new FileStream(part,offset>0?FileMode.Open:FileMode.Create,FileAccess.Write,FileShare.Read,65536,true)){
                                if(output.Length!=offset)throw new InvalidDataException("本地下载片段已变化，请重新创建任务。");output.Position=offset;
                                int count=first;
                                while(count>0){token.ThrowIfCancellationRequested();if(info.total>0&&received>info.total-count)throw new InvalidDataException("下载文件长度与服务器声明不一致。");await output.WriteAsync(buffer,0,count,token).ConfigureAwait(false);received+=count;session+=count;progress(received,info.total,session/Math.Max(.001,watch.Elapsed.TotalSeconds));count=await Read(stream,buffer,buffer.Length,response,token).ConfigureAwait(false);}
                                await output.FlushAsync(token).ConfigureAwait(false);
                            }
                            token.ThrowIfCancellationRequested();if(received==0||info.total>0&&received!=info.total)throw new InvalidDataException("下载连接提前中断，可以继续或重试。");
                            string target=Path.Combine(folder,info.filename),stem=Path.GetFileNameWithoutExtension(info.filename),ext=Path.GetExtension(info.filename);for(int i=1;File.Exists(target);i++)target=Path.Combine(folder,stem+" ("+i+")"+ext);
                            File.Move(part,target);if(File.Exists(record))File.Delete(record);progress(received,received,0);return target;
                        }
                    }
                }
            }
            throw new InvalidDataException("服务器返回了无效的下载范围。");
        }
    }
}
