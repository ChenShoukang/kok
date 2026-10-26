using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace kok {
    public static class CreativeEdits {
        public static double Number(Dictionary<string,object> args,string key,double fallback,double min,double max){double result=args.ContainsKey(key)?Convert.ToDouble(args[key],CultureInfo.InvariantCulture):fallback;if(Double.IsNaN(result)||Double.IsInfinity(result)||result<min||result>max)throw new InvalidDataException("编辑参数超出有效范围。");return result;}
        static string N(double n){return n.ToString("0.######",CultureInfo.InvariantCulture);}
        public static string CaptionFilter(Dictionary<string,object> edit,string directory,List<string> temporary){
            string text=edit.ContainsKey("caption")?Convert.ToString(edit["caption"]):"";if(String.IsNullOrWhiteSpace(text))return null;if(text.Length>2000)throw new InvalidDataException("字幕文字过长。");
            string name="kok-caption-"+Guid.NewGuid().ToString("N")+".txt",file=Path.Combine(directory,name);File.WriteAllText(file,text,new UTF8Encoding(false));temporary.Add(file);
            string font=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"msyh.ttc");string face=File.Exists(font)?"fontfile='"+font.Replace("\\","/").Replace(":","\\:")+"'":"font='sans-serif'";
            return "drawtext="+face+":textfile='"+name+"':expansion=none:fontsize=h*0.045:fontcolor=white:borderw=2:bordercolor=black@0.7:x=(w-text_w)/2:y=h-text_h-h*0.06";
        }
        public static List<string> MediaArguments(string source,string output,string kind,Dictionary<string,object> edit){
            double start=Number(edit,"start",0,0,86400),end=Number(edit,"end",0,0,86400),rate=Number(edit,"rate",1,.5,2),volume=Number(edit,"volume",1,0,2);
            if(end<=start||end-start<.05)throw new InvalidDataException("结束时间必须晚于开始时间。");
            if(edit.ContainsKey("caption")&&Convert.ToString(edit["caption"]).Length>2000)throw new InvalidDataException("字幕文字过长。");
            double duration=(end-start)/rate,fadeIn=Number(edit,"fadeIn",0,0,5),fadeOut=Number(edit,"fadeOut",0,0,5);
            var args=new List<string>{"-hide_banner","-nostdin","-v","error","-nostats","-progress","pipe:1","-protocol_whitelist","file,pipe","-ss",N(start),"-t",N(end-start),"-i",source};
            var audio=new List<string>{"atempo="+N(rate),"volume="+N(volume)};
            if(fadeIn>0)audio.Add("afade=t=in:st=0:d="+N(Math.Min(fadeIn,duration/2)));
            if(fadeOut>0){double fade=Math.Min(fadeOut,duration/2);audio.Add("afade=t=out:st="+N(Math.Max(0,duration-fade))+":d="+N(fade));}
            if(kind=="video"){
                args.AddRange(new[]{"-map","0:v:0","-map","0:a:0?"});var filters=new List<string>{"setpts=(PTS-STARTPTS)/"+N(rate)};
                int rotation=(int)Number(edit,"rotation",0,0,270);if(rotation%90!=0)throw new InvalidDataException("旋转角度无效。");if(rotation==90)filters.Add("transpose=1");if(rotation==180)filters.AddRange(new[]{"hflip","vflip"});if(rotation==270)filters.Add("transpose=2");
                filters.Add("pad=ceil(iw/2)*2:ceil(ih/2)*2");args.AddRange(new[]{"-vf",String.Join(",",filters),"-c:v","libx264","-preset","veryfast","-crf","20","-pix_fmt","yuv420p"});
                if(edit.ContainsKey("mute")&&Convert.ToBoolean(edit["mute"]))args.Add("-an");else args.AddRange(new[]{"-af",String.Join(",",audio),"-c:a","aac","-b:a","192k"});
                args.AddRange(new[]{"-movflags","+faststart"});
            }else if(kind=="music"){
                args.AddRange(new[]{"-map","0:a:0","-vn","-af",String.Join(",",audio)});
                string ext=Path.GetExtension(output).ToLowerInvariant();if(ext==".wav")args.AddRange(new[]{"-c:a","pcm_s16le"});else if(ext==".mp3")args.AddRange(new[]{"-c:a","libmp3lame","-b:a","192k"});else if(ext==".m4a")args.AddRange(new[]{"-c:a","aac","-b:a","192k"});else throw new InvalidDataException("请选择支持的导出格式。");
            }else throw new InvalidDataException("请选择音频或视频文件。");
            args.AddRange(new[]{"-map_metadata","0","-n",output});return args;
        }
        public static async Task ExportMedia(string encoder,string source,string destination,string kind,Dictionary<string,object> edit,CancellationToken token,Action<double> progress){
            if(String.IsNullOrEmpty(encoder))throw new InvalidOperationException("请先配置本地媒体组件。");
            string temp=Path.Combine(Path.GetDirectoryName(destination),".kok-edit-"+Guid.NewGuid().ToString("N")+Path.GetExtension(destination));
            var args=MediaArguments(source,temp,kind,edit);double duration=(Number(edit,"end",0,0,86400)-Number(edit,"start",0,0,86400))/Number(edit,"rate",1,.5,2);var temporary=new List<string>();
            try{if(kind=="video"){string caption=CaptionFilter(edit,Path.GetDirectoryName(temp),temporary);if(caption!=null){int at=args.IndexOf("-vf")+1;args[at]+=","+caption;}}
                int code=await DownloadTools.Run(encoder,args,line=>{if(line.StartsWith("out_time_ms=",StringComparison.Ordinal)){double micros;if(Double.TryParse(line.Substring(12),NumberStyles.Float,CultureInfo.InvariantCulture,out micros))progress(Math.Min(99,micros/1000000/duration*100));}},line=>{},token,Path.GetDirectoryName(temp));
                token.ThrowIfCancellationRequested();if(code!=0||!File.Exists(temp)||new FileInfo(temp).Length<44)throw new InvalidDataException("导出失败，请检查媒体格式或调整参数。");
                if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);progress(100);
            }finally{if(File.Exists(temp))File.Delete(temp);foreach(string file in temporary)if(File.Exists(file))File.Delete(file);}
        }
    }
    public partial class MainWindow {
        static readonly string[] CreativeActions={"saveCreativeDraft","creativeSource","probeCreativeMedia","chooseMergePdfs","exportEditedAsset","creativeMediaState","chooseCreativeEncoder","exportMediaEdit","cancelMediaEdit"};
        readonly Dictionary<string,CancellationTokenSource> creativeJobs=new Dictionary<string,CancellationTokenSource>();
        void CancelCreativeJobs(){foreach(var cancel in creativeJobs.Values.ToArray())cancel.Cancel();}
        string ChooseEditDestination(Form owner,Entry entry,string extension,string filter){
            string name=entry.name;foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
            nativeFileDialogOpen=true;try{using(var dialog=new SaveFileDialog{Title=L("另存编辑副本"),Filter=filter,FileName=name+"-edit"+extension,DefaultExt=extension.TrimStart('.'),AddExtension=true}){
                if(dialog.ShowDialog(owner)!=DialogResult.OK)return null;EnsureExportDestination(dialog.FileName);if(!String.Equals(Path.GetExtension(dialog.FileName),extension,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("请选择支持的导出格式。");return dialog.FileName;
            }}finally{nativeFileDialogOpen=false;}
        }
        async Task<object> HandleCreativeAction(string action,Dictionary<string,object> args,Form owner){
            if(action=="cancelMediaEdit"){CancellationTokenSource active;if(creativeJobs.TryGetValue(StringArg(args,"jobId"),out active))active.Cancel();return true;}
            if(action=="creativeMediaState")return new{available=downloads.LocalEncoder!=null};
            if(action=="chooseMergePdfs"){nativeFileDialogOpen=true;try{using(var dialog=new OpenFileDialog{Title=L("添加 PDF 页面"),Filter="PDF|*.pdf",Multiselect=true}){if(dialog.ShowDialog(owner)!=DialogResult.OK)return new object[0];if(dialog.FileNames.Length>10||dialog.FileNames.Any(p=>!String.Equals(Path.GetExtension(p),".pdf",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("每次最多添加 10 份 PDF。");library.Import(dialog.FileNames,null);Notify("libraryChanged",null);return library.Db.items.Where(i=>dialog.FileNames.Contains(i.path,StringComparer.OrdinalIgnoreCase)).Select(i=>new{id=i.id,name=i.name,size=i.size}).ToArray();}}finally{nativeFileDialogOpen=false;}}
            if(action=="chooseCreativeEncoder"){nativeFileDialogOpen=true;try{using(var dialog=new FolderBrowserDialog{Description=L("选择包含 ffmpeg.exe 的文件夹"),ShowNewFolderButton=false}){if(dialog.ShowDialog(owner)!=DialogResult.OK)return new{cancelled=true};downloads.SetFfmpeg(dialog.SelectedPath);return new{available=true};}}finally{nativeFileDialogOpen=false;}}
            var entry=RequireEntry(args);var target=library;
            if(action=="probeCreativeMedia"){using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20)))return await MediaSequence.Probe(downloads.LocalProbe,entry.path,timeout.Token);}
            if(action=="creativeSource"){var info=new FileInfo(entry.path);return new{signature=info.Length+"|"+info.LastWriteTimeUtc.Ticks};}
            if(action=="saveCreativeDraft"){
                var draft=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(args["draft"]));if(draft==null||Library.Json.Serialize(draft).Length>2*1024*1024)throw new InvalidDataException("编辑草稿过大。");entry.editDraft=draft;target.Save();return true;
            }
            if(action=="exportEditedAsset"){
                string format=StringArg(args,"format");byte[] bytes;
                if(format=="pdf"){string encoded=StringArg(args,"base64");if(encoded.Length>44*1024*1024)throw new InvalidDataException("此 PDF 超过轻量编辑的大小限制。");bytes=Convert.FromBase64String(encoded);if(bytes.Length<8||Encoding.ASCII.GetString(bytes,0,5)!="%PDF-")throw new InvalidDataException("PDF 内容无效。");}
                else if(new[]{"md","txt","docx","epub"}.Contains(format)){string text=StringArg(args,"text");if(text.Length>16*1024*1024)throw new InvalidDataException("编辑正文过大。");bytes=format=="docx"||format=="epub"?TextDocumentExport.Export(text,entry.name,format,(int)CreativeEdits.Number(args,"columns",1,1,2)):new UTF8Encoding(false).GetBytes(text);}
                else throw new InvalidDataException("请选择支持的导出格式。");
                string destination=ChooseEditDestination(owner,entry,"."+format,format.ToUpperInvariant()+"|*."+format);if(destination==null)return new{cancelled=true};
                string temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}finally{if(File.Exists(temp))File.Delete(temp);}
                target.Import(new[]{destination},null);var saved=target.Db.items.FirstOrDefault(i=>String.Equals(i.path,destination,StringComparison.OrdinalIgnoreCase));
                if(format=="pdf"&&saved!=null&&args.ContainsKey("annotations"))target.Update(saved.id,new Dictionary<string,object>{{"annotations",args["annotations"]}});
                Notify("libraryChanged",null);return new{saved=true,name=Path.GetFileName(destination),id=saved==null?"":saved.id};
            }
            string job=StringArg(args,"jobId");if(job.Length<1||job.Length>80||creativeJobs.ContainsKey(job)||creativeJobs.Count>=2)throw new InvalidOperationException("请等待当前导出完成。");
            var edit=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(args["edit"]));string extension=entry.kind=="video"?".mp4":"."+StringArg(args,"format");if(entry.kind=="music"&&!new[]{".wav",".mp3",".m4a"}.Contains(extension))throw new InvalidDataException("请选择支持的导出格式。");
            var clips=args.ContainsKey("clips")?Library.Json.Deserialize<List<MediaEditClip>>(Library.Json.Serialize(args["clips"])):new List<MediaEditClip>{new MediaEditClip{id=entry.id,edit=edit}};
            if(clips==null||clips.Count<1||clips.Count>50)throw new InvalidDataException("每个项目支持 1 至 50 个片段。");foreach(var clip in clips){var source=library.Find(clip.id);if(source==null||source.kind!=entry.kind||!File.Exists(source.path))throw new InvalidDataException("片段素材已移除或类型不匹配。");clip.source=source.path;CreativeEdits.MediaArguments(source.path,"check"+extension,entry.kind,clip.edit);}
            string encoder=downloads.LocalEncoder;if(encoder==null)throw new InvalidOperationException("请先配置本地媒体组件。");
            // Validate before opening a dialog or starting a process.
            string output=ChooseEditDestination(owner,entry,extension,extension.TrimStart('.').ToUpperInvariant()+"|*"+extension);if(output==null)return new{cancelled=true};
            using(var cancel=new CancellationTokenSource()){creativeJobs.Add(job,cancel);backgroundLibraryWrites++;try{
                await MediaSequence.Export(encoder,downloads.LocalProbe,clips,output,entry.kind,cancel.Token,p=>Notify("creativeExportProgress",new{jobId=job,progress=p}));cancel.Token.ThrowIfCancellationRequested();if(library!=target)throw new OperationCanceledException();target.Import(new[]{output},null);Notify("libraryChanged",null);return new{saved=true,name=Path.GetFileName(output)};
            }catch(OperationCanceledException){return new{cancelled=true};}finally{creativeJobs.Remove(job);backgroundLibraryWrites--;}}
        }
    }
}
