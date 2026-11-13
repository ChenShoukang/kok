using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace kok {
    // One extractable source for exact Agent reads: the complete readable text (document body plus
    // notes/annotations), a content digest used to prove that a recorded read still matches the
    // current source, and a bounded reason when nothing can be read.
    internal sealed class AgentReadableSource {
        public string id="",name="",ext="",text="",digest="",reason="";
        public bool readable,removed,capped;
        public int Length{get{return text==null?0:text.Length;}}
    }
    // P0-2 exact reading (v0.12.0). read_items must return the actual source text in deterministic
    // pages instead of keyword-matched 700-character samples, and the host must be able to prove that
    // every required source was read from character 0 to its end before a full-source task may finish.
    // This type owns validation, the paging instructions and the coverage check; the Library partial
    // below owns extraction, paging and the rendered result.
    static class AgentReading {
        internal const int DefaultMaxChars=8000;   // one page normally stays inside the host's 8000-char result window
        internal const int MinMaxChars=200;
        internal const int MaxMaxChars=12000;      // matches read_document's maximum exact window
        internal const int MaxOffset=380000;       // matches the lightweight document text limit
        internal const int MaxReadableChars=120000; // ReadKnowledgeBody's own extraction cap
        internal const int OpeningChars=800;       // verbatim source opening kept for older exact-read results
        internal const int DefaultReadBackChars=8000; // artifact read-back must continuously cover this much (or the whole file)
        // Coverage domains: read_items covers the readable source (body plus notes/annotations), read_document
        // covers the managed document body only, read_presentation covers 1-based slide page ranges. All use the
        // same digest rules but different current text, so coverage rows name their domain and FullCoverage/
        // ReadBackSufficient/DeckCoverage validate against that domain.
        internal const string DomainReadable="readable";
        internal const string DomainDocument="document";
        internal const string DomainPresentation="presentation";
        // One digest formula shared by every exact reader (read_items and read_document). A recorded read only
        // proves current coverage while this digest still equals the freshly extracted text of the same domain.
        internal static string SourceDigest(string id,string text,bool capped){
            return Library.StudyHash(new{version=1,id=id??"",text=text??"",capped=capped});
        }
        // CRLF/CR folding used by the readable extraction; read_document maps its raw body offsets through the
        // same folding so its coverage rows line up with read_items ranges when both domains describe one text.
        internal static string NormalizeSourcePrefix(string text,int length){
            if(String.IsNullOrEmpty(text)||length<=0)return "";
            if(length>text.Length)length=text.Length;
            var builder=new StringBuilder(length);
            for(int n=0;n<length;n++){char c=text[n];if(c=='\r'){builder.Append('\n');if(n+1<length&&text[n+1]=='\n')n++;}else builder.Append(c);}
            return builder.ToString();
        }
        internal static string NormalizeSourceText(string text){
            return String.IsNullOrEmpty(text)?"":NormalizeSourcePrefix(text,text.Length).TrimEnd();
        }
        internal static int NormalizedOffset(string text,int offset){
            if(String.IsNullOrEmpty(text)||offset<=0)return 0;
            if(offset>text.Length)offset=text.Length;
            int count=0;
            for(int n=0;n<offset;n++){if(text[n]=='\r'){count++;if(n+1<offset&&text[n+1]=='\n')n++;}else count++;}
            return count;
        }

        internal static string DisplayName(string name,int max){
            name=(name??"").Trim();if(name.Length==0)name="未命名资料";
            if(name.Length>max){int length=max;if(length>0&&Char.IsHighSurrogate(name[length-1]))length--;name=name.Substring(0,length)+"…";}
            return name;
        }
        // read_items{ids[<=8],offset?,max_chars?,question?}. The question hint is accepted for prompt
        // compatibility but never samples or limits the returned exact text.
        internal static void ValidateReadItems(AgentAction action,Func<string,bool> exists){
            if(action==null)throw new FormatException("unknown tool");
            var args=action.args??(action.args=new Dictionary<string,object>());
            object value;List<string> ids=null;
            if(args.TryGetValue("ids",out value)&&value!=null){try{ids=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(value));}catch{throw new FormatException("ids must be an array of file IDs");}}
            if(ids==null||ids.Count==0)throw new FormatException("missing ids");
            if(ids.Count>8)throw new FormatException("ids allows at most 8 file IDs");
            for(int n=0;n<ids.Count;n++){
                string id=ids[n];if(id==null||id.Trim().Length==0||id.Length>32||id.Any(Char.IsControl))throw new FormatException("ids contains an empty or invalid item");
                ids[n]=id.Trim();
            }
            if(ids.Distinct().Count()!=ids.Count)throw new FormatException("ids contains duplicate file IDs");
            foreach(string id in ids)if(!Regex.IsMatch(id,@"^[a-f0-9]{32}$")||exists==null||!exists(id))throw new FormatException("unknown or out-of-scope file ID "+(id.Length>40?id.Substring(0,40):id));
            AgentDocumentTools.Number(args,"offset",0,0,MaxOffset);
            AgentDocumentTools.Number(args,"max_chars",DefaultMaxChars,MinMaxChars,MaxMaxChars);
            if(args.TryGetValue("question",out value)&&value!=null){
                string question=Convert.ToString(value);
                if(question.Length>300)throw new FormatException("question is longer than 300 characters");
                if(question.Any(c=>Char.IsControl(c)&&c!='\n'&&c!='\t'))throw new FormatException("question contains control characters");
            }
        }
        // Coverage rows written by ReadAgentItemsExact (domain=readable), ReadAgentDocument (domain=document) and
        // ReadAgentPresentation (domain=presentation, digits are page indexes and digests are file signatures).
        // Only `mode=exact` rows count; readable/document rows need a 64-hex content digest.
        internal sealed class AgentCoverageRow {
            public string id="",name="",digest="",domain=DomainReadable;
            public int totalChars,readStart,readEnd;
            public bool hasMore,capped;
        }
        internal static List<AgentCoverageRow> CoverageRows(AgentCall call){
            var rows=new List<AgentCoverageRow>();
            if(call==null||call.result==null)return rows;
            object raw;if(!call.result.TryGetValue("coverage",out raw)||raw==null)return rows;
            List<Dictionary<string,object>> values=null;
            try{values=AgentTaskState.Get<List<Dictionary<string,object>>>(call.result,"coverage");}catch{}
            if(values==null){try{values=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(raw));}catch{}}
            if(values==null)return rows;
            foreach(var value in values){
                if(value==null)continue;
                string mode=TextValue(value,"mode");if(mode!=""&&mode!="exact")continue;
                string digest=TextValue(value,"digest");
                string domain=TextValue(value,"domain");if(domain!=DomainDocument&&domain!=DomainPresentation)domain=DomainReadable;
                if(domain==DomainPresentation){if(digest.Length==0)continue;}else if(digest.Length!=64)continue;
                int? total=IntValue(value,"totalChars"),start=IntValue(value,"readStart"),end=IntValue(value,"readEnd");
                if(!total.HasValue||!start.HasValue||!end.HasValue)continue;
                rows.Add(new AgentCoverageRow{id=TextValue(value,"id"),name=TextValue(value,"name"),digest=digest,domain=domain,totalChars=total.Value,readStart=start.Value,readEnd=end.Value,hasMore=BoolValue(value,"hasMore"),capped=BoolValue(value,"capped")});
            }
            return rows;
        }
        // The current text of one coverage domain plus the digest/length a recorded read must match.
        sealed class AgentCoverageView { public string domain="",text="",digest=""; public int total; }
        static AgentCoverageView ReadableView(Library lib,string id,AgentReadableSource known){
            var source=known??lib.AgentReadSourceText(id);
            if(source==null||source.removed||!source.readable)return null;
            return new AgentCoverageView{domain=DomainReadable,text=source.text,total=source.Length,digest=source.digest};
        }
        // read_document's coverage domain is the current managed document body (no notes/annotations).
        static AgentCoverageView DocumentView(Library lib,string id){
            try{
                var document=lib.DocumentSource(id);
                if(document==null)return null;
                string text=NormalizeSourceText(document.text);
                return new AgentCoverageView{domain=DomainDocument,text=text,total=text.Length,digest=SourceDigest(id,text,false)};
            }catch{return null;}
        }
        static bool SameText(AgentCoverageView a,AgentCoverageView b){return a!=null&&b!=null&&a.total==b.total&&a.digest==b.digest;}
        // Merge the recorded ranges of the given domain(s) and prove [0,total) is tiled with no gap.
        static bool TiledCoverage(List<AgentCoverageRow> rows,string[] domains,int total,string digest,out List<string> gaps,out bool stale,out int validCount){
            gaps=new List<string>();stale=false;validCount=0;
            var valid=new List<AgentCoverageRow>();
            if(rows!=null)foreach(var row in rows){
                if(row==null||Array.IndexOf(domains,row.domain)<0)continue;
                if(row.totalChars!=total||row.digest!=digest){stale=true;continue;}
                if(row.readStart<0||row.readStart>=total||row.readEnd>total||row.readEnd<=row.readStart)continue;
                valid.Add(row);
            }
            validCount=valid.Count;
            if(valid.Count==0){gaps.Add("0-"+total);return false;}
            valid.Sort((a,b)=>a.readStart!=b.readStart?a.readStart.CompareTo(b.readStart):a.readEnd.CompareTo(b.readEnd));
            int position=0;foreach(var row in valid){if(row.readStart>position)gaps.Add(position+"-"+row.readStart);if(row.readEnd>position)position=row.readEnd;}
            if(position<total)gaps.Add(position+"-"+total);
            return gaps.Count==0;
        }
        // A readable deck plus its current page count and version token (file signature). Used by FullCoverage
        // (deck page coverage satisfies a full-source requirement for a PPTX source) and by DeckCoverage
        // (artifact read-back must cover every page of the FINAL deck).
        sealed class AgentPresentationView { public int totalPages; public string digest=""; }
        static AgentPresentationView PresentationView(Library lib,string id){
            try{
                var item=lib.Find(id);
                if(item==null||item.ext!="PPTX")return null;
                string digest=lib.PresentationCoverageDigest(id);
                int totalPages=lib.PresentationPageCount(id);
                if(String.IsNullOrEmpty(digest)||totalPages<=0)return null;
                return new AgentPresentationView{totalPages=totalPages,digest=digest};
            }catch{return null;}
        }
        static string PresentationPageRange(int fromIndex,int toIndex){
            return fromIndex>=toIndex?"第 "+(fromIndex+1)+" 页":"第 "+(fromIndex+1)+"-"+(toIndex+1)+" 页";
        }
        static string PresentationGapText(List<string> gaps){
            if(gaps==null||gaps.Count==0)return "全部页";
            var parts=new List<string>();
            foreach(string gap in gaps.Take(4)){
                int dash=gap==null?-1:gap.IndexOf('-');int from,to;
                if(dash>0&&Int32.TryParse(gap.Substring(0,dash),out from)&&Int32.TryParse(gap.Substring(dash+1),out to))parts.Add(PresentationPageRange(from,to-1));
                else parts.Add(gap??"");
            }
            if(gaps.Count>4)parts.Add("等 "+(gaps.Count-4)+" 段");
            return String.Join("、",parts);
        }
        // Artifact presentation read-back (#7A, used by the delivery acceptance layer): after `afterSequence`, the
        // merged read_presentation PAGE intervals must tile [0,current totalPages) and every row must still match
        // the deck's current version token. One-page reads of an 8-page deck therefore never pass. `detail` names
        // the covered/ missing pages so the model can continue from the first unread page.
        internal static bool DeckCoverage(Library lib,AgentTaskState task,string id,int afterSequence,out int totalPages,out int coveredPages,out string detail){
            detail="";totalPages=0;coveredPages=0;
            if(lib==null||task==null||String.IsNullOrEmpty(id)){detail="无法确认演示文稿回读：缺少资料库或任务状态。";return false;}
            var view=PresentationView(lib,id);
            if(view==null){detail="无法确认演示文稿覆盖：该成果不是可读取的 PPTX 演示文稿，或文件/版本暂时无法读取。";return false;}
            totalPages=view.totalPages;
            var rows=new List<AgentCoverageRow>();
            foreach(var call in task.calls){
                if(call==null||call.status!="done"||call.result==null||call.sequence<=afterSequence)continue;
                if(call.tool!="read_presentation")continue;
                foreach(var row in CoverageRows(call))if(row.id==id&&row.domain==DomainPresentation)rows.Add(row);
            }
            var valid=new List<AgentCoverageRow>();bool stale=false;
            foreach(var row in rows){
                if(row.totalChars!=totalPages||row.digest!=view.digest){stale=true;continue;}
                if(row.readStart<0||row.readStart>=totalPages||row.readEnd>totalPages||row.readEnd<=row.readStart)continue;
                valid.Add(row);
            }
            valid.Sort((a,b)=>a.readStart!=b.readStart?a.readStart.CompareTo(b.readStart):a.readEnd.CompareTo(b.readEnd));
            int covered=0,end=0;var missing=new List<string>();
            foreach(var row in valid){
                if(row.readEnd<=end)continue;
                int start=Math.Max(row.readStart,end);covered+=row.readEnd-start;
                if(row.readStart>end)missing.Add("" + end+"-"+(row.readStart-1));
                end=row.readEnd;
                if(end>=totalPages)break;
            }
            if(end<totalPages)missing.Add("" + end+"-"+(totalPages-1));
            coveredPages=Math.Min(covered,totalPages);
            if(missing.Count==0&&coveredPages>=totalPages){detail="";return true;}
            var item=lib.Find(id);string label=(item!=null&&!String.IsNullOrEmpty(item.name)?DisplayName(item.name,40):id)+" ["+id+"]";
            var pageParts=new List<string>();
            foreach(string range in missing.Take(4)){
                int dash=range.IndexOf('-');int from,to;
                if(dash>0&&Int32.TryParse(range.Substring(0,dash),out from)&&Int32.TryParse(range.Substring(dash+1),out to))pageParts.Add(PresentationPageRange(from,to));
            }
            if(missing.Count>4)pageParts.Add("等 "+missing.Count+" 段");
            detail="演示文稿回读不足："+label+" 共 "+totalPages+" 页，已覆盖 "+coveredPages+" 页"+(stale?"（部分读取记录因文件版本变化已失效）":"")+"；缺少 "+String.Join("、",pageParts)+(stale?"":"")+"。请用 read_presentation 从缺失页继续读取（start_page=…，max_pages 最多 8）直到 has_more=false，或完整读取演示文稿文本；只读部分页不足以证明成果全部页已核对。";
            if(detail.Length>1200)detail=detail.Substring(0,1200)+"…";
            return false;
        }
        // Task-level exact-source coverage: done read_items (readable domain: body plus notes), read_document
        // (document domain: managed body) and read_presentation (presentation domain: slide page ranges) calls
        // all count, each validated against its own current text/version digest. For a PPTX source the page
        // coverage OR a full readable/document read satisfies the requirement (#7B). recall_result never counts.
        // Unread sources/ranges are named in `detail` so the model can continue paging.
        internal static bool FullCoverage(Library lib,AgentTaskState task,IEnumerable<string> ids,out string detail){
            detail="";
            if(lib==null)return true;
            var wanted=(ids??Enumerable.Empty<string>()).Where(id=>!String.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
            if(wanted.Count==0)return true;
            var reads=new Dictionary<string,List<AgentCoverageRow>>(StringComparer.Ordinal);
            if(task!=null)foreach(var call in task.calls){
                if(call==null||call.status!="done"||call.result==null)continue;
                if(call.tool!="read_items"&&call.tool!="read_document"&&call.tool!="read_presentation")continue;
                foreach(var row in CoverageRows(call)){
                    if(row.id.Length==0||row.digest.Length==0)continue;
                    List<AgentCoverageRow> list;if(!reads.TryGetValue(row.id,out list)){list=new List<AgentCoverageRow>();reads[row.id]=list;}list.Add(row);
                }
            }
            var problems=new List<string>();
            foreach(string id in wanted){
                List<AgentCoverageRow> rows;reads.TryGetValue(id,out rows);if(rows==null)rows=new List<AgentCoverageRow>();
                var source=lib.AgentReadSourceText(id);
                string label=DisplayName(source.name.Length==0?id:source.name,40)+" ["+id+"]";
                // #7B: a PPTX source is covered by reading every slide page OR by a full readable/document read.
                var presentation=PresentationView(lib,id);
                List<string> presentationGaps=null;bool presentationStale=false;int presentationValid=0;
                if(presentation!=null&&TiledCoverage(rows,new[]{DomainPresentation},presentation.totalPages,presentation.digest,out presentationGaps,out presentationStale,out presentationValid))continue;
                if(source.removed&&presentation==null){problems.Add(label+" 已移除或不在资料库中");continue;}
                bool readableFull=false,documentFull=false;
                var readable=ReadableView(lib,id,source);var document=DocumentView(lib,id);
                List<string> readableGaps=null,documentGaps=null;bool readableStale=false,documentStale=false;int readableValid=0,documentValid=0;
                if(!source.capped&&readable!=null){
                    bool compatible=SameText(readable,document);
                    string[] readDomains=compatible?new[]{DomainReadable,DomainDocument}:new[]{DomainReadable};
                    string[] documentDomains=compatible?new[]{DomainReadable,DomainDocument}:new[]{DomainDocument};
                    readableFull=TiledCoverage(rows,readDomains,readable.total,readable.digest,out readableGaps,out readableStale,out readableValid);
                    if(document!=null)documentFull=TiledCoverage(rows,documentDomains,document.total,document.digest,out documentGaps,out documentStale,out documentValid);
                    if(readableFull||documentFull)continue;
                }
                bool presentationHasRows=presentation!=null&&rows.Any(row=>row.domain==DomainPresentation);
                if(presentation!=null&&(presentationHasRows||!source.readable||source.capped)){
                    string missing=presentationGaps==null?"全部页":PresentationGapText(presentationGaps);
                    problems.Add(label+" 演示文稿共 "+presentation.totalPages+" 页，全文覆盖尚未完成：缺少 "+missing+(presentationValid==0&&presentationHasRows?"（读取记录已失效：文件版本已变化，请重新 read_presentation）":presentationStale?"（部分旧读取记录因文件版本变化已失效）":"")+"（read_presentation 按页读取并跟随 next_page 读完全部页，或完整读取其文本）");
                    continue;
                }
                if(!source.readable){problems.Add(label+" 无法读取原文："+(String.IsNullOrWhiteSpace(source.reason)?"无可读正文或笔记":source.reason)+"（不能计为全文覆盖）");continue;}
                if(source.capped){problems.Add(label+" 正文超过 "+MaxReadableChars+" 字符可读上限，不能计为全文覆盖（请改用 partial 或拆分来源）");continue;}
                var readableRows=rows.Where(row=>row.domain==DomainReadable).ToList();
                var documentRows=rows.Where(row=>row.domain==DomainDocument).ToList();
                var view=readable!=null&&(readableRows.Count>0||documentRows.Count==0)?readable:document;
                if(view==null){problems.Add(label+" 无法读取原文（不能计为全文覆盖）");continue;}
                bool readableDomain=view.domain==DomainReadable;
                bool hasRows=readableDomain?readableRows.Count>0:documentRows.Count>0;
                var gaps=readableDomain?readableGaps:documentGaps;bool stale=readableDomain?readableStale:documentStale;int validCount=readableDomain?readableValid:documentValid;
                if(!hasRows){problems.Add(label+" 尚未读取任何原文（total_chars="+view.total+"）");continue;}
                if(validCount==0){problems.Add(label+" 的读取记录已失效（内容已变化），需重新读取（total_chars="+view.total+"）");continue;}
                problems.Add(label+" 未读全（total_chars="+view.total+"；缺少字符 "+String.Join("、",gaps.Take(3))+(gaps.Count>3?" 等 "+gaps.Count+" 段":"")+(stale?"；部分旧读取记录因内容变化已失效":"")+"）");
            }
            if(problems.Count==0)return true;
            detail="全文覆盖未完成："+String.Join("；",problems)+"。请用 read_items/read_document 或 read_presentation 继续精确读取未覆盖区间，直到 has_more=false；recall_result 回放不计原文覆盖。";
            if(detail.Length>1600)detail=detail.Substring(0,1600)+"…";
            return false;
        }
        // Artifact read-back sufficiency (#7): after `afterSequence`, the merge of read_items (readable) and
        // read_document (document) exact ranges must continuously cover [0,min(total,minChars)); the whole file
        // when it is no longer than minChars. Stale digests (content changed) never count. `detail` names the
        // covered prefix and the first unread range so the model can continue reading instead of only re-reading
        // one character. "存在回读" alone is therefore not enough: a two-character read fails.
        internal static bool ReadBackSufficient(Library lib,AgentTaskState task,string id,int afterSequence,int minChars,out string detail){
            detail="";
            int minimum=minChars>0?minChars:DefaultReadBackChars;
            if(lib==null||task==null||String.IsNullOrEmpty(id)){detail="回读不足：缺少资料库或任务状态，无法确认成果内容。";return false;}
            var source=lib.AgentReadSourceText(id);
            string label=DisplayName(source.name.Length==0?id:source.name,40)+" ["+id+"]";
            var document=DocumentView(lib,id);
            if(source.removed&&document==null){detail="回读不足：成果"+label+"已不在资料库中。";return false;}
            if(!source.readable&&document==null){detail="回读不足：成果"+label+"没有可读取的正文（"+(String.IsNullOrWhiteSpace(source.reason)?"无可读正文":source.reason)+"）。";return false;}
            var readable=ReadableView(lib,id,source);
            var rows=new List<AgentCoverageRow>();
            foreach(var call in task.calls){
                if(call==null||call.status!="done"||call.result==null||call.sequence<=afterSequence)continue;
                if(call.tool!="read_items"&&call.tool!="read_document")continue;
                foreach(var row in CoverageRows(call))if(row.id==id&&row.digest.Length==64)rows.Add(row);
            }
            bool compatible=SameText(readable,document);
            string[] both=compatible?new[]{DomainReadable,DomainDocument}:null;
            int readablePrefix=-1,documentPrefix=-1;List<string> readableMissing=null,documentMissing=null;bool stale=false;
            if(readable!=null){
                int required=Math.Min(readable.total,minimum);int covered;bool rs;
                PrefixCoverage(rows,compatible?both:new[]{DomainReadable},readable.total,readable.digest,required,out covered,out readableMissing,out rs);readablePrefix=covered;stale|=rs;
            }
            if(document!=null){
                int required=Math.Min(document.total,minimum);int covered;bool ds;
                PrefixCoverage(rows,compatible?both:new[]{DomainDocument},document.total,document.digest,required,out covered,out documentMissing,out ds);documentPrefix=covered;stale|=ds;
            }
            int best=Math.Max(readablePrefix,documentPrefix);
            if(best<0){detail="回读不足：成果"+label+"没有可读取的正文。";return false;}
            bool useReadable=readablePrefix>=documentPrefix&&readable!=null;
            int bestTotal=useReadable?readable.total:(document!=null?document.total:readable.total);
            int bestRequired=Math.Min(bestTotal,minimum);
            if(best>=bestRequired){detail="";return true;}
            var missing=useReadable?readableMissing:documentMissing;
            string ranges=missing!=null&&missing.Count>0?String.Join("、",missing.Take(3))+(missing.Count>3?" 等 "+missing.Count+" 段":""):(best+"-"+bestRequired);
            detail="回读不足：成果"+label+"共 "+bestTotal+" 字符，要求连续回读 0-"+bestRequired+"，当前仅连续覆盖 0-"+best+"（缺少字符 "+ranges+(stale?"；部分读取记录因内容变化已失效":"")+"）。请用 read_items 或 read_document 从 offset="+best+" 继续读取到至少 "+bestRequired+" 字符后，再结束任务；只有读取回执不足以证明成果内容已被核对。";
            if(detail.Length>1200)detail=detail.Substring(0,1200)+"…";
            return false;
        }
        internal static bool ReadBackSufficient(Library lib,AgentTaskState task,string id,int afterSequence,out string detail){
            return ReadBackSufficient(lib,task,id,afterSequence,DefaultReadBackChars,out detail);
        }
        // Longest continuous prefix [0,covered) of the required range; missing names the first unread range.
        static void PrefixCoverage(List<AgentCoverageRow> rows,string[] domains,int total,string digest,int required,out int covered,out List<string> missing,out bool stale){
            covered=0;missing=new List<string>();stale=false;
            if(required<=0)return;
            var valid=new List<AgentCoverageRow>();
            foreach(var row in rows){
                if(row==null||Array.IndexOf(domains,row.domain)<0)continue;
                if(row.totalChars!=total||row.digest!=digest){stale=true;continue;}
                if(row.readStart<0||row.readStart>=total||row.readEnd>total||row.readEnd<=row.readStart)continue;
                valid.Add(row);
            }
            valid.Sort((a,b)=>a.readStart!=b.readStart?a.readStart.CompareTo(b.readStart):a.readEnd.CompareTo(b.readEnd));
            foreach(var row in valid){if(row.readStart>covered)break;if(row.readEnd>covered)covered=row.readEnd;if(covered>=required)break;}
            if(covered>total)covered=total;
            if(covered<required)missing.Add(covered+"-"+required);
        }
        // Reading guidance appended to AgentCognition.Instructions by the delivery agent. Bounded and
        // non-repeating: one static rule block plus the unread ranges of the most recent exact read.
        internal static string Instructions(AgentTaskState task){
            var builder=new StringBuilder("READING EXACT SOURCES (read_items/read_document): ");
            builder.Append("read_items returns exact contiguous source text (body plus notes) page by page, never keyword-sampled excerpts. ");
            builder.Append("Each result starts with a Coverage summary and per-source headers giving this page's character range, total_chars, has_more and next_offset. ");
            builder.Append("read_document returns the exact managed-document body with mode=exact and the same coverage schema (its coverage domain is the body only; notes are not body); both tools accumulate the same per-source coverage, and max_chars is a HARD cap so skipped sources are named with a next_offset to continue. ");
            builder.Append("read_presentation records per-page coverage (domain=presentation) for a PPTX source: every slide page counts toward full coverage, and a presentation artifact read-back must cover ALL pages (follow next_page, at most 8 pages per call), never a single page. ");
            builder.Append("Text that was not returned yet is marked 尚未读取/NOT READ YET: it is unread, not missing from the source. ");
            builder.Append("When has_more=true, continue with the same ids and offset=next_offset (you may raise max_chars up to ").Append(MaxMaxChars).Append(" to reduce round trips) until has_more=false. ");
            builder.Append("For tasks that require complete sources (全文/全部/完整/every source), page every required source to its end before writing or calling finish_task; never claim a source lacks a fact while it still has unread ranges. ");
            builder.Append("recall_result replays a stored tool result; it does not read original sources and never increases source coverage. search_library snippets are for orientation only. ");
            return builder.ToString()+UnreadReminder(task);
        }
        static string UnreadReminder(AgentTaskState task){
            if(task==null||task.calls==null)return "";
            AgentCall last=null;
            foreach(var call in task.calls)if(call!=null&&call.status=="done"&&(call.tool=="read_items"||call.tool=="read_document"||call.tool=="read_presentation")&&call.result!=null&&call.result.ContainsKey("coverage"))last=call;
            if(last==null)return "";
            var parts=new List<string>();
            foreach(var row in CoverageRows(last)){
                if(!row.hasMore||row.id.Length==0)continue;
                if(row.domain==DomainPresentation)parts.Add(DisplayName(row.name,36)+" ["+row.id+"] 第 "+(row.readEnd+1)+"-"+row.totalChars+" 页尚未读取（start_page="+(row.readEnd+1)+"）");
                else parts.Add(DisplayName(row.name,36)+" ["+row.id+"] chars "+row.readEnd+"-"+row.totalChars+" 尚未读取（offset="+row.readEnd+"）");
                if(parts.Count>=3)break;
            }
            return parts.Count==0?"":"UNREAD FROM YOUR LAST EXACT READ: "+String.Join("；",parts)+". ";
        }
        static string TextValue(Dictionary<string,object> row,string key){object value;if(row==null||!row.TryGetValue(key,out value)||value==null)return "";return Convert.ToString(value)??"";}
        static int? IntValue(Dictionary<string,object> row,string key){object value;if(row==null||!row.TryGetValue(key,out value)||value==null)return null;try{double number=Convert.ToDouble(value);if(Double.IsNaN(number)||Double.IsInfinity(number)||number<Int32.MinValue||number>Int32.MaxValue)return null;return (int)number;}catch{return null;}}
        static bool BoolValue(Dictionary<string,object> row,string key){object value;if(row==null||!row.TryGetValue(key,out value)||value==null)return false;try{return Convert.ToBoolean(value);}catch{return false;}}
    }
    public partial class Library {
        // Extract one source exactly once and digest it. The digest covers the exact text handed to the
        // model so FullCoverage can reject reads recorded before a content change.
        internal AgentReadableSource AgentReadSourceText(string id){
            var source=new AgentReadableSource{id=id??""};
            Entry item=null;
            lock(gate){
                var live=Find(id);
                if(live!=null)item=new Entry{id=live.id,path=live.path,name=live.name,kind=live.kind,ext=live.ext,notes=live.notes,ownedDocument=live.ownedDocument,generatedSubtitles=live.generatedSubtitles,chapterTexts=live.chapterTexts,
                    editDraft=live.editDraft==null?null:new Dictionary<string,object>(live.editDraft),
                    annotations=(live.annotations??new List<Annotation>()).Take(40).Select(a=>new Annotation{text=a.text,note=a.note}).ToList()};
            }
            if(item==null){source.removed=true;source.reason="资料已移除或不在资料库中";return source;}
            source.name=item.name??"";source.ext=item.ext??"";
            string body="";
            try{lock(knowledgeGate){int skipped=0;bool capped;body=ReadKnowledgeBody(item,ref skipped,out capped)??"";source.capped=capped;}}
            catch(Exception error){source.reason=String.IsNullOrWhiteSpace(error.Message)?"无法读取正文":error.Message;return source;}
            string text=body,notes=AgentReadNotes(item);
            if(!String.IsNullOrWhiteSpace(notes))text=(text.Length>0?text+"\n\n":"")+notes;
            text=AgentReading.NormalizeSourceText(text);
            if(text.Length>AgentReading.MaxReadableChars){text=text.Substring(0,AgentReading.MaxReadableChars);source.capped=true;}
            source.text=text;source.readable=!String.IsNullOrWhiteSpace(text);
            if(!source.readable&&String.IsNullOrEmpty(source.reason))source.reason="无可读正文或笔记（文件缺失、过大或格式不支持）";
            source.digest=AgentReading.SourceDigest(source.id,text,source.capped);
            return source;
        }
        static string AgentReadNotes(Entry item){
            var parts=new List<string>();
            if(!String.IsNullOrWhiteSpace(item.notes))parts.Add(item.notes);
            foreach(var annotation in (item.annotations??new List<Annotation>()).Take(40)){
                if(annotation==null)continue;
                string text=(annotation.text??"")+"\n"+(annotation.note??"");
                if(!String.IsNullOrWhiteSpace(text))parts.Add(text);
            }
            return String.Join("\n",parts);
        }
        static string AgentNormalizeText(string text){return String.IsNullOrEmpty(text)?"":text.Replace("\r\n","\n").Replace('\r','\n').TrimEnd();}
        sealed class AgentPageSlice {public int start,end,total;public bool more;}
        // Deterministic page windows. offset is a per-source character offset, so one next_offset can
        // continue every requested source. A single-source page backs up to a line break (at most 160
        // chars) so a line/number at the page boundary is not split; multi-source pages stay exact.
        static List<AgentPageSlice> AgentPageSlices(List<AgentReadableSource> sources,int offset,int per){
            var pages=new List<AgentPageSlice>();bool single=sources.Count==1;
            foreach(var source in sources){
                int length=source.Length,start=Math.Min(offset,length),end=Math.Min(length,start+Math.Max(2,per));
                // A shared offset may fall inside a pair in another source. Re-read the whole pair;
                // the small overlap is explicit in readStart/readEnd and never leaves a coverage gap.
                if(start>0&&start<length&&Char.IsLowSurrogate(source.text[start])&&Char.IsHighSurrogate(source.text[start-1]))start--;
                if(single&&end>start&&end<length){
                    int searchStart=Math.Max(start,end-160);
                    if(searchStart<end){int newline=source.text.LastIndexOf('\n',end-1,end-searchStart);if(newline>=searchStart)end=newline+1;}
                }
                if(end>start&&end<length&&Char.IsHighSurrogate(source.text[end-1])&&Char.IsLowSurrogate(source.text[end]))end--;
                if(end<start)end=start;
                pages.Add(new AgentPageSlice{start=start,end=end,total=length,more=end<length});
            }
            return pages;
        }
        static int AgentPageNextOffset(List<AgentReadableSource> sources,List<AgentPageSlice> pages,int offset,int per){
            bool more=false;foreach(var page in pages)if(page.more)more=true;
            if(!more)return -1;
            if(sources.Count==1)return pages[0].end;
            // The shortest safe end wins; offset+per could skip a high surrogate from another source.
            return pages.Where(page=>page.more).Min(page=>page.end);
        }
        static string RenderAgentReadPage(List<AgentReadableSource> sources,List<AgentPageSlice> pages,int offset,int per,int maxChars,int requestedCount,List<string> skippedLabels){
            int next=AgentPageNextOffset(sources,pages,offset,per),remaining=0;
            foreach(var page in pages)if(page.more)remaining++;
            var builder=new StringBuilder();
            builder.Append("read_items exact · Coverage: ").Append(sources.Count).Append('/').Append(requestedCount).Append(" sources");
            builder.Append(" · offset=").Append(offset).Append(" · max_chars=").Append(maxChars).Append(" · per_source=").Append(per);
            builder.Append(" · has_more=").Append(next>=0?"true":"false").Append(" · next_offset=").Append(next);
            if(skippedLabels.Count>0)builder.Append("\n[SKIPPED] ").Append(String.Join("；",skippedLabels));
            for(int n=0;n<sources.Count;n++){
                var source=sources[n];var page=pages[n];
                builder.Append("\n\n[S").Append(n+1).Append("] ").Append(AgentReading.DisplayName(source.name,80)).Append(" · id=").Append(source.id);
                builder.Append(" · chars ").Append(page.start).Append("-").Append(page.end).Append(" of ").Append(page.total);
                builder.Append(" · full=").Append(page.start==0&&page.end==page.total?"true":"false");
                builder.Append(" · has_more=").Append(page.more?"true":"false").Append(" · next_offset=").Append(page.more?page.end:-1).Append('\n');
                if(page.end>page.start)builder.Append(source.text.Substring(page.start,page.end-page.start));
                else builder.Append(offset>=source.Length?"(offset is past this source's end; no new text in this page)":"(no new text in this page)");
                if(page.more)builder.Append("\n[尚未读取 / NOT READ YET] chars ").Append(page.end).Append("-").Append(page.total).Append(" of this source were not returned in this page.");
            }
            if(next>=0)builder.Append("\n\n[尚未读取 / NOT READ YET] ").Append(remaining).Append(" of ").Append(sources.Count).Append(" sources still have unread exact text. This page is exact contiguous source text (no keyword sampling). Continue with read_items{ids:[...], offset=").Append(next).Append("} until has_more=false before claiming a full-source read.");
            else builder.Append("\n\n[READ END] has_more=false: every requested source was returned to its end (or the offset is past its end).");
            builder.Append("\nCoverage: ").Append(sources.Count).Append('/').Append(requestedCount).Append(" sources; exact contiguous text, not sampled; ");
            var parts=new List<string>();
            for(int n=0;n<sources.Count;n++){
                var source=sources[n];var page=pages[n];string name=AgentReading.DisplayName(source.name,40);
                bool full=page.start==0&&page.end==page.total;
                parts.Add(full?name+" full":name+" partially read "+page.start+"-"+page.end+" of "+page.total);
            }
            builder.Append(String.Join("; ",parts)).Append('.');
            return builder.ToString();
        }
        // read_items exact implementation, called by AgentTools.ReadAgentTool in place of the old
        // BuildKnowledgeContext sampling branch. Scope is validated before this call by the tool layer
        // against the effective task scope (original sources plus artifacts created by this task).
        internal Dictionary<string,object> ReadAgentItemsExact(AgentTaskState state,AgentCall call){
            var args=call!=null&&call.args!=null?call.args:new Dictionary<string,object>();
            var ids=AgentTaskState.Get<List<string>>(args,"ids");
            if(ids==null||ids.Count==0||ids.Count>8||ids.Distinct().Count()!=ids.Count)throw new InvalidDataException("read_items 需要 1 至 8 个不重复的资料 ID。");
            int offset=AgentDocumentTools.Number(args,"offset",0,0,AgentReading.MaxOffset);
            int maxChars=AgentDocumentTools.Number(args,"max_chars",AgentReading.DefaultMaxChars,AgentReading.MinMaxChars,AgentReading.MaxMaxChars);
            // Share this turn's result budget across queued reads. Otherwise three parallel exact
            // reads can each record 12k of coverage while CallContents only sends 14k in total.
            if(state!=null&&state.options!=null){
                int used=0,pending=1;
                if(call!=null&&!String.IsNullOrEmpty(call.turnId)){
                    var round=state.calls.Where(c=>c.turnId==call.turnId).ToList();
                    used=round.Where(c=>c!=call&&c.status=="done"&&c.result!=null&&c.result.ContainsKey("text")).Sum(c=>(Convert.ToString(c.result["text"])??"").Length);
                    pending=Math.Max(1,round.Count(c=>(c.tool=="read_items"||c.tool=="read_document")&&(c.status=="queued"||c.status=="running")));
                }
                maxChars=Math.Min(maxChars,Math.Max(AgentReading.MinMaxChars,(state.options.budget-512-used)/pending));
            }
            var sources=new List<AgentReadableSource>();var skipped=new List<string>();var skippedLabels=new List<string>();
            foreach(string id in ids){
                var source=AgentReadSourceText(id);
                if(source.readable&&!source.removed)sources.Add(source);
                else{
                    skipped.Add(id);
                    skippedLabels.Add(AgentReading.DisplayName(source.name.Length==0?id:source.name,32)+" ["+id+"]: "+(String.IsNullOrEmpty(source.reason)?"无法读取正文或笔记":source.reason));
                }
            }
            if(sources.Count==0)throw new InvalidDataException("这些资料没有可读取的正文或笔记："+String.Join("；",skippedLabels));
            int per=Math.Max(1,(maxChars-(220+260*sources.Count))/sources.Count);
            var included=new List<AgentReadableSource>(sources);var omitted=new List<AgentReadableSource>();
            List<AgentPageSlice> pages=null;string text="";
            for(int attempt=0;attempt<4;attempt++){
                pages=AgentPageSlices(included,offset,per);
                text=RenderAgentReadPage(included,pages,offset,per,maxChars,ids.Count,skippedLabels);
                int overflow=text.Length-maxChars;
                if(overflow<=0||per<=1)break;
                int reduce=Math.Max(1,(overflow+included.Count-1)/included.Count);
                if(per-reduce<1)reduce=per-1;
                per-=reduce;
            }
            // Hard cap (#18). The normal renderer includes every readable source; when metadata plus text still
            // exceed max_chars the page drops sources from the end (keeping at least one) and names them so the
            // next call with the same offset continues them. A minimal renderer guarantees the cap even at 200.
            var labels=new List<string>(skippedLabels);
            while(text.Length>maxChars&&included.Count>1){
                var dropped=included[included.Count-1];included.RemoveAt(included.Count-1);omitted.Add(dropped);
                labels=BudgetLabels(skippedLabels,omitted,offset);
                pages=AgentPageSlices(included,offset,per);
                text=RenderAgentReadPage(included,pages,offset,per,maxChars,ids.Count,labels);
            }
            int normalNext=AgentPageNextOffset(included,pages,offset,per);
            bool omittedUnread=omitted.Any(source=>offset<source.Length);
            int next=omittedUnread?offset:normalNext;
            if(text.Length>maxChars){
                labels=BudgetLabels(skippedLabels,omitted,offset);
                text=TrimToBudget(RenderMinimalAgentReadPage(included,pages,offset,per,maxChars,ids.Count,labels,next),maxChars);
            }
            var coverage=new List<Dictionary<string,object>>();var references=new List<ChatReference>();var openings=new List<Dictionary<string,object>>();
            foreach(var source in sources)openings.Add(new Dictionary<string,object>{{"id",source.id},{"name",AgentReading.DisplayName(source.name,80)},{"text",source.text.Substring(0,Math.Min(AgentReading.OpeningChars,source.Length))}});
            for(int n=0;n<included.Count;n++){
                var source=included[n];var page=pages[n];
                bool full=page.start==0&&page.end==page.total,more=page.end<source.Length;
                coverage.Add(new Dictionary<string,object>{
                    {"id",source.id},{"name",AgentReading.DisplayName(source.name,80)},{"totalChars",source.Length},
                    {"readStart",page.start},{"readEnd",page.end},{"readChars",page.end-page.start},
                    {"full",full},{"hasMore",more},{"nextOffset",more?page.end:-1},
                    {"digest",source.digest},{"mode","exact"},{"sampled",!full},{"capped",source.capped},
                    {"domain",AgentReading.DomainReadable},{"offset",offset},{"pageMax",per}
                });
                string excerpt=page.end>page.start?source.text.Substring(page.start,Math.Min(280,page.end-page.start)):source.text.Substring(0,Math.Min(160,source.Length));
                references.Add(new ChatReference{id="S"+(n+1),fileId=source.id,label=AgentReading.DisplayName(source.name,80),excerpt=excerpt});
            }
            foreach(var source in omitted){
                int start=Math.Min(Math.Max(0,offset),source.Length);bool more=start<source.Length;
                coverage.Add(new Dictionary<string,object>{
                    {"id",source.id},{"name",AgentReading.DisplayName(source.name,80)},{"totalChars",source.Length},
                    {"readStart",start},{"readEnd",start},{"readChars",0},{"full",false},
                    {"hasMore",more},{"nextOffset",more?start:-1},
                    {"digest",source.digest},{"mode","exact"},{"sampled",true},{"capped",source.capped},
                    {"domain",AgentReading.DomainReadable},{"offset",offset},{"pageMax",per},{"budgetSkipped",true}
                });
            }
            return new Dictionary<string,object>{
                {"text",text},{"coverage",coverage},{"references",references},{"openings",openings},{"skipped",skipped},
                {"mode","exact"},{"domain",AgentReading.DomainReadable},{"offset",offset},{"max_chars",maxChars},{"per_source",per},
                {"total_chars",sources.Sum(source=>source.Length)},{"has_more",next>=0},{"next_offset",next},
                {"page_sources",included.Count},{"requested_sources",ids.Count},{"budget_skipped",omitted.Select(source=>source.id).ToList()}
            };
        }
        static List<string> BudgetLabels(List<string> skippedLabels,List<AgentReadableSource> omitted,int offset){
            var labels=new List<string>(skippedLabels);
            foreach(var source in omitted){
                int start=Math.Min(Math.Max(0,offset),source.Length);
                labels.Add("预算未包含 "+AgentReading.DisplayName(source.name.Length==0?source.id:source.name,28)+" ["+source.id+"]，本页因 max_chars 未包含该来源；可继续 offset="+start+"（has_more="+(start<source.Length?"true":"false")+"）");
            }
            return labels;
        }
        // Absolute-cap fallback renderer: one source header plus as much text as the remaining budget allows.
        static string RenderMinimalAgentReadPage(List<AgentReadableSource> sources,List<AgentPageSlice> pages,int offset,int per,int maxChars,int requestedCount,List<string> skippedLabels,int next){
            if(sources.Count==0)return "";
            var source=sources[0];var page=pages[0];
            var builder=new StringBuilder();
            builder.Append("read_items exact;offset=").Append(offset).Append(";max_chars=").Append(maxChars).Append(";has_more=").Append(next>=0?"true":"false").Append(";next_offset=").Append(next);
            foreach(string label in skippedLabels){
                string line="\n[SKIPPED] "+label;
                if(builder.Length+line.Length>maxChars-12)break;
                builder.Append(line);
            }
            builder.Append("\n[S1] ").Append(AgentReading.DisplayName(source.name,32)).Append(" ").Append(source.id).Append(" chars ").Append(page.start).Append("-").Append(page.end).Append('/').Append(page.total).Append('\n');
            int room=maxChars-builder.Length;
            if(room>0&&page.end>page.start){
                int take=Math.Min(room,page.end-page.start);
                if(take>0&&Char.IsHighSurrogate(source.text[page.start+take-1]))take--;
                if(take>0)builder.Append(source.text.Substring(page.start,take));
            }
            return builder.ToString();
        }
        static string TrimToBudget(string text,int maxChars){
            if(text==null)return "";
            if(text.Length<=maxChars)return text;
            int end=Math.Max(0,maxChars);
            if(end>0&&end<text.Length&&Char.IsHighSurrogate(text[end-1]))end--;
            return text.Substring(0,end);
        }
    }
}
