using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Xml.Linq;

namespace kok {
    public class Cue { public double start,end; public string text; }
    public class SubtitleTrack { public string name; public List<Cue> cues=new List<Cue>(); }
    public static class Subtitles {
        static readonly string[] extensions={".srt",".vtt",".ass",".ssa",".sub",".sbv",".lrc",".ttml",".dfxp",".smi"};
        public static List<SubtitleTrack> Discover(string video) {
            string stem=Path.GetFileNameWithoutExtension(video);
            var tracks=new List<SubtitleTrack>();
            foreach(string file in Directory.GetFiles(Path.GetDirectoryName(video)).Where(p=>extensions.Contains(Path.GetExtension(p).ToLowerInvariant())&&(Path.GetFileNameWithoutExtension(p).Equals(stem,StringComparison.OrdinalIgnoreCase)||Path.GetFileNameWithoutExtension(p).StartsWith(stem+".",StringComparison.OrdinalIgnoreCase))).OrderBy(p=>p).Take(20)) {
                try { var track=Read(file);if(track.cues.Count>0)tracks.Add(track); } catch { }
            }
            return tracks;
        }
        public static double Timestamp(string value) {
            value=value.Trim().Replace(',','.');
            if(value.EndsWith("ms"))return Double.Parse(value.Substring(0,value.Length-2),System.Globalization.CultureInfo.InvariantCulture)/1000;
            if(value.EndsWith("s"))return Double.Parse(value.Substring(0,value.Length-1),System.Globalization.CultureInfo.InvariantCulture);
            double n=0;foreach(string part in value.Split(':'))n=n*60+Double.Parse(part,System.Globalization.CultureInfo.InvariantCulture);return n;
        }
        static string Clean(string value) { return Regex.Replace(Regex.Replace(value,@"\{[^}]*\}",""),"<[^>]*>","").Replace(@"\N","\n").Replace(@"\n","\n").Replace(@"\h"," ").Trim(); }
        static void Add(SubtitleTrack t,double start,double end,string text) { text=Clean(text);if(start>=0&&end>start&&text.Length>0&&t.cues.Count<50000)t.cues.Add(new Cue{start=start,end=end,text=text}); }
        public static SubtitleTrack Read(string path) {
            if(new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException("字幕超过 8 MB。");
            string text=Library.ReadText(path).Replace("\r\n","\n").Replace('\r','\n');string ext=Path.GetExtension(path).ToLowerInvariant();
            var track=new SubtitleTrack{name=Path.GetFileName(path)};
            if(ext==".ass"||ext==".ssa") {
                foreach(string line in text.Split('\n'))if(line.StartsWith("Dialogue:",StringComparison.OrdinalIgnoreCase)) { string[] p=line.Substring(9).Split(new[]{','},10);if(p.Length==10)try{Add(track,Timestamp(p[1]),Timestamp(p[2]),p[9]);}catch(FormatException){} }
            } else if(ext==".lrc") {
                foreach(string line in text.Split('\n')) foreach(Match m in Regex.Matches(line,@"\[(\d+:\d+(?:\.\d+)?)\]"))Add(track,Timestamp(m.Groups[1].Value),Timestamp(m.Groups[1].Value)+5,Regex.Replace(line,@"\[[^\]]*\]",""));
                track.cues=track.cues.OrderBy(c=>c.start).ToList();for(int i=0;i<track.cues.Count-1;i++)track.cues[i].end=track.cues[i+1].start;
            } else if(ext==".sub") {
                double fps=25;var rows=text.Split('\n');
                foreach(string row in rows){var m=Regex.Match(row,@"^\{(\d+)\}\{(\d+)\}(.*)$");if(!m.Success)continue;int a=Int32.Parse(m.Groups[1].Value),b=Int32.Parse(m.Groups[2].Value);double f;if(a<=1&&Double.TryParse(m.Groups[3].Value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out f)&&f>=10&&f<=120){fps=f;continue;}Add(track,a/fps,b/fps,m.Groups[3].Value.Replace('|','\n'));}
            } else if(ext==".ttml"||ext==".dfxp") {
                using(var reader=System.Xml.XmlReader.Create(new StringReader(text),new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Ignore,XmlResolver=null})) {
                    var doc=XDocument.Load(reader);foreach(var p in doc.Descendants().Where(x=>x.Name.LocalName=="p")){string a=(string)p.Attribute("begin"),b=(string)p.Attribute("end"),duration=(string)p.Attribute("dur");if(a==null)continue;try{double start=Timestamp(a);double end=b!=null?Timestamp(b):start+Timestamp(duration??"5s");Add(track,start,end,p.Value);}catch(FormatException){}}
                }
            } else if(ext==".smi") {
                var matches=Regex.Matches(text,@"<sync\s+start\s*=\s*[""']?(\d+)[""']?[^>]*>(.*?)(?=<sync|$)",RegexOptions.IgnoreCase|RegexOptions.Singleline);
                for(int i=0;i<matches.Count;i++){double a=Double.Parse(matches[i].Groups[1].Value)/1000,b=i+1<matches.Count?Double.Parse(matches[i+1].Groups[1].Value)/1000:a+5;Add(track,a,b,System.Net.WebUtility.HtmlDecode(matches[i].Groups[2].Value));}
            } else {
                foreach(string block in Regex.Split(text,@"\n\s*\n")) {
                    string[] lines=block.Split('\n');for(int i=0;i<lines.Length;i++) {
                        var m=Regex.Match(lines[i],@"(\d{1,3}:\d{2}(?::\d{2})?[.,]\d+)\s*(?:-->|,)\s*(\d{1,3}:\d{2}(?::\d{2})?[.,]\d+)");
                        if(m.Success){try{Add(track,Timestamp(m.Groups[1].Value),Timestamp(m.Groups[2].Value),String.Join("\n",lines.Skip(i+1)));}catch(FormatException){}break;}
                    }
                }
            }
            track.cues=track.cues.OrderBy(c=>c.start).ToList();
            if(track.cues.Count==0)throw new InvalidDataException("未找到可读的文字字幕。IDX/SUB 位图字幕或内嵌字幕请使用专业播放器。");
            return track;
        }
    }
}
