using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using kok;

// v0.12.1 C2 (reading-agent): unified exact reading coverage across read_items/read_document, hard page
// budgets, sufficient artifact read-back (#7), no second clipping of exact pages (#16), read_document
// coverage recognition (#17), fair result budgets (#18), retained source openings/constraints (#3).
// Owned by reading-agent; Run(root,check) is registered by Lead in NativeTests.
class AgentReadingV013Tests {
    public static void Run(string root, Action<bool,string> check) {
        Directory.CreateDirectory(root);
        ReadDocumentDelivery(Path.Combine(root,"read-document-delivery"),check);
        ReadDocumentCoverage(Path.Combine(root,"read-document-coverage"),check);
        ReadDocumentNotesCoverage(Path.Combine(root,"read-document-notes"),check);
        ReadBackSufficiency(Path.Combine(root,"read-back-sufficiency"),check);
        ContextBudgets(Path.Combine(root,"context-budgets"),check);
        MiddleConstraintRetention(Path.Combine(root,"middle-constraint-retention"),check);
        RetainedConstraintExtraction(Path.Combine(root,"retained-constraints"),check);
        PresentationCoverage(Path.Combine(root,"presentation-coverage"),check);
        PresentationSourceCoverage(Path.Combine(root,"presentation-source-coverage"),check);
        RetainedFinalRevision(Path.Combine(root,"retained-final-revision"),check);
    }
    static Library Smart(string path){var library=new Library(path);library.SetAppMode("smart",true);return library;}
    static AgentTaskState NewTask(params string[] ids){return new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{goal="读取资料全文",allowed=ids.ToList(),budget=14000,requestLimit=6}};}
    static AgentService Service(Library library){return new AgentService(new ModuleContext(library,null,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null));}
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> Dict(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static Dictionary<string,string> Contents(AgentService service,AgentTaskState state){
        var method=typeof(AgentService).GetMethod("CallContents",BindingFlags.NonPublic|BindingFlags.Instance);
        return (Dictionary<string,string>)method.Invoke(service,new object[]{state});
    }
    static string ContextText(AgentService service,AgentTaskState state){return String.Join("\n",Contents(service,state).Values);}
    static List<object> Messages(AgentService service,AgentTaskState state){
        var method=typeof(AgentService).GetMethod("TaskMessages",BindingFlags.NonPublic|BindingFlags.Instance);
        return (List<object>)method.Invoke(service,new object[]{state,"TEST_CATALOG","TEST_GUIDANCE","TEST_DYNAMIC",false});
    }
    static string MessageText(AgentService service,AgentTaskState state){return Library.Json.Serialize(Messages(service,state));}
    static List<Dictionary<string,object>> Rows(Dictionary<string,object> result){return AgentTaskState.Get<List<Dictionary<string,object>>>(result,"coverage")??new List<Dictionary<string,object>>();}
    static int Int(Dictionary<string,object> row,string key){return row.ContainsKey(key)&&row[key]!=null?Convert.ToInt32(row[key]):-999;}
    static string Str(Dictionary<string,object> row,string key){return row.ContainsKey(key)&&row[key]!=null?Convert.ToString(row[key]):"";}
    static bool Bool(Dictionary<string,object> row,string key){return row.ContainsKey(key)&&row[key]!=null&&Convert.ToBoolean(row[key]);}
    static void AddDialogue(AgentTaskState state,AgentCall call,string text){
        state.dialogue.Add(new AgentDialogue{id=Id(),role="agent",text=text,turnId=call.turnId,sequence=state.sequence++});
    }
    // read_items through the real exact reader (the tool layer normally precedes this with scope validation).
    static AgentCall ReadItems(Library library,AgentTaskState task,List<string> ids,int offset,int maxChars){
        var call=new AgentCall{id=Id(),turnId=Id(),tool="read_items",status="done",args=new Dictionary<string,object>{{"ids",new List<string>(ids)}}};
        if(offset>0)call.args["offset"]=offset;
        call.args["max_chars"]=maxChars;
        var result=library.ReadAgentItemsExact(task,call);
        call.result=result;call.sequence=task.calls.Count+1;task.calls.Add(call);
        return call;
    }
    static AgentCall ReadDocument(Library library,AgentTaskState task,string id,int offset,int maxChars){
        var call=new AgentCall{id=Id(),turnId=Id(),tool="read_document",status="done",args=new Dictionary<string,object>{{"id",id},{"offset",offset},{"max_chars",maxChars}}};
        var result=library.ReadAgentDocument(call,CancellationToken.None);
        call.result=result;call.sequence=task.calls.Count+1;task.calls.Add(call);
        return call;
    }
    // Real editable PPTX fixture (v0.12.2 C-C coverage tests).
    static Entry Deck(Library library,string name,int pages){
        var slides=new List<Dictionary<string,object>>();
        for(int n=1;n<=pages;n++)slides.Add(new Dictionary<string,object>{{"title","第 "+n+" 页"},{"bullets",new[]{"要点 "+n}}});
        return library.CreatePresentation(name,"",AgentSlides.Build(new Dictionary<string,object>{{"title",name},{"slides",slides}}));
    }
    static AgentCall ReadPresentation(Library library,AgentTaskState task,string id,int startPage,int maxPages){
        var call=new AgentCall{id=Id(),turnId=Id(),tool="read_presentation",status="done",args=new Dictionary<string,object>{{"id",id},{"start_page",startPage},{"max_pages",maxPages}}};
        var result=library.ReadAgentPresentation(call);
        call.result=result;call.sequence=task.calls.Count+1;task.calls.Add(call);
        return call;
    }
    static int Count(string text,string token){int count=0,at=0;if(String.IsNullOrEmpty(text)||String.IsNullOrEmpty(token))return 0;while((at=text.IndexOf(token,at,StringComparison.Ordinal))>=0){count++;at+=token.Length;}return count;}

    // ---- #16: a 10,518-character document read through read_document (10,738-char serialized result) must
    // reach the model intact, middle included, instead of being cropped a second time to ~7,974 characters.
    static void ReadDocumentDelivery(string root,Action<bool,string> check){
        var library=Smart(root);var service=Service(library);
        string body=new string('a',6000)+"MIDDLE_CRITICAL_FACT"+new string('b',4500);
        var document=library.CreateDocument("中段关键事实","md","",body);
        var task=NewTask(document.id);
        var call=ReadDocument(library,task,document.id,0,12000);
        string original=Convert.ToString(call.result["text"]);
        check(original.Length==10738&&original.Contains("MIDDLE_CRITICAL_FACT"),"read_document serializes the whole 10,518-character document (10,738-character result) including the middle fact");
        check(Str(call.result,"mode")=="exact"&&Str(call.result,"domain")=="document","read_document marks its result mode=exact and coverage domain=document");
        var rows=Rows(call.result);
        bool metadata=rows.Count==1&&Str(rows[0],"id")==document.id&&Str(rows[0],"domain")=="document"&&Str(rows[0],"mode")=="exact"&&Bool(rows[0],"full")&&!Bool(rows[0],"hasMore")&&Int(rows[0],"nextOffset")==-1&&Int(rows[0],"readStart")==0&&Int(rows[0],"readEnd")==body.Length&&Int(rows[0],"totalChars")==body.Length&&Str(rows[0],"digest").Length==64;
        check(metadata,"read_document coverage row uses the read_items schema (ranges/total/full/hasMore/digest/domain)");
        var openings=AgentTaskState.Get<List<Dictionary<string,object>>>(call.result,"openings");
        check(openings!=null&&openings.Count==1&&Str(openings[0],"id")==document.id&&Str(openings[0],"text").Length==AgentReading.OpeningChars&&body.StartsWith(Str(openings[0],"text")),"read_document keeps a per-source opening of the first 800 verbatim source characters");
        string sent=Contents(service,task)[call.id];
        check(sent==original&&sent.Contains("MIDDLE_CRITICAL_FACT")&&!sent.Contains("middle省略"),"the full read_document result reaches the model untouched: no second middle clipping and the middle fragment survives");
    }

    // ---- #17: FullCoverage accepts a complete read_document read (one page and multi-page paging).
    static void ReadDocumentCoverage(string root,Action<bool,string> check){
        var library=Smart(root);var service=Service(library);
        var small=library.CreateDocument("短文档","md","",new string('讀',1000));
        var smallTask=NewTask(small.id);string detail;
        check(!AgentReading.FullCoverage(library,smallTask,new[]{small.id},out detail)&&detail.Contains("尚未读取"),"before any read, FullCoverage names the unread source instead of accepting it");
        ReadDocument(library,smallTask,small.id,0,12000);
        check(AgentReading.FullCoverage(library,smallTask,new[]{small.id},out detail)&&detail=="","read_document reading a 1,000-character document to its end satisfies FullCoverage");
        // Multi-page: page read_document across a 20,000-character body and require exact ranges to tile.
        var large=library.CreateDocument("长文档","md","",new string('x',20000));
        var largeTask=NewTask(large.id);
        var first=ReadDocument(library,largeTask,large.id,0,12000);
        check(!AgentReading.FullCoverage(library,largeTask,new[]{large.id},out detail)&&detail.Contains("未读全")&&detail.Contains("20000"),"a partial read_document page is rejected with the missing character range");
        int offset=Int(Rows(first.result)[0],"nextOffset");int guard=0;
        while(offset>=0&&guard++<10){var page=ReadDocument(library,largeTask,large.id,offset,12000);offset=Int(Rows(page.result)[0],"nextOffset");}
        check(guard>=1&&guard<=10&&AgentReading.FullCoverage(library,largeTask,new[]{large.id},out detail)&&detail=="","paging read_document by next_offset tiles the whole 20,000-character body and satisfies FullCoverage");
        // Cross-tool: read_items prefix + read_document remainder must merge when both domains are the same text.
        var mixed=library.CreateDocument("混合读取","md","",new string('m',20000));
        var mixedTask=NewTask(mixed.id);var prefix=ReadItems(library,mixedTask,new List<string>{mixed.id},0,12000);
        int prefixEnd=Int(Rows(prefix.result)[0],"readEnd");
        check(prefixEnd>0&&prefixEnd<20000,"the read_items prefix stops before the source end");
        check(!AgentReading.FullCoverage(library,mixedTask,new[]{mixed.id},out detail),"the read_items prefix alone is not full coverage");
        ReadDocument(library,mixedTask,mixed.id,prefixEnd,12000);
        check(AgentReading.FullCoverage(library,mixedTask,new[]{mixed.id},out detail)&&detail=="","read_items plus read_document exact ranges merge across compatible domains into full coverage");
        // recall_result still never counts, and a stale read is rejected after content changes.
        var recallTask=NewTask(small.id);
        recallTask.calls.Add(new AgentCall{id=Id(),tool="recall_result",status="done",result=new Dictionary<string,object>{{"text","stored result replay"}}});
        check(!AgentReading.FullCoverage(library,recallTask,new[]{small.id},out detail),"recall_result replay never counts as original-source coverage");
        library.SaveDocument(large.id,new string('x',20000)+" changed",library.DocumentSource(large.id).signature,null);
        check(!AgentReading.FullCoverage(library,largeTask,new[]{large.id},out detail)&&(detail.Contains("失效")||detail.Contains("内容已变化")),"a content change invalidates read_document coverage digests");
    }

    // ---- Lead review point 1: with notes the readable domain (body+notes) and the document domain (body)
    // differ, so coverage rows carry a domain and read_document is validated against the document domain.
    static void ReadDocumentNotesCoverage(string root,Action<bool,string> check){
        var library=Smart(root);
        var document=library.CreateDocument("带备注成果","md","",new string('a',1000));
        var item=library.Find(document.id);
        item.notes="内部备注：预算上限 15000 元，验收负责人 江岚。";library.Save();
        var readable=library.AgentReadSourceText(document.id);
        check(readable.text.Length>1000&&readable.text.Contains("预算上限 15000 元"),"notes are part of the readable domain (AgentReadSourceText) but not of the document body");
        var task=NewTask(document.id);string detail;
        var call=ReadDocument(library,task,document.id,0,12000);
        var rows=Rows(call.result);
        check(rows.Count==1&&Str(rows[0],"domain")=="document"&&Str(rows[0],"digest")!=readable.digest,"read_document rows use the document domain and a digest that differs from the body+notes readable digest");
        check(AgentReading.FullCoverage(library,task,new[]{document.id},out detail)&&detail=="","a complete read_document body read satisfies FullCoverage for a document that also has notes (document domain; notes are not body)");
        check(!library.DocumentSource(document.id).text.Contains("预算上限"),"the document body itself does not contain the note text, so the domains really differ");
    }

    // ---- #7: "exists a read-back" is not enough; the read-back must continuously cover the artifact body.
    static void ReadBackSufficiency(string root,Action<bool,string> check){
        var library=Smart(root);
        string body=new string('x',10000);
        var artifact=library.CreateDocument("回读检查成果","md","",body);
        var readable=library.AgentReadSourceText(artifact.id);
        // Two-character read-back row recorded after the create call: must NOT be sufficient.
        var twoCharTask=NewTask();
        twoCharTask.calls.Add(new AgentCall{id=Id(),tool="create_document",status="done",sequence=1,result=new Dictionary<string,object>{{"id",artifact.id}}});
        var twoRow=new Dictionary<string,object>{{"id",artifact.id},{"name",artifact.name},{"totalChars",readable.Length},{"readStart",0},{"readEnd",2},{"readChars",2},{"full",false},{"hasMore",true},{"nextOffset",2},{"digest",readable.digest},{"mode","exact"},{"sampled",false},{"domain","readable"}};
        twoCharTask.calls.Add(new AgentCall{id=Id(),tool="read_items",status="done",sequence=2,result=new Dictionary<string,object>{{"text","xx"},{"coverage",new List<Dictionary<string,object>>{twoRow}}}});
        string detail;
        check(!AgentReading.ReadBackSufficient(library,twoCharTask,artifact.id,1,8000,out detail)&&detail.StartsWith("回读不足")&&detail.Contains("0-8000"),"a 2-character read-back of a 10,000-character artifact is insufficient and detail names the uncovered range");
        // A whole read_document body read after creation is sufficient (#7 fix through the document domain).
        var documentTask=NewTask();
        documentTask.calls.Add(new AgentCall{id=Id(),tool="create_document",status="done",sequence=1,result=new Dictionary<string,object>{{"id",artifact.id}}});
        ReadDocument(library,documentTask,artifact.id,0,12000);
        check(AgentReading.ReadBackSufficient(library,documentTask,artifact.id,1,8000,out detail)&&detail=="","reading the artifact body fully with read_document after creation is a sufficient read-back");
        // A full read_items paging read is equally sufficient.
        var itemsTask=NewTask();
        itemsTask.calls.Add(new AgentCall{id=Id(),tool="create_document",status="done",sequence=1,result=new Dictionary<string,object>{{"id",artifact.id}}});
        int offset=0,guard=0;
        while(offset>=0&&guard++<10){var page=ReadItems(library,itemsTask,new List<string>{artifact.id},offset,12000);offset=Int(Rows(page.result)[0],"nextOffset");}
        check(AgentReading.ReadBackSufficient(library,itemsTask,artifact.id,1,8000,out detail)&&detail=="","paging read_items through the whole artifact is a sufficient read-back");
        // Reads recorded at or before afterSequence never count; a later content change invalidates the digest.
        var beforeCreate=NewTask();
        ReadDocument(library,beforeCreate,artifact.id,0,12000);
        beforeCreate.calls.Add(new AgentCall{id=Id(),tool="create_document",status="done",sequence=beforeCreate.calls.Count+1,result=new Dictionary<string,object>{{"id",artifact.id}}});
        check(!AgentReading.ReadBackSufficient(library,beforeCreate,artifact.id,beforeCreate.calls.Max(c=>c.sequence),8000,out detail)&&detail.Contains("回读不足"),"a read recorded before the write cannot prove the post-write artifact content");
        library.SaveDocument(artifact.id,body+"\n后续修改",library.DocumentSource(artifact.id).signature,null);
        check(!AgentReading.ReadBackSufficient(library,documentTask,artifact.id,1,8000,out detail)&&detail.Contains("回读不足")&&detail.Contains("失效"),"a content change invalidates the recorded read-back digest");
    }

    // ---- #18: hard max_chars cap, no starvation, and a fair floor for every parallel result.
    static void ContextBudgets(string root,Action<bool,string> check){
        var library=Smart(root);var service=Service(library);
        var docs=Enumerable.Range(1,8).Select(n=>library.CreateDocument("资料"+n,"md","",new string((char)('a'+n),1000))).ToList();
        var ids=docs.Select(document=>document.id).ToList();
        var tiny=NewTask(ids.ToArray());tiny.options.budget=6000;string turn=Id();
        // Probe scenario (#18): four 200-character pages over 8 sources in one turn.
        for(int n=0;n<4;n++)tiny.calls.Add(new AgentCall{id=Id(),turnId=turn,tool="read_items",status="queued",args=Dict(new{ids,max_chars=200})});
        foreach(var call in tiny.calls){call.status="running";call.result=library.ReadAgentItemsExact(tiny,call);call.status="done";}
        bool capped=tiny.calls.All(call=>Convert.ToString(call.result["text"]).Length<=200);
        check(capped,"read_items honors max_chars=200 as a hard cap even with 8 requested sources");
        var tinySent=Contents(service,tiny);
        check(tiny.calls.All(call=>tinySent[call.id].Length>=Convert.ToString(call.result["text"]).Length-10),"each small page reaches the model essentially unchanged (no secondary starvation crop)");
        check(tiny.calls.All(call=>tinySent[call.id].Length>0)&&tinySent.Values.Sum(value=>value.Length)<=tiny.options.budget,"every one of the four results is delivered and the shared 6,000-character budget is respected");
        // Fairness (#18): four large non-exact results in a 6,000-character budget each get their floor.
        var large=NewTask();large.options.budget=6000;
        string largeTurn=Id();
        for(int n=0;n<4;n++)large.calls.Add(new AgentCall{id=Id(),turnId=largeTurn,tool="read_items",status="done",result=new Dictionary<string,object>{{"text",new string('q',8000)+(char)('0'+n)}}});
        var largeSent=Contents(service,large);
        check(largeSent.Count==4&&largeSent.Values.All(value=>value.Length>=300),"four 8,000-character historic results under a 6,000-character budget each keep at least 300 characters (no 29-character starvation)");
        check(largeSent.Values.Sum(value=>value.Length)<=large.options.budget,"the fair result allocation never exceeds the task result budget");
        // Newest placement: the newest result in that fair set keeps clearly more than the floor.
        string newestId=large.calls.Last().id;
        check(largeSent[newestId].Length>300&&largeSent[newestId].Length>largeSent[large.calls[0].id].Length,"the newest result still receives more room than the oldest under the fair budget");
    }

    // ---- #3: constraints near character 500 of three sources and their tail revisions must all appear in the
    // final model context (openings + retained constraints + the last exact page).
    static void MiddleConstraintRetention(string root,Action<bool,string> check){
        var library=Smart(root);var service=Service(library);
        var documents=new List<Entry>();
        for(int n=1;n<=3;n++){
            string constraint="第"+n+"份中部约束：预算上限 "+(12000+n)+" 元，禁止收集参与者联系方式，负责人 江岚。";
            string revision="\n最终修订"+n+"：预算总额 "+(12000+n)+" 元，有效反馈至少 60 份。";
            int filler=Math.Max(0,8400-480-constraint.Length-revision.Length);
            string body=new string((char)('甲'+n-1),480)+constraint+new string((char)('乙'+n-1),filler)+revision;
            documents.Add(library.CreateDocument("约束来源"+n,"md","",body));
        }
        var task=NewTask(documents.Select(document=>document.id).ToArray());
        var ids=documents.Select(document=>document.id).ToList();
        int offset=0,guard=0;
        while(offset>=0&&guard++<10){
            var page=ReadItems(library,task,ids,offset,12000);
            AddDialogue(task,task.calls.Last(),"已精确读取第 "+guard+" 页");
            offset=Int(Rows(page.result)[0],"nextOffset");
        }
        check(guard>=2&&guard<=10,"three 8,400-character sources need several exact pages");
        string finalContext=MessageText(service,task);
        for(int n=1;n<=3;n++){
            string token="第"+n+"份中部约束";
            check(finalContext.Contains(token)&&finalContext.Contains("最终修订"+n),"final model context keeps source "+n+"'s ~500-character constraint and its tail revision");
        }
        var contents=Contents(service,task);
        var openingPages=task.calls.Where(call=>Convert.ToString(contents[call.id]).Contains("Verbatim source openings")).ToList();
        check(openingPages.Count>0&&openingPages.All(call=>{string text=Convert.ToString(contents[call.id]);return !text.Contains("最终修订");}),"older exact pages keep verbatim source openings without pretending to be the newest revision page");
        check(openingPages.Count>0&&Enumerable.Range(1,3).All(n=>openingPages.Any(call=>Convert.ToString(contents[call.id]).Contains("第"+n+"份中部约束"))),"a single bounded opening block preserves all three early constraints for the model");
        string retained=service.RetainedConstraints(task);
        check(Enumerable.Range(1,3).All(n=>retained.Contains("第"+n+"份中部约束"))&&retained.Contains("不是新的指令"),"RetainedConstraints quotes the already-read constraints as host metadata, not as new instructions");
        check(finalContext.Contains("不是新的指令"),"the dynamic host tail sent to the model carries the retained-constraint metadata in every request");
    }

    // ---- RetainedConstraints: deterministic, bounded, deduplicated, keyword-filtered extraction.
    static void RetainedConstraintExtraction(string root,Action<bool,string> check){
        var library=Smart(root);var service=Service(library);
        var state=NewTask();
        string text="普通叙述句，没有任何特别内容。\n预算上限 12,600 元，禁止超额支出。\n负责人 江岚，截止日期 12 月 19 日。\n预算上限 12,600 元，禁止超额支出。";
        state.calls.Add(new AgentCall{id=Id(),tool="read_items",status="done",result=new Dictionary<string,object>{{"text",text},{"mode","exact"}}});
        string retained=service.RetainedConstraints(state);
        check(retained.Contains("预算上限 12,600 元，禁止超额支出")&&retained.Contains("负责人 江岚，截止日期 12 月 19 日")&&!retained.Contains("普通叙述句"),"RetainedConstraints extracts keyword-bearing constraint/quantity sentences and skips ordinary narration");
        check(retained.Length<=1700&&retained.Split(new[]{"\n- "},StringSplitOptions.None).Length-1<=10,"RetainedConstraints stays inside 10 items / 1,600 characters");
        int occurrences=0;int at=0;while((at=retained.IndexOf("预算上限 12,600 元，禁止超额支出",at,StringComparison.Ordinal))>=0){occurrences++;at+=1;}
        check(occurrences==1,"duplicate constraint sentences from the same read are de-duplicated once");
        var nonexact=NewTask();
        nonexact.calls.Add(new AgentCall{id=Id(),tool="read_items",status="done",result=new Dictionary<string,object>{{"text","预算上限 9,999 元。"}}});
        check(service.RetainedConstraints(nonexact)=="","legacy sampled results without mode=exact contribute no retained constraints");
        var many=NewTask();var builder=new StringBuilder();
        for(int n=1;n<=15;n++)builder.Append("第").Append(n).Append("项要求：预算上限 ").Append(1000+n).Append(" 元。\n");
        many.calls.Add(new AgentCall{id=Id(),tool="read_items",status="done",result=new Dictionary<string,object>{{"text",builder.ToString()},{"mode","exact"}}});
        string manyRetained=service.RetainedConstraints(many);
        check(manyRetained.Length<=1700&&manyRetained.Split(new[]{"\n- "},StringSplitOptions.None).Length-1<=10,"many constraint sentences are capped at 10 items and 1,600 characters");
    }

    // ---- #7A/#7B (C-C): read_presentation records per-page coverage, DeckCoverage requires every page of the
    // current deck version, and a one-page read of an 8-page artifact is insufficient.
    static void PresentationCoverage(string root,Action<bool,string> check){
        var library=Smart(root);var service=Service(library);
        var deck=Deck(library,"八页演示",8);
        var task=NewTask(deck.id);
        var full=ReadPresentation(library,task,deck.id,1,8);
        string mode=Str(full.result,"mode"),domain=Str(full.result,"domain");
        var rows=Rows(full.result);
        check(mode=="exact-read"&&domain=="presentation","read_presentation marks its result mode=exact-read/domain=presentation (not the character-exact mode)");
        check(rows.Count==1&&Str(rows[0],"domain")=="presentation"&&Str(rows[0],"mode")=="exact"&&Int(rows[0],"totalChars")==8&&Int(rows[0],"readStart")==0&&Int(rows[0],"readEnd")==8&&Bool(rows[0],"full")&&!Bool(rows[0],"hasMore")&&Int(rows[0],"nextOffset")==-1&&Str(rows[0],"digest")==library.PresentationSignature(deck.id),"read_presentation coverage row records page ranges, total pages, full/next and the deck signature");
        int total,covered;string detail;
        check(AgentReading.DeckCoverage(library,task,deck.id,0,out total,out covered,out detail)&&total==8&&covered==8&&detail=="","DeckCoverage accepts a full 8-page read of the current deck version");
        var partial=NewTask(deck.id);var one=ReadPresentation(library,partial,deck.id,1,1);var oneRow=Rows(one.result)[0];
        check(Int(oneRow,"readEnd")==1&&!Bool(oneRow,"full")&&Bool(oneRow,"hasMore")&&Int(oneRow,"nextOffset")==2,"a one-page read records has_more and next_page for the remainder");
        check(!AgentReading.DeckCoverage(library,partial,deck.id,0,out total,out covered,out detail)&&total==8&&covered==1&&detail.Contains("回读不足")&&detail.Contains("第 2-8 页"),"DeckCoverage rejects a one-page read of an 8-page artifact and names the missing pages");
        // A re-saved deck is a new version: old page reads must stop counting.
        var revised=Deck(library,"八页演示",5);
        var bytes=File.ReadAllBytes(revised.path);
        library.SavePresentation(deck.id,bytes,library.PresentationSignature(deck.id),null);
        check(!AgentReading.DeckCoverage(library,task,deck.id,0,out total,out covered,out detail)&&total==5&&detail.Contains("失效"),"a saved deck version invalidates the previous page coverage");
        var reread=NewTask(deck.id);ReadPresentation(library,reread,deck.id,1,8);
        check(AgentReading.DeckCoverage(library,reread,deck.id,0,out total,out covered,out detail)&&total==5&&covered==5&&detail=="","re-reading every page of the new deck version restores DeckCoverage");
        // mode=exact-read must not enter CallContents' exact-page no-clip path.
        var fake=NewTask();fake.calls.Add(new AgentCall{id=Id(),turnId=Id(),tool="read_presentation",status="done",result=new Dictionary<string,object>{{"mode","exact-read"},{"domain","presentation"},{"text",new string('x',9000)}}});
        string sent=Contents(service,fake)[fake.calls[0].id];
        check(sent.Length<=8000&&sent.Contains("middle省略"),"read_presentation mode=exact-read keeps the normal bounded context clip instead of the exact-page pass-through");
    }

    // ---- #7B: a fully read PPTX source counts as full coverage through page coverage OR a full text read.
    static void PresentationSourceCoverage(string root,Action<bool,string> check){
        var library=Smart(root);
        var deck=Deck(library,"八页来源",8);
        var task=NewTask(deck.id);string detail;
        check(!AgentReading.FullCoverage(library,task,new[]{deck.id},out detail),"an unread PPTX source never satisfies FullCoverage");
        ReadPresentation(library,task,deck.id,1,1);
        check(!AgentReading.FullCoverage(library,task,new[]{deck.id},out detail)&&(detail.Contains("第 2-8 页")||detail.Contains("缺少")),"a partial slide-page read is rejected and names the missing pages");
        ReadPresentation(library,task,deck.id,2,7);
        check(AgentReading.FullCoverage(library,task,new[]{deck.id},out detail)&&detail=="","reading every slide page with read_presentation satisfies FullCoverage for a PPTX source");
        // A full read_items text read of the same deck is the alternative accepted path.
        var textTask=NewTask(deck.id);
        int offset=0,guard=0;
        while(offset>=0&&guard++<10){var page=ReadItems(library,textTask,new List<string>{deck.id},offset,12000);offset=Int(Rows(page.result)[0],"nextOffset");}
        check(AgentReading.FullCoverage(library,textTask,new[]{deck.id},out detail)&&detail=="","a full read_items text read also satisfies FullCoverage for a PPTX source");
        // A deck version change invalidates the page coverage that was recorded before it.
        var replacementSlides=new List<Dictionary<string,object>>();
        for(int n=1;n<=8;n++)replacementSlides.Add(new Dictionary<string,object>{{"title","新版第 "+n+" 页"},{"bullets",new[]{"新要点 "+n}}});
        byte[] replacementBytes=AgentSlides.Build(new Dictionary<string,object>{{"title","新版八页来源"},{"slides",replacementSlides}});
        library.SavePresentation(deck.id,replacementBytes,library.PresentationSignature(deck.id),null);
        check(!AgentReading.FullCoverage(library,task,new[]{deck.id},out detail)&&detail.Contains("失效"),"a saved deck version invalidates full slide-page coverage");
    }

    // ---- #12: constraint retention prefers the source's final revision and never lets old requirements crowd it out.
    static void RetainedFinalRevision(string root,Action<bool,string> check){
        var library=Smart(root);var service=Service(library);
        string oldRules=String.Join("。",Enumerable.Range(1,10).Select(n=>"原方案条目"+n+"要求每周例行检查并登记记录"))+"。最终修订要求预算上限为八万元，以上原方案作废。";
        var state=NewTask();
        string header="read_items exact · Coverage: 1/1 sources\n\n[S1] 含新旧要求的资料 · id="+new string('a',32)+" · chars 0-"+oldRules.Length+" of "+oldRules.Length+"\n";
        state.calls.Add(new AgentCall{id=Id(),tool="read_items",status="done",result=new Dictionary<string,object>{{"text",header+oldRules},{"mode","exact"}}});
        string retained=service.RetainedConstraints(state);
        check(retained.Contains("最终修订")&&retained.Contains("八万元")&&!retained.Contains("原方案条目1"),"a final revision supersedes earlier requirements: the retained block keeps the revision and drops obsolete rules");
        check(retained.Contains("优先当前有效/最终修订")&&retained.Contains("旧版本可能已废止"),"the retained block title says current/final revision takes priority and older versions may be void");
        // Independent per-source quota: one long obsolete source must not consume the whole block.
        var multi=NewTask();
        string sourceA=String.Concat(Enumerable.Range(1,6).Select(n=>"来源A要求"+n+"：预算上限 "+(100+n)+" 元。"));
        string sourceB=String.Concat(Enumerable.Range(1,6).Select(n=>"来源B要求"+n+"：预算上限 "+(200+n)+" 元。"));
        string multiText="read_items exact · Coverage: 2/2 sources\n\n[S1] 来源A · id="+new string('a',32)+" · chars 0-"+sourceA.Length+" of "+sourceA.Length+"\n"+sourceA+"\n\n[S2] 来源B · id="+new string('b',32)+" · chars 0-"+sourceB.Length+" of "+sourceB.Length+"\n"+sourceB;
        multi.calls.Add(new AgentCall{id=Id(),tool="read_items",status="done",result=new Dictionary<string,object>{{"text",multiText},{"mode","exact"}}});
        string multiRetained=service.RetainedConstraints(multi);
        int countA=Count(multiRetained,"来源A要求"),countB=Count(multiRetained,"来源B要求");
        check(countA>=1&&countB>=1&&countA<=4&&countB<=4&&multiRetained.Split(new[]{"\n- "},StringSplitOptions.None).Length-1<=10,"each source has its own <=4 item quota and one long source cannot starve another");
        // read_document's single inner document text also participates in per-source extraction / revision priority.
        var documentTask=NewTask();
        string inner=JsonString(new Dictionary<string,object>{{"id",new string('c',32)},{"name","文档"},{"offset",0},{"total_chars",oldRules.Length},{"next_offset",-1},{"text",oldRules},{"signature","sha256:"+new string('0',64)},{"recovery",false}});
        documentTask.calls.Add(new AgentCall{id=Id(),tool="read_document",status="done",result=new Dictionary<string,object>{{"text",inner},{"mode","exact"}}});
        string documentRetained=service.RetainedConstraints(documentTask);
        check(documentRetained.Contains("最终修订")&&!documentRetained.Contains("原方案条目1"),"read_document inner text is split as one source and still prefers its final revision");
    }
    static string JsonString(Dictionary<string,object> value){return Library.Json.Serialize(value);}
}
