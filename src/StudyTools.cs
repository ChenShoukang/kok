using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    public class StudyReport {
        public string id,kind,title,body,fingerprint,created,language;
        public List<string> fileIds=new List<string>();
        public List<ChatReference> references=new List<ChatReference>();
        public bool sampled;
    }
    public class VideoChapter { public double start; public string title,summary; }
    public class VideoOutline { public string fingerprint,created,language,track; public bool sampled; public List<VideoChapter> chapters=new List<VideoChapter>(); }
    public class OutlineDraft { public List<OutlineRow> chapters; }
    public class OutlineRow { public string sourceId,title,summary; }
    public class OutlinePart { public string id,text; public double start; }
    public partial class Library {
        public void SaveStudy(string id,StudyReport report){lock(gate){var entry=Find(id);if(entry==null)throw new FileNotFoundException("资料已移除。");if(report.fileIds.Any(x=>Find(x)==null))throw new FileNotFoundException("资料已移除。");if(entry.aiStudies==null)entry.aiStudies=new List<StudyReport>();entry.aiStudies.RemoveAll(x=>x.fingerprint==report.fingerprint);entry.aiStudies.Insert(0,report);while(entry.aiStudies.Count>8)entry.aiStudies.RemoveAt(entry.aiStudies.Count-1);Save();}}
        public void SaveOutline(string id,VideoOutline outline){lock(gate){var entry=Find(id);if(entry==null)throw new FileNotFoundException("资料已移除。");entry.videoOutline=outline;Save();}}
        static string StableStudyJson(object value){
            var map=value as Dictionary<string,object>;if(map!=null)return "{"+String.Join(",",map.Keys.OrderBy(k=>k,StringComparer.Ordinal).Select(k=>Json.Serialize(k)+":"+StableStudyJson(map[k])))+"}";
            var list=value as object[];if(list!=null)return "["+String.Join(",",list.Select(StableStudyJson))+"]";return Json.Serialize(value);
        }
        public static string StudyHash(object value){var canonical=StableStudyJson(Json.DeserializeObject(Json.Serialize(value)));using(var hash=SHA256.Create())return String.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical)).Select(b=>b.ToString("x2")));}
    }
    public partial class MainWindow {
        async Task<object> RunStudyTask(Dictionary<string,object> args){
            library.RequireAiMode();if(fileChatCancellation!=null)throw new InvalidOperationException("请先停止当前回答。");
            var owner=library;string kind=StringArg(args,"kind"),jobId=StringArg(args,"jobId"),context=StringArg(args,"context"),source=StringArg(args,"source");
            List<string> ids;try{ids=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(args["ids"]));}catch{throw new InvalidDataException("请选择有效的对比或回顾资料。");}
            if(!new[]{"compare","review"}.Contains(kind)||ids==null||ids.Count<(kind=="compare"?2:1)||ids.Count>50||ids.Any(String.IsNullOrEmpty)||ids.Distinct().Count()!=ids.Count||ids.Any(id=>owner.Find(id)==null)||context.Length>38000||String.IsNullOrWhiteSpace(context)||source.Length>200||jobId.Length<1||jobId.Length>80)throw new InvalidDataException("请选择有效的对比或回顾资料。");
            var references=Library.ReadChatReferences(args);if(references==null||references.Count==0||references.Any(r=>String.IsNullOrEmpty(r.fileId)||!ids.Contains(r.fileId))||ids.Any(id=>!references.Any(r=>r.fileId==id)))throw new InvalidDataException("引用来源与所选资料不匹配。");
            bool english=owner.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(owner.Db.settings["uiLanguage"])=="en",sampled=args.ContainsKey("sampled")&&Convert.ToBoolean(args["sampled"]);
            string language=english?"en":"zh",fingerprint=Library.StudyHash(new{version=2,kind=kind,ids=ids,context=context,source=source,references=references,language=language,provider=translations.PublicConfig()});
            var previous=(owner.Find(ids[0]).aiStudies??new List<StudyReport>()).FirstOrDefault(r=>r.fingerprint==fingerprint);if(previous!=null)return new{report=previous,cached=true};
            string question=kind=="compare"?
                "Compare ONLY the supplied selected files. Use three short sections with these headings: "+(english?"Common ground, Differences, Evidence":"共同点、差异、依据与待确认")+". Identify the file names for each claim, distinguish conflicting claims, and cite the exact supplied [S1] source IDs. Cover every selected file, grouping related files when necessary. Observe the per-file scope labels: compare the supplied readable text; do not speculate about missing sequels or insist that complete extracted text is sampled. If files are unrelated, say so rather than invent shared claims. Do not make a Markdown table.":
                "Review ONLY the supplied highlights and personal notes. Use three short sections with these headings: "+(english?"Source highlights, Personal thoughts, Open questions":"原文划线、个人想法、待思考问题")+". Preserve the distinction between quoted source text, personal notes, and your own suggestions. Identify links between notes from different files where evidence supports them, and cite the supplied [S1] source IDs. Do not claim to have read the entire original document.";
            question+=" Use only the requested response language, including headings; no bilingual headings. Keep each section brief and avoid repeated quotes or generic caveats. Keep the answer under "+(english?Math.Min(900,180+ids.Count*15).ToString()+" words.":Math.Min(1800,350+ids.Count*35).ToString()+" Chinese characters.");
            using(var cancel=ModuleCancellation()){fileChatCancellation=cancel;fileChatJob=jobId;try{
                string answer=await translations.ChatAsync(kind=="compare"?"Selected file comparison":"Notes review",context,source,question,null,"",english,cancel.Token,delta=>Notify("fileChatDelta",new{jobId=jobId,text=delta}));
                cancel.Token.ThrowIfCancellationRequested();owner.RequireAiMode();if(library!=owner)throw new OperationCanceledException(cancel.Token);
                var report=new StudyReport{id=Guid.NewGuid().ToString("N"),kind=kind,title=english?(kind=="compare"?"File comparison":"Notes review"):(kind=="compare"?"多文件对比":"笔记回顾"),body=answer,fingerprint=fingerprint,created=DateTime.UtcNow.ToString("o"),language=language,fileIds=ids,references=references,sampled=sampled};owner.SaveStudy(ids[0],report);return new{report=report,cached=false};
            }catch(OperationCanceledException){throw new InvalidOperationException("AI 回答已停止。");}finally{fileChatCancellation=null;fileChatJob="";}}
        }
        public static List<OutlinePart> OutlineParts(List<Cue> cues,out bool sampled){
            sampled=false;if(cues==null||cues.Count==0||cues.Count>50000||cues.Any(c=>c==null||c.text==null||c.text.Length>10000||Double.IsNaN(c.start)||Double.IsInfinity(c.start)||Double.IsNaN(c.end)||Double.IsInfinity(c.end)||c.start<0||c.end<=c.start||c.end>31536000)||cues.Sum(c=>(long)c.text.Length)>2*1024*1024)throw new InvalidDataException("字幕内容为空、过长或格式无效。");
            var ordered=cues.OrderBy(c=>c.start).ToList();double first=ordered[0].start,last=ordered.Max(c=>c.end),span=Math.Max(120,(last-first)/16);
            var parts=new List<OutlinePart>();foreach(var group in ordered.GroupBy(c=>Math.Min(15,(int)((c.start-first)/span)))){
                string text=String.Join("\n",group.Select(c=>c.text));if(text.Length>1600){text=text.Substring(0,600)+"\n…\n"+text.Substring(Math.Max(600,text.Length/2-250),500)+"\n…\n"+text.Substring(text.Length-500);sampled=true;}
                parts.Add(new OutlinePart{id="S"+(parts.Count+1),start=group.First().start,text=text});
            }return parts;
        }
        public static List<VideoChapter> ParseOutline(string answer,List<OutlinePart> parts){
            try{string text=answer.Trim();if(text.StartsWith("```")){int first=text.IndexOf('\n'),last=text.LastIndexOf("```",StringComparison.Ordinal);text=text.Substring(first+1,last-first-1).Trim();}
                var draft=Library.Json.Deserialize<OutlineDraft>(text);if(draft==null||draft.chapters==null||draft.chapters.Count!=parts.Count)throw new FormatException();
                var chapters=new List<VideoChapter>();foreach(var part in parts){var rows=draft.chapters.Where(r=>r!=null&&r.sourceId==part.id).ToList();if(rows.Count!=1)throw new FormatException();var row=rows[0];if(String.IsNullOrWhiteSpace(row.title)||row.title.Length>100||String.IsNullOrWhiteSpace(row.summary)||row.summary.Length>500)throw new FormatException();chapters.Add(new VideoChapter{start=part.start,title=row.title.Trim(),summary=row.summary.Trim()});}return chapters;
            }catch{throw new InvalidDataException("字幕目录格式无效，请重试。");}
        }
        async Task<object> GenerateVideoOutline(Dictionary<string,object> args){
            library.RequireAiMode();if(fileChatCancellation!=null)throw new InvalidOperationException("请先停止当前回答。");var owner=library;var item=RequireEntry(args);if(item.kind!="video")throw new InvalidDataException("请选择视频文件。");
            string jobId=StringArg(args,"jobId"),track=StringArg(args,"track");if(jobId.Length<1||jobId.Length>80||track.Length>200)throw new InvalidDataException("字幕内容为空、过长或格式无效。");
            List<Cue> cues;try{cues=Library.Json.Deserialize<List<Cue>>(Library.Json.Serialize(args["cues"]));}catch{throw new InvalidDataException("字幕内容为空、过长或格式无效。");}bool sampled;var parts=OutlineParts(cues,out sampled);
            bool english=owner.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(owner.Db.settings["uiLanguage"])=="en";string language=english?"en":"zh",fingerprint=Library.StudyHash(new{version=1,cues=cues,language=language,provider=translations.PublicConfig()});
            if(item.videoOutline!=null&&item.videoOutline.fingerprint==fingerprint)return new{outline=item.videoOutline,cached=true};
            string context=String.Join("\n\n",parts.Select(p=>"["+p.id+"] ["+TimeSpan.FromSeconds(p.start).ToString()+"]\n"+p.text));
            using(var cancel=ModuleCancellation()){fileChatCancellation=cancel;fileChatJob=jobId;try{
                string answer=await translations.ChatAsync(item.name,context,sampled?"Subtitle segments, sampled":"Subtitle segments","Return ONLY valid JSON with this schema: {\"chapters\":[{\"sourceId\":\"S1\",\"title\":\"short title\",\"summary\":\"one brief sentence\"}]}. Include exactly one chapter for EVERY supplied source ID, in source order. Each title is at most 20 words and each summary at most 45 words. Use the requested response language. Summarize the evidence in each segment without inventing events. Do not invent timestamps; the application assigns them.",null,"",english,cancel.Token,delta=>{});
                var chapters=ParseOutline(answer,parts);cancel.Token.ThrowIfCancellationRequested();owner.RequireAiMode();if(library!=owner)throw new OperationCanceledException(cancel.Token);
                var outline=new VideoOutline{fingerprint=fingerprint,created=DateTime.UtcNow.ToString("o"),language=language,track=track,sampled=sampled,chapters=chapters};owner.SaveOutline(item.id,outline);return new{outline=outline,cached=false};
            }catch(OperationCanceledException){throw new InvalidOperationException("AI 回答已停止。");}finally{fileChatCancellation=null;fileChatJob="";}}
        }
    }
}
