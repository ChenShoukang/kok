using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

// v0.12.2 C-B: final-deliverable-bound acceptance. One final artifact ID/version per required kind (type,
// explicit format, measured length, real page count, current read-back all on that same file); targeted
// edits need a post-edit read-back; chat-only deliveries are measured on the final answer; source coverage
// counts complete presentation page reads. Fixtures mirror the independent review probe (#5/#7A/#8/#9/#10/#11)
// and are fully offline: real Library files and native task events, no model requests.
// Owned by acceptance-v122; Run(root,check) signature is registered by Lead.
class AgentAcceptanceV122Tests {
    static Action<bool,string> check;
    public static void Run(string root,Action<bool,string> check){
        AgentAcceptanceV122Tests.check=check;
        Directory.CreateDirectory(root);
        CrossDocumentMixed(root);
        CrossPresentationMixed(root);
        DeckSinglePageReadBack(root);
        EditWithoutPostRead(root);
        CharacterMeasuredUnit(root);
        RequiredDocumentFormat(root);
        ChatAnswerLength(root);
        PositiveNegativeControls(root);
        FullPresentationSourceCoverage(root);
        LegacyCompatibility(root);
    }
    static void Verify(bool ok,string name){check(ok,name);}
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> D(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static string Output(AgentCall call){if(call==null||call.status!="done")throw new Exception((call==null?"missing call":call.tool+" failed: "+call.error));return Convert.ToString(call.result["id"]);}
    static List<Dictionary<string,object>> Slides(int pages){
        var rows=new List<Dictionary<string,object>>();
        for(int n=1;n<=pages;n++)rows.Add(new Dictionary<string,object>{{"title","页面"+n},{"bullets",new[]{"第"+n+"页核对内容"}}});
        return rows;
    }

    // Offline native-task fixture: real Library + journaled task state + engine wired to the production read/
    // write tools; DeliveryCheck points at the production final-deliverable acceptance.
    sealed class Fx {
        public Library lib;public AgentService service;public AgentTaskState state;public AgentTaskEngine engine;
        public Fx(string root,string goal,params string[] ids){
            lib=new Library(root);lib.SetAppMode("smart",true);
            service=new AgentService(new ModuleContext(lib,null,new ModulePolicy(lib),null,CancellationToken.None,null,new Operations(lib,null),null));
            var options=new AgentTaskOptions{goal=goal,sessionId=Id(),kernelVersion=3,allowed=ids.ToList(),scope=new AgentScope{mode=ids.Length>0?"selected":"all",ids=ids.ToList()},requestLimit=6,budget=14000};
            service.PrepareDelivery(options);
            var journal=new AgentJournal(lib.DataDir);string taskId=Id();
            var created=journal.Append(taskId,"task.created",new{options,protocol="tools"},options.sessionId,"acceptance","created");
            state=AgentTaskState.Replay(taskId,new List<AgentEvent>{created});
            engine=new AgentTaskEngine(state,journal,()=>{},null,
                (s,c,t)=>lib.ReadAgentTool(s,c,t),
                (s,c,t)=>Task.FromResult(lib.WriteAgentTool(s,c,t)),
                (s,a,turn,provider)=>{var c=lib.PrepareAgentCall(s,a,turn,provider);c.steering=s.steerVersion;return c;},
                ()=>{},Current,()=>true,c=>lib.OperationStatus(c.id)!="undone");
            engine.DeliveryCheck=s=>service.DeliveryProblem(s);
        }
        public bool Current(AgentCall call){
            try{return (bool)typeof(AgentService).GetMethod("EvidenceCurrentFor",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(service,new object[]{call,state});}
            catch{return false;}
        }
        public void Sources(params string[] ids){
            state.options.allowed=ids.ToList();
            state.options.scope=new AgentScope{mode=ids.Length>0?"selected":"all",ids=ids.ToList()};
            try{state.options.delivery=AgentDeliveryRules.Derive(state.options,state.adjustments);}catch{}
        }
        public AgentCall Tool(string tool,object args,bool write=false){
            var call=lib.PrepareAgentCall(state,new AgentAction{tool=tool,args=D(args)},Id(),null);call.steering=state.steerVersion;
            engine.Emit("tool.proposed",new{call});call=state.calls.Last();
            try{
                if(write){var result=lib.WriteAgentTool(state,call,CancellationToken.None);engine.Emit("tool.completed",new{callId=call.id,result=result.result,stamp=result.stamp});}
                else{var result=lib.ReadAgentTool(state,call,CancellationToken.None).GetAwaiter().GetResult();engine.Emit("tool.completed",new{callId=call.id,result});}
            }catch(Exception error){engine.Emit("tool.failed",new{callId=call.id,kind=AgentFailure.Classify(error).kind,message=error.Message,retryable=false});}
            return state.calls.Last();
        }
        public AgentCall Doc(string name,string text){return DocFormat(name,"md",text);}
        public AgentCall DocFormat(string name,string format,string text){return Tool("create_document",new{name,format,text},true);}
        public AgentCall Deck(string title,int pages){return Tool("create_presentation",new{title,slides=Slides(pages)},true);}
        public AgentCall Read(string id){return Tool("read_document",new{id,max_chars=12000});}
        public AgentCall ReadDeck(string id,int startPage,int maxPages){return Tool("read_presentation",new{id,start_page=startPage,max_pages=maxPages});}
        public void Reply(string text){engine.Emit("assistant.reply",new{text,turnId=Id(),done=true,sources=new string[0],steering=state.steerVersion});}
        // Runs the real finish_task path. The finish call is proposed into the state first (as the engine does)
        // so a chat-length summary in the finish payload is visible to acceptance.
        public string Finish(string summary,params string[] evidence){
            var operations=state.calls.Where(c=>c.status=="done"&&!AgentCognition.IsControl(c.tool)).ToList();
            if(evidence.Length==0)evidence=operations.Where(Current).Take(1).Select(c=>c.id).ToArray();
            var steps=new[]{new AgentPlanStep{id="step",title="核对最终交付物",kind=evidence.Length==0?"reasoning":"tool",status="done",evidence=evidence.ToList()}};
            engine.Emit("task.plan",new{steps,steering=state.steerVersion});
            var finish=new AgentCall{id=Id(),tool="finish_task",steering=state.steerVersion,args=D(new{status="completed",summary,checks=new[]{new{claim="符合任务要求",evidence}}})};
            engine.Emit("tool.proposed",new{call=finish});
            var stored=state.calls.Last();
            try{AgentCognition.Run(engine,stored,Current);return "";}
            catch(AgentFailure error){return error.kind+": "+error.Message;}
        }
    }

    // #5 reproduction A: document A (20 chars) is read back, document B (150 chars) is not. The final
    // deliverable is B, so B's missing read-back must reject completed; A's conditions may not be borrowed.
    static void CrossDocumentMixed(string root){
        var fx=new Fx(Path.Combine(root,"cross-documents"),"创建一份100-200字的报告。");
        var shortDoc=fx.Doc("已读但只有20字",new string('短',20));var shortRead=fx.Read(Output(shortDoc));
        var finalDoc=fx.Doc("足够字数但未读",new string('文',150));
        string problem=fx.Finish("报告完成",shortRead.id);
        Verify(problem!=""&&problem.StartsWith("verification"),"acceptance: mixed documents cannot borrow length from one file and read-back from another (completed rejected)");
        Verify(problem.Contains(Output(finalDoc))||problem.Contains("足够字数但未读"),"acceptance: rejection names the FINAL document ID that failed read-back, not the other file");
    }

    // #5 reproduction B: an older 8-page deck was never read back, the newer 5-page deck was read fully. The
    // final deliverable is the 5-page deck, so the page count check must reject (5 != 8), not match the old one.
    static void CrossPresentationMixed(string root){
        var fx=new Fx(Path.Combine(root,"cross-presentations"),"制作8页PPT。");
        fx.Deck("8页草稿未回读",8);
        var latest=fx.Deck("5页最新版",5);
        var read=fx.ReadDeck(Output(latest),1,8);
        string problem=fx.Finish("8页演示文稿完成",read.id);
        Verify(problem!=""&&problem.StartsWith("verification"),"acceptance: old 8-page / latest 5-page decks cannot be combined into a completed delivery (rejected)");
        Verify(problem.Contains("实际 5 页")&&problem.Contains("8 页")||problem.Contains(Output(latest)),"acceptance: rejection reports the FINAL deck's real page count against the requested page count");
    }

    // #7A artifact side: a single successful read_presentation(max_pages=1) is not a full read-back for an
    // 8-page deliverable; a full page-range read then accepts completed.
    static void DeckSinglePageReadBack(string root){
        var fx=new Fx(Path.Combine(root,"deck-single-page"),"制作8页PPT。");
        var deck=fx.Deck("8页成品",8);
        var partial=fx.ReadDeck(Output(deck),1,1);
        string problem=fx.Finish("演示文稿已核对",partial.id);
        Verify(problem!=""&&problem.StartsWith("verification"),"acceptance: one read page is not a sufficient read-back for an 8-page final deck");
        var full=fx.ReadDeck(Output(deck),1,8);
        string accepted=fx.Finish("演示文稿全部核对",full.id);
        Verify(accepted=="","acceptance: reading the final deck's full page range accepts completed");
    }

    // #8: a replace_document_text receipt alone is not verification; the edited final version needs a read
    // after the edit. The control then reads the modified body and completes.
    static void EditWithoutPostRead(string root){
        var fx=new Fx(Path.Combine(root,"edit-no-post-read"),"把这份报告的预算从100元修改为200元。");
        var target=fx.lib.CreateDocument("待修改报告","md","","项目预算100元。其他内容保留。");
        fx.Sources(target.id);
        fx.Read(target.id);
        var edit=fx.Tool("replace_document_text",new{id=target.id,old_text="100元",new_text="200元"},true);
        Verify(Output(edit)==target.id,"acceptance: the targeted edit was applied to the expected target");
        string problem=fx.Finish("正文已经修改并核对",edit.id);
        Verify(problem!=""&&problem.StartsWith("verification"),"acceptance: an edit receipt without a post-edit read-back is rejected (#8 fallback)");
        Verify(problem.Contains("回读"),"acceptance: post-edit rejection asks for a read-back of the edited version");
        var postRead=fx.Read(target.id);
        string accepted=fx.Finish("修改并回读完成",postRead.id);
        Verify(accepted=="","acceptance: a current read-back after the edit accepts completed");
        Verify(fx.lib.DocumentSource(target.id).text.Contains("200元")&&fx.lib.DocumentSource(target.id).text.Contains("其他内容保留"),"acceptance: the accepted edit kept the unrelated body text");
    }

    // #9: "100-200字符" with English content must be measured in characters, not Hanzi (149 chars / 120
    // non-space characters is inside the range, Hanzi-only counting would wrongly report 0).
    static void CharacterMeasuredUnit(string root){
        var fx=new Fx(Path.Combine(root,"character-unit"),"创建一份文档，正文长度100-200字符，内容全部使用英文。");
        string english=String.Join(" ",Enumerable.Repeat("word",30));
        var doc=fx.Doc("English document",english);
        var read=fx.Read(Output(doc));
        string problem=fx.Finish("已生成英文文档",read.id);
        Verify(problem=="","acceptance: a 100-200 CHARACTER requirement is satisfied by a 149-character English document (#9 unit-aware meter)");
    }

    // #10: an explicit DOCX requirement rejects an MD delivery; a real DOCX delivery with a sufficient
    // read-back is accepted.
    static void RequiredDocumentFormat(string root){
        var fx=new Fx(Path.Combine(root,"format-mismatch"),"创建一份Word文档，格式为DOCX。");
        var md=fx.Doc("交付的MD",new string('文',100));
        var mdRead=fx.Read(Output(md));
        string problem=fx.Finish("Word文档完成",mdRead.id);
        Verify(problem!=""&&problem.StartsWith("verification"),"acceptance: an explicit DOCX requirement rejects an MD delivery");
        Verify(problem.Contains("DOCX")&&(problem.Contains("MD")||problem.Contains("格式")),"acceptance: format rejection reports the required DOCX and the actual MD format");
        var ok=new Fx(Path.Combine(root,"format-docx"),"创建一份Word文档，格式为DOCX。");
        var docx=ok.DocFormat("交付的Word文档","docx",new string('文',100));
        var docxRead=ok.Read(Output(docx));
        Verify(ok.lib.Find(Output(docx)).ext=="DOCX","acceptance: the DOCX control really produced a DOCX file");
        Verify(ok.Finish("Word文档完成",docxRead.id)=="","acceptance: a real DOCX delivery with a sufficient read-back is accepted");
    }

    // #11: a chat-only delivery is measured too. "摘要：项目可行。" (6 Hanzi) is far below 800-1200; a final
    // answer of 900 Hanzi in the same unit is accepted.
    static void ChatAnswerLength(string root){
        var fx=new Fx(Path.Combine(root,"chat-short"),"写800-1200字的摘要，直接在聊天里回复，不要创建文件。");
        string problem=fx.Finish("摘要：项目可行。");
        Verify(problem!=""&&problem.StartsWith("verification"),"acceptance: a chat-only 800-1200字 request rejects a 6-character final answer (#11)");
        Verify(problem.Contains("800")&&(problem.Contains("实测")||problem.Contains("字数")),"acceptance: chat-length rejection reports the measured value and the required range");
        var ok=new Fx(Path.Combine(root,"chat-long"),"写800-1200字的摘要，直接在聊天里回复，不要创建文件。");
        ok.Reply(new string('答',900));
        string accepted=ok.Finish("已按要求完成");
        Verify(accepted=="","acceptance: a 900-Hanzi final chat answer satisfies the 800-1200字 chat requirement");
    }

    // Positive/negative controls on the same production finish path (#5): a valid final document passes, the
    // same document without read-back is rejected.
    static void PositiveNegativeControls(string root){
        var valid=new Fx(Path.Combine(root,"control-valid"),"创建一份100-200字的报告。");
        var doc=valid.Doc("合格报告",new string('文',150));
        var read=valid.Read(Output(doc));
        Verify(valid.Finish("报告完成",read.id)=="","acceptance control: a 150-Hanzi final document read back after creation is accepted");
        var unread=new Fx(Path.Combine(root,"control-unread"),"创建一份100-200字的报告。");
        var unreadDoc=unread.Doc("未读报告",new string('文',150));
        string problem=unread.Finish("报告完成");
        Verify(problem!=""&&problem.StartsWith("verification")&&(problem.Contains(Output(unreadDoc))||problem.Contains("未读报告")),"acceptance control: the same valid document without read-back is rejected");
        // A 5-page deck that matches the request and is fully read is a positive control for the deck path.
        var deckOk=new Fx(Path.Combine(root,"control-deck"),"制作5页PPT。");
        var deck=deckOk.Deck("5页成品",5);
        var deckRead=deckOk.ReadDeck(Output(deck),1,5);
        Verify(deckOk.Finish("演示文稿完成",deckRead.id)=="","acceptance control: a final deck whose real page count matches the request and is fully read is accepted");
    }

    // #7A source side: a presentation used as a source counts its exact page coverage toward full-source
    // coverage; reading every page satisfies the requirement, a partial page read does not.
    static void FullPresentationSourceCoverage(string root){
        var full=new Fx(Path.Combine(root,"ppt-source-full"),"完整阅读这份PPT并总结，直接在聊天里回复。");
        var source=full.lib.CreatePresentation("8页来源","",AgentSlides.Build(D(new{title="8页来源",slides=Slides(8)})));
        full.Sources(source.id);
        var allPages=full.ReadDeck(source.id,1,8);
        Verify(allPages.status=="done","acceptance: the presentation source was read through read_presentation");
        string accepted=full.Finish("已读完全部8页并总结",allPages.id);
        Verify(accepted=="","acceptance: complete presentation page coverage satisfies full-source coverage (#7A)");
        var partial=new Fx(Path.Combine(root,"ppt-source-partial"),"完整阅读这份PPT并总结，直接在聊天里回复。");
        var source2=partial.lib.CreatePresentation("8页来源","",AgentSlides.Build(D(new{title="8页来源",slides=Slides(8)})));
        partial.Sources(source2.id);
        var firstPage=partial.ReadDeck(source2.id,1,1);
        string problem=partial.Finish("已读完并总结",firstPage.id);
        Verify(problem!=""&&problem.StartsWith("verification"),"acceptance: reading only one page of a required presentation source is not full coverage (#7A)");
    }

    // Compatibility: a task journaled by an older host has options.delivery==null; the delivery layer must
    // not start enforcing a derived requirement on resume.
    static void LegacyCompatibility(string root){
        var fx=new Fx(Path.Combine(root,"legacy"),"创建一份100-200字的报告。");
        fx.state.options.delivery=null;
        var doc=fx.Doc("历史任务报告",new string('文',20));
        var read=fx.Read(Output(doc));
        Verify(fx.Finish("历史任务完成",read.id)=="","acceptance: options.delivery==null keeps the pre-0.12 finish behavior (no derived acceptance on legacy tasks)");
    }
}
