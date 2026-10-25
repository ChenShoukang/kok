using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace kok {
    public static class YoutubeSite {
        sealed class Reporter : IProgress<double> {readonly Action<double> report;public Reporter(Action<double> report){this.report=report;}public void Report(double value){report(value);}}
        static YoutubeClient Client(System.Net.Http.HttpClient http){return new YoutubeClient(http,DownloadNetwork.Cookies("youtube").GetCookies(new Uri("https://www.youtube.com/")).Cast<Cookie>().ToArray());}
        static string StreamKey(IStreamInfo stream){var match=System.Text.RegularExpressions.Regex.Match(stream.Url,@"(?:[?&])itag=(\d+)");return match.Success?match.Groups[1].Value:stream.Container+"/"+stream.Bitrate.BitsPerSecond;}
        public static async Task<MediaPage> Resolve(string input,CancellationToken token){
            try{using(var http=DownloadNetwork.Client("https://www.youtube.com/",true))using(var youtube=Client(http)){
                    return await ResolveClient(youtube,input,token);
                }}catch(InvalidDataException){throw;}catch(OperationCanceledException){throw;}catch(Exception){throw new InvalidDataException("YouTube 解析失败，请检查网络或保存站点登录信息；部分视频受站点风控或权限限制。");}
        }
        internal static async Task<MediaPage> ResolveClient(YoutubeClient youtube,string input,CancellationToken token){
                    var video=await youtube.Videos.GetAsync(input,token);if(video.Duration==null)throw new InvalidDataException("暂不下载正在直播的内容。");
                    var manifest=await youtube.Videos.Streams.GetManifestAsync(video.Id,token);var page=new MediaPage{url=input,provider="youtube",fromPage=true,title=video.Title};
                    var audios=manifest.GetAudioOnlyStreams().OrderByDescending(a=>a.IsAudioLanguageDefault==true).ThenByDescending(a=>a.Bitrate.BitsPerSecond).ToList();
                    foreach(var stream in manifest.GetVideoOnlyStreams().OrderByDescending(v=>v.VideoCodec.StartsWith("avc",StringComparison.OrdinalIgnoreCase))){
                        var audio=audios.FirstOrDefault(a=>a.Container==stream.Container)??audios.FirstOrDefault();if(audio==null)continue;
                        var source=SiteAdapters.Candidate(input,stream.Url,"video",stream.VideoQuality.Label+" · "+stream.Container,stream.VideoResolution.Height,video.Id+"/"+StreamKey(stream)+"/"+StreamKey(audio));if(source==null)continue;source.provider="youtube";source.videoStream=stream;source.audioStream=audio;source.duration=video.Duration.Value.TotalSeconds;source.bytes=stream.Size.Bytes+audio.Size.Bytes;page.candidates.Add(source);
                    }
                    foreach(var stream in manifest.GetMuxedStreams()){var source=SiteAdapters.Candidate(input,stream.Url,"video",stream.VideoQuality.Label+" · "+stream.Container,stream.VideoResolution.Height,video.Id+"/combined/"+StreamKey(stream));if(source==null)continue;source.provider="youtube";source.muxedStream=stream;source.duration=video.Duration.Value.TotalSeconds;source.bytes=stream.Size.Bytes;page.candidates.Add(source);}
                    foreach(var stream in audios){var source=SiteAdapters.Candidate(input,stream.Url,"music","Audio · "+stream.Container,0,video.Id+"/audio/"+StreamKey(stream));if(source==null)continue;source.provider="youtube";source.audioStream=stream;source.duration=video.Duration.Value.TotalSeconds;source.bytes=stream.Size.Bytes;page.candidates.Add(source);}
                    try{var captions=await youtube.Videos.ClosedCaptions.GetManifestAsync(video.Id,token);foreach(var track in captions.Tracks.Take(20))page.captions.Add(new MediaCaption{url=track.Url,language=track.Language.Code,format="youtube"});}catch(OperationCanceledException){throw;}catch(Exception){}
                    if(page.candidates.Count==0)throw new InvalidDataException("YouTube 没有返回可下载的音视频轨道。");return page;
        }
        public static async Task<string> Download(MediaCandidate source,string folder,string title,string ffmpeg,string ffprobe,Action<long,long,double> progress,CancellationToken token,bool audioOnly=false){
            if(ffmpeg==null||ffprobe==null)throw new InvalidOperationException("站点音画合并需要 FFmpeg / FFprobe，请先安装媒体组件。");Directory.CreateDirectory(folder);
            try{using(var http=DownloadNetwork.Client("https://www.youtube.com/",true))using(var youtube=Client(http)){
                    var combined=source.muxedStream as IStreamInfo;if(combined!=null){string combinedFile=Path.Combine(folder,"youtube-combined."+combined.Container);await youtube.Videos.Streams.DownloadAsync(combined,combinedFile,new Reporter(p=>progress((long)(combined.Size.Bytes*p),combined.Size.Bytes,0)),token);if(audioOnly)return await StreamDownloads.ExtractAudio(combinedFile,ffmpeg,ffprobe,token);string combinedTarget=Path.Combine(folder,DirectDownloads.SafeName(title+"."+combined.Container,"video.mp4"));for(int n=1;File.Exists(combinedTarget);n++)combinedTarget=Path.Combine(folder,DirectDownloads.SafeName(title+"-"+n+"."+combined.Container,"video.mp4"));File.Move(combinedFile,combinedTarget);return combinedTarget;}
                    var video=source.videoStream as IStreamInfo;var audio=source.audioStream as IStreamInfo;if(audio==null)throw new InvalidDataException("站点没有返回可用音轨。");
                    string audioFile=Path.Combine(folder,"youtube-audio."+audio.Container),videoFile=video==null?null:Path.Combine(folder,"youtube-video."+video.Container);long videoBytes=video==null?0:video.Size.Bytes,audioBytes=audio.Size.Bytes;var watch=System.Diagnostics.Stopwatch.StartNew();
                    if(video!=null)await youtube.Videos.Streams.DownloadAsync(video,videoFile,new Reporter(p=>{long received=(long)(videoBytes*p);progress(received,videoBytes+audioBytes,received/Math.Max(.001,watch.Elapsed.TotalSeconds));}),token);
                    await youtube.Videos.Streams.DownloadAsync(audio,audioFile,new Reporter(p=>{long received=videoBytes+(long)(audioBytes*p);progress(received,videoBytes+audioBytes,received/Math.Max(.001,watch.Elapsed.TotalSeconds));}),token);
                    token.ThrowIfCancellationRequested();if(videoFile!=null){string merged=await StreamDownloads.Mux(videoFile,audioFile,folder,title,ffmpeg,ffprobe,token);File.Delete(videoFile);File.Delete(audioFile);return merged;}
                    string target=Path.Combine(folder,DirectDownloads.SafeName(title+"."+(audio.Container.ToString()=="mp4"?"m4a":audio.Container.ToString()),"audio.m4a"));for(int n=1;File.Exists(target);n++)target=Path.Combine(folder,DirectDownloads.SafeName(title+"-"+n+Path.GetExtension(target),"audio.m4a"));File.Move(audioFile,target);return target;
                }}catch(InvalidDataException){throw;}catch(OperationCanceledException){throw;}catch(Exception){throw new InvalidDataException("YouTube 下载未完成，请检查网络、登录状态或稍后重试。");}
        }
        public static async Task SaveCaptions(MediaPage page,string file,CancellationToken token){
            using(var http=DownloadNetwork.Client("https://www.youtube.com/",true))using(var youtube=Client(http)){
                var manifest=await youtube.Videos.ClosedCaptions.GetManifestAsync(page.url,token);int number=0;foreach(var caption in page.captions.Where(c=>c.format=="youtube")){var track=manifest.Tracks.FirstOrDefault(t=>t.Language.Code==caption.language);if(track==null)continue;string language=System.Text.RegularExpressions.Regex.Replace(caption.language??"sub",@"[^a-zA-Z0-9_-]","");string target=Path.Combine(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file)+"."+language+"."+(++number)+".srt");if(File.Exists(target))continue;string temp=target+".part";await youtube.Videos.ClosedCaptions.DownloadAsync(track,temp,null,token);token.ThrowIfCancellationRequested();File.Move(temp,target);}
            }
        }
    }
}
