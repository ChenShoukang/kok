using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    public class MediaEditClip {public string id,source;public Dictionary<string,object> edit=new Dictionary<string,object>();}
    public static class MediaSequence {
        const string LocalFormats="mov,mp4,m4a,3gp,3g2,mj2,matroska,webm,mp3,wav,flac,ogg,aac,asf,avi,mpegts,mpeg,mpegvideo,flv,aiff,ape,wv";
        static string N(double value){return value.ToString("0.######",CultureInfo.InvariantCulture);}
        public static async Task<StreamProbe> Probe(string executable,string source,CancellationToken token){
            if(String.IsNullOrEmpty(executable))throw new InvalidOperationException("请先配置本地媒体组件。");var text=new StringBuilder();int code=await DownloadTools.Run(executable,new[]{"-v","error","-protocol_whitelist","file,pipe","-format_whitelist",LocalFormats,"-i",source,"-show_entries","format=duration:stream=codec_type,width,height","-of","json"},line=>{if(text.Length<1024*1024)text.AppendLine(line);},line=>{},token);
            if(code!=0)throw new InvalidDataException("无法读取媒体信息。");return Library.Json.Deserialize<StreamProbe>(text.ToString());
        }
        public static async Task Export(string encoder,string probe,List<MediaEditClip> clips,string destination,string kind,CancellationToken token,Action<double> progress){
            if(clips==null||clips.Count<1||clips.Count>50)throw new InvalidDataException("每个项目支持 1 至 50 个片段。");
            if(clips.Count==1){await CreativeEdits.ExportMedia(encoder,clips[0].source,destination,kind,clips[0].edit,token,progress);return;}
            var details=new Dictionary<string,StreamProbe>();foreach(var clip in clips){CreativeEdits.MediaArguments(clip.source,"check"+Path.GetExtension(destination),kind,clip.edit);if(!details.ContainsKey(clip.source))details[clip.source]=await Probe(probe,clip.source,token);}
            var first=details[clips[0].source].streams.FirstOrDefault(s=>s.codec_type=="video");int width=first==null?1280:first.width,height=first==null?720:first.height;
            if((int)CreativeEdits.Number(clips[0].edit,"rotation",0,0,270)%180!=0){int swap=width;width=height;height=swap;}double scale=Math.Min(1,Math.Min((width>=height?1920.0:1080)/Math.Max(1,width),(width>=height?1080.0:1920)/Math.Max(1,height)));width=Math.Max(16,(int)(width*scale)/2*2);height=Math.Max(16,(int)(height*scale)/2*2);
            string root=Path.GetDirectoryName(destination),stamp="kok-sequence-"+Guid.NewGuid().ToString("N");var temporary=new List<string>();
            try{
                var segments=new List<string>();for(int index=0;index<clips.Count;index++){
                    var clip=clips[index];double start=CreativeEdits.Number(clip.edit,"start",0,0,86400),end=CreativeEdits.Number(clip.edit,"end",0,0,86400),rate=CreativeEdits.Number(clip.edit,"rate",1,.5,2),duration=(end-start)/rate,volume=CreativeEdits.Number(clip.edit,"volume",1,0,2);
                    double sourceDuration;Double.TryParse(details[clip.source].format.duration,NumberStyles.Float,CultureInfo.InvariantCulture,out sourceDuration);if(sourceDuration>0&&(start>=sourceDuration||end>sourceDuration+.25))throw new InvalidDataException("片段范围超过原素材时长。");
                    bool hasAudio=details[clip.source].streams.Any(s=>s.codec_type=="audio"),mute=clip.edit.ContainsKey("mute")&&Convert.ToBoolean(clip.edit["mute"]);
                    string segment=Path.Combine(root,stamp+"-"+index+(kind=="video"?".mp4":".wav"));temporary.Add(segment);segments.Add(segment);
                    var args=new List<string>{"-hide_banner","-nostdin","-v","error","-protocol_whitelist","file,pipe","-format_whitelist",LocalFormats,"-ss",N(start),"-t",N(end-start),"-i",clip.source};
                    if(kind=="video"&&!hasAudio)args.AddRange(new[]{"-f","lavfi","-t",N(duration),"-i","anullsrc=r=48000:cl=stereo"});
                    var af=new List<string>{"atempo="+N(hasAudio?rate:1),"volume="+N(mute?0:volume),"aresample=48000","aformat=channel_layouts=stereo","asetpts=PTS-STARTPTS"};
                    double fadeIn=CreativeEdits.Number(clip.edit,"fadeIn",0,0,5),fadeOut=CreativeEdits.Number(clip.edit,"fadeOut",0,0,5);if(fadeIn>0)af.Add("afade=t=in:st=0:d="+N(Math.Min(duration/2,fadeIn)));if(fadeOut>0){double fade=Math.Min(duration/2,fadeOut);af.Add("afade=t=out:st="+N(duration-fade)+":d="+N(fade));}
                    if(kind=="video"){
                        var vf=new List<string>{"setpts=(PTS-STARTPTS)/"+N(rate)};int rotation=(int)CreativeEdits.Number(clip.edit,"rotation",0,0,270);if(rotation==90)vf.Add("transpose=1");if(rotation==180)vf.AddRange(new[]{"hflip","vflip"});if(rotation==270)vf.Add("transpose=2");vf.Add("scale="+width+":"+height+":force_original_aspect_ratio=decrease");vf.Add("pad="+width+":"+height+":(ow-iw)/2:(oh-ih)/2");vf.Add("setsar=1");vf.Add("fps=30");
                        string caption=CreativeEdits.CaptionFilter(clip.edit,root,temporary);if(caption!=null)vf.Add(caption);args.AddRange(new[]{"-map","0:v:0","-map",hasAudio?"0:a:0":"1:a:0","-vf",String.Join(",",vf),"-af",String.Join(",",af),"-c:v","libx264","-preset","veryfast","-crf","20","-pix_fmt","yuv420p","-c:a","aac","-ar","48000","-ac","2","-b:a","192k","-t",N(duration)});
                    }else{if(!hasAudio)throw new InvalidDataException("素材没有音轨。");args.AddRange(new[]{"-map","0:a:0","-vn","-af",String.Join(",",af),"-c:a","pcm_s16le","-ar","48000","-ac","2"});}
                    args.AddRange(new[]{"-n",segment});int code=await DownloadTools.Run(encoder,args,line=>{},line=>{},token,root);if(code!=0||!File.Exists(segment))throw new InvalidDataException("导出失败，请检查媒体格式或调整参数。");progress((index+1)*85.0/clips.Count);
                }
                string list=Path.Combine(root,stamp+".txt");temporary.Add(list);File.WriteAllLines(list,segments.Select(p=>"file '"+Path.GetFileName(p)+"'"),new UTF8Encoding(false));string output=Path.Combine(root,stamp+Path.GetExtension(destination));temporary.Add(output);
                var concat=new List<string>{"-hide_banner","-nostdin","-v","error","-protocol_whitelist","file,pipe","-f","concat","-safe","1","-i",list};
                if(kind=="video")concat.AddRange(new[]{"-c","copy","-movflags","+faststart"});else if(Path.GetExtension(destination).Equals(".wav",StringComparison.OrdinalIgnoreCase))concat.AddRange(new[]{"-c:a","pcm_s16le"});else if(Path.GetExtension(destination).Equals(".mp3",StringComparison.OrdinalIgnoreCase))concat.AddRange(new[]{"-c:a","libmp3lame","-b:a","192k"});else concat.AddRange(new[]{"-c:a","aac","-b:a","192k"});concat.AddRange(new[]{"-n",output});
                int finalCode=await DownloadTools.Run(encoder,concat,line=>{},line=>{},token);token.ThrowIfCancellationRequested();if(finalCode!=0||!File.Exists(output)||new FileInfo(output).Length<44)throw new InvalidDataException("导出失败，请检查媒体格式或调整参数。");if(File.Exists(destination))File.Replace(output,destination,null);else File.Move(output,destination);progress(100);
            }finally{foreach(string file in temporary)if(File.Exists(file))File.Delete(file);}
        }
    }
}
