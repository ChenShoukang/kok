using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace kok {
    public class SitePart {public string source,file;public long size;}
    public static class SiteTransfer {
        static async Task<string> Part(string url,string folder,string referer,string name,Action<long,long> progress,CancellationToken token){
            Directory.CreateDirectory(folder);string record=Path.Combine(folder,"complete-part.json");SitePart saved=null;
            if(File.Exists(record))try{saved=Library.Json.Deserialize<SitePart>(File.ReadAllText(record,Encoding.UTF8));}catch{}
            if(saved!=null&&saved.source==SiteAdapters.Hash(url)&&DownloadManager.IsInside(saved.file,folder)&&File.Exists(saved.file)&&new FileInfo(saved.file).Length==saved.size){progress(saved.size,saved.size);return saved.file;}
            string file=await DirectDownloads.Download(url,folder,(info,notice)=>{},(received,total,speed)=>progress(received,total),token,referer,name);
            File.WriteAllText(record,Library.Json.Serialize(new SitePart{source=SiteAdapters.Hash(url),file=file,size=new FileInfo(file).Length}),new UTF8Encoding(false));return file;
        }
        public static async Task<string> Pair(MediaCandidate source,string folder,string title,string ffmpeg,string ffprobe,Action<long,long,double> progress,CancellationToken token){
            if(ffmpeg==null||ffprobe==null)throw new InvalidOperationException("站点音画合并需要 FFmpeg / FFprobe，请先安装媒体组件。");
            long videoReceived=0,videoTotal=0,audioReceived=0,audioTotal=0;var watch=Stopwatch.StartNew();
            Action update=()=>progress(videoReceived+audioReceived,videoTotal>0&&audioTotal>0?videoTotal+audioTotal:0,(videoReceived+audioReceived)/Math.Max(.001,watch.Elapsed.TotalSeconds));
            string video=await Part(source.url,Path.Combine(folder,"video-track"),source.referer,"video.mp4",(received,total)=>{videoReceived=received;videoTotal=total;update();},token);
            string audio=await Part(source.audioUrl,Path.Combine(folder,"audio-track"),source.referer,"audio.m4a",(received,total)=>{audioReceived=received;audioTotal=total;update();},token);
            string target=await StreamDownloads.Mux(video,audio,folder,title,ffmpeg,ffprobe,token);
            File.Delete(video);File.Delete(audio);return target;
        }
    }
}
