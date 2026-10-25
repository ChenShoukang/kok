using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    public class VideoDownload {
        public string id,url,title,quality,status="queued",error="",folder="",created,backend="native",notice="",streamKind="",selection="",browserToken="";
        public bool subtitles,imported,sourceIsAudio,mediaFinished;
        public List<MediaCaption> captions=new List<MediaCaption>();
        public double progress,speed,duration,position;
        public long received,total;
        public List<string> files=new List<string>();
        internal CancellationTokenSource cancellation;
        internal bool pauseRequested;
    }
    public class DownloadStore {public string folder,ffmpegFolder="";public List<VideoDownload> jobs=new List<VideoDownload>();}
    public sealed class DownloadManager : IDisposable {
        readonly object gate=new object();
        readonly string stateFile;
        readonly SemaphoreSlim serial=new SemaphoreSlim(1,1);
        readonly DownloadTools tools;
        readonly DownloadSessions sessions;
        readonly Dictionary<string,Tuple<DateTime,MediaPage>> browserPages=new Dictionary<string,Tuple<DateTime,MediaPage>>();
        DownloadStore store;
        bool disposed;
        public Action<VideoDownload> Completed;
        public DownloadManager(string dataDir,string appDir){
            sessions=new DownloadSessions(dataDir);tools=new DownloadTools(dataDir,appDir);stateFile=Path.Combine(dataDir,"downloads.json");
            if(File.Exists(stateFile))try{store=Library.Json.Deserialize<DownloadStore>(File.ReadAllText(stateFile,Encoding.UTF8));}catch{File.Copy(stateFile,stateFile+".broken-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"),true);}
            if(store==null)store=new DownloadStore();if(store.jobs==null)store.jobs=new List<VideoDownload>();
            if(String.IsNullOrWhiteSpace(store.folder))store.folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),"kok Downloads");
            store.jobs=store.jobs.Where(j=>j!=null&&j.id!=null&&j.id.Length==32).Take(100).ToList();
            foreach(var job in store.jobs){if(job.files==null)job.files=new List<string>();if(job.captions==null)job.captions=new List<MediaCaption>();job.backend="native";if(!new[]{"best","original","1080","720","480","audio"}.Contains(job.quality))job.quality="best";if(!new[]{"completed","error","cancelled","paused"}.Contains(job.status))job.status="paused";}
        }
        public DownloadTools Tools {get{return tools;}}
        public DownloadSessions Sessions {get{return sessions;}}
        public static string ValidateUrl(string input){
            Uri uri;string value=(input??"").Trim();if(value.Length>4096||value.Any(Char.IsControl)||!Uri.TryCreate(value,UriKind.Absolute,out uri)||!new[]{"http","https"}.Contains(uri.Scheme)||!String.IsNullOrEmpty(uri.UserInfo)||String.IsNullOrEmpty(uri.Host))throw new InvalidDataException("请输入有效的 HTTP 或 HTTPS 视频链接。");return uri.AbsoluteUri;
        }
        public static bool IsInside(string file,string folder){return Path.GetFullPath(file).StartsWith(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
        static string FindExecutable(string name){foreach(var folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))try{string candidate=Path.Combine(folder.Trim('"'),name);if(Path.IsPathRooted(candidate)&&File.Exists(candidate))return Path.GetFullPath(candidate);}catch(ArgumentException){}return null;}
        string Ffmpeg(){string manual=null;if(!String.IsNullOrEmpty(store.ffmpegFolder)){string file=Path.Combine(store.ffmpegFolder,"ffmpeg.exe");if(File.Exists(file)){if(File.Exists(Path.Combine(store.ffmpegFolder,"ffprobe.exe")))return file;manual=file;}}return tools.Ffmpeg??manual??FindExecutable("ffmpeg.exe");}
        string Ffprobe(){string engine=Ffmpeg();if(engine==null)return null;if(String.Equals(engine,tools.Ffmpeg,StringComparison.OrdinalIgnoreCase))return tools.Ffprobe;string probe=Path.Combine(Path.GetDirectoryName(engine),"ffprobe.exe");return File.Exists(probe)?probe:null;}
        void Save(){
            string temp=stateFile+".tmp";File.WriteAllText(temp,Library.Json.Serialize(store),new UTF8Encoding(false));if(File.Exists(stateFile))File.Replace(temp,stateFile,stateFile+".bak");else File.Move(temp,stateFile);
        }
        public object Snapshot(){lock(gate){return new{sites=sessions.Status(),tools=tools.Status(),folder=store.folder,ffmpegAvailable=Ffmpeg()!=null,ffprobeAvailable=Ffprobe()!=null,jobs=store.jobs.Select(j=>new{id=j.id,title=j.title,source=Source(j.url),quality=j.quality,backend=j.backend,notice=j.notice,streamKind=j.streamKind,duration=j.duration,position=j.position,sourceIsAudio=j.sourceIsAudio,status=j.status,error=j.error,progress=j.progress,speed=j.speed,received=j.received,total=j.total,imported=j.imported,hasFile=j.files.Any(File.Exists),created=j.created}).ToArray()};}}
        static string Source(string value){Uri uri;return Uri.TryCreate(value,UriKind.Absolute,out uri)?uri.Host:"";}
        public void SetFolder(string folder){lock(gate){store.folder=Path.GetFullPath(folder);Save();}}
        public void SetFfmpeg(string folder){if(!File.Exists(Path.Combine(folder,"ffmpeg.exe"))||!File.Exists(Path.Combine(folder,"ffprobe.exe")))throw new InvalidDataException("所选文件夹需要同时包含 ffmpeg.exe 和 ffprobe.exe。");lock(gate){store.ffmpegFolder=Path.GetFullPath(folder);Save();}}
        public static string ValidateQuality(string quality){if(!new[]{"best","original","1080","720","480","audio"}.Contains(quality))throw new InvalidDataException("请选择有效的下载清晰度。");return quality;}
        async Task<object> Inspect(MediaCandidate source,string quality,CancellationToken token){
            if(source.provider=="youtube"||!String.IsNullOrEmpty(source.audioUrl))return new{backend="native",title=source.label,filename=source.label,source=Source(source.url),duration=source.duration,kind=quality=="audio"?"music":source.kind,streamKind="",bytes=source.bytes,resumeSupported=source.provider!="youtube",requiresMerge=!String.IsNullOrEmpty(source.audioUrl)||source.videoStream!=null};
            string kind;try{return await DirectDownloads.Probe(source.url,token,source.referer,source.filenameHint);}catch(StreamLinkException manifest){kind=manifest.Kind;}
            var media=await StreamDownloads.Probe(source.url,kind,Ffmpeg(),Ffprobe(),token,quality,source.referer);return new{backend="native",title=media.title,filename=media.title,source=Source(source.url),duration=media.duration,kind=media.audio?"music":"video",streamKind=media.kind,resumeSupported=false};
        }
        public async Task<object> Probe(string input,CancellationToken token,string quality="best",string selection=""){using(DownloadNetwork.Use(sessions))return await ProbeCore(input,token,quality,selection);}
        async Task<object> ProbeCore(string input,CancellationToken token,string quality,string selection){
            ValidateQuality(quality);string url=MediaResolver.NormalizeInput(input);var page=await MediaResolver.Resolve(url,token);var chosen=MediaResolver.Select(page,quality,selection);
            var info=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(await Inspect(chosen,quality,token)));
            info["provider"]=page.provider;info["url"]=url;info["fromPage"]=page.fromPage;if(page.title!="")info["title"]=page.title;
            info["selected"]=chosen.id;info["captionCount"]=page.captions.Count;info["sources"]=page.candidates.Select(c=>new{id=c.id,label=c.label,kind=c.kind,height=c.height}).ToArray();return info;
        }
        MediaPage BrowserPage(string key,string url){lock(gate){Tuple<DateTime,MediaPage> cached;if(!browserPages.TryGetValue(key,out cached)||cached.Item1<DateTime.UtcNow||cached.Item2.url!=url)throw new InvalidDataException("网页辅助解析已过期，请重新解析后添加下载。");return cached.Item2;}}
        public async Task<object> RegisterBrowserPage(MediaPage page,string quality,CancellationToken token){using(DownloadNetwork.Use(sessions)){ValidateQuality(quality);var chosen=MediaResolver.Select(page,quality,"");var info=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(await Inspect(chosen,quality,token)));string key=Guid.NewGuid().ToString("N");lock(gate){foreach(string expired in browserPages.Where(p=>p.Value.Item1<DateTime.UtcNow).Select(p=>p.Key).ToArray())browserPages.Remove(expired);if(browserPages.Count>=20)browserPages.Remove(browserPages.Keys.First());browserPages[key]=Tuple.Create(DateTime.UtcNow.AddMinutes(10),page);}info["browserToken"]=key;info["title"]=page.title;info["url"]=page.url;info["fromPage"]=true;info["selected"]=chosen.id;info["captionCount"]=page.captions.Count;info["sources"]=page.candidates.Select(c=>new{id=c.id,label=c.label,kind=c.kind,height=c.height}).ToArray();return info;}}
        public void Start(IEnumerable<string> inputs,string quality,bool subtitles,string selection="",string browserToken=""){
            ValidateQuality(quality);if(!String.IsNullOrEmpty(selection)&&!System.Text.RegularExpressions.Regex.IsMatch(selection,"^[a-f0-9]{16}$"))throw new InvalidDataException("请选择有效的媒体资源。");
            var urls=inputs.Select(MediaResolver.NormalizeInput).Distinct(StringComparer.Ordinal).ToArray();if(urls.Length<1||urls.Length>20)throw new InvalidDataException("一次可以添加 1 至 20 个视频链接。");
            lock(gate){if(disposed)throw new ObjectDisposedException("downloads");if(store.jobs.Count+urls.Length>100)throw new InvalidOperationException("下载历史已满，请先清理已结束的任务。");
                foreach(string url in urls){var job=new VideoDownload{id=Guid.NewGuid().ToString("N"),url=url,title=Source(url),quality=quality,selection=urls.Length==1?selection:"",browserToken=urls.Length==1?browserToken:"",subtitles=subtitles,created=DateTime.UtcNow.ToString("o")};job.folder=Path.Combine(store.folder,"kok-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+job.id.Substring(0,6));store.jobs.Insert(0,job);Schedule(job);}Save();}
        }
        void Schedule(VideoDownload job){job.cancellation=new CancellationTokenSource();job.pauseRequested=false;job.status="queued";job.error="";job.speed=0;var token=job.cancellation.Token;var slot=serial.WaitAsync(token);Task.Run(()=>Run(job,token,slot));}
        public static void RecordProgress(VideoDownload job,long received,long total,double speed){job.received=Math.Max(0,received);job.total=Math.Max(0,total);job.speed=Math.Max(0,speed);job.progress=job.total>0?Math.Min(100,job.received*100.0/job.total):0;}
        async Task Run(VideoDownload job,CancellationToken token,Task slot){using(DownloadNetwork.Use(sessions))await RunCore(job,token,slot);}
        async Task RunCore(VideoDownload job,CancellationToken token,Task slot){
            bool entered=false;
            try{
                await slot;entered=true;token.ThrowIfCancellationRequested();lock(gate){job.status="downloading";Save();}
                string file=null;MediaPage page=null;
                lock(gate){if(job.mediaFinished&&job.files.Count==1&&IsInside(job.files[0],job.folder)&&File.Exists(job.files[0])){file=job.files[0];page=new MediaPage{url=job.url,captions=job.captions};}}
                if(file==null){
                    page=String.IsNullOrEmpty(job.browserToken)?await MediaResolver.Resolve(job.url,token):BrowserPage(job.browserToken,job.url);var source=MediaResolver.Select(page,job.quality,job.selection);string streamKind=null;
                    if(job.quality=="audio"&&!String.IsNullOrEmpty(source.audioUrl)){source.url=source.audioUrl;source.audioUrl="";source.kind="music";source.filenameHint="audio.m4a";}
                    if(job.quality=="audio"&&source.kind!="music"&&(Ffmpeg()==null||Ffprobe()==null))throw new InvalidOperationException("仅音频导出需要 FFmpeg / FFprobe，请先安装媒体组件。");
                    if(source.provider=="youtube"){
                        lock(gate){job.title=page.title;job.sourceIsAudio=source.kind=="music"||job.quality=="audio";job.notice="YouTube 任务由站点解析库处理，暂停后恢复会重新下载。";Save();}
                        if(job.quality=="audio")source.videoStream=null;
                        file=await YoutubeSite.Download(source,job.folder,page.title,Ffmpeg(),Ffprobe(),(received,total,speed)=>{lock(gate)RecordProgress(job,received,total,speed);},token,job.quality=="audio");
                    }else if(!String.IsNullOrEmpty(source.audioUrl)&&job.quality!="audio"){
                        lock(gate){job.title=page.title;job.sourceIsAudio=false;job.notice="正在分别下载视频和音轨，完成后自动合并。";Save();}
                        file=await SiteTransfer.Pair(source,job.folder,page.title,Ffmpeg(),Ffprobe(),(received,total,speed)=>{lock(gate)RecordProgress(job,received,total,speed);},token);
                    }else{
                    try{file=await DirectDownloads.Download(source.url,job.folder,(info,notice)=>{lock(gate){job.title=page.title!=""?page.title:info.filename;job.sourceIsAudio=info.audio;job.total=info.total;job.notice=notice;job.streamKind="";Save();}},(received,total,speed)=>{lock(gate)RecordProgress(job,received,total,speed);},token,source.referer,source.filenameHint);}
                    catch(StreamLinkException manifest){streamKind=manifest.Kind;}
                    if(streamKind!=null){
                        var media=await StreamDownloads.Probe(source.url,streamKind,Ffmpeg(),Ffprobe(),token,job.quality,source.referer);if(page.title!="")media.title=DirectDownloads.SafeName(page.title+(media.audio?".m4a":".mp4"),media.title);
                        lock(gate){job.title=page.title!=""?page.title:media.title;job.sourceIsAudio=media.audio;job.streamKind=media.kind;job.duration=media.duration;job.position=0;job.total=0;job.received=0;job.progress=0;job.notice="分段视频由 FFmpeg 合并保存，暂停后恢复会重新下载。";Save();}
                        file=await StreamDownloads.Download(source.url,media,Ffmpeg(),Ffprobe(),job.folder,(received,position,speed)=>{lock(gate){job.received=received;job.position=position;job.speed=speed;job.progress=job.duration>0?Math.Min(99,position*100/job.duration):0;}},token,source.referer);
                    }
                    }
                    if(job.quality=="audio"&&!job.sourceIsAudio){lock(gate)job.status="processing";file=await StreamDownloads.ExtractAudio(file,Ffmpeg(),Ffprobe(),token);lock(gate)job.sourceIsAudio=true;}
                    if(page.title!=""&&job.streamKind==""){string name=DirectDownloads.SafeName(page.title+Path.GetExtension(file),Path.GetFileName(file)),target=Path.Combine(job.folder,name);for(int n=1;File.Exists(target)&&!String.Equals(target,file,StringComparison.OrdinalIgnoreCase);n++)target=Path.Combine(job.folder,Path.GetFileNameWithoutExtension(name)+" ("+n+")"+Path.GetExtension(name));if(!String.Equals(target,file,StringComparison.OrdinalIgnoreCase)){File.Move(file,target);file=target;}}
                    lock(gate){job.files=new List<string>{file};job.mediaFinished=true;job.captions=page.captions;Save();}
                }
                token.ThrowIfCancellationRequested();
                if(job.subtitles&&page.captions.Count>0){lock(gate)job.status="processing";try{await MediaResolver.SaveCaptions(page,file,token);}catch(OperationCanceledException){throw;}catch(Exception){lock(gate)job.notice="媒体已下载，部分字幕未能保存；可手动加载字幕。";}}
                lock(gate){job.files=job.files.Where(f=>IsInside(f,job.folder)&&File.Exists(f)&&Library.Classify(f)!=null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(job.files.Count==0)throw new InvalidDataException("未找到下载完成的媒体文件。");job.status="completed";job.progress=100;job.speed=0;Save();}
                if(!disposed&&Completed!=null)Completed(job);
            }catch(OperationCanceledException){lock(gate){job.status=job.pauseRequested?"paused":"cancelled";job.speed=0;if(!disposed)Save();}}
            catch(Exception error){lock(gate){job.status="error";job.error=error is InvalidOperationException||error is InvalidDataException?error.Message:"下载未完成，请检查链接、网络或保存位置后重试。";if(!disposed)Save();}}
            finally{lock(gate){if(job.cancellation!=null){job.cancellation.Dispose();job.cancellation=null;}}if(entered)serial.Release();}
        }
        VideoDownload Require(string id){var job=store.jobs.FirstOrDefault(j=>j.id==id);if(job==null)throw new InvalidDataException("下载任务不存在。");return job;}
        static void Cancel(CancellationTokenSource cancellation){if(cancellation!=null)try{cancellation.Cancel();}catch(ObjectDisposedException){}}
        public void Stop(string id,bool pause){CancellationTokenSource cancellation;lock(gate){var job=Require(id);if(new[]{"completed","cancelled","error","paused"}.Contains(job.status))return;job.pauseRequested=pause;job.status=pause?"pausing":"cancelling";cancellation=job.cancellation;Save();}Cancel(cancellation);}
        public void Resume(string id){lock(gate){var job=Require(id);if(!new[]{"paused","error","cancelled"}.Contains(job.status)||job.cancellation!=null)throw new InvalidOperationException("请等待当前下载任务停止后重试。");ValidateUrl(job.url);Schedule(job);Save();}}
        public void PauseAll(){CancellationTokenSource[] pending;lock(gate){var jobs=store.jobs.Where(j=>j.cancellation!=null).ToArray();foreach(var job in jobs)job.pauseRequested=true;pending=jobs.Select(j=>j.cancellation).ToArray();}foreach(var cancellation in pending)Cancel(cancellation);}
        public void ClearFinished(){lock(gate){store.jobs.RemoveAll(j=>new[]{"completed","cancelled","error"}.Contains(j.status)&&j.cancellation==null);Save();}}
        public string[] Files(string id){lock(gate){var job=Require(id);if(job.status!="completed")throw new InvalidOperationException("请等待下载完成。");return job.files.Where(f=>IsInside(f,job.folder)&&File.Exists(f)).ToArray();}}
        public string Quality(string id){lock(gate)return Require(id).quality;}
        public bool IsAudio(string id){lock(gate){var job=Require(id);return job.sourceIsAudio||job.quality=="audio";}}
        public void MarkImported(string id){lock(gate){Require(id).imported=true;Save();}}
        public void Dispose(){CancellationTokenSource[] pending;lock(gate){disposed=true;var jobs=store.jobs.Where(j=>j.cancellation!=null).ToArray();foreach(var job in jobs){job.pauseRequested=true;job.status="paused";}pending=jobs.Select(j=>j.cancellation).ToArray();Save();}foreach(var cancellation in pending)Cancel(cancellation);}
    }
    public partial class Library {
        public void ImportDownloaded(string[] files,bool audio){lock(gate){Import(files,null);if(audio)foreach(string file in files){var item=FindByPath(file);if(item!=null)item.kind="music";}Save();}}
        Entry FindByPath(string path){return Db.items.FirstOrDefault(i=>String.Equals(i.path,path,StringComparison.OrdinalIgnoreCase));}
    }
}
