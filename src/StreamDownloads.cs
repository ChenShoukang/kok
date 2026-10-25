using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace kok {
    sealed class StreamLinkException : Exception {public readonly string Kind;public StreamLinkException(string kind){Kind=kind;}}
    public class StreamTrack {public int index,width,height;public string codec_type,codec_name,bit_rate;}
    public class StreamFormat {public string duration;}
    public class StreamProbe {public List<StreamTrack> streams=new List<StreamTrack>();public StreamFormat format=new StreamFormat();}
    public class StreamMedia {public string kind,title;public double duration;public bool audio;public int videoIndex=-1,audioIndex=-1;}
    // Site extraction stays separate: this path accepts actual VOD manifest URLs only.
    public static class StreamDownloads {
        const string Unsupported="这个分段链接暂不支持，请使用普通点播清单；直播、加密和 DRM 内容暂不支持。";
        public static string Kind(string url,string mime){string ext=Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();if(ext==".m3u8"||mime.Contains("mpegurl"))return "hls";if(ext==".mpd"||mime.Contains("dash+xml"))return "dash";return null;}
        static string Resolve(string url,string value){return DownloadManager.ValidateUrl(new Uri(new Uri(url),value.Trim()).AbsoluteUri);}
        static async Task<Tuple<string,string>> Manifest(string url,CancellationToken token,string referer){
            using(var client=DirectDownloads.Client(url))using(var response=await DirectDownloads.Open(client,url,null,false,null,token,referer).ConfigureAwait(false)){
                DirectDownloads.RequireSuccess(response);if(response.Content.Headers.ContentLength>2*1024*1024)throw new InvalidDataException("视频清单过大，暂不支持。");
                using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var output=new MemoryStream()){
                    byte[] buffer=new byte[16384];int read;while((read=await DirectDownloads.Read(stream,buffer,buffer.Length,response,token).ConfigureAwait(false))>0){if(output.Length+read>2*1024*1024)throw new InvalidDataException("视频清单过大，暂不支持。");output.Write(buffer,0,read);}
                    return Tuple.Create(response.RequestMessage.RequestUri.AbsoluteUri,Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF'));
                }
            }
        }
        static async Task ValidateManifest(string url,string kind,HashSet<string> visited,int depth,CancellationToken token,string referer){
            if(depth>4||visited.Count>=20)throw new InvalidDataException("视频清单嵌套过多，暂不支持。");if(!visited.Add(url))return;
            var fetched=await Manifest(url,token,referer).ConfigureAwait(false);url=fetched.Item1;string text=fetched.Item2;
            if(kind=="hls"){
                if(!text.TrimStart().StartsWith("#EXTM3U",StringComparison.Ordinal))throw new InvalidDataException(Unsupported);
                bool master=text.Contains("#EXT-X-STREAM-INF:");if(!master&&!text.Contains("#EXT-X-ENDLIST"))throw new InvalidDataException(Unsupported);
                var child=new List<string>();bool nextVariant=false;
                foreach(string raw in text.Split('\n')){
                    string line=raw.Trim();if(line.Length==0)continue;
                    if(line.StartsWith("#EXT-X-KEY:",StringComparison.Ordinal)||line.StartsWith("#EXT-X-SESSION-KEY:",StringComparison.Ordinal)){if(!Regex.IsMatch(line,@"(?:[:,])METHOD=NONE(?:,|$)"))throw new InvalidDataException(Unsupported);}
                    foreach(Match match in Regex.Matches(line,"URI=\"([^\"]+)\"")){string target=Resolve(url,match.Groups[1].Value);if(line.StartsWith("#EXT-X-MEDIA:",StringComparison.Ordinal)||line.StartsWith("#EXT-X-I-FRAME-STREAM-INF:",StringComparison.Ordinal))child.Add(target);}
                    if(!line.StartsWith("#",StringComparison.Ordinal)){string target=Resolve(url,line);if(nextVariant)child.Add(target);nextVariant=false;}
                    if(line.StartsWith("#EXT-X-STREAM-INF:",StringComparison.Ordinal))nextVariant=true;
                }
                if(master&&child.Count==0)throw new InvalidDataException(Unsupported);
                foreach(string target in child.Distinct())await ValidateManifest(target,"hls",visited,depth+1,token,referer).ConfigureAwait(false);
            }else{
                XDocument document;
                try{using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024}))document=XDocument.Load(reader);}
                catch(XmlException){throw new InvalidDataException(Unsupported);}
                if(document.Root==null||document.Root.Name.LocalName!="MPD"||String.Equals((string)document.Root.Attribute("type"),"dynamic",StringComparison.OrdinalIgnoreCase)||document.Descendants().Any(e=>e.Name.LocalName=="ContentProtection"))throw new InvalidDataException(Unsupported);
                foreach(var element in document.Descendants()){
                    if(element.Name.LocalName=="BaseURL"||element.Name.LocalName=="Location")Resolve(url,element.Value);
                    foreach(var attribute in element.Attributes()){
                        if(attribute.Name.LocalName=="href")throw new InvalidDataException(Unsupported);
                        if(new[]{"media","initialization","sourceURL","index"}.Contains(attribute.Name.LocalName))Resolve(url,attribute.Value);
                    }
                }
            }
        }
        public static List<string> NetworkArguments(string kind,string referer=null){var args=new List<string>{"-hide_banner","-v","error","-protocol_whitelist","http,https,tcp,tls","-format_whitelist","hls,dash,mov,mpegts,aac,mp3,matroska,webm","-rw_timeout","20000000","-user_agent","kok/0.4.1","-f",kind};if(!String.IsNullOrEmpty(referer))args.AddRange(new[]{"-referer",referer});return args;}
        static double Number(string value){double number;return Double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&!Double.IsNaN(number)&&!Double.IsInfinity(number)&&number>0?number:0;}
        static async Task<StreamProbe> Inspect(string executable,List<string> args,CancellationToken token){
            var json=new StringBuilder();bool overflow=false;int code=await DownloadTools.Run(executable,args,line=>{lock(json){if(json.Length+line.Length<=512*1024)json.AppendLine(line);else overflow=true;}},line=>{},token).ConfigureAwait(false);
            if(code!=0||overflow)throw new InvalidDataException("无法读取分段视频，请检查链接、网络和 FFmpeg 组件。");
            try{var probe=Library.Json.Deserialize<StreamProbe>(json.ToString());if(probe==null||probe.streams==null||!probe.streams.Any(s=>s.codec_type=="video"||s.codec_type=="audio"))throw new InvalidDataException();return probe;}
            catch{throw new InvalidDataException("无法读取分段视频，请检查链接、网络和 FFmpeg 组件。");}
        }
        public static async Task<StreamMedia> Probe(string url,string kind,string ffmpeg,string ffprobe,CancellationToken token,string quality="best",string referer=null){
            if(ffmpeg==null||ffprobe==null)throw new InvalidOperationException("这个链接需要 FFmpeg / FFprobe，请先一键安装媒体组件。");
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                try{
                    await ValidateManifest(url,kind,new HashSet<string>(StringComparer.Ordinal),0,timeout.Token,referer).ConfigureAwait(false);
                    var args=NetworkArguments(kind,referer);args.AddRange(new[]{"-i",url,"-show_entries","format=duration:stream=index,codec_type,width,height,bit_rate","-of","json"});var probe=await Inspect(ffprobe,args,timeout.Token).ConfigureAwait(false);
                    var videos=probe.streams.Where(s=>s.codec_type=="video");int limit;if(Int32.TryParse(quality,out limit)){var limited=videos.Where(s=>s.height<=limit);videos=limited.Any()?limited:videos.OrderBy(s=>s.height).Take(1);}
                    var video=videos.OrderByDescending(s=>(long)s.width*s.height).ThenByDescending(s=>Number(s.bit_rate)).FirstOrDefault();var audio=probe.streams.Where(s=>s.codec_type=="audio").OrderByDescending(s=>Number(s.bit_rate)).FirstOrDefault();
                    if(quality=="audio"){if(audio==null)throw new InvalidDataException("这个资源没有可导出的音轨。");video=null;}
                    string name=Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath);if(String.IsNullOrWhiteSpace(name))name="media";
                    return new StreamMedia{kind=kind,title=DirectDownloads.SafeName(name+(video==null?".m4a":".mp4"),"media.mp4"),audio=video==null,duration=probe.format==null?0:Number(probe.format.duration),videoIndex=video==null?-1:video.index,audioIndex=audio==null?-1:audio.index};
                }catch(OperationCanceledException){token.ThrowIfCancellationRequested();throw new InvalidOperationException("分段视频解析超时，请稍后重试。");}
            }
        }
        public static async Task<string> ExtractAudio(string file,string ffmpeg,string ffprobe,CancellationToken token){
            if(ffmpeg==null||ffprobe==null)throw new InvalidOperationException("仅音频导出需要 FFmpeg / FFprobe，请先安装媒体组件。");
            var formats="mov,matroska,webm,mpegts,aac,mp3,wav,flac,ogg,aiff,avi,asf,mpeg";
            var info=await Inspect(ffprobe,new List<string>{"-v","error","-protocol_whitelist","file","-format_whitelist",formats,"-i",file,"-show_entries","stream=index,codec_type,codec_name","-of","json"},token).ConfigureAwait(false);
            var audio=info.streams.FirstOrDefault(s=>s.codec_type=="audio");if(audio==null)throw new InvalidDataException("这个资源没有可导出的音轨。");
            string target=Path.Combine(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file)+".m4a"),part=Path.Combine(Path.GetDirectoryName(file),"audio-export.part");
            for(int n=1;File.Exists(target);n++)target=Path.Combine(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file)+"-audio-"+n+".m4a");
            var args=new List<string>{"-v","error","-nostdin","-y","-protocol_whitelist","file","-format_whitelist",formats,"-i",file,"-map","0:"+audio.index,"-vn","-map_metadata","-1","-c:a",audio.codec_name=="aac"||audio.codec_name=="alac"?"copy":"aac","-f","mp4",part};
            int code=await DownloadTools.Run(ffmpeg,args,line=>{},line=>{},token).ConfigureAwait(false);token.ThrowIfCancellationRequested();
            if(code!=0||!File.Exists(part)||new FileInfo(part).Length==0)throw new InvalidDataException("音频导出未完成，已保留下载的媒体文件。");
            File.Move(part,target);File.Delete(file);return target;
        }
        public static async Task<string> Mux(string video,string audio,string folder,string title,string ffmpeg,string ffprobe,CancellationToken token){
            if(ffmpeg==null||ffprobe==null)throw new InvalidOperationException("站点音画合并需要 FFmpeg / FFprobe，请先安装媒体组件。");
            string part=Path.Combine(folder,"site-merge.part"),target=Path.Combine(folder,DirectDownloads.SafeName(title+".mp4","video.mp4"));
            var args=new List<string>{"-hide_banner","-v","error","-nostdin","-y","-protocol_whitelist","file","-format_whitelist","mov,matroska,webm,mpegts,aac,mp3","-i",video,"-protocol_whitelist","file","-format_whitelist","mov,matroska,webm,mpegts,aac,mp3","-i",audio,"-map","0:v:0","-map","1:a:0","-c","copy","-map_metadata","-1","-movflags","+faststart","-f","mp4",part};
            int code=await DownloadTools.Run(ffmpeg,args,line=>{},line=>{},token);token.ThrowIfCancellationRequested();if(code!=0||!File.Exists(part)||new FileInfo(part).Length==0)throw new InvalidDataException("站点音画合并失败，已保留下载片段，可重试。");
            var check=await Inspect(ffprobe,new List<string>{"-v","error","-protocol_whitelist","file","-show_entries","stream=index,codec_type","-of","json","-i",part},token);if(!check.streams.Any(s=>s.codec_type=="video")||!check.streams.Any(s=>s.codec_type=="audio"))throw new InvalidDataException("合并文件缺少视频或音轨，请重新下载。");
            for(int n=1;File.Exists(target);n++)target=Path.Combine(folder,DirectDownloads.SafeName(title+"-"+n+".mp4","video.mp4"));File.Move(part,target);return target;
        }
        public static async Task<string> Download(string url,StreamMedia media,string ffmpeg,string ffprobe,string folder,Action<long,double,double> progress,CancellationToken token,string referer=null){
            Directory.CreateDirectory(folder);string part=Path.Combine(folder,"stream-media.part");var args=NetworkArguments(media.kind,referer);
            args.InsertRange(0,new[]{"-nostdin","-y","-xerror"});args.AddRange(new[]{"-i",url});
            if(media.videoIndex>=0)args.AddRange(new[]{"-map","0:"+media.videoIndex});if(media.audioIndex>=0)args.AddRange(new[]{"-map","0:"+media.audioIndex});
            args.AddRange(new[]{"-c","copy","-map_metadata","-1","-movflags","+faststart","-f","mp4","-progress","pipe:1","-stats_period","0.5","-nostats",part});
            long received=0;double seconds=0;var watch=Stopwatch.StartNew();bool errors=false;
            int code=await DownloadTools.Run(ffmpeg,args,line=>{int at=line.IndexOf('=');if(at<0)return;string key=line.Substring(0,at),value=line.Substring(at+1);long bytes;if(key=="total_size"&&Int64.TryParse(value,out bytes))received=Math.Max(0,bytes);if(key=="out_time_us")seconds=Number(value)/1000000;if(key=="progress")progress(received,seconds,received/Math.Max(.001,watch.Elapsed.TotalSeconds));},line=>{if(!String.IsNullOrWhiteSpace(line))errors=true;},token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();if(code!=0||errors||!File.Exists(part)||new FileInfo(part).Length==0)throw new InvalidDataException("分段下载未完成，请检查链接或网络后重试。");
            var check=await Inspect(ffprobe,new List<string>{"-v","error","-show_entries","format=duration:stream=index,codec_type","-of","json","-i",part},token).ConfigureAwait(false);
            double actual=check.format==null?0:Number(check.format.duration);if(media.duration>0&&actual+2<media.duration)throw new InvalidDataException("分段下载不完整，可以重新下载。");
            string file=Path.Combine(folder,media.title),stem=Path.GetFileNameWithoutExtension(media.title),extension=Path.GetExtension(media.title);for(int n=1;File.Exists(file);n++)file=Path.Combine(folder,stem+" ("+n+")"+extension);File.Move(part,file);progress(new FileInfo(file).Length,actual,0);return file;
        }
    }
}
