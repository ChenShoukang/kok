using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

// v0.12.2 C-A: the ordered "current effective task requirement" state machine. Goal + every adjustment are
// replayed in user-message order; later messages replace or cancel earlier requirements (review #1-#4, #6),
// units (#9) and formats (#10) are persisted, and chat-only can migrate both ways. Everything is offline: a
// real isolated Library and native task events, no model/network call. Owned by intent-agent.
class AgentIntentV013Tests {
    static Action<bool,string> check;
    public static void Run(string root,Action<bool,string> check){
        AgentIntentV013Tests.check=check;
        Directory.CreateDirectory(root);
        IntentSmoke();
        StateMachine();
        DeriveCompatibility();
        SteerChecks(Path.Combine(root,"steer"));
        DeliveryIntegration(Path.Combine(root,"delivery"));
        ToolProfileChecks(Path.Combine(root,"profile"));
    }
    static void Verify(bool ok,string name){check(ok,name);}

    // ---- shared helpers -------------------------------------------------------------------------
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> Dict(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static AgentTaskOptions Options(string goal,List<string> allowed){return new AgentTaskOptions{sessionId=Id(),goal=goal,kernelVersion=3,requestLimit=6,budget=14000,allowed=allowed??new List<string>(),scope=new AgentScope{mode=allowed!=null&&allowed.Count>0?"selected":"library",ids=allowed??new List<string>()}};}
    static AgentDeliveryRequirement Derive(string goal,params string[] adjustments){return AgentDeliveryRules.Derive(new AgentTaskOptions{goal=goal,kernelVersion=3,allowed=new List<string>()},adjustments);}
    static AgentDeliveryRequirement DeriveAllowed(List<string> allowed,string goal,params string[] adjustments){return AgentDeliveryRules.Derive(new AgentTaskOptions{goal=goal,kernelVersion=3,allowed=allowed},adjustments);}
    static AgentTaskContract Contract(string goal,params string[] adjustments){return AgentTaskIntent.Current(new AgentTaskOptions{goal=goal,kernelVersion=3,allowed=new List<string>()},adjustments);}
    static void ApplyEv(AgentTaskState state,string kind,object data){state.Apply(new AgentEvent{version=3,id=Id(),sequence=state.sequence+1,kind=kind,data=Dict(data)});}
    static AgentEvent Emit(AgentJournal journal,AgentTaskState state,string kind,object data){var e=journal.Append(state.id,kind,data,state.options.sessionId,"fixture",state.status);state.Apply(e);return e;}
    static AgentTaskState Seed(AgentJournal journal,AgentTaskOptions options){string id=Id();var e=journal.Append(id,"task.created",new{options=options,protocol="tools"},options.sessionId,"fixture","created");return AgentTaskState.Replay(id,new List<AgentEvent>{e});}
    static AgentTaskState Replay(AgentJournal journal,AgentTaskState state){return AgentTaskState.Replay(state.id,journal.Events(state.id));}
    sealed class Fixture {
        public Library Lib;public AgentService Service;
        public Fixture(string root){
            Lib=new Library(root);Lib.SetAppMode("smart",true);
            Service=new AgentService(new ModuleContext(Lib,new TranslationService(Lib.DataDir),new ModulePolicy(Lib),null,CancellationToken.None,null,new Operations(Lib,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory));
        }
    }
    static AgentCall ReadTool(Library lib,AgentTaskState state,string tool,object args){
        var call=lib.PrepareAgentCall(state,new AgentAction{tool=tool,args=Dict(args)},Id(),null);
        ApplyEv(state,"tool.proposed",new{call});call=state.calls.Last();
        try{var result=lib.ReadAgentTool(state,call,CancellationToken.None).GetAwaiter().GetResult();ApplyEv(state,"tool.completed",new{callId=call.id,result});}
        catch(Exception error){ApplyEv(state,"tool.failed",new{callId=call.id,kind=AgentFailure.Classify(error).kind,message=error.Message,retryable=false});}
        return state.calls.Last();
    }
    static AgentCall CreateTool(Library lib,AgentTaskState state,string tool,object args){
        var call=lib.PrepareAgentCall(state,new AgentAction{tool=tool,args=Dict(args)},Id(),null);
        ApplyEv(state,"tool.proposed",new{call});call=state.calls.Last();
        var receipt=lib.WriteAgentTool(state,call,CancellationToken.None);
        ApplyEv(state,"tool.completed",new{callId=call.id,result=receipt.result,stamp=receipt.stamp});
        return state.calls.Last();
    }
    static AgentCall CreateDocument(Library lib,AgentTaskState state,string name,string text,string format="md"){return CreateTool(lib,state,"create_document",new{name,format,text});}
    static AgentCall CreateDeck(Library lib,AgentTaskState state,string title,int pages){
        var slides=new List<Dictionary<string,object>>();
        for(int n=1;n<=pages;n++)slides.Add(new Dictionary<string,object>{{"title","第 "+n+" 页"},{"bullets",new object[]{"要点 "+n}}});
        return CreateTool(lib,state,"create_presentation",new{title,slides});
    }
    static string CreatedId(AgentCall call){return Convert.ToString(call.result["id"]);}
    static string Instructions(AgentTaskState state){return AgentCognition.Instructions(state);}

    // ---- 1) old API compatibility + unit/format helpers (single message) -------------------------
    static void IntentSmoke(){
        string negated=AgentTaskIntent.Actions("不要做PPT，请写一份报告。");
        Verify(!negated.ToLowerInvariant().Contains("ppt")&&negated.Contains("报告"),"intent: a negated PPT clause is removed while the positive report request survives");
        Verify(!AgentTaskIntent.Actions("不要新建报告而是修改这份报告。").Contains("新建")&&AgentTaskIntent.Actions("不要新建报告而是修改这份报告。").Contains("修改"),"intent: a negation ends at 而是 so the following positive edit request survives");
        Verify(AgentTaskIntent.IsPresentation("制作8页PPT。")&&AgentTaskIntent.IsPresentation("8页slides")&&AgentTaskIntent.IsPresentation("制作presentation"),"intent: CJK/English-mixed positive presentation tokens are recognized");
        Verify(!AgentTaskIntent.IsPresentation("不要做PPT，请写一份报告。"),"intent: a negated PPT mention is not a presentation request");
        Verify(AgentTaskIntent.ChatOnly("直接在聊天里回复，不要创建文件")&&!AgentTaskIntent.ChatOnly("不要做PPT，请写一份报告。"),"intent: chat-only detection still distinguishes chat answers from file requests");
        Verify(AgentTaskIntent.RequestedPages("阅读第2页材料，制作8页PPT。")==8,"intent: a source page position is not the requested deliverable page count");
        Verify(AgentTaskIntent.RequestedPages("制作8页PPT，改为5页PPT。")==5,"intent: inside one message the LAST page revision wins (8 -> 5, review #2)");
        Verify(AgentTaskIntent.RequestedPages("把这份 PPT 第 2 页改成蓝色")==0,"intent: editing an existing page requests no new page count");
        Verify(AgentTaskIntent.RequestedPages("制作一个十页的幻灯片介绍方案")==10,"intent: Chinese page numerals still parse (十页 -> 10)");
        var allowed=new List<string>{new string('a',32),new string('b',32)};
        Verify(AgentTaskIntent.ReferencedSourceIds(allowed,"只根据第二份资料写报告").SequenceEqual(new[]{allowed[1]}),"intent: 只根据第二份资料 narrows the source set");
        Verify(AgentTaskIntent.ReferencedSourceIds(allowed,"重点核对第二份资料").Count==2,"intent: 重点/优先 emphasis alone never narrows the source set (review #4)");
        Verify(AgentTaskIntent.ReferencedSourceIds(allowed,"根据资料写报告").Count==2,"intent: no reference keeps every allowed source");
        AgentTaskIntent.RequestedLength("写一份800-1200字的报告",out int min,out int max);
        Verify(min==800&&max==1200&&AgentTaskIntent.RequestedLengthUnit("写一份800-1200字的报告")==AgentMeasureUnit.Hanzi,"intent: 800-1200字 is a Hanzi range");
        AgentTaskIntent.RequestedLength("不少于300字",out min,out max);
        Verify(min==300&&max==0,"intent: 不少于300字 parses to min=300, no maximum");
        AgentTaskIntent.RequestedLength("不超过500字",out min,out max);
        Verify(min==0&&max==500,"intent: 不超过500字 parses to max=500, no minimum");
        AgentTaskIntent.RequestedLength("正文100-200字符",out min,out max);
        Verify(min==100&&max==200&&AgentTaskIntent.RequestedLengthUnit("正文100-200字符")==AgentMeasureUnit.Characters,"intent: 100-200字符 keeps the Characters unit (review #9)");
        AgentTaskIntent.RequestedLength("100-200 words",out min,out max);
        Verify(min==100&&max==200&&AgentTaskIntent.RequestedLengthUnit("100-200 words")==AgentMeasureUnit.Words,"intent: 100-200 words keeps the Words unit");
        AgentTaskIntent.RequestedLength("取消字数限制",out min,out max);
        Verify(min==0&&max==0&&AgentTaskIntent.RequestedLengthUnit("取消字数限制")==AgentMeasureUnit.None,"intent: 取消字数限制 clears every bound and the unit");
        Verify(AgentTaskIntent.RequestedFormat("创建一份Word文档，格式为DOCX。")=="DOCX","intent: DOCX/Word maps to the DOCX format");
        Verify(AgentTaskIntent.RequestedFormat("创建一份Markdown文档")=="MD"&&AgentTaskIntent.RequestedFormat("创建一份纯文本文件")=="TXT","intent: Markdown/MD and TXT/纯文本 formats are recognized (review #10)");
        Verify(AgentTaskIntent.NormalizedWorkflow("制作8页PPT")=="slides"&&AgentTaskIntent.NormalizedWorkflow("不要做PPT，请写一份报告")=="","intent: workflow uses positive presentation intent only");
        string combined=AgentTaskIntent.Combined("写一份报告",new[]{"不要做PPT，改为5页。","把报告保存成文档。"});
        Verify(combined.Contains("写一份报告")&&!combined.Contains("PPT")&&combined.Contains("保存成文档"),"intent: Combined keeps the goal and each adjustment's positive actions");
        Verify(AgentDeliveryRules.MeasuredLength("中文 English 123",AgentMeasureUnit.Hanzi)==2,"measure: Hanzi counts only CJK characters");
        Verify(AgentDeliveryRules.MeasuredLength("**粗体** 123 ABC，。",AgentMeasureUnit.Hanzi)==2,"measure: Hanzi ignores Markdown markers/digits/Latin/punctuation");
        Verify(AgentDeliveryRules.MeasuredLength(String.Join(" ",Enumerable.Repeat("word",30)),AgentMeasureUnit.Characters)==120,"measure: Characters counts non-whitespace characters after Markdown stripping");
        Verify(AgentDeliveryRules.MeasuredLength(String.Join(" ",Enumerable.Repeat("word",30)),AgentMeasureUnit.Words)==30,"measure: Words counts English word tokens");
        Verify(AgentDeliveryRules.MeasuredChars("**粗体** 123 ABC，。")==2,"measure: MeasuredChars keeps the legacy Hanzi meter");
        // ParseSegment exposes per-message operations for diagnostics.
        var seg=AgentTaskIntent.ParseSegment("制作8页PPT，改为5页PPT。");
        Verify(seg.Pages==5&&seg.Presentation,"parse: one message keeps its final page revision and the presentation request");
        var cancelSeg=AgentTaskIntent.ParseSegment("先不要修改，只分析这份报告。");
        Verify(cancelSeg.CancelEdit&&!cancelSeg.Edit,"parse: 先不要修改/只分析 is a cancel-edit operation");
        var maxSeg=AgentTaskIntent.ParseSegment("取消字数限制，按内容需要撰写。");
        Verify(maxSeg.CancelLength,"parse: 取消字数限制 is a cancel-length operation");
        var negSeg=AgentTaskIntent.ParseSegment("不要做3页PPT，请做8页PPT。");
        Verify(negSeg.NegatesPresentation&&negSeg.Presentation&&negSeg.Pages==8,"parse: an object-scoped negation is recorded without suppressing the positive same-kind request (#6)");
    }

    // ---- 2) ordered state machine: replace / cancel / migrate -----------------------------------
    static void StateMachine(){
        // review #2: page and length replacement/cancel, same message and cross message.
        Verify(Derive("制作8页PPT，改为5页PPT。").requestedPages==5,"state: same-message page revision 8 -> 5");
        Verify(Derive("制作8页PPT。","改为5页PPT，不要8页。").requestedPages==5,"state: cross-message page revision 8 -> 5");
        Verify(Derive("制作8页PPT。","不要8页。").requestedPages==0,"state: a matching negated page count cancels the page spec");
        var length=Derive("创建一份800-1200字的报告。","改为300-500字。");
        Verify(length.minChars==300&&length.maxChars==500&&length.lengthUnit==AgentMeasureUnit.Hanzi,"state: a later message replaces the length range (review #2)");
        var cancelled=Derive("创建一份800-1200字的报告。","取消字数限制，按内容需要撰写。");
        Verify(cancelled.minChars==0&&cancelled.maxChars==0&&cancelled.lengthUnit==AgentMeasureUnit.None,"state: 取消字数限制 clears the stale range");
        var unitChars=Derive("创建一份文档，正文长度100-200字符，内容全部使用英文。");
        Verify(unitChars.requiresDocumentArtifact&&unitChars.minChars==100&&unitChars.maxChars==200&&unitChars.lengthUnit==AgentMeasureUnit.Characters,"state: character-unit length requirement maps to the requirement");
        var unitWords=Derive("Create a report with 100-200 words.");
        Verify(unitWords.minChars==100&&unitWords.maxChars==200&&unitWords.lengthUnit==AgentMeasureUnit.Words,"state: word-unit length requirement maps to the requirement");
        // review #3: 改成/先别做 replace or cancel the old task.
        var replaced=Derive("制作8页PPT。","改成写一份报告。");
        Verify(!replaced.requiresPresentationArtifact&&replaced.requiresDocumentArtifact&&replaced.requestedPages==0,"state: 改成写一份报告 REPLACES the deck requirement (no 8-page PPT left)");
        var sameKind=Derive("制作8页PPT。","改为5页PPT。");
        Verify(sameKind.requiresPresentationArtifact&&sameKind.requestedPages==5&&!sameKind.requiresDocumentArtifact,"state: 改为5页PPT on the same kind is a spec change, not a replacement");
        var cancelEdit=Derive("修改这份报告的预算。","先不要修改，只分析这份报告有哪些问题。");
        Verify(!cancelEdit.requiresTargetedEdit,"state: 先不要修改/只分析 cancels the targeted edit requirement");
        var cancelEdit2=Derive("修改这份报告的预算。","暂时别改，先给意见。");
        Verify(!cancelEdit2.requiresTargetedEdit,"state: 暂时别改 cancels the edit even without the word 修改");
        var cancelFullRead=DeriveAllowed(new List<string>{new string('a',32)},"完整阅读全部资料并分析。","不需要通读了，只看开头并给出初步意见。");
        Verify(!cancelFullRead.requiresFullSourceCoverage,"state: 只看开头/不需要通读 cancels the full-read requirement");
        Verify(DeriveAllowed(new List<string>{new string('a',32)},"完整阅读全部资料并分析。").requiresFullSourceCoverage,"state: an explicit complete-read goal still requires full coverage");
        // additive union vs replacement.
        var both=Derive("生成一份报告，并制作8页PPT。");
        Verify(both.requiresDocumentArtifact&&both.requiresPresentationArtifact&&both.requestedPages==8,"state: 并/同时 keeps BOTH deliverables");
        var convert=Derive("把这份报告转 8 页 PPT");
        Verify(convert.requiresPresentationArtifact&&!convert.requiresDocumentArtifact&&convert.requestedPages==8,"state: converting a report to a deck requires only the deck");
        // review #6: negative + positive same kind keeps the requirement.
        var negPres=Derive("不要做3页PPT，请做8页PPT。");
        Verify(negPres.requiresPresentationArtifact&&negPres.requestedPages==8,"state: 不要做3页PPT，请做8页PPT still requires the 8-page deck (#6)");
        var negDoc=Derive("不要创建旧版报告，请创建新版报告。");
        Verify(negDoc.requiresDocumentArtifact,"state: 不要创建旧版报告，请创建新版报告 still requires a document (#6)");
        var negOnly=Derive("制作8页PPT。","不要做PPT了。");
        Verify(!negOnly.requiresPresentationArtifact&&!negOnly.requiresArtifact,"state: a later 不要做PPT alone cancels the deck requirement");
        var negEdit=Derive("不要修改现有报告，创建一份新的摘要文档。");
        Verify(negEdit.requiresDocumentArtifact&&!negEdit.requiresTargetedEdit,"state: a prohibited edit does not cancel the positive creation request");
        // review #4: source scope only narrows on explicit exclusive selection; 全部 restores.
        var sources=new List<string>{new string('a',32),new string('b',32),new string('c',32)};
        var emphasis=DeriveAllowed(sources,"完整对比全部资料，重点核对第二份资料，创建一份报告。");
        Verify(emphasis.requiredSourceIds.Count==3&&emphasis.requiresFullSourceCoverage,"state: 重点核对第二份 with 全部 keeps all three sources and full coverage (review #4)");
        var narrowed=DeriveAllowed(sources,"只根据第二份资料创建一份报告。");
        Verify(narrowed.requiredSourceIds.Count==1&&narrowed.requiredSourceIds[0]==sources[1],"state: 只根据第二份资料 narrows to the second source");
        var expanded=DeriveAllowed(sources,"只根据第二份资料创建一份报告。","改为根据全部资料撰写报告。");
        Verify(expanded.requiredSourceIds.Count==3,"state: 改为全部/使用全部 restores every allowed source");
        var priority=DeriveAllowed(sources,"优先第二份资料，写一份报告。");
        Verify(priority.requiredSourceIds.Count==3,"state: 优先/重点 emphasis never narrows the source set");
        var allSources=DeriveAllowed(sources,"只看第二份资料写报告。","改为全部资料都要。");
        Verify(allSources.requiredSourceIds.Count==3,"state: an explicit 全部 later in the conversation restores the full scope");
        // formats and counts.
        var docx=Derive("创建一份Word文档，格式为DOCX。");
        Verify(docx.requiresDocumentArtifact&&docx.requiredFormat=="DOCX","state: explicit DOCX format lands on the requirement");
        var md=DeriveAllowed(new List<string>{new string('a',32)},"创建一份Markdown文档。");
        Verify(md.requiresDocumentArtifact&&md.requiredFormat=="MD","state: explicit Markdown format lands on the requirement");
        var txt=Derive("创建一份纯文本TXT文件。");
        Verify(txt.requiresDocumentArtifact&&txt.requiredFormat=="TXT","state: explicit TXT format lands on the requirement");
        var formatOverride=Derive("创建一份Word文档。","改成 Markdown 格式。");
        Verify(formatOverride.requiredFormat=="MD","state: a later message overrides the document format");
        var count2=Derive("创建两份报告。");
        Verify(count2.requiresDocumentArtifact&&count2.requiredCount==2,"state: 两份报告 records requiredCount=2");
        var countBack=Derive("创建两份报告。","改为一份报告。");
        Verify(countBack.requiredCount==1,"state: a later 一份报告 resets the deliverable count");
        // review #11: chat length is kept on the requirement.
        var chatLength=Derive("写800-1200字的摘要，直接在聊天里回复，不要创建文件。");
        Verify(chatLength.chatOnly&&!chatLength.requiresArtifact&&chatLength.minChars==800&&chatLength.maxChars==1200,"state: chat-only delivery keeps the length requirement (review #11)");
        // old API single-message unit/format requests.
        var instructionsState=new AgentTaskState{id=Id(),options=Options("制作8页PPT。",new List<string>())};
        instructionsState.options.delivery=AgentDeliveryRules.Derive(instructionsState.options);
        ApplyEv(instructionsState,"task.adjusted",new{text="改为5页PPT，不要8页。"});
        string deckInstructions=AgentDeliveryRules.Instructions(instructionsState);
        Verify(deckInstructions.Contains("exactly 5 pages")&&!deckInstructions.Contains("8 pages"),"state: instructions are generated from the latest requirement (5 pages, not 8)");
        Verify(deckInstructions.Contains("LATEST REQUIREMENT WINS"),"state: instructions state that the latest requirement wins and cancelled work is not redone");
        var lengthState=new AgentTaskState{id=Id(),options=Options("创建一份800-1200字的报告。",new List<string>())};
        lengthState.options.delivery=AgentDeliveryRules.Derive(lengthState.options);
        ApplyEv(lengthState,"task.adjusted",new{text="取消字数限制。"});
        Verify(!AgentDeliveryRules.Instructions(lengthState).Contains("800-1200"),"state: instructions never hint at a cancelled length range");
        // #1: chat <-> file migration, four messages.
        var migration=AgentTaskIntent.Current(new AgentTaskOptions{goal="总结资料，直接在聊天里回复，不要创建文件。",kernelVersion=3,allowed=new List<string>()},new[]{"现在请把总结保存为一个新的文档。"});
        Verify(!migration.ChatOnly&&migration.RequiresDocument,"state: chat->save keeps chatOnly=false with a document requirement");
        var back=AgentTaskIntent.Current(new AgentTaskOptions{goal="总结资料，直接在聊天里回复，不要创建文件。",kernelVersion=3,allowed=new List<string>()},new[]{"现在请把总结保存为一个新的文档。","还是直接在聊天里回复吧，不要文件。"});
        Verify(back.ChatOnly&&!back.RequiresDocument&&!back.RequiresPresentation,"state: save->chat clears the document requirement again (#1)");
        var again=AgentTaskIntent.Current(new AgentTaskOptions{goal="总结资料，直接在聊天里回复，不要创建文件。",kernelVersion=3,allowed=new List<string>()},new[]{"现在请把总结保存为一个新的文档。","还是直接在聊天里回复吧，不要文件。","还是保存成文档。"});
        Verify(!again.ChatOnly&&again.RequiresDocument,"state: chat->save again restores the document requirement");
    }

    // ---- 3) v0.12.1 derivation compatibility (users/probes/older tests) --------------------------
    static void DeriveCompatibility(){
        var sources=new List<string>{new string('a',32),new string('b',32),new string('c',32)};
        var report=DeriveAllowed(sources,"根据这三份资料生成一份总结报告");
        Verify(report.kind=="document"&&report.requiresArtifact&&!report.artifactIsPresentation&&report.requiredSourceIds.Count==3,"compat: a report goal requires a document and keeps the three sources");
        var ppt=DeriveAllowed(new List<string>{new string('a',32)},"把这份报告转成 8 页 PPT");
        Verify(ppt.kind=="presentation"&&ppt.requiresArtifact&&ppt.artifactIsPresentation&&ppt.requestedPages==8,"compat: 报告转成8页PPT requires a PPTX deck with 8 pages");
        var convert=DeriveAllowed(new List<string>{new string('a',32)},"把报告转 8 页 PPT");
        Verify(convert.kind=="presentation"&&convert.requestedPages==8,"compat: a bare conversion phrase still requires the deck");
        var longReport=DeriveAllowed(sources,"根据这三份长资料按最终修订生成一份报告");
        foreach(string goal in new[]{"根据所选资料创建100-300字的DOCX研究报告，采用最终修订预算，保留禁止事项和来源。","完整阅读资料，以最终修订为准创建报告。","按修订版资料生成一份新文档。","Create a report based on the final revision.","Write a new report using the revised budget."}){
            var versionedSource=DeriveAllowed(sources,goal);
            Verify(!versionedSource.requiresTargetedEdit,"source revision metadata does not demand an unrelated body edit: "+goal);
        }
        Verify(DeriveAllowed(sources,"请最终修订这份报告的预算段落。").requiresTargetedEdit,"an imperative final revision still requires a real body edit");
        Verify(longReport.requiresArtifact&&longReport.requiresFullSourceCoverage,"compat: generating from provided long sources requires full-source coverage");
        var brief=DeriveAllowed(sources,"只看第一份资料，写一段简要摘要");
        Verify(!brief.requiresFullSourceCoverage,"compat: an explicitly partial summary does not require full coverage");
        var chinese=DeriveAllowed(new List<string>{new string('a',32)},"制作一个十页的幻灯片介绍方案");
        Verify(chinese.kind=="presentation"&&chinese.requestedPages==10,"compat: a Chinese page count (十页) is parsed for deck creation");
        var edit=DeriveAllowed(new List<string>{new string('a',32)},"只把志愿者 6 人改为 8 人，其他内容不要动");
        Verify(edit.kind=="edit"&&edit.requiresTargetedEdit&&!edit.requiresArtifact,"compat: a targeted revision requires an edit, not a new artifact");
        foreach(string goal in new[]{"修改这份现有报告：只把6名志愿者改为8名志愿者，不要创建新报告，其他文字和预算完全保留。保存后核对。","Edit this existing report; do not create a new document.","Don't create a report, revise this existing document.","不要新建报告而是修改这份报告。","Rewrite this existing report without creating another document."}){
            var revision=DeriveAllowed(new List<string>{new string('a',32)},goal);
            Verify(revision.kind=="edit"&&revision.requiresTargetedEdit&&!revision.requiresArtifact,"compat: a prohibition on creating documents does not demand a new artifact: "+goal);
        }
        var preserve=DeriveAllowed(new List<string>{new string('a',32)},"不要修改现有报告，创建一份新的摘要文档。");
        Verify(preserve.kind=="document"&&!preserve.requiresTargetedEdit,"compat: a prohibited edit does not hide the later positive creation request");
        var continuation=DeriveAllowed(new List<string>{new string('a',32)},"续写接下来的情节");
        Verify(!continuation.requiresTargetedEdit&&!continuation.requiresArtifact,"compat: a continuation goal is content generation, not a body-edit requirement");
        var targeted=DeriveAllowed(new List<string>{new string('a',32)},"把报告中的志愿者人数从 6 改成 8");
        Verify(targeted.kind=="edit"&&targeted.requiresTargetedEdit,"compat: an explicit target plus a replacement verb is a targeted edit");
        var paragraph=DeriveAllowed(new List<string>{new string('a',32)},"把这份报告第 2 段改成新的开头");
        Verify(paragraph.kind=="edit"&&paragraph.requiresTargetedEdit,"compat: a paragraph-anchored rewrite is a targeted edit");
        var noteSummary=DeriveAllowed(new List<string>{new string('a',32)},"给这份笔记打标签并写一份摘要");
        Verify(!noteSummary.requiresTargetedEdit,"compat: metadata tagging plus summary creation is not a targeted body edit");
        var englishNote=DeriveAllowed(new List<string>{new string('a',32)},"Append this note");
        Verify(!englishNote.requiresTargetedEdit,"compat: an English note-append goal (write_note) is not a body-edit requirement");
        var metadata=DeriveAllowed(new List<string>{new string('a',32),new string('b',32)},"为这两份资料建议添加标签，先提出方案让我确认");
        Verify(!metadata.requiresTargetedEdit&&!metadata.requiresArtifact,"compat: metadata-only work is not misread as a body rewrite requirement");
        var coverage=DeriveAllowed(sources,"通读全部资料，回答有哪些风险");
        Verify(coverage.requiresFullSourceCoverage&&!coverage.requiresArtifact&&coverage.requiredSourceIds.Count==3,"compat: a read-everything goal requires exact full-source coverage");
        var question=DeriveAllowed(new List<string>{new string('a',32)},"这份资料讲了什么");
        Verify(question.kind=="read"&&!question.requiresArtifact&&!question.requiresTargetedEdit&&!question.requiresFullSourceCoverage,"compat: a plain question has no delivery requirement");
        var discuss=AgentDeliveryRules.Derive(new AgentTaskOptions{goal="生成一份报告",intent="discuss",allowed=new List<string>{new string('a',32)}},null);
        var readOnly=AgentDeliveryRules.Derive(new AgentTaskOptions{goal="生成一份报告",approvalPolicy="read_only",allowed=new List<string>{new string('a',32)}},null);
        Verify(!discuss.requiresArtifact&&!readOnly.requiresArtifact,"compat: discussion-only and read-only tasks never require an artifact");
    }

    // ---- 4) task.adjusted persistence/replay + tool re-derive (#1, #12/#13) ----------------------
    static void SteerChecks(string root){
        Directory.CreateDirectory(root);
        var fixture=new Fixture(Path.Combine(root,"library"));var lib=fixture.Lib;var service=fixture.Service;
        var source=lib.CreateDocument("调整来源","md","","ADJUST_SOURCE").id;
        var journal=new AgentJournal(Path.Combine(root,"journal"));
        // #12: page replacement is persisted in options.delivery, live Derive, instructions and replay.
        var deckOptions=Options("制作8页PPT。",new List<string>{source});service.PrepareDelivery(deckOptions);
        var deck=Seed(journal,deckOptions);
        Verify(deck.options.delivery!=null&&deck.options.delivery.requestedPages==8,"steer: the task-start baseline records the 8-page deck");
        // #1: chat-only task -> save file: the auto profile must re-derive and open create_document.
        var chatOptions=Options("总结这份资料，直接在聊天里回复，不要创建文件。",new List<string>{source});service.PrepareDelivery(chatOptions);
        Verify(chatOptions.allowedToolsAuto,"steer: PrepareDelivery marks an unset tool list as automatic");
        var chat=Seed(journal,chatOptions);
        chat.options.allowedTools=AgentToolProfiles.ForTask(chat.options,lib); // simulate a persisted auto profile
        var chatProfile=AgentToolProfiles.ForTask(chat.options,lib,chat.adjustments);
        Verify(!chatProfile.Contains("create_document"),"steer: a chat-only task has no creation tools");
        Emit(journal,chat,"task.adjusted",new{text="现在请把总结保存为一个新的文档。"});
        Verify(chat.options.delivery!=null&&chat.options.delivery.requiresDocumentArtifact&&!chat.options.delivery.chatOnly,"steer: chat->save updates the persisted requirement (#1)");
        Verify(chat.options.allowedTools.Contains("create_document"),"steer: chat->save re-derives the persisted automatic tool profile with create_document");
        var saveProfile=AgentToolProfiles.ForTask(chat.options,lib,chat.adjustments);
        var toolAllowed=typeof(AgentService).GetMethod("ToolAllowed",BindingFlags.NonPublic|BindingFlags.Instance);
        bool hostAllows=(bool)toolAllowed.Invoke(service,new object[]{chat,AgentRules.Tools.First(t=>t.Name=="create_document")});
        Verify(saveProfile.Contains("create_document")&&hostAllows,"steer: ForTask and the host ToolAllowed both see create_document after chat->save, even with a stale persisted list (#1)");
        var chatReplay=Replay(journal,chat);
        Verify(chatReplay.options.delivery!=null&&chatReplay.options.delivery.requiresDocumentArtifact&&chatReplay.options.allowedToolsAuto&&chatReplay.options.allowedTools.Contains("create_document"),"steer: the re-derived requirement and auto tool policy are replay-safe");
        Emit(journal,chat,"task.adjusted",new{text="还是直接在聊天里回复吧，不要文件。"});
        Verify(chat.options.delivery!=null&&chat.options.delivery.chatOnly&&!chat.options.delivery.requiresDocumentArtifact,"steer: save->chat clears the document requirement again (#1)");
        var backProfile=AgentToolProfiles.ForTask(chat.options,lib,chat.adjustments);
        Verify(!backProfile.Contains("create_document"),"steer: save->chat removes create_document from the current profile (no historical OR, #1)");
        // #13: read task adjusted to save a document.
        var readOptions=Options("总结这份资料",new List<string>{source});service.PrepareDelivery(readOptions);
        var read=Seed(journal,readOptions);
        Emit(journal,read,"task.adjusted",new{text="把总结保存为一个新的文档。"});
        Verify(read.options.delivery!=null&&read.options.delivery.requiresDocumentArtifact&&read.options.allowedTools.Contains("create_document"),"steer: a read task adjusted to save a document opens create_document (#13)");
        var readReplay=Replay(journal,read);
        Verify(readReplay.options.allowedTools.Contains("create_document"),"steer: the re-derived automatic tool list is replay-safe");
        // explicit user-picked list is never widened.
        var explicitOptions=Options("总结这份资料",new List<string>{source});
        explicitOptions.allowedTools=new List<string>{"read_items","finish_task"};explicitOptions.allowedToolsAuto=false;service.PrepareDelivery(explicitOptions);
        var explicitState=Seed(journal,explicitOptions);
        Emit(journal,explicitState,"task.adjusted",new{text="把总结保存为一个新的文档。"});
        Verify(explicitState.options.allowedTools.SequenceEqual(new[]{"read_items","finish_task"}),"steer: an explicit user-picked allowedTools list stays authoritative after an adjustment");
        // #12 page-count acceptance follows the adjustment.
        var pageOptions=Options("制作8页PPT。",new List<string>());service.PrepareDelivery(pageOptions);
        var pageTask=new AgentTaskState{id=Id(),options=pageOptions};
        var five=CreateDeck(lib,pageTask,"五页演示",5);ReadTool(lib,pageTask,"read_presentation",new{id=CreatedId(five),start_page=1,max_pages=8});
        Verify(service.DeliveryProblem(pageTask)!="","steer: before the adjustment an 8-page requirement rejects the real 5-page deck");
        ApplyEv(pageTask,"task.adjusted",new{text="改为5页PPT，不要8页。"});
        Verify(service.DeliveryProblem(pageTask)=="","steer: after task.adjusted the same 5-page deck satisfies the delivery check");
        // instructions use the latest contract.
        string deckInstructions=AgentDeliveryRules.Instructions(pageTask);
        Verify(deckInstructions.Contains("exactly 5 pages")&&!deckInstructions.Contains("8 pages"),"steer: instructions are regenerated from the latest adjustment");
    }

    // ---- 5) C-A -> C-B integration: acceptance reads the same current contract -------------------
    static void DeliveryIntegration(string root){
        Directory.CreateDirectory(root);
        var fixture=new Fixture(Path.Combine(root,"library"));var lib=fixture.Lib;var service=fixture.Service;
        // Length range + cancel; measured unit is Hanzi.
        var lengthOptions=Options("创建一份300-500字的报告",new List<string>());service.PrepareDelivery(lengthOptions);
        var lengthTask=new AgentTaskState{id=Id(),options=lengthOptions};
        var okDoc=CreateDocument(lib,lengthTask,"400字报告",new string('字',400));ReadTool(lib,lengthTask,"read_items",new{ids=new[]{CreatedId(okDoc)}});
        Verify(service.DeliveryProblem(lengthTask)=="","delivery: a 400-Hanzi document satisfies the current 300-500字 requirement");
        ApplyEv(lengthTask,"task.adjusted",new{text="改为800-1200字。"});
        Verify(service.DeliveryProblem(lengthTask)!="","delivery: a later 800-1200字 requirement rejects the old 400-Hanzi document (#2)");
        ApplyEv(lengthTask,"task.adjusted",new{text="取消字数限制。"});
        Verify(service.DeliveryProblem(lengthTask)=="","delivery: 取消字数限制 removes the stale range check (#2)");
        // Character unit acceptance: an all-English 120-non-whitespace-character document passes 100-200字符.
        var charOptions=Options("创建一份文档，正文长度100-200字符，内容全部使用英文。",new List<string>());service.PrepareDelivery(charOptions);
        var charTask=new AgentTaskState{id=Id(),options=charOptions};
        var englishDoc=CreateDocument(lib,charTask,"English document",String.Join(" ",Enumerable.Repeat("word",30)));
        ReadTool(lib,charTask,"read_items",new{ids=new[]{CreatedId(englishDoc)}});
        Verify(service.DeliveryProblem(charTask)=="","delivery: an English document satisfying 100-200字符 passes with the Characters unit (#9)");
        // Explicit format: DOCX required, MD delivered -> rejected; then DOCX delivered -> accepted.
        var formatOptions=Options("创建一份Word文档，格式为DOCX。",new List<string>());service.PrepareDelivery(formatOptions);
        var formatTask=new AgentTaskState{id=Id(),options=formatOptions};
        var wrong=CreateDocument(lib,formatTask,"交付的MD",new string('文',100));ReadTool(lib,formatTask,"read_items",new{ids=new[]{CreatedId(wrong)}});
        Verify(service.DeliveryProblem(formatTask)!="","delivery: a required DOCX is rejected when the final document is MD (#10)");
        var right=CreateDocument(lib,formatTask,"交付的DOCX",new string('文',100),"docx");ReadTool(lib,formatTask,"read_document",new{id=CreatedId(right)});
        Verify(service.DeliveryProblem(formatTask)=="","delivery: a real DOCX final deliverable satisfies the format requirement");
        // Chat-only length (#11).
        var chatOptions=Options("写800-1200字的摘要，直接在聊天里回复，不要创建文件。",new List<string>());service.PrepareDelivery(chatOptions);
        var chatTask=new AgentTaskState{id=Id(),options=chatOptions};
        Verify(service.DeliveryProblem(chatTask)!="","delivery: a chat-only answer shorter than 800 characters is rejected (#11)");
        chatTask.dialogue.Add(new AgentDialogue{id=Id(),role="agent",text=new string('答',850),sequence=chatTask.sequence+1});
        Verify(service.DeliveryProblem(chatTask)=="","delivery: a chat answer within 800-1200 characters passes (#11)");
        // Post-edit read-back (#8).
        var editDoc=lib.CreateDocument("预算报告","md","","项目预算100元。其他内容保留。");
        var editOptions=Options("把这份报告的预算从100元修改为200元。",new List<string>{editDoc.id});service.PrepareDelivery(editOptions);
        var editTask=new AgentTaskState{id=Id(),options=editOptions};
        ReadTool(lib,editTask,"read_document",new{id=editDoc.id});
        var replace=lib.PrepareAgentCall(editTask,new AgentAction{tool="replace_document_text",args=Dict(new{id=editDoc.id,old_text="100元",new_text="200元"})},Id(),null);
        ApplyEv(editTask,"tool.proposed",new{call=replace});replace=editTask.calls.Last();
        var replaceReceipt=lib.WriteAgentTool(editTask,replace,CancellationToken.None);
        ApplyEv(editTask,"tool.completed",new{callId=replace.id,result=replaceReceipt.result,stamp=replaceReceipt.stamp});
        Verify(service.DeliveryProblem(editTask)!="","delivery: an edit receipt alone is not enough without a post-edit read-back (#8)");
        ReadTool(lib,editTask,"read_document",new{id=editDoc.id});
        Verify(service.DeliveryProblem(editTask)=="","delivery: a current read-back after the edit satisfies the targeted-revision gate (#8)");
        // Final-deliverable binding (#5): the last document must itself satisfy length AND read-back.
        var mixOptions=Options("创建一份100-200字的报告",new List<string>());service.PrepareDelivery(mixOptions);
        var mixTask=new AgentTaskState{id=Id(),options=mixOptions};
        var small=CreateDocument(lib,mixTask,"已读但只有20字",new string('短',20));ReadTool(lib,mixTask,"read_items",new{ids=new[]{CreatedId(small)}});
        CreateDocument(lib,mixTask,"足够字数但未读",new string('文',150));
        Verify(service.DeliveryProblem(mixTask)!="","delivery: a read small document and an unread long document cannot be combined to pass (#5)");
        // Final-deck binding (#5B): the LAST deck's page count and full-page read-back are the ones checked.
        var deckOptions=Options("制作8页PPT",new List<string>());service.PrepareDelivery(deckOptions);
        var deckTask=new AgentTaskState{id=Id(),options=deckOptions};
        CreateDeck(lib,deckTask,"8页草稿未回读",8);
        var latest=CreateDeck(lib,deckTask,"5页最新版",5);ReadTool(lib,deckTask,"read_presentation",new{id=CreatedId(latest),start_page=1,max_pages=8});
        Verify(service.DeliveryProblem(deckTask)!="","delivery: the latest 5-page deck cannot borrow the old 8-page count (#5B)");
        var finalDeck=CreateDeck(lib,deckTask,"8页最终版",8);ReadTool(lib,deckTask,"read_presentation",new{id=CreatedId(finalDeck),start_page=1,max_pages=8});
        Verify(service.DeliveryProblem(deckTask)=="","delivery: the final 8-page deck with a full-page read-back passes (#5B)");
        // One-page read-back of an 8-page deliverable is rejected (#7A).
        var partialOptions=Options("制作8页PPT",new List<string>());service.PrepareDelivery(partialOptions);
        var partialTask=new AgentTaskState{id=Id(),options=partialOptions};
        var partialDeck=CreateDeck(lib,partialTask,"8页成品",8);ReadTool(lib,partialTask,"read_presentation",new{id=CreatedId(partialDeck),start_page=1,max_pages=1});
        Verify(service.DeliveryProblem(partialTask)!="","delivery: read_presentation(max_pages=1) on an 8-page deliverable is not sufficient (#7A)");
        // Full-source coverage counts presentation page reads for a PPTX source.
        var pptSource=lib.CreatePresentation("8页来源","",File.ReadAllBytes(lib.Find(CreatedId(finalDeck)).path));
        var coverageOptions=Options("完整阅读这份PPT并总结，直接在聊天里回复。",new List<string>{pptSource.id});service.PrepareDelivery(coverageOptions);
        var coverageTask=new AgentTaskState{id=Id(),options=coverageOptions};
        ReadTool(lib,coverageTask,"read_presentation",new{id=pptSource.id,start_page=1,max_pages=8});
        Verify(service.DeliveryProblem(coverageTask)=="","delivery: a fully page-read PPTX source counts as full-source coverage (#7B)");
    }

    // ---- 6) tool profiles read the same current contract (#1) -----------------------------------
    static void ToolProfileChecks(string root){
        Directory.CreateDirectory(root);
        var fixture=new Fixture(Path.Combine(root,"library"));var lib=fixture.Lib;var service=fixture.Service;
        string source=lib.CreateDocument("报告来源","md","","REPORT_FACT_报告").id;
        var reportOptions=new AgentTaskOptions{goal="根据资料写一份完整的报告",allowed=new List<string>{source}};service.PrepareDelivery(reportOptions);
        var report=AgentToolProfiles.ForTask(reportOptions,lib);
        Verify(report.Count<=14&&report.Contains("create_document")&&report.Contains("read_document")&&report.Contains("replace_document_text")&&!report.Contains("generate_subtitles")&&!report.Contains("index_video_frames"),"profile: the pure report base profile is <=14 tools with the delivery writes and no media capabilities");
        // Configured capabilities are appended after the base cap and are never trimmed (#19).
        string execDir=Path.Combine(lib.DataDir,"exec-workspace");Directory.CreateDirectory(execDir);AgentExecution.Save(lib,true,execDir,false);
        var configuredOptions=new AgentTaskOptions{goal="联网查询资料，写一份报告，同时检查工作区代码",allowed=new List<string>{source},useMemory=true,allowDelegation=true,allowSandbox=true,searchEndpoint="https://example.invalid/search",sessionId=Id()};
        service.PrepareDelivery(configuredOptions);
        var configured=AgentToolProfiles.ForTask(configuredOptions,lib);
        Verify(new[]{"web_search","fetch_url","run_code","delegate_parallel","search_memory","remember_memory","list_folder","read_text_file","write_text_file","run_command","create_document","read_items","finish_task"}.All(configured.Contains),"profile: configured capabilities plus the delivery tools survive the base-profile cap (#19)");
        if(ModuleRegistry.Builtins.ToolEnabled("mcp_call",lib)&&ModuleRegistry.Builtins.ToolEnabled("list_mcp_tools",lib)){
            Verify(configured.Contains("mcp_call")&&configured.Contains("list_mcp_tools"),"profile: an enabled MCP module adds mcp_call and list_mcp_tools to the automatic executable profile (#20)");
            var discuss=AgentToolProfiles.ForTask(new AgentTaskOptions{goal="解释这份报告",intent="discuss",allowed=new List<string>{source}},lib);
            Verify(discuss.Contains("list_mcp_tools")&&!discuss.Contains("mcp_call"),"profile: discussion/read-only tasks can discover MCP tools but never call them (#20)");
        }
        // chatOnly follows the CURRENT contract; a stale persisted auto list with chat->save is re-derived (#1).
        var chatOptions=new AgentTaskOptions{goal="写一份300字的摘要，直接在聊天里回复，不要创建文件。",allowed=new List<string>{source},sessionId=Id()};service.PrepareDelivery(chatOptions);
        var chatProfile=AgentToolProfiles.ForTask(chatOptions,lib);
        Verify(!chatProfile.Contains("create_document")&&!chatProfile.Contains("create_presentation")&&!chatProfile.Contains("copy_presentation"),"profile: a chat-only task never exposes file-creation tools");
        var saveProfile=AgentToolProfiles.ForTask(chatOptions,lib,new[]{"现在请把总结保存为一个新的文档。"});
        Verify(saveProfile.Contains("create_document"),"profile: chat->save opens create_document from the current contract (#1)");
        var backProfile=AgentToolProfiles.ForTask(chatOptions,lib,new[]{"现在请把总结保存为一个新的文档。","还是直接在聊天里回复吧，不要文件。"});
        Verify(!backProfile.Contains("create_document"),"profile: save->chat removes create_document again (no historical OR, #1)");
        // An automatic (allowedToolsAuto=true) persisted list with a manual override is still re-derived.
        var autoOptions=new AgentTaskOptions{goal="总结这份资料",allowed=new List<string>{source},sessionId=Id()};
        autoOptions.allowedTools=new List<string>{"read_items","finish_task"};autoOptions.allowedToolsAuto=true;
        var dynamic=AgentToolProfiles.ForTask(autoOptions,lib,new[]{"把总结保存为一个新的文档。"});
        Verify(dynamic.Contains("create_document"),"profile: an auto (allowedToolsAuto=true) persisted list is re-derived from the current contract even after a manual override");
        // Explicit list stays authoritative.
        var explicitList=new List<string>{"read_items","finish_task"};
        var explicitOptions=new AgentTaskOptions{goal="根据资料写一份完整的报告",allowed=new List<string>{source},allowedTools=new List<string>(explicitList),allowedToolsAuto=false};
        Verify(AgentToolProfiles.ForTask(explicitOptions,lib).SequenceEqual(explicitList),"profile: an explicit allowedTools list wins over the automatic goal profile");
        // The host's visible schema filter follows the same dynamic rule after task.adjusted.
        var taskTools=typeof(AgentService).GetMethod("TaskTools",BindingFlags.NonPublic|BindingFlags.Instance);
        var hostTask=new AgentTaskState{id=Id(),options=new AgentTaskOptions{goal="总结这份资料",allowed=new List<string>{source},sessionId=Id(),kernelVersion=3,allowedTools=new List<string>{"read_items","finish_task"},allowedToolsAuto=true}};
        bool beforeVisible=((AgentToolSpec[])taskTools.Invoke(service,new object[]{hostTask})).Any(t=>t.Name=="create_document");
        ApplyEv(hostTask,"task.adjusted",new{text="把总结保存为一个新的文档。"});
        bool afterVisible=((AgentToolSpec[])taskTools.Invoke(service,new object[]{hostTask})).Any(t=>t.Name=="create_document");
        Verify(!beforeVisible&&afterVisible,"profile: the host's visible TaskTools schema re-derives after task.adjusted (#13)");
    }
}
