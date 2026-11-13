using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace kok {
    // Host-owned delivery acceptance requirement (v0.12.0 P0-3, v0.12.1 C1, v0.12.2 C-A). The requirement is a
    // projection of the single CURRENT task contract (AgentTaskIntent.Current): goal plus every mid-task
    // adjustment applied in user-message order. It is persisted inside AgentTaskOptions as the task-start
    // baseline (Replay stability) but every live consumer re-derives it, so the latest requirement always wins.
    public class AgentDeliveryRequirement {
        public string kind="read";                  // read | document | edit | presentation
        public bool requiresArtifact;               // the current contract demands at least one real library file
        public bool artifactIsPresentation;         // the single delivered artifact must be a PPTX deck
        public int requestedPages;                  // 0 = page count not specified
        public bool requiresFullSourceCoverage;     // "全文/全部/完整/每份" goals (cancelable)
        public bool requiresTargetedEdit;           // "把 X 改成 Y" goals (cancelable)
        public List<string> requiredSourceIds=new List<string>();
        public bool requiresDocumentArtifact;
        public bool requiresPresentationArtifact;
        public bool chatOnly;
        public int minChars,maxChars;               // numeric length bounds (unit in lengthUnit)
        // v0.12.2 C-A:
        public AgentMeasureUnit lengthUnit=AgentMeasureUnit.None; // Hanzi/Characters/Words
        public string requiredFormat="";            // "" / "MD" / "DOCX" / "TXT"
        public int requiredCount=1;                 // explicit "两份/三份" deliverables
    }

    internal static class AgentDeliveryRules {
        internal static readonly string[] ArtifactTools={"create_document","create_presentation","copy_presentation"};
        internal static readonly string[] EditTools={"replace_document_text","edit_presentation"};
        internal static readonly string[] ReadBackTools={"read_items","read_document","read_presentation"};

        const RegexOptions Opt=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant;

        // Derive the host-verifiable requirement from the CURRENT task contract (review #1-#4, #6, #9, #10).
        internal static AgentDeliveryRequirement Derive(AgentTaskOptions o){return Derive(o,null);}
        internal static AgentDeliveryRequirement Derive(AgentTaskOptions o,IEnumerable<string> adjustments){
            var requirement=new AgentDeliveryRequirement();
            if(o==null)return requirement;
            var adjustmentList=(adjustments??Enumerable.Empty<string>()).Where(item=>!String.IsNullOrWhiteSpace(item)).ToList();
            try{
                AgentTaskContract contract=AgentTaskIntent.Current(o,adjustmentList);
                requirement.requiredSourceIds=contract.RequiredSourceIds.ToList();
                requirement.requiresDocumentArtifact=contract.RequiresDocument;
                requirement.requiresPresentationArtifact=contract.RequiresPresentation;
                requirement.requiresArtifact=contract.RequiresDocument||contract.RequiresPresentation;
                requirement.artifactIsPresentation=contract.RequiresPresentation&&!contract.RequiresDocument;
                requirement.requiresTargetedEdit=contract.RequiresTargetedEdit;
                requirement.requiresFullSourceCoverage=contract.RequiresFullSourceCoverage;
                requirement.chatOnly=contract.ChatOnly;
                requirement.requestedPages=contract.RequestedPages;
                requirement.minChars=contract.MinLength;
                requirement.maxChars=contract.MaxLength;
                requirement.lengthUnit=contract.MinLength>0||contract.MaxLength>0?contract.LengthUnit:AgentMeasureUnit.None;
                requirement.requiredFormat=contract.RequiresDocument?contract.RequiredDocumentFormat:"";
                requirement.requiredCount=contract.RequiredCount<1?1:contract.RequiredCount;
                requirement.kind=contract.RequiresDocument?"document":contract.RequiresPresentation?"presentation":contract.RequiresTargetedEdit?"edit":"read";
                // discussion/read-only tasks never require an artifact or a body edit; the source scope stays.
                if(o.intent=="discuss"||o.approvalPolicy=="read_only"){
                    requirement.requiresDocumentArtifact=false;
                    requirement.requiresPresentationArtifact=false;
                    requirement.requiresArtifact=false;
                    requirement.artifactIsPresentation=false;
                    requirement.requiresTargetedEdit=false;
                    requirement.chatOnly=false;
                    requirement.requestedPages=0;
                    requirement.requiredFormat="";
                    requirement.minChars=0;requirement.maxChars=0;
                    requirement.lengthUnit=AgentMeasureUnit.None;
                    requirement.kind="read";
                }
                return requirement;
            }catch{
                var fallback=new AgentTaskOptions{goal=o.goal,allowed=o.allowed,intent=o.intent,approvalPolicy=o.approvalPolicy};
                var allowed=(o.allowed??new List<string>()).Where(id=>!String.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
                requirement.requiredSourceIds=allowed;
                requirement.requiresFullSourceCoverage=AgentTaskIntent.Actions(AgentTaskIntent.Combined(fallback.goal,adjustmentList)).IndexOf("全文",StringComparison.Ordinal)>=0;
                return requirement;
            }
        }

        // Kept for callers that only need the page count; the ordered parsing lives in AgentTaskIntent.
        internal static int RequestedPages(string text){return AgentTaskIntent.RequestedPages(text);}

        internal static string UnitName(AgentMeasureUnit unit){
            switch(unit){
                case AgentMeasureUnit.Characters:return "字符";
                case AgentMeasureUnit.Words:return "英文单词";
                case AgentMeasureUnit.Hanzi:return "汉字";
                default:return "汉字";
            }
        }

        // Body length in Chinese characters after removing Markdown markers (legacy meter, unit=Hanzi).
        internal static int MeasuredChars(string text){return MeasuredLength(text,AgentMeasureUnit.Hanzi);}

        // Measure the delivered body with the requested unit (review #9): Hanzi counts CJK characters,
        // Characters counts non-whitespace characters after removing Markdown markers, Words counts English
        // word tokens.
        internal static int MeasuredLength(string text,AgentMeasureUnit unit){
            if(String.IsNullOrWhiteSpace(text))return 0;
            try{
                string cleaned=StripMarkdown(text);
                switch(unit){
                    case AgentMeasureUnit.Characters:{
                        int count=0;
                        foreach(char c in cleaned)if(!Char.IsWhiteSpace(c))count++;
                        return count;}
                    case AgentMeasureUnit.Words:{
                        return Regex.Matches(cleaned,@"[A-Za-z]+(?:['’\-][A-Za-z]+)*",RegexOptions.CultureInvariant).Count;}
                    case AgentMeasureUnit.Hanzi:
                    case AgentMeasureUnit.None:
                    default:{
                        int count=0;
                        foreach(char c in cleaned)if(c>='\u4e00'&&c<='\u9fff')count++;
                        return count;}
                }
            }catch{return 0;}
        }
        static string StripMarkdown(string text){
            string value=text;
            value=Regex.Replace(value,@"(?m)^\s*```[^\r\n]*$"," ");
            value=Regex.Replace(value,@"(?m)^\s{0,3}(?:>\s?)+"," ");
            value=Regex.Replace(value,@"(?m)^\s{0,3}#{1,6}\s*"," ");
            value=Regex.Replace(value,@"(?m)^\s{0,3}(?:[-*+]|\d{1,3}[.)])\s+"," ");
            value=Regex.Replace(value,@"(?m)^\s*\|?[\s:|-]*-[\s:|-]*\|?\s*$"," ");
            value=Regex.Replace(value,@"!?\[([^\]]*)\]\([^)]*\)","$1");
            value=Regex.Replace(value,@"\[([^\]]+)\]\[[^\]]*\]","$1");
            value=Regex.Replace(value,@"<[^>\r\n]{1,120}>"," ");
            value=Regex.Replace(value,@"`{1,3}"," ");
            value=Regex.Replace(value,@"[*_~]{1,3}"," ");
            value=Regex.Replace(value,@"\|"," ");
            value=Regex.Replace(value,@"\\([\\`*_{}\[\]()#+\-.!|>~])","$1");
            return value;
        }

        internal static string RangeText(AgentDeliveryRequirement requirement){
            if(requirement==null)return "";
            string unit=UnitName(requirement.lengthUnit);
            if(requirement.minChars>0&&requirement.maxChars>0)return requirement.minChars+"-"+requirement.maxChars+" 个"+unit;
            if(requirement.minChars>0)return "不少于 "+requirement.minChars+" 个"+unit;
            if(requirement.maxChars>0)return "不超过 "+requirement.maxChars+" 个"+unit;
            return "";
        }
        internal static string LengthRangeText(AgentDeliveryRequirement requirement){return RangeText(requirement);}

        // Compact guidance appended to the task instructions. It reads the SAME current contract as the
        // acceptance layer, so it never hints at replaced/cancelled requirements (review #2/#3).
        internal static string Instructions(AgentTaskState task){
            if(task==null||task.options==null)return "";
            AgentTaskContract contract;AgentDeliveryRequirement requirement;
            try{contract=AgentTaskIntent.Current(task.options,task.adjustments);requirement=Derive(task.options,task.adjustments);}
            catch{return "";}
            if(contract==null||requirement==null)return "";
            var parts=new List<string>();
            if(contract.ChatOnly){
                if(requirement.minChars>0||requirement.maxChars>0)
                    parts.Add("CHAT DELIVERY (host-verified): the user asked for a chat answer, so do NOT create or edit library files; the final chat answer (or the finish_task summary when longer) is measured and must be "+RangeText(requirement)+".");
                else
                    parts.Add("CHAT DELIVERY (host-verified): the user asked for a chat answer, so do NOT create or edit library files; answer directly in the chat.");
                if(contract.RequiresFullSourceCoverage&&contract.RequiredSourceIds.Count>0)parts.Add(FullCoverageInstruction());
                return parts.Count==0?"":" "+String.Join(" ",parts);
            }
            if(contract.RequiresDocument){
                string range=RangeText(requirement);
                string text="DELIVERY ACCEPTANCE (host-verified): the CURRENT requirement needs a real library document"+
                    (String.IsNullOrEmpty(contract.RequiredDocumentFormat)?"":" in "+contract.RequiredDocumentFormat+" format")+
                    (contract.RequiredCount>1?" ("+contract.RequiredCount+" deliverables)":"")+
                    ". The FINAL deliverable is the last document this task creates: it must still exist and belong to this task"+
                    (range==""?"":", the measured body length must be "+range)+
                    ", and there must be a sufficient read-back of THAT file after its creation/last edit (read_items/read_document covering at least the first 8000 characters or the whole file).";
                parts.Add(text);
            }
            if(contract.RequiresPresentation){
                parts.Add("DELIVERY ACCEPTANCE (host-verified): the CURRENT requirement needs a real editable PPTX; the FINAL deliverable is the LAST deck this task creates and its actual slide count must be"+(contract.RequestedPages>0?" exactly "+contract.RequestedPages+" pages (including a cover)":" whatever the current requirement asks")+". A completed result needs a full-page read-back via read_presentation (cover every page; max_pages must reach the end) or a full-text read_items/read_document read-back of that same deck.");
            }
            if(contract.RequiresTargetedEdit)parts.Add("DELIVERY ACCEPTANCE (host-verified): this is a targeted revision; completed requires a successful, still-current replace_document_text/edit_presentation on the target AND a current read-back AFTER the edit (document coverage for docs, every page for decks). No automatic read-back means you must read the edited file yourself. Preserve unrelated text; never create a new file merely to revise one.");
            if(contract.RequiresFullSourceCoverage&&contract.RequiredSourceIds.Count>0)parts.Add(FullCoverageInstruction());
            if(parts.Count==0)return "";
            parts.Add("LATEST REQUIREMENT WINS: page count, length range/unit, format, source scope, deliverable kinds and edit/read requirements come from the newest user messages; replaced or cancelled requirements are NOT checked and must not be redone. If the host rejects completed, fix ONLY the item it names and keep everything already produced; skipped/undone/uncertain steps cannot be repaired, use partial instead.");
            return " "+String.Join(" ",parts);
        }
        internal static string FullCoverageInstruction(){return "FULL-SOURCE COVERAGE: the current requirement asks for complete sources. Use read_items/read_document with offset/max_chars (follow has_more/next_offset) until every required source is read to its end (presentation sources count through read_presentation page coverage); a sampled or partial read is NOT full coverage. recall_result does not add coverage.";}

        internal static string ArtifactToolHint(AgentDeliveryRequirement requirement){
            if(requirement==null)return "create_document";
            if(requirement.requiresDocumentArtifact&&requirement.requiresPresentationArtifact)return "create_document/create_presentation";
            if(requirement.requiresPresentationArtifact)return "create_presentation";
            return requirement.artifactIsPresentation?"create_presentation":"create_document";
        }
        internal static string ArtifactTypeText(AgentDeliveryRequirement requirement){
            if(requirement==null)return "成果";
            if(requirement.requiresDocumentArtifact&&requirement.requiresPresentationArtifact)return "文档与演示文稿";
            if(requirement.requiresPresentationArtifact)return "演示文稿";
            if(requirement.requiresDocumentArtifact)return "文档";
            return requirement.artifactIsPresentation?"演示文稿":"文档";
        }
    }

    public partial class AgentService {
        // Called by the host when the task starts and again by the state machine after each task.adjusted:
        // stores the derived requirement inside the persisted options (replay baseline) and marks an automatic
        // tool profile so a later adjustment can re-derive both delivery and tools. Never throws into the loop.
        internal void PrepareDelivery(AgentTaskOptions o){
            if(o==null)return;
            try{
                o.delivery=AgentDeliveryRules.Derive(o);
                if(o.allowedTools==null)o.allowedToolsAuto=true;
            }catch{
                o.delivery=new AgentDeliveryRequirement{requiredSourceIds=(o.allowed??new List<string>()).ToList()};
            }
        }
    }
}
