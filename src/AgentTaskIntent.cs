using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace kok {
    // Measurement unit of a requested length bound (v0.12.2 C-A / review #9). "字符" and "汉字" must never be
    // conflated: an all-English document of 149 characters is not 149 Chinese characters.
    // Public because AgentDeliveryRequirement (public, persisted in the task journal) exposes a lengthUnit field.
    public enum AgentMeasureUnit { None=0, Hanzi=1, Characters=2, Words=3 }

    // One "requirement operation" extracted from a single user message. Ops are replayed in order by
    // AgentTaskIntent.Current: a later op for the same property overrides/cancels the earlier state.
    internal sealed class AgentRequirementOp {
        // replace|document|presentation|pages|cancel-pages|length|cancel-length|format|sources|sources-all|
        // fullread|cancel-fullread|edit|cancel-edit|count|chat|cancel-document|cancel-presentation
        public string Kind="";
        public string Value="";
        public int Number;
        public int Min,Max;
        public AgentMeasureUnit Unit;
        public bool Negative;
    }

    // Parsed requirement operations of one user message (the original goal or one mid-task adjustment). The
    // summary fields are exposed for tests/diagnostics; `ops` is what Current replays in text order.
    internal sealed class AgentRequirementSegment {
        public List<AgentRequirementOp> ops=new List<AgentRequirementOp>();
        public bool ChatOnly;                       // reply in chat / do not create a file
        public int? Pages;                          // final page count of THIS message (last revision wins)
        public bool ReplaceDeliverableKind;         // 改成/改为/换成 + another deliverable kind
        public string DocumentFormat="";            // "" / "MD" / "DOCX" / "TXT"
        public int? LengthMin,LengthMax;public AgentMeasureUnit LengthUnit; // final length values of THIS message
        public bool CancelLength;
        public bool FullRead,CancelFullRead;        // complete read / only-beginning
        public List<string> ExclusiveSourceIds=new List<string>(); // resolved IDs when allowed is known, else ordinal keys
        public bool AllSources;
        public bool CancelEdit;
        public bool NegatesDocument,NegatesPresentation; // object/version-scoped negations (positive same kind wins)
        public string NegationScope="";             // 旧版/新版/3页/... (diagnostics only)
        // v0.12.2 helpers (not part of the public contract, used to build ops):
        public bool Document,Presentation;          // positive deliverable requests in THIS message
        public string ReplaceTarget="";             // "document"/"presentation" target of a replacement
        public bool Edit;
    }

    // The CURRENT effective task contract: goal + every adjustment applied in user-message order. Tool
    // availability, prompts, source scope and delivery acceptance all read this one state (review #1-#4, #6).
    internal sealed class AgentTaskContract {
        public bool RequiresDocument,RequiresPresentation;
        public string RequiredDocumentFormat="";
        public int RequestedPages;                  // 0 = unspecified
        public int MinLength,MaxLength;public AgentMeasureUnit LengthUnit;
        public bool RequiresTargetedEdit;public string EditVersionHint="";
        public bool RequiresFullSourceCoverage;
        public List<string> RequiredSourceIds=new List<string>();
        public bool ChatOnly;
        public int RequiredCount=1;
    }

    internal static class AgentTaskIntent {
        const RegexOptions Opt=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant;

        // ---- shared pattern fragments -----------------------------------------------------------
        // English tokens use ASCII-adjacency assertions instead of \b: .NET treats CJK characters as word
        // characters, so \bppt\b fails in "制作8页PPT。"
        const string PresToken="(?:(?<![A-Za-z])pptx?(?![A-Za-z])|(?<![A-Za-z])powerpoint(?![A-Za-z])|power\\s*point|(?<![A-Za-z])presentations?(?![A-Za-z])|(?<![A-Za-z])slides?(?![A-Za-z])|幻灯片|演示文稿|演示稿|课件|演示)";
        const string DocToken="(?:报告|文档|文件|文章|纪要|总结|摘要|笔记|方案|简报|讲稿|说明|手册|备忘|记录|表格|周报|日报|月报|计划书|稿件|清单|report|document|file|essay|summary|memo|brief|notes?|article|proposal|spreadsheet|manuscript)";

        static readonly Regex NegatedClause=new Regex(
            @"(?:不要|请勿|无需|无须|不必|不得|禁止|不再|避免|勿|不用|不需要|不做|不创建|不新建|不生成|不保存|不写|不加|不改|不删|别(?=[做创写用生保改加删重忘弄动提再出换描])"+
            @"|\bdo\s+not\b|\bdoes\s+not\b|\bdon['’]t\b|\bnever\b|\bwithout\b|\bno\s+need\s+to\b)"+
            @"[^。！？；;，,\.\r\n]*?(?=而是|但是|但要|instead|\bbut\b|\bhowever\b|[。！？；;，,\.\r\n]|$)",
            Opt|RegexOptions.Compiled);
        static readonly Regex PresentationNoun=new Regex("(?<![A-Za-z])pptx?(?![A-Za-z])|(?<![A-Za-z])powerpoint(?![A-Za-z])|power\\s*point|(?<![A-Za-z])presentations?(?![A-Za-z])|(?<![A-Za-z])slides?(?![A-Za-z])|幻灯|演示文稿|演示稿|课件|演示",Opt|RegexOptions.Compiled);
        static readonly Regex PresentationObject=new Regex(
            @"(?:生成|制作|创建|新建|做|写|整理|汇总|输出|设计|准备|出|要|需要|来)\s*(?:成|为)?\s*(?:一份|一个|一|个|套|几个)?\s*(?:\d{1,3}\s*(?:页|张|片)\s*(?:的)?\s*)?(?:[一二三四五六七八九十两]{1,3}\s*(?:页|张|片)\s*(?:的)?\s*)?"+PresToken,
            Opt|RegexOptions.Compiled);
        static readonly Regex ConvertPattern=new Regex(@"转成|转换为|转一份|转成一份|变成|做成一份|做成|转|convert|turn\s+(?:it|this)\s+into|导出为|导出成|export",Opt|RegexOptions.Compiled);
        // "改成/改为/换成/变成/转成/做成 + another deliverable noun": replacement, not addition.
        static readonly Regex ReplaceKind=new Regex(
            @"(?:改成|改为|换成|换为|变成|变更为|转成|转换为|做成)\s*[^。！？；;，,\.\r\n]{0,16}?"+PresToken,
            Opt|RegexOptions.Compiled);
        static readonly Regex ReplaceDocKind=new Regex(
            @"(?:改成|改为|换成|换为|变成|变更为|转成|转换为|做成)\s*[^。！？；;，,\.\r\n]{0,16}?"+DocToken,
            Opt|RegexOptions.Compiled);
        static readonly Regex DocumentNoun=new Regex(DocToken,Opt|RegexOptions.Compiled);
        // Creation verb BOUND to a document noun (bounded gap, no extra clause): "生成一份报告"/"保存为一个新的
        // 文档" create a document, while "修改这份现有报告…保存后核对" is a revision, not a new artifact.
        static readonly Regex DocumentObject=new Regex(
            @"(?:创建|新建|生成|制作|编写|起草|撰写|整理成|汇总成|输出|导出|保存(?:为|成|到)?|做|写|(?<![A-Za-z])creat(?:e|es|ing|ed)(?![A-Za-z])|(?<![A-Za-z])generat(?:e|es|ing|ed)(?![A-Za-z])|(?<![A-Za-z])writ(?:e|es|ing|ten)(?![A-Za-z])|(?<![A-Za-z])mak(?:e|es|ing)(?![A-Za-z])|(?<![A-Za-z])draft(?:s|ing|ed)?(?![A-Za-z])|(?<![A-Za-z])compos(?:e|es|ing)(?![A-Za-z])|(?<![A-Za-z])produc(?:e|es|ing)(?![A-Za-z])|(?<![A-Za-z])sav(?:e|es|ed|ing)(?![A-Za-z]))\s*(?:一份|一个|一|个|套|几个)?\s*(?:新|另一|其他|别的)?\s*(?:的)?\s*[^。！？；;，,\.\r\n]{0,12}?"+DocToken,
            Opt|RegexOptions.Compiled);
        static readonly Regex CreationCue=new Regex(
            @"创建|新建|生成|制作|编写|起草|撰写|写|整理|汇总|输出|保存|导出|做|create|make|build|generate|write|draft|compose|produce|save|export|prepare",Opt|RegexOptions.Compiled);
        static readonly Regex InlineOnly=new Regex(@"一段|几句|一条|简要|简短|简单说明|概括一下|概述一下",Opt|RegexOptions.Compiled);
        static readonly Regex InlineSave=new Regex(@"保存|新建|创建|生成|制作|输出|资料库|文档库|文件|另存|导出|存档|save|export",Opt|RegexOptions.Compiled);
        static readonly Regex MultiDeliverable=new Regex(@"并|同时|以及|另外|另|还要|还需|再|和|、|\band\b|\bplus\b|\balso\b|\bas\s+well\s+as\b",Opt|RegexOptions.Compiled);
        static readonly Regex BodyVerb=new Regex(
            @"修改|改成|改为|改到|改掉|改一下|替换|修订|更正|编辑|改写|重写|润色|排版|扩写|精简|缩短|删除|删掉|删去|插入|追加|增加|添加|补充|更新|调整|续写|replace|revise|edit|change|update|remove|delete|insert|append|rewrite|polish|adjust",Opt|RegexOptions.Compiled);
        static readonly Regex StrongBodyVerb=new Regex(
            @"修改|改成|改为|改到|改掉|替换|修订|更正|删除|删掉|删去|插入|重写|改写|润色|排版|扩写|精简|缩短|replace|revise|rewrite",Opt|RegexOptions.Compiled);
        static readonly Regex MetadataNoun=new Regex(
            @"标签|分类|收藏|备注|笔记|重命名|文件名|移入|移出|移动到|归档|整理到|\bnotes?\b|\bmemos?\b|\btags?\b|\bcollections?\b|\bfavorites?\b",Opt|RegexOptions.Compiled);
        static readonly Regex MetadataContentNoun=new Regex(
            @"正文|内容|文字|段落|预算|人数|金额|数字|日期|开头|结尾|章节|句子|表格|第\s*\d+\s*(?:段|节|行|章)",Opt|RegexOptions.Compiled);
        static readonly Regex BodyContentNoun=new Regex(
            @"正文|内容|文字|段落|预算|人数|金额|数字|日期|标题|开头|结尾|章节|句子|表格|第\s*\d+\s*(?:段|节|行|章)",Opt|RegexOptions.Compiled);
        static readonly Regex TargetReference=new Regex(
            @"这份|该份|上述|刚才|现有|已有|原有|原文|文档|报告|草稿|演示文稿|幻灯片|第\s*\d+\s*(?:页|段|章|节|行)|文中|里面|其中|existing|current|this\s+(?:document|file|draft|report|presentation|deck|note)",Opt|RegexOptions.Compiled);
        static readonly Regex ContentCue=new Regex(
            @"正文|内容|文字|段落|预算|人数|金额|数字|日期|标题|开头|结尾|章节|句子|表格|报告|文档|文章|总结|摘要|方案|讲稿|备注",Opt|RegexOptions.Compiled);
        static readonly Regex FullSource=new Regex(
            @"全文|全部|完整|每份|每一份|每个来源|所有资料|所有来源|所有文档|所有文件|逐字|逐份|通读|读全|精读|全文覆盖|完整阅读|完整读取|详细阅读|\bfull[- ]?text\b|\bcomplete\b|\bentire\b|\bwhole\b",Opt|RegexOptions.Compiled);
        static readonly Regex SourceReference=new Regex(
            @"资料|文档|文件|来源|材料|素材|报告|source|sources|documents?|files?|materials?|report",Opt|RegexOptions.Compiled);
        static readonly Regex ScopedSample=new Regex(
            @"选一份|选取一份|挑选一份|其中一份|任选一份|简要摘要|简短摘要|简要概括|只概括|只要摘要|只摘要|只要大意|简要说明|简述|brief\s+summary|brief\s+overview|pick\s+one|one\s+of\s+the\s+(?:sources|documents)",Opt|RegexOptions.Compiled);
        static readonly Regex ChatCue=new Regex(
            @"(?:聊天|对话|会话)(?:里|中|内)?(?:直接)?(?:回复|回答|说明|给出|告诉|发)|直接(?:在)?(?:聊天|对话|会话)|(?:仅|只)(?:在)?(?:聊天|对话|会话)|(?:仅|只)回复|直接回复|回复即可|(?:reply|answer|respond)\s+(?:only\s+)?in\s+(?:the\s+)?(?:chat|conversation)|chat[- ]only|just\s+(?:reply|answer)|reply\s+directly|answer\s+directly",Opt|RegexOptions.Compiled);
        static readonly Regex NoFileCue=new Regex(
            @"(?:不要|不用|不需要|无需|无须|不必|别|勿)(?:再)?(?:创建|新建|生成|保存|输出|写|导出|做)(?:为|成|到)?(?:任何|一个|一份)?(?:新)?(?:的)?(?:文件|文档|报告|成果|成品)|(?:do\s+not|don['’]t|never|without|no\s+need\s+to)\s+(?:create|save|write|export|produce)\s+(?:a\s+|an\s+|any\s+)?(?:new\s+)?(?:file|document|report|artifact)|不需要(?:任何)?文件|不用文件|(?:no|without)\s+(?:new\s+)?(?:file|document|artifact)",Opt|RegexOptions.Compiled);
        static readonly Regex ChatWriteCue=new Regex(
            @"修改|改成|改为|替换|修订|编辑|改写|重写|润色|排版|删除|插入|创建|新建|保存|另存|导出|写入|更新|\breplace\b|\brevise\b|\bedit\b|\brewrite\b|\bremove\b|\bdelete\b|\binsert\b|\bupdate\b|\bcreate\b|\bsave\b|\bexport\b",Opt|RegexOptions.Compiled);
        static readonly Regex ArtifactCreateCue=new Regex(
            @"创建|新建|生成|制作|保存|导出|写|整理|汇总|输出|create|make|build|generate|save|export|write|draft|compose|produce",Opt|RegexOptions.Compiled);

        // Negation that removes a DELIVERABLE (object/version scoped; #6). Only creation verbs count so
        // "不要修改现有报告，创建一份新的摘要文档" does not cancel the later document.
        static readonly Regex NegatedPresentation=new Regex(
            @"(?:不要|不用|不需要|无需|无须|不必|别|勿)(?:再)?(?:做|创建|新建|生成|制作|写|输出|整理|转|导出|弄|搞)(?:任何|一个|一份)?(?:新|另一|其他|别的)?(?:的)?[^。！？；;，,\.\r\n]{0,12}?"+PresToken+
            @"|(?:do\s+not|don['’]t|never)\s+(?:make|create|build|generate|write|produce|do)\s+(?:a\s+|an\s+|any\s+)?(?:new\s+)?(?:(?<![A-Za-z])pptx?(?![A-Za-z])|(?<![A-Za-z])presentations?(?![A-Za-z])|(?<![A-Za-z])slides?(?![A-Za-z])|deck)"+
            @"|without\s+(?:creat(?:e|ing)|mak(?:e|ing)|build(?:ing)?|generat(?:e|ing)|writ(?:e|ing)|produc(?:e|ing)|do(?:ing)?)\s+(?:a\s+|an\s+|any\s+)?(?:new\s+)?(?:(?<![A-Za-z])pptx?(?![A-Za-z])|(?<![A-Za-z])presentations?(?![A-Za-z])|(?<![A-Za-z])slides?(?![A-Za-z])|deck)",Opt|RegexOptions.Compiled);
        static readonly Regex NegatedDocument=new Regex(
            @"(?:不要|不用|不需要|无需|无须|不必|别|勿)(?:再)?(?:创建|新建|生成|保存|输出|写|导出|做|起草|撰写)(?:为|成|到)?(?:任何|一个|一份)?(?:新|另一|其他|别的)?(?:的)?[^。！？；;，,\.\r\n]{0,12}?(?:文件|文档|报告|文章|总结|摘要|成品)"+
            @"|(?:do\s+not|don['’]t|never)\s+(?:create|save|write|export|produce|make)\s+(?:a\s+|an\s+|any\s+)?(?:new\s+|another\s+)?(?:file|document|report|article|summary|artifact)"+
            @"|without\s+(?:creat(?:e|ing)|sav(?:e|ing)|writ(?:e|ing)|export(?:ing)?|produc(?:e|ing)|mak(?:e|ing))\s+(?:a\s+|an\s+|any\s+)?(?:new\s+|another\s+)?(?:file|document|report|article|summary|artifact)",Opt|RegexOptions.Compiled);
        static readonly Regex CancelDeliverable=new Regex(
            @"取消(?:这份|那份|当前的|旧的|新)?\s*(?:报告|文档|文件|PPT|幻灯片|演示文稿)|不要(?:这份|那份)?(?:报告|文档|文件|PPT|幻灯片|演示文稿)了?|(?:cancel|drop)\s+(?:the\s+)?(?:report|document|file|deck)",Opt|RegexOptions.Compiled);
        static readonly Regex NegationMarker=new Regex(
            @"(?:不要|请勿|无需|无须|不必|不得|禁止|不再|避免|勿|不用|不需要|不做|不创建|不新建|不生成|不保存|不写|不加|不改|不删|别(?=[做创写用生保改加删重忘弄动提再出换描]))|(?<![A-Za-z])do\s+not(?![A-Za-z])|(?<![A-Za-z])don['’]t(?![A-Za-z])|(?<![A-Za-z])never(?![A-Za-z])|(?<![A-Za-z])without(?![A-Za-z])|no\s+need\s+to",Opt|RegexOptions.Compiled);
        static readonly Regex CancelEditMarker=new Regex(
            @"先不要修改|先别修改|先不要改|先别改|暂时不要修改|暂时别改|暂不修改|暂缓修改|不要修改|别修改|不用修改|无需修改|不需要修改|不要改动|不用改|不改了|不要编辑|不用编辑|先不编辑|先分析|只分析|仅分析|只做分析|只要分析|只阅读|只读不写|分析即可|先不要动|先别动|暂时不要动",Opt|RegexOptions.Compiled);
        static readonly Regex CancelFullReadMarker=new Regex(
            @"只看开头|只看前面|只看前几页|只看一下|只读开头|只读前\s*\d*\s*页|读前\s*\d+\s*页|只看前\s*\d+\s*页|不用通读|不需要通读|不必通读|无需通读|不要通读|不用全文|不需要全文|不必全文|无需全文|不要全文|不用读全|不用读完|不必读全|不要读全|不用精读|不需要精读|初步意见|初步了解|初步看看|大致看看|大致浏览|粗略看看|粗略浏览|简单浏览|随便看看|只浏览|只看摘要|只看结论|看看开头|看个大概|读个大概",Opt|RegexOptions.Compiled);
        static readonly Regex FullReadMarker=new Regex(
            @"全文|全部|完整|每份|每一份|每个来源|所有资料|所有来源|所有文档|所有文件|逐字|逐份|通读|读全|精读|全文覆盖|完整阅读|完整读取|详细阅读|\bfull[- ]?text\b|\bcomplete\b|\bentire\b|\bwhole\b",Opt|RegexOptions.Compiled);
        static readonly Regex LengthCancel=new Regex(
            @"取消.{0,6}(?:字数|长度)|不限(?:制)?(?:字数|长度)|字数不限|长度不限|长度随意|不要(?:限制)?(?:字数|长度)|不需要(?:字数|长度)(?:限制|要求)?|不限制(?:字数|长度)|无字数要求|无长度要求|字数随意|篇幅随意|长度不限",Opt|RegexOptions.Compiled);
        static readonly Regex SourceExclusive=new Regex(@"(?<![A-Za-z])(?:只|仅|唯独|惟独|only|solely|exclusively)(?![A-Za-z])",Opt|RegexOptions.Compiled);
        static readonly Regex SourceAll=new Regex(
            @"全部|所有|全部资料|所有来源|改为全部|使用全部|all\s+(?:the\s+)?(?:sources?|documents?|files?|materials?)|every\s+(?:source|document|file)",Opt|RegexOptions.Compiled);
        static readonly Regex OrdinalSource=new Regex(@"第\s*([0-9]{1,2}|[一二三四五六七八九十两]{1,3})\s*(?:份|个|篇|条|项|本|册|套)");
        static readonly Regex EnglishOrdinalSource=new Regex(@"\bthe\s+([a-z]+(?:[- ][a-z]+)?)\s+(?:source|document|file|material|report|one)\b",Opt);
        static readonly Regex EnglishNumericSource=new Regex(@"\b(?:the\s+)?([0-9]{1,2})(?:st|nd|rd|th)\s+(?:source|document|file|material|report|one)\b",Opt);
        static readonly Regex PageCountPattern=new Regex(
            @"(?<!\d)(\d{1,3})\s*(?:页|张|片)|(?:pages?|slides?)\s*[:：=]?\s*(\d{1,3})|(\d{1,3})\s*(?:pages?|slides?)|([一二三四五六七八九十两]{1,3})\s*(?:页|张|片)",Opt|RegexOptions.Compiled);
        static readonly Regex CountPattern=new Regex(
            @"(\d{1,2}|[一二两三四五六七八九十]{1,3})\s*(?:份|个|篇)\s*(?:的)?\s*(?:报告|文档|文件|PPT|演示文稿|幻灯片|总结|摘要|文章|report|document|file|deck)",Opt|RegexOptions.Compiled);
        static readonly Regex FormatWord=new Regex(@"(?<![A-Za-z])(?:docx|word)(?![A-Za-z])|word\s*文档|文档\s*(?:格式\s*)?(?:为|是)?\s*word|WORD\s*格式",Opt|RegexOptions.Compiled);
        static readonly Regex FormatMarkdown=new Regex(@"(?<![A-Za-z])(?:markdown|md)(?![A-Za-z])|markdown\s*格式",Opt|RegexOptions.Compiled);
        static readonly Regex FormatTxt=new Regex(@"(?<![A-Za-z])txt(?![A-Za-z])|纯文本|plain\s*text",Opt|RegexOptions.Compiled);
        static readonly Regex VersionHint=new Regex(@"旧版|新版|最终版|修订版|定稿|草稿|初稿|第\s*\d+\s*版|(?<![A-Za-z])v\d+(?:\.\d+)?(?![A-Za-z])",Opt|RegexOptions.Compiled);

        // Length bounds. A bound is only created by an explicit range/min/max phrase (a bare "300字" target is
        // not treated as a hard bound), which keeps the acceptance layer from rejecting reasonable drafts.
        static readonly Regex LengthRange=new Regex(@"(\d{1,6})\s*(?:-|–|—|~|～|到|至)\s*(\d{1,6})\s*(?:个)?\s*(字符|汉字|单词|chars?|characters?|words?|字)",Opt|RegexOptions.Compiled);
        static readonly Regex LengthMin=new Regex(
            @"(?:(?:不少于|不低于|至少|最少|不小于)\s*(\d{1,6})\s*(?:个)?\s*(字符|汉字|单词|chars?|characters?|words?|字))|(?:(\d{1,6})\s*(?:个)?\s*(字符|汉字|单词|chars?|characters?|words?|字)\s*(?:以上|起))",Opt|RegexOptions.Compiled);
        static readonly Regex LengthMax=new Regex(
            @"(?:(?:不超过|不得超过|不能超过|不要超过|不多于|至多|最多|(?<!不)少于|(?<!不)不足)\s*(\d{1,6})\s*(?:个)?\s*(字符|汉字|单词|chars?|characters?|words?|字))|(?:(\d{1,6})\s*(?:个)?\s*(字符|汉字|单词|chars?|characters?|words?|字)\s*(?:以内|以下|之内))",Opt|RegexOptions.Compiled);

        // ---- old API kept for compatibility (single message / single call) --------------------------------

        // Remove negated clauses from a request: what remains is the set of requested actions.
        internal static string Actions(string text){
            if(String.IsNullOrWhiteSpace(text))return "";
            try{return NegatedClause.Replace(text," ");}catch{return text;}
        }

        // Goal plus every adjustment's positive actions, newline separated (prompts/tool categories).
        internal static string Combined(string goal,IEnumerable<string> adjustments){
            var builder=new StringBuilder(goal??"");
            if(adjustments!=null)foreach(string item in adjustments){
                if(String.IsNullOrWhiteSpace(item))continue;
                builder.Append('\n').Append(Actions(item));
            }
            return builder.ToString();
        }

        internal static bool IsPresentation(string text){
            if(String.IsNullOrWhiteSpace(text))return false;
            try{return PresentationNoun.IsMatch(Actions(text));}catch{return false;}
        }

        // True only when one message is a chat answer that must not create/edit a library file.
        internal static bool ChatOnly(string text){
            if(String.IsNullOrWhiteSpace(text))return false;
            try{
                bool chat=ChatCue.IsMatch(text),noFile=NoFileCue.IsMatch(text);
                if(!chat&&!noFile)return false;
                string actions=Actions(text);
                bool write=ChatWriteCue.IsMatch(actions);
                if(chat&&noFile)return true;
                if(chat)return !write;
                return !write&&!ArtifactCreateCue.IsMatch(actions);
            }catch{return false;}
        }

        // A requested "N pages" deliverable count. Source positions are removed first; within one message the
        // LAST page revision wins ("制作8页PPT，改为5页PPT" -> 5, review #2).
        internal static int RequestedPages(string text){
            if(String.IsNullOrWhiteSpace(text))return 0;
            try{
                string value=StripSourcePositions(Actions(text));
                Match last=null;
                foreach(Match match in PageCountPattern.Matches(value))last=match;
                if(last==null)return 0;
                if(last.Groups[1].Success)return ClampPages(Int32.Parse(last.Groups[1].Value));
                if(last.Groups[2].Success)return ClampPages(Int32.Parse(last.Groups[2].Value));
                if(last.Groups[3].Success)return ClampPages(Int32.Parse(last.Groups[3].Value));
                return ClampPages(ChineseNumber(last.Groups[4].Value));
            }catch{return 0;}
        }
        static string StripSourcePositions(string text){
            string value=Regex.Replace(text,@"第\s*(?:[0-9]{1,3}|[一二三四五六七八九十两]{1,3})\s*(?:页|张|片)"," ",Opt);
            value=Regex.Replace(value,@"(?:阅读|读取|查看|参考|翻到|读到|看|读|read|see|view|open)\s*(?:到|至)?\s*(?:\d{1,3}|[一二三四五六七八九十两]{1,3})\s*(?:页|张|片|pages?)"," ",Opt);
            value=Regex.Replace(value,@"(?:页|张|片)\s*(?:的)?\s*(?:材料|资料|文档|原文|文件|内容)"," ",Opt);
            return value;
        }
        static int ClampPages(int value){return value<1?0:Math.Min(100,value);}
        internal static int ChineseNumber(string value){
            if(String.IsNullOrEmpty(value))return 0;
            int ten=value.IndexOf('十');
            if(ten>=0){
                string high=ten>0?value.Substring(0,ten):"",low=ten+1<value.Length?value.Substring(ten+1):"";
                return (high==""?1:ChineseDigit(high))*10+(low==""?0:ChineseDigit(low));
            }
            return ChineseDigit(value);
        }
        static int ChineseDigit(string value){
            const string digits="零一二三四五六七八九";
            if(String.IsNullOrEmpty(value)||value.Length!=1)return 0;
            return value=="两"?2:digits.IndexOf(value[0]);
        }

        // Single-message source narrowing: ONLY an explicit exclusive marker (只/仅/only...) plus a resolvable
        // 资料 ordinal narrows the set (review #4). "重点/优先/着重 + 第二份" keeps every source.
        internal static List<string> ReferencedSourceIds(List<string> allowed,string text){
            var sources=(allowed??new List<string>()).Where(id=>!String.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
            if(sources.Count==0||String.IsNullOrWhiteSpace(text))return sources;
            try{
                var keys=OrdinalSourceKeys(text);
                if(keys.Count==0||!SourceExclusive.IsMatch(text))return sources;
                var result=new List<string>();
                foreach(int index in keys)if(index>=1&&index<=sources.Count&&!result.Contains(sources[index-1]))result.Add(sources[index-1]);
                return result.Count==0?sources:result;
            }catch{return sources;}
        }
        static List<int> OrdinalSourceKeys(string text){
            var keys=new List<int>();
            foreach(Match match in OrdinalSource.Matches(text)){int index=OrdinalNumber(match.Groups[1].Value);if(index>0&&!keys.Contains(index))keys.Add(index);}
            foreach(Match match in EnglishOrdinalSource.Matches(text)){int index=EnglishOrdinal(match.Groups[1].Value);if(index>0&&!keys.Contains(index))keys.Add(index);}
            foreach(Match match in EnglishNumericSource.Matches(text)){int index=Int32.Parse(match.Groups[1].Value);if(index>0&&!keys.Contains(index))keys.Add(index);}
            return keys;
        }
        static int OrdinalNumber(string value){
            if(String.IsNullOrEmpty(value))return 0;
            if(Char.IsDigit(value[0]))return Int32.TryParse(value,out int number)?number:0;
            return ChineseNumber(value);
        }
        static int EnglishOrdinal(string value){
            switch((value??"").Trim().ToLowerInvariant()){
                case "first":case "one":return 1;case "second":case "two":return 2;case "third":case "three":return 3;
                case "fourth":case "four":return 4;case "fifth":case "five":return 5;case "sixth":case "six":return 6;
                case "seventh":case "seven":return 7;case "eighth":case "eight":return 8;case "ninth":case "nine":return 9;
                case "tenth":case "ten":return 10;case "eleventh":case "eleven":return 11;case "twelfth":case "twelve":return 12;
                case "thirteenth":case "thirteen":return 13;case "fourteenth":case "fourteen":return 14;case "fifteenth":case "fifteen":return 15;
                case "sixteenth":case "sixteen":return 16;case "seventeenth":case "seventeen":return 17;case "eighteenth":case "eighteen":return 18;
                case "nineteenth":case "nineteen":return 19;case "twentieth":case "twenty":return 20;case "twenty-first":case "twenty first":return 21;
                case "twenty-second":case "twenty second":return 22;case "twenty-third":case "twenty third":return 23;case "twenty-fourth":case "twenty fourth":return 24;
                default:return 0;
            }
        }

        // Measurable body-length bounds of one message; the LAST explicit length phrase wins. A cancel phrase
        // ("取消字数限制/不限字数") yields (0,0). A bare "300字" target is not treated as a hard bound.
        internal static void RequestedLength(string text,out int minChars,out int maxChars){
            minChars=0;maxChars=0;
            LengthEvent ev=LastLengthEvent(text);
            if(ev==null||ev.Cancel)return;
            minChars=ev.Min;maxChars=ev.Max;
        }
        // Measurement unit attached to the last length requirement of one message (None when cancelled/absent).
        internal static AgentMeasureUnit RequestedLengthUnit(string text){
            LengthEvent ev=LastLengthEvent(text);
            return ev==null||ev.Cancel?AgentMeasureUnit.None:ev.Unit;
        }
        sealed class LengthEvent {public int Index;public bool Cancel;public int Min,Max;public AgentMeasureUnit Unit;}
        static LengthEvent LastLengthEvent(string text){
            if(String.IsNullOrWhiteSpace(text))return null;
            LengthEvent last=null;
            try{
                foreach(Match match in LengthCancel.Matches(text))last=Newer(last,new LengthEvent{Index=match.Index,Cancel=true});
                foreach(Match match in LengthRange.Matches(text)){
                    int min=Positive(match.Groups[1].Value),max=Positive(match.Groups[2].Value);
                    if(min>max){int swap=min;min=max;max=swap;}
                    if(min<=0)continue;
                    last=Newer(last,new LengthEvent{Index=match.Index,Min=min,Max=max,Unit=UnitOf(match.Groups[3].Value)});
                }
                foreach(Match match in LengthMin.Matches(text)){
                    string number=match.Groups[1].Success?match.Groups[1].Value:match.Groups[3].Value;
                    string unit=match.Groups[2].Success?match.Groups[2].Value:match.Groups[4].Value;
                    int min=Positive(number);if(min<=0)continue;
                    last=Newer(last,new LengthEvent{Index=match.Index,Min=min,Max=0,Unit=UnitOf(unit)});
                }
                foreach(Match match in LengthMax.Matches(text)){
                    string number=match.Groups[1].Success?match.Groups[1].Value:match.Groups[3].Value;
                    string unit=match.Groups[2].Success?match.Groups[2].Value:match.Groups[4].Value;
                    int max=Positive(number);if(max<=0)continue;
                    last=Newer(last,new LengthEvent{Index=match.Index,Min=0,Max=max,Unit=UnitOf(unit)});
                }
            }catch{}
            return last;
        }
        static LengthEvent Newer(LengthEvent current,LengthEvent candidate){return current==null||candidate.Index>=current.Index?candidate:current;}
        static int Positive(string value){
            if(!Int32.TryParse(value,out int number))return 0;
            return number<1?0:Math.Min(1000000,number);
        }
        static AgentMeasureUnit UnitOf(string unit){
            if(String.IsNullOrWhiteSpace(unit))return AgentMeasureUnit.None;
            string value=unit.Trim().ToLowerInvariant();
            if(value.Contains("字符")||value.StartsWith("char"))return AgentMeasureUnit.Characters;
            if(value.Contains("单词")||value.StartsWith("word"))return AgentMeasureUnit.Words;
            if(value.Contains("汉字")||value=="字")return AgentMeasureUnit.Hanzi;
            return AgentMeasureUnit.None;
        }

        // Explicit document format requested by one message, last mention wins: "" / "MD" / "DOCX" / "TXT".
        internal static string RequestedFormat(string text){
            if(String.IsNullOrWhiteSpace(text))return "";
            try{
                string actions=Actions(text);
                int index=-1;string format="";
                var word=LastMatch(FormatWord,actions);if(word!=null&&word.Index>index){index=word.Index;format="DOCX";}
                var markdown=LastMatch(FormatMarkdown,actions);if(markdown!=null&&markdown.Index>index){index=markdown.Index;format="MD";}
                var txt=LastMatch(FormatTxt,actions);if(txt!=null&&txt.Index>index){index=txt.Index;format="TXT";}
                return format;
            }catch{return "";}
        }
        static Match LastMatch(Regex pattern,string text){Match last=null;foreach(Match match in pattern.Matches(text))last=match;return last;}

        // Host workflow selection: a positive presentation request uses the "slides" workflow.
        internal static string NormalizedWorkflow(string goal){
            try{return IsPresentation(goal)?"slides":"";}catch{return "";}
        }

        // ---- new API: per-message parsing and the ordered state machine ---------------------------------

        // Parse one message without a resolved allowed-source list (ExclusiveSourceIds then holds ordinal keys
        // such as "2"; Current resolves them against the task's allowed list).
        internal static AgentRequirementSegment ParseSegment(string text){return ParseSegmentCore(text,null);}

        static AgentRequirementSegment ParseSegmentCore(string text,List<string> allowed){
            var seg=new AgentRequirementSegment();
            if(String.IsNullOrWhiteSpace(text))return seg;
            string raw=text;
            string actions=Actions(raw);
            seg.ChatOnly=ChatOnly(raw);
            // positive deliverables
            bool presentation=AgentTaskIntent.IsPresentation(actions);
            bool convert=presentation&&Match(actions,ConvertPattern);
            bool presentationObject=presentation&&Match(actions,PresentationObject);
            // A document deliverable needs a CREATION verb bound to a document noun ("生成一份报告",
            // "保存为一个新的文档"); "修改这份现有报告…保存后核对" is a revision and must not become a new artifact.
            bool documentObject=Match(actions,DocumentObject);
            bool inlineOnly=Match(actions,InlineOnly)&&!Match(actions,InlineSave);
            bool document=documentObject&&!inlineOnly&&!convert;
            int pages=RequestedPages(actions);
            bool edit=IsBodyEdit(actions);
            // "改成/改为 + another deliverable kind": replacement target
            string replaceTarget="";
            if(Match(actions,ReplaceKind))replaceTarget="presentation";
            else if(Match(actions,ReplaceDocKind))replaceTarget="document";
            if(replaceTarget=="presentation")presentation=true;
            if(replaceTarget=="document")document=true;
            bool presentationArtifact=presentation&&(convert||presentationObject||(pages>0&&!edit)||replaceTarget=="presentation");
            if(presentationArtifact&&document&&!Match(actions,MultiDeliverable)&&replaceTarget!="document")document=false;
            // A new deliverable request / replacement is not a body rewrite ("改成写一份报告").
            if((presentationArtifact||document)&&!Match(actions,BodyContentNoun))edit=false;
            // object/version-scoped negations (only the CLAUSE they are in; a positive same kind wins, #6)
            bool negDocument=false,negPresentation=false;
            string negationScope="";
            foreach(string clause in Clauses(raw)){
                if(!NegationMarker.IsMatch(clause))continue;
                string scope=VersionOrPageScope(clause);
                if(scope!=""&&negationScope=="")negationScope=scope;
                if(NegatedPresentation.IsMatch(clause)){negPresentation=true;continue;}
                if(NegatedDocument.IsMatch(clause)){negDocument=true;continue;}
                if(CancelDeliverable.IsMatch(clause)){
                    if(PresentKind(clause)=="presentation")negPresentation=true;else if(PresentKind(clause)=="document")negDocument=true;
                }
            }
            // The flags always record the detected object-scoped negation; a positive same-kind request in the
            // SAME message keeps the requirement and only records the constraint (#6).
            bool cancelDocument=negDocument&&!document;
            bool cancelPresentation=negPresentation&&!presentationArtifact;
            bool cancelEdit=CancelEditMarker.IsMatch(raw)&&!edit;
            // full read: last event of this message wins
            bool? fullRead=LastFullReadEvent(raw);
            // length: last event of this message wins
            LengthEvent length=LastLengthEvent(raw);
            // format
            string format=RequestedFormat(actions);
            // sources
            string sourceOp="",sourceValue="";
            var narrow=SourceNarrowEvent(raw,allowed,out var narrowIds,out var narrowKeys);
            var all=LastMatch(SourceAll,raw);
            if(narrow.HasValue&&(all==null||narrow.Value>=all.Index)){
                sourceOp="sources";
                sourceValue=allowed==null?String.Join(",",narrowKeys):String.Join(",",narrowIds);
                if(allowed==null)foreach(string key in narrowKeys)seg.ExclusiveSourceIds.Add(key);
                else foreach(string id in narrowIds)seg.ExclusiveSourceIds.Add(id);
            }else if(all!=null){sourceOp="sources-all";seg.AllSources=true;}
            // required count ("两份报告")
            int count=0;Match lastCount=null;
            foreach(Match match in CountPattern.Matches(actions)){int value=CountNumber(match.Groups[1].Value);if(value>0){count=value;lastCount=match;}}
            if(count<1)count=1;
            // assemble ops (order: replacement, kinds, pages, length, format, sources, fullread, edit, count, cancels, chat)
            if(replaceTarget!=""){seg.ReplaceDeliverableKind=true;seg.ReplaceTarget=replaceTarget;Add(seg.ops,"replace",replaceTarget);}
            if(document){seg.Document=true;Add(seg.ops,"document");}
            if(presentationArtifact){seg.Presentation=true;Add(seg.ops,"presentation");}
            seg.Pages=pages;if(pages>0)AddNumber(seg.ops,"pages",pages);
            string cancelPage=NegatedPageValue(raw);
            if(cancelPage=="0")seg.ops.Add(new AgentRequirementOp{Kind="cancel-pages",Number=0,Negative=true});
            else if(cancelPage!=""&&Int32.TryParse(cancelPage,out int canceledPages))seg.ops.Add(new AgentRequirementOp{Kind="cancel-pages",Number=canceledPages,Negative=true});
            if(length!=null){
                if(length.Cancel){seg.CancelLength=true;Add(seg.ops,"cancel-length");}
                else{seg.LengthMin=length.Min;seg.LengthMax=length.Max;seg.LengthUnit=length.Unit;seg.ops.Add(new AgentRequirementOp{Kind="length",Min=length.Min,Max=length.Max,Unit=length.Unit});}
            }
            seg.DocumentFormat=format;if(format!="")seg.ops.Add(new AgentRequirementOp{Kind="format",Value=format});
            if(sourceOp!=""){var op=new AgentRequirementOp{Kind=sourceOp,Value=sourceValue};seg.ops.Add(op);}
            if(fullRead==true){seg.FullRead=true;Add(seg.ops,"fullread");}
            else if(fullRead==false){seg.CancelFullRead=true;Add(seg.ops,"cancel-fullread");}
            if(edit){seg.Edit=true;Add(seg.ops,"edit");}
            else if(cancelEdit){seg.CancelEdit=true;Add(seg.ops,"cancel-edit");}
            if(lastCount!=null)AddNumber(seg.ops,"count",count);
            if(cancelDocument)Add(seg.ops,"cancel-document");
            if(cancelPresentation)Add(seg.ops,"cancel-presentation");
            seg.NegatesDocument=negDocument;seg.NegatesPresentation=negPresentation;
            seg.NegationScope=negationScope;
            if(seg.ChatOnly)Add(seg.ops,"chat");
            return seg;
        }
        static void Add(List<AgentRequirementOp> ops,string kind){ops.Add(new AgentRequirementOp{Kind=kind});}
        static void Add(List<AgentRequirementOp> ops,string kind,string value){ops.Add(new AgentRequirementOp{Kind=kind,Value=value});}
        static void AddNumber(List<AgentRequirementOp> ops,string kind,int number){ops.Add(new AgentRequirementOp{Kind=kind,Number=number});}
        static bool Match(string text,Regex pattern){return !String.IsNullOrWhiteSpace(text)&&pattern.IsMatch(text);}
        static List<string> Clauses(string raw){
            var result=new List<string>();
            if(String.IsNullOrWhiteSpace(raw))return result;
            foreach(string part in Regex.Split(raw,@"[。！？；;，,\n]|(?:但是|但|而是|instead|however|\bbut\b)"))
                if(!String.IsNullOrWhiteSpace(part))result.Add(part);
            return result;
        }
        static string VersionOrPageScope(string clause){
            var version=LastMatch(VersionHint,clause);if(version!=null)return version.Value;
            var page=LastMatch(PageCountPattern,clause);if(page!=null)return page.Value;
            return "";
        }
        static string PresentKind(string clause){
            if(PresentationNoun.IsMatch(clause))return "presentation";
            if(DocumentNoun.IsMatch(clause))return "document";
            return "";
        }
        static string NegatedPageValue(string raw){
            foreach(string clause in Clauses(raw)){
                if(!NegationMarker.IsMatch(clause))continue;
                if(Regex.IsMatch(clause,@"页数不限|不限页数|取消页数|不要页数|无页数要求",Opt))return "0";
                var match=Regex.Match(clause,@"(?:不要|不用|不需要|无需|无须|不必|别|勿)(?:再)?(?:做|要|用|出)?\s*(?:第\s*)?(\d{1,3}|[一二三四五六七八九十两]{1,3})\s*(?:页|张|片)",Opt);
                if(match.Success){int value=match.Groups[1].Value[0]>='0'&&match.Groups[1].Value[0]<='9'?Int32.Parse(match.Groups[1].Value):ChineseNumber(match.Groups[1].Value);if(value>0)return Convert.ToString(value);}
            }
            return "";
        }
        static int CountNumber(string value){
            if(String.IsNullOrEmpty(value))return 0;
            if(Char.IsDigit(value[0]))return Int32.TryParse(value,out int number)?number:0;
            return ChineseNumber(value);
        }
        // Source narrowing event: exclusive marker AND resolvable ordinal. Returns the LAST exclusive-marker
        // index so a later "改为全部" can override it (review #4).
        static int? SourceNarrowEvent(string raw,List<string> allowed,out List<string> ids,out List<string> keys){
            ids=new List<string>();keys=new List<string>();
            if(String.IsNullOrWhiteSpace(raw))return null;
            var ordinalKeys=OrdinalSourceKeys(raw);
            if(ordinalKeys.Count==0)return null;
            var exclusive=LastMatch(SourceExclusive,raw);
            if(exclusive==null)return null;
            foreach(int index in ordinalKeys){
                if(allowed==null)keys.Add(Convert.ToString(index));
                else if(index>=1&&index<=allowed.Count)ids.Add(allowed[index-1]);
            }
            if(allowed==null){if(keys.Count==0)return null;}
            else if(ids.Count==0)return null;
            return exclusive.Index;
        }
        static bool? LastFullReadEvent(string raw){
            if(String.IsNullOrWhiteSpace(raw))return null;
            Match last=null;bool lastCancel=false;
            foreach(Match match in CancelFullReadMarker.Matches(raw))if(last==null||match.Index>=last.Index){last=match;lastCancel=true;}
            foreach(Match match in FullReadMarker.Matches(raw))if(last==null||match.Index>=last.Index){last=match;lastCancel=false;}
            if(last==null)return null;
            return !lastCancel;
        }
        // Body edit detection: a body-rewrite clause targeting an existing artifact. Metadata work (tags,
        // collections, notes) does not count, but a body-edit clause next to it must survive.
        static bool IsBodyEdit(string actions){
            if(String.IsNullOrWhiteSpace(actions))return false;
            // A revision used as source/version metadata is not a request to edit an existing file.
            // Keep imperative uses such as "修订这份报告" and "最终修订这份报告" intact.
            string filtered=Regex.Replace(actions,@"(?:最终|最新|最后)(?:的)?修订(?=版|稿|预算|内容|要求|方案|结果|记录|的|为准|生成|创建|制作|编写|撰写|输出|[，,。；;、\s]|$)|修订(?:版|稿|记录|说明)|\b(?:final|latest)\s+revision\b|\brevised\s+(?:source|material|document|budget|version)s?\b"," ",Opt);
            filtered=RemoveMetadataClauses(filtered);
            if(!Match(filtered,BodyVerb))return false;
            if(Match(filtered,InlineOnly)&&!Match(filtered,InlineSave))return false;
            bool target=Match(filtered,TargetReference);
            bool strong=Match(filtered,StrongBodyVerb);
            bool content=Match(filtered,ContentCue);
            if(Match(filtered,new Regex(@"(?:改成|改为|改到|调整为|调整到)\s*\d{1,3}\s*(?:页|张|片)",Opt))&&!content)return false;
            if(target)return true;
            return strong&&content;
        }
        static string RemoveMetadataClauses(string actions){
            var parts=Regex.Split(actions,@"[，,。；;、\n]|\s*(?:并|且|同时|以及|另外|另)\s*");
            var builder=new StringBuilder();
            foreach(string part in parts){
                if(String.IsNullOrWhiteSpace(part))continue;
                bool metadata=Match(part,MetadataNoun);
                if(metadata&&!Match(part,MetadataContentNoun)&&!Match(part,StrongBodyVerb))continue;
                builder.Append(part).Append(' ');
            }
            return builder.ToString();
        }

        // ---- ordered state machine: current effective contract -----------------------------------------
        internal static AgentTaskContract Current(AgentTaskOptions o){
            return Current(o,null);
        }
        internal static AgentTaskContract Current(AgentTaskOptions o,IEnumerable<string> adjustments){
            var contract=new AgentTaskContract();
            if(o==null)return contract;
            var allowed=AllowedSources(o.allowed);
            contract.RequiredSourceIds=allowed.ToList();
            var segments=new List<string>();
            if(!String.IsNullOrWhiteSpace(o.goal))segments.Add(o.goal);
            if(adjustments!=null)foreach(string item in adjustments)if(!String.IsNullOrWhiteSpace(item))segments.Add(item);
            bool? fullRead=null;
            try{
                foreach(string text in segments){
                    AgentRequirementSegment seg=ParseSegmentCore(text,allowed);
                    foreach(AgentRequirementOp op in seg.ops)Apply(contract,op,allowed,ref fullRead);
                }
                if(fullRead==null)fullRead=DefaultFullRead(o,allowed,contract,segments);
                contract.RequiresFullSourceCoverage=fullRead.Value;
            }catch{
                contract.RequiresFullSourceCoverage=DefaultFullRead(o,allowed,contract,segments);
            }
            // sanitize
            if(!contract.RequiresDocument)contract.RequiredDocumentFormat="";
            if(contract.ChatOnly){
                contract.RequiresDocument=false;contract.RequiresPresentation=false;contract.RequiresTargetedEdit=false;
                contract.RequestedPages=0;contract.RequiredDocumentFormat="";
            }
            if(contract.RequestedPages<0||contract.RequestedPages>100)contract.RequestedPages=0;
            if(contract.RequiredCount<1)contract.RequiredCount=1;
            if(contract.RequiredSourceIds==null||contract.RequiredSourceIds.Count==0)contract.RequiredSourceIds=allowed.ToList();
            contract.RequiredSourceIds=contract.RequiredSourceIds.Where(id=>!String.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
            if(contract.RequiresTargetedEdit){
                Match hint=null;foreach(string text in segments){var match=LastMatch(VersionHint,text);if(match!=null)hint=match;}
                contract.EditVersionHint=hint==null?"":hint.Value;
            }else contract.EditVersionHint="";
            return contract;
        }
        static List<string> AllowedSources(List<string> allowed){
            return (allowed??new List<string>()).Where(id=>!String.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        }
        static void Apply(AgentTaskContract contract,AgentRequirementOp op,List<string> allowed,ref bool? fullRead){
            if(op==null||String.IsNullOrEmpty(op.Kind))return;
            switch(op.Kind){
                case "replace":{
                    bool targetPres=op.Value=="presentation",targetDoc=op.Value=="document";
                    if(!targetPres&&!targetDoc)break;
                    bool targetRequired=targetPres?contract.RequiresPresentation:contract.RequiresDocument;
                    bool otherRequired=targetPres?contract.RequiresDocument:contract.RequiresPresentation;
                    if(!(targetRequired&&!otherRequired)){
                        // a real replacement: only the target kind remains, old specs are cleared
                        contract.RequiresDocument=targetDoc;contract.RequiresPresentation=targetPres;
                        contract.RequestedPages=0;contract.MinLength=0;contract.MaxLength=0;contract.LengthUnit=AgentMeasureUnit.None;
                        contract.RequiredDocumentFormat="";contract.RequiredCount=1;
                    }
                    contract.ChatOnly=false;
                    break;}
                case "document":contract.RequiresDocument=true;contract.ChatOnly=false;break;
                case "presentation":contract.RequiresPresentation=true;contract.ChatOnly=false;break;
                case "cancel-document":contract.RequiresDocument=false;if(!contract.RequiresPresentation)contract.RequiredDocumentFormat="";break;
                case "cancel-presentation":contract.RequiresPresentation=false;if(!contract.RequiresDocument)contract.RequestedPages=0;break;
                case "pages":if(op.Number>0)contract.RequestedPages=op.Number;break;
                case "cancel-pages":if(op.Number==0)contract.RequestedPages=0;else if(contract.RequestedPages==op.Number)contract.RequestedPages=0;break;
                case "length":contract.MinLength=op.Min;contract.MaxLength=op.Max;contract.LengthUnit=op.Max>0||op.Min>0?op.Unit:AgentMeasureUnit.None;break;
                case "cancel-length":contract.MinLength=0;contract.MaxLength=0;contract.LengthUnit=AgentMeasureUnit.None;break;
                case "format":contract.RequiredDocumentFormat=op.Value??"";break;
                case "sources":if(!String.IsNullOrEmpty(op.Value))contract.RequiredSourceIds=op.Value.Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).ToList();break;
                case "sources-all":contract.RequiredSourceIds=allowed.ToList();break;
                case "fullread":fullRead=true;break;
                case "cancel-fullread":fullRead=false;break;
                case "edit":contract.RequiresTargetedEdit=true;contract.ChatOnly=false;break;
                case "cancel-edit":contract.RequiresTargetedEdit=false;break;
                case "count":if(op.Number>=1)contract.RequiredCount=op.Number;break;
                case "chat":contract.ChatOnly=true;contract.RequiresDocument=false;contract.RequiresPresentation=false;contract.RequiresTargetedEdit=false;contract.RequestedPages=0;contract.RequiredDocumentFormat="";break;
            }
        }
        // Default full-source obligation when no message explicitly stated/cancelled it: the v0.12.1 heuristic
        // (explicit 全文/全部/每份, or a creative deliverable over user-provided sources unless explicitly partial).
        static bool DefaultFullRead(AgentTaskOptions o,List<string> allowed,AgentTaskContract contract,List<string> segments){
            string actions=Actions(Combined(o==null?"":o.goal,segments.Count>1?segments.Skip(1):Enumerable.Empty<string>()));
            bool fullExplicit=Match(actions,FullSource);
            bool sourceReference=Match(actions,SourceReference);
            bool scopedSample=Match(actions,ScopedSample);
            bool artifact=contract.RequiresDocument||contract.RequiresPresentation;
            return fullExplicit||(artifact&&allowed.Count>0&&sourceReference&&!scopedSample);
        }
    }
}
