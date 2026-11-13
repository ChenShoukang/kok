using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using kok;

// v0.12.0 P0-2: exact paged reading of long sources, coverage accounting and bounded context clipping.
// Owned by reading-agent; register/keep Run(root,check) signature stable.
class AgentReadingV012Tests {
    public static void Run(string root, Action<bool,string> check) {
        Directory.CreateDirectory(root);
        ShortSource(Path.Combine(root,"short"),check);
        LongPaging(Path.Combine(root,"long-paging"),check);
        MultiSource(Path.Combine(root,"multi-source"),check);
        CoverageInvalidation(Path.Combine(root,"coverage-invalidation"),check);
        SkippedSources(Path.Combine(root,"skipped"),check);
        ValidationAndGuidance(Path.Combine(root,"validation"),check);
        ModelVisibility(Path.Combine(root,"model-visibility"),check);
        ExtractionLimits(Path.Combine(root,"extraction-limits"),check);
        UnicodePages(Path.Combine(root,"unicode-pages"),check);
    }
    static Library Smart(string path){var library=new Library(path);library.SetAppMode("smart",true);return library;}
    static AgentTaskState NewTask(params string[] ids){return new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{goal="读取资料全文",allowed=ids.ToList()}};}
    // Deterministic filler so a source can be sized exactly; the tail is appended verbatim.
    static string Padded(string label,int target,string tail){
        var builder=new StringBuilder();int room=Math.Max(0,target-tail.Length),line=0;
        while(builder.Length<room)builder.Append(label).Append(" 第 ").Append(line++).Append(" 行：用于精确分页读取测试的普通正文内容。\n");
        if(builder.Length>room)builder.Length=room;
        builder.Append(tail);return builder.ToString();
    }
    static Dictionary<string,object> Read(Library library,AgentTaskState task,List<string> ids,int offset=0,int maxChars=0){
        var call=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",status="done",args=new Dictionary<string,object>{{"ids",new List<string>(ids)}}};
        if(offset>0)call.args["offset"]=offset;
        if(maxChars>0)call.args["max_chars"]=maxChars;
        var result=library.ReadAgentItemsExact(task,call);call.result=result;task.calls.Add(call);return result;
    }
    static List<Dictionary<string,object>> Rows(Dictionary<string,object> result){return AgentTaskState.Get<List<Dictionary<string,object>>>(result,"coverage");}
    static int Int(Dictionary<string,object> row,string key){return Convert.ToInt32(row[key]);}
    static bool Bool(Dictionary<string,object> row,string key){return Convert.ToBoolean(row[key]);}
    static string Str(Dictionary<string,object> row,string key){return Convert.ToString(row[key]);}
    // ---- short source: one exact page, full coverage, stable digest ------------------------------
    static void ShortSource(string root,Action<bool,string> check){
        var library=Smart(root);
        string body="短资料正文：开幕日期 2026-12-19，验收负责人 江岚，验收口令 青鹭482。";
        var document=library.CreateDocument("短资料","md","",body);
        var task=NewTask(document.id);
        var result=Read(library,task,new List<string>{document.id});
        string text=Convert.ToString(result["text"]);var rows=Rows(result);
        bool metadata=rows.Count==1&&Str(rows[0],"id")==document.id&&Bool(rows[0],"full")&&!Bool(rows[0],"hasMore")&&Int(rows[0],"nextOffset")==-1&&Int(rows[0],"readStart")==0&&Int(rows[0],"readEnd")==body.Length&&Int(rows[0],"totalChars")==body.Length;
        check(metadata,"a short source returns completely in one exact page with full character coverage metadata");
        check(text.Contains(body)&&text.Contains("Coverage: 1/1 sources")&&text.Contains("not sampled")&&!Convert.ToBoolean(result["has_more"])&&Convert.ToInt32(result["next_offset"])==-1,"one short exact read returns the whole body, has_more=false and next_offset=-1");
        var references=AgentTaskState.Get<List<ChatReference>>(result,"references");
        check(references.Count==1&&references[0].fileId==document.id&&references[0].id=="S1"&&references[0].excerpt.Length>0,"exact reads keep one ChatReference per source for the existing reference UI");
        string detail;
        check(AgentReading.FullCoverage(library,task,new[]{document.id},out detail)&&detail=="","one complete exact page satisfies FullCoverage with an empty detail");
        var repeat=Read(library,NewTask(document.id),new List<string>{document.id});
        check(Str(Rows(repeat)[0],"digest").Length==64&&Str(Rows(repeat)[0],"digest")==Str(rows[0],"digest"),"exact read digests are stable for identical current content");
        // Journal/replay stores tool results through JavaScriptSerializer round-trips; coverage rows must
        // survive that conversion so finish-time coverage checks keep working after a task reload.
        var replayed=NewTask(document.id);
        replayed.calls.Add(new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",status="done",result=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(result))});
        check(AgentReading.FullCoverage(library,replayed,new[]{document.id},out detail)&&detail=="","exact coverage rows survive the journal JSON round-trip used by task replay");
    }
    // ---- 12,277-character source: bounded pages, no gap/duplicate, tail revision facts ----------
    static void LongPaging(string root,Action<bool,string> check){
        var library=Smart(root);
        const string tail="\n最终修订（覆盖旧值）：\n开幕日期：2026-12-19\n项目负责人：江岚\n预算总额：12,600 元\n资金上限：15,000 元\n余额：2,400 元\n志愿者：8 名\n有效反馈：至少 60 份\n验收口令：青鹭482\n验收负责人：江岚";
        string body=Padded("长资料占位",12277,tail);
        check(body.Length==12277,"the paging fixture is a 12,277-character source");
        var document=library.CreateDocument("长资料修订版","md","",body);
        var task=NewTask(document.id);
        int offset=0,pages=0,maxPageLength=0;bool consistent=true,markers=true,firstPageUnsampled=true;
        string lastText="";
        var intervals=new List<int[]>();
        while(true){
            var result=Read(library,task,new List<string>{document.id},offset);
            string text=Convert.ToString(result["text"]);var rows=Rows(result);
            pages++;maxPageLength=Math.Max(maxPageLength,text.Length);lastText=text;
            bool hasMore=Convert.ToBoolean(result["has_more"]);int next=Convert.ToInt32(result["next_offset"]);
            if(rows.Count!=1)throw new Exception("FAIL: long exact read returned "+rows.Count+" coverage rows");
            var row=rows[0];int start=Int(row,"readStart"),end=Int(row,"readEnd"),total=Int(row,"totalChars");
            consistent&=start==offset&&end>start&&total==body.Length&&end<=total;
            consistent&=Bool(row,"hasMore")==(end<total)&&Int(row,"nextOffset")==(end<total?end:-1);
            consistent&=hasMore==(next>=0)&&(!hasMore||next==end);
            if(hasMore&&!text.Contains("尚未读取"))markers=false;
            if(start==0)firstPageUnsampled&=end<body.Length&&!text.Contains("预算总额：12,600 元")&&text.Contains(body.Substring(0,120));
            if(end==total)markers&=text.Contains("最终修订")&&text.Contains("预算总额：12,600 元")&&text.Contains("资金上限：15,000 元")&&text.Contains("有效反馈：至少 60 份")&&text.Contains("验收负责人：江岚");
            intervals.Add(new[]{start,end});
            if(!hasMore)break;
            offset=next;if(++pages>40)throw new Exception("FAIL: exact paging did not terminate");
        }
        bool tiled=intervals.Count>1&&intervals[0][0]==0&&intervals[intervals.Count-1][1]==body.Length;
        for(int n=1;n<intervals.Count;n++)tiled&=intervals[n][0]==intervals[n-1][1];
        check(consistent&&markers,"every long page keeps its header, has_more and next_offset consistent with the returned character range");
        check(tiled,"paging the 12,277-character source tiles [0,total) with no gap and no duplicated range");
        check(pages>=2&&pages<=8&&maxPageLength<=AgentReading.DefaultMaxChars,"a 12,277-character source needs several bounded exact pages, none over the default window");
        check(firstPageUnsampled,"the first long page starts at character 0 with contiguous source text instead of keyword sampling");
        check(lastText.Contains("最终修订")&&lastText.Contains("预算总额：12,600 元")&&lastText.Contains("资金上限：15,000 元")&&lastText.Contains("有效反馈：至少 60 份")&&lastText.Contains("验收负责人：江岚"),"the final exact page carries the tail revision facts (12,600 / 15,000 / at least 60 / acceptance owner)");
        string detail;
        check(AgentReading.FullCoverage(library,task,new[]{document.id},out detail)&&detail=="","fully paging the long source satisfies FullCoverage");
    }
    // ---- three long sources: one or more reads cover every name and every tail fact -------------
    static void MultiSource(string root,Action<bool,string> check){
        var library=Smart(root);
        string tailA="\n资料A最终修订：预算总额 12,600 元，验收负责人 江岚。",tailB="\n资料B最终修订：资金上限 15,000 元，余额 2,400 元。",tailC="\n资料C最终修订：有效反馈至少 60 份，志愿者 8 名，验收口令 青鹭482。";
        var a=library.CreateDocument("甲资料","md","",Padded("甲资料内容",8400,tailA));
        var b=library.CreateDocument("乙资料","md","",Padded("乙资料内容",8700,tailB));
        var c=library.CreateDocument("丙资料","md","",Padded("丙资料内容",9000,tailC));
        var ids=new List<string>{a.id,b.id,c.id};var task=NewTask(a.id,b.id,c.id);
        var first=Read(library,task,ids,0);string firstText=Convert.ToString(first["text"]);
        check(firstText.Contains("甲资料")&&firstText.Contains("乙资料")&&firstText.Contains("丙资料")&&firstText.Contains("Coverage: 3/3 sources"),"the first exact multi-source page names every requested source");
        string detail;
        check(!AgentReading.FullCoverage(library,task,ids,out detail)&&detail.Length>0&&(detail.Contains("未读全")||detail.Contains("尚未读取")),"partial multi-source reads are rejected by FullCoverage with the unread ranges in detail");
        int offset=0,pages=0;string lastText="",allText="";bool consistent=true;
        while(true){
            var result=pages==0?first:Read(library,task,ids,offset);
            pages++;lastText=Convert.ToString(result["text"]);allText+=lastText+"\n";
            bool hasMore=Convert.ToBoolean(result["has_more"]);int next=Convert.ToInt32(result["next_offset"]);
            var rows=Rows(result);
            consistent&=rows.Count==3&&hasMore==(next>=0);
            foreach(var row in rows){
                int start=Int(row,"readStart"),end=Int(row,"readEnd"),total=Int(row,"totalChars");
                consistent&=start<=end&&end<=total&&Bool(row,"hasMore")==(end<total)&&Int(row,"nextOffset")==(end<total?end:-1);
            }
            if(!hasMore)break;
            offset=next;if(++pages>40)throw new Exception("FAIL: multi-source exact paging did not terminate");
        }
        check(consistent,"multi-source exact pages keep per-source readRanges, has_more and next_offset self-consistent");
        check(pages>=3&&pages<=8,"three 8,400-9,000 character sources need several bounded exact pages, not one sampled excerpt");
        check(allText.Contains("资料A最终修订")&&allText.Contains("资料B最终修订")&&allText.Contains("资料C最终修订")&&allText.Contains("12,600 元")&&allText.Contains("15,000 元")&&allText.Contains("至少 60 份")&&allText.Contains("验收负责人 江岚"),"the exact pages together expose every source's tail revision fact");
        check(lastText.Contains("资料C最终修订")&&lastText.Contains("至少 60 份"),"the last multi-source page still carries the final source's tail facts");
        check(AgentReading.FullCoverage(library,task,ids,out detail)&&detail=="","paging all three long sources satisfies FullCoverage");
    }
    // ---- content changes invalidate coverage; recall_result never counts as coverage ------------
    static void CoverageInvalidation(string root,Action<bool,string> check){
        var library=Smart(root);
        string body=Padded("覆盖失效测试的正文",9000,"\n末尾事实：预算总额 12,600 元，验收负责人 江岚。");
        var document=library.CreateDocument("覆盖失效资料","md","",body);
        var task=NewTask(document.id);
        var first=Read(library,task,new List<string>{document.id},0);
        string detail;
        check(!AgentReading.FullCoverage(library,task,new[]{document.id},out detail)&&detail.Length>0,"a partial exact read is not accepted as full coverage");
        int offset=Convert.ToInt32(first["next_offset"]);int guard=0;
        while(offset>=0&&guard++<40){var page=Read(library,task,new List<string>{document.id},offset);offset=Convert.ToInt32(page["next_offset"]);}
        check(AgentReading.FullCoverage(library,task,new[]{document.id},out detail)&&detail=="","reading every exact page satisfies FullCoverage");
        string changed=body+"\n补充修改：预算总额改为 13,000 元。";
        library.SaveDocument(document.id,changed,library.DocumentSource(document.id).signature,null);
        check(!AgentReading.FullCoverage(library,task,new[]{document.id},out detail)&&(detail.Contains("内容已变化")||detail.Contains("失效")),"a content change invalidates the previously complete exact coverage");
        var recallOnly=NewTask(document.id);
        recallOnly.calls.Add(new AgentCall{id=Guid.NewGuid().ToString("N"),tool="recall_result",status="done",result=new Dictionary<string,object>{{"text","stored result replay"}}});
        check(!AgentReading.FullCoverage(library,recallOnly,new[]{document.id},out detail),"recall_result replay never counts as original-source coverage");
        var legacy=NewTask(document.id);
        legacy.calls.Add(new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",status="done",result=new Dictionary<string,object>{{"text","legacy sampled excerpt"}}});
        check(!AgentReading.FullCoverage(library,legacy,new[]{document.id},out detail),"old sampled read_items results without exact ranges do not count as coverage");
        int offset2=0;guard=0;
        while(offset2>=0&&guard++<40){var page=Read(library,task,new List<string>{document.id},offset2);offset2=Convert.ToInt32(page["next_offset"]);}
        check(AgentReading.FullCoverage(library,task,new[]{document.id},out detail)&&detail=="","re-reading the changed source restores full coverage");
        check(AgentReading.FullCoverage(library,task,new string[0],out detail)&&detail=="","an empty required-source set is trivially covered");
    }
    // ---- unreadable sources are reported in `skipped`, never silently dropped -------------------
    static void SkippedSources(string root,Action<bool,string> check){
        var library=Smart(root);
        string body="可读正文：预算总额 12,600 元，验收负责人 江岚。";
        var document=library.CreateDocument("可读资料","md","",body);
        byte[] imageBytes;using(var image=new System.Drawing.Bitmap(16,12))using(var stream=new MemoryStream()){image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);imageBytes=stream.ToArray();}
        string photoPath=Path.Combine(root,"unreadable.png");File.WriteAllBytes(photoPath,imageBytes);
        library.Import(new[]{photoPath},null);
        var photo=library.Db.items.First(item=>item.path==photoPath);
        var task=NewTask(document.id,photo.id);
        var result=Read(library,task,new List<string>{document.id,photo.id});
        var skipped=AgentTaskState.Get<List<string>>(result,"skipped");string text=Convert.ToString(result["text"]);
        check(skipped.Count==1&&skipped[0]==photo.id&&Rows(result).Count==1&&text.Contains("[SKIPPED]"),"a source without readable text is reported in skipped instead of being silently replaced by sampling");
        string detail;
        check(!AgentReading.FullCoverage(library,task,new[]{document.id,photo.id},out detail)&&detail.Contains(photo.id),"an unreadable required source cannot silently satisfy full-source coverage");
        bool rejected=false;try{library.ReadAgentItemsExact(task,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=new Dictionary<string,object>{{"ids",new List<string>{photo.id}}}});}catch(InvalidDataException){rejected=true;}
        check(rejected,"a read where no requested source has readable text fails with an explicit error instead of an empty page");
    }
    // Verify the actual string passed into model messages, not just the reader's coverage metadata.
    static void ModelVisibility(string root,Action<bool,string> check){
        var library=Smart(root);
        var service=new AgentService(new ModuleContext(library,null,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null));
        var method=typeof(AgentService).GetMethod("CallContents",BindingFlags.NonPublic|BindingFlags.Instance);
        var doc=library.CreateDocument("中段有关键事实","md","",new string('甲',6500)+"中段事实：只统计人数，禁止收集联系方式。"+new string('乙',4000));
        var task=NewTask(doc.id);var result=Read(library,task,new List<string>{doc.id},0,12000);
        var sent=(Dictionary<string,string>)method.Invoke(service,new object[]{task});
        check(Convert.ToString(result["text"]).Length>8000&&sent[task.calls[0].id]==Convert.ToString(result["text"])&&sent[task.calls[0].id].Contains("禁止收集联系方式"),"a 12k exact read reaches the model intact, including facts in the former omitted middle");
        var batch=NewTask(doc.id);string turn=Guid.NewGuid().ToString("N");
        for(int n=0;n<3;n++)batch.calls.Add(new AgentCall{id=Guid.NewGuid().ToString("N"),turnId=turn,tool="read_items",status="queued",args=new Dictionary<string,object>{{"ids",new List<string>{doc.id}},{"offset",n*3000},{"max_chars",12000}}});
        foreach(var call in batch.calls){call.status="running";call.result=library.ReadAgentItemsExact(batch,call);call.status="done";}
        sent=(Dictionary<string,string>)method.Invoke(service,new object[]{batch});
        check(batch.calls.All(c=>sent[c.id]==Convert.ToString(c.result["text"]))&&sent.Values.Sum(s=>s.Length)<=batch.options.budget,"parallel exact reads share the result budget so every returned coverage range reaches the model intact");
        var small=NewTask(doc.id);small.options.budget=6000;var smallResult=Read(library,small,new List<string>{doc.id},0,12000);
        sent=(Dictionary<string,string>)method.Invoke(service,new object[]{small});
        check(sent[small.calls[0].id]==Convert.ToString(smallResult["text"])&&Convert.ToBoolean(smallResult["has_more"]),"a small task budget paginates source text instead of discarding a supposedly read middle");
        var first=library.CreateDocument("活动来源","md","","活动目标：建立稳定阅读习惯。\n"+new string('甲',8200)+"\n最终日期为12月19日。");
        var second=library.CreateDocument("预算来源","md","","原始预算说明。\n"+new string('乙',8200)+"\n最终预算12600元。");
        var third=library.CreateDocument("隐私来源","md","","签到只统计人数，不收集或公开参与者联系方式。\n"+new string('丙',8200)+"\n志愿者最终为8名。");
        var longTask=NewTask(first.id,second.id,third.id);int next=0;
        do{var page=Read(library,longTask,new List<string>{first.id,second.id,third.id},next,12000);longTask.calls.Last().turnId=Guid.NewGuid().ToString("N");next=Convert.ToInt32(page["next_offset"]);}while(next>=0);
        sent=(Dictionary<string,string>)method.Invoke(service,new object[]{longTask});
        string beginnings=sent[longTask.calls[0].id];
        check(beginnings.Contains("建立稳定阅读习惯")&&beginnings.Contains("不收集或公开参与者联系方式")&&sent[longTask.calls.Last().id]==Convert.ToString(longTask.calls.Last().result["text"])&&sent.Values.Sum(s=>s.Length)<=longTask.options.budget,"later revision pages preserve the earlier source goals and privacy restrictions within the same model result budget");
    }
    static void ExtractionLimits(string root,Action<bool,string> check){
        var library=Smart(root);var doc=library.CreateDocument("超出全文上限","md","",new string('长',120100)+"末尾不能假装读过");
        var first=library.AgentReadSourceText(doc.id);var cached=library.AgentReadSourceText(doc.id);
        string detail;
        check(first.capped&&cached.capped&&first.Length==120000&&!AgentReading.FullCoverage(library,NewTask(doc.id),new[]{doc.id},out detail)&&detail.Contains("上限"),"extraction limits stay visible on cache hits and can never count as complete source coverage");
    }
    static void UnicodePages(string root,Action<bool,string> check){
        var library=Smart(root);var a=library.CreateDocument("emoji A","md","",String.Concat(Enumerable.Repeat("😀",1800))+"甲结束");
        var b=library.CreateDocument("emoji B","md","","乙"+String.Concat(Enumerable.Repeat("😀",1800))+"乙结束");
        var task=NewTask(a.id,b.id);int offset=0,guard=0;bool valid=true;
        do{
            var page=Read(library,task,new List<string>{a.id,b.id},offset,2000);string text=Convert.ToString(page["text"]);
            for(int n=0;n<text.Length;n++)if(Char.IsSurrogate(text[n])){if(!Char.IsHighSurrogate(text[n])||n+1>=text.Length||!Char.IsLowSurrogate(text[n+1]))valid=false;else n++;}
            int next=Convert.ToInt32(page["next_offset"]);if(next>=0&&next<=offset)valid=false;offset=next;
        }while(offset>=0&&guard++<30);
        string detail;
        check(valid&&offset<0&&AgentReading.FullCoverage(library,task,new[]{a.id,b.id},out detail),"shared-offset paging preserves emoji in differently aligned sources and leaves no coverage gaps");
    }
    // ---- validation, signature and instruction guidance ----------------------------------------
    static void ValidationAndGuidance(string root,Action<bool,string> check){
        var library=Smart(root);
        var document=library.CreateDocument("校验资料","md","","VALIDATION_BODY");
        var spec=AgentRules.Tools.First(tool=>tool.Name=="read_items");
        Func<string,bool> exists=id=>id==document.id;
        Func<Dictionary<string,object>,bool> rejects=args=>{try{spec.Validate(new AgentAction{tool="read_items",args=args},exists);return false;}catch(FormatException){return true;}};
        bool accepts=true;try{spec.Validate(new AgentAction{tool="read_items",args=new Dictionary<string,object>{{"ids",new List<string>{document.id}},{"offset",100},{"max_chars",4000}}},exists);}catch{accepts=false;}
        check(accepts,"read_items accepts in-range offset and max_chars");
        check(spec.Signature.Contains("read_items{ids[<=8],offset?,max_chars?")&&spec.Validate!=null,"the read_items spec advertises offset/max_chars and validates them outside the AgentRuntime switch");
        string[] fakeIds=Enumerable.Range(1,9).Select(n=>n.ToString("x32")).ToArray();
        check(rejects(new Dictionary<string,object>{{"ids",new List<string>{document.id}},{"offset",-1}})
            &&rejects(new Dictionary<string,object>{{"ids",new List<string>{document.id}},{"offset","0"}})
            &&rejects(new Dictionary<string,object>{{"ids",new List<string>{document.id}},{"max_chars",100}})
            &&rejects(new Dictionary<string,object>{{"ids",new List<string>{document.id}},{"max_chars",13000}})
            &&rejects(new Dictionary<string,object>{{"ids",new List<string>()}})
            &&rejects(new Dictionary<string,object>{{"ids",fakeIds.ToList()}})
            &&rejects(new Dictionary<string,object>{{"ids",new List<string>{document.id,document.id}}})
            &&rejects(new Dictionary<string,object>{{"ids",new List<string>{new string('f',32)}}})
            &&rejects(new Dictionary<string,object>())
            &&rejects(new Dictionary<string,object>{{"ids",new List<string>{document.id}},{"question",new string('q',400)}}),"read_items validation rejects bad offsets, page sizes, ids, out-of-scope ids and oversized question hints");
        var task=NewTask(document.id);string instructions=AgentReading.Instructions(task);
        check(instructions.Contains("read_items")&&instructions.Contains("has_more")&&instructions.Contains("next_offset")&&instructions.Contains("尚未读取")&&instructions.Contains("recall_result")&&instructions.Contains("exact contiguous")&&instructions.Length<1800,"reading instructions explain exact paging, has_more/next_offset, unread marking and that recall does not count");
        Read(library,task,new List<string>{document.id},0);
        string followUp=AgentReading.Instructions(task);
        check(AgentReading.Instructions(null).Length>0&&followUp.Length<=2000,"instruction guidance is bounded and safe with an empty task");
        // Integration: the registered read tool routes read_items to the exact reader (scope validated
        // by the tool layer) instead of the old BuildKnowledgeContext sampling branch.
        var toolState=NewTask(document.id);
        var toolResult=library.ReadAgentTool(toolState,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=new Dictionary<string,object>{{"ids",new List<string>{document.id}}}},CancellationToken.None).GetAwaiter().GetResult();
        check(Convert.ToString(toolResult["text"]).Contains("VALIDATION_BODY")&&Rows(toolResult).Count==1&&Rows(toolResult)[0]["mode"].ToString()=="exact","ReadAgentTool routes read_items to the exact paged reader");
    }
}
