using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using kok;

static class AgentV16Tests {
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> D(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static byte[] Part(string path,string name){using(var zip=ZipFile.OpenRead(path))using(var stream=zip.GetEntry(name).Open())using(var memory=new MemoryStream()){stream.CopyTo(memory);return memory.ToArray();}}
    public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
    static async Task RunAsync(string root,Action<bool,string> check){
        Directory.CreateDirectory(root);var library=new Library(Path.Combine(root,"library"));library.SetAppMode("smart",true);
        string workspace=Path.Combine(root,"workspace");Directory.CreateDirectory(workspace);AgentExecution.Save(library,true,workspace,false);
        var context=new ModuleContext(library,new TranslationService(library.DataDir),new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
        var service=new AgentService(context);check(AgentBusinessCore.Available(AppDomain.CurrentDomain.BaseDirectory),"v16: the packaged TypeScript business core is present alongside the SDK runtime");
        var source=library.CreateDocument("最终修订","md","","# 活动方案\n最终预算为8万元，禁止收集手机号。\n"+new string('甲',900));
        var options=new AgentTaskOptions{goal="根据所选资料创建400-600汉字DOCX报告，采用最终修订预算。",sessionId=Id(),allowed=new List<string>{source.id},scope=new AgentScope{mode="selected",ids=new List<string>{source.id}}};
        AgentBusinessCore.Prepare(options,library,AppDomain.CurrentDomain.BaseDirectory);service.PrepareDelivery(options);
        var plan=AgentBusinessCore.Plan(options,null);check(plan.language=="TypeScript"&&plan.contract.RequiresDocument&&plan.contract.RequiredDocumentFormat=="DOCX"&&!plan.contract.RequiresTargetedEdit,"v16: actual native-to-TypeScript planning returns the required file and treats source revision labels as data");
        var changed=AgentTaskIntent.Current(options,new[]{"改为300-500汉字，只看开头，给初步意见"});check(changed.MinLength==300&&changed.MaxLength==500&&!changed.RequiresFullSourceCoverage,"v16: the native requirement facade uses current TypeScript message-order rules");
        var explicitOptions=new AgentTaskOptions{goal="修复工作区代码",sessionId=Id(),allowedTools=new List<string>{"calculate"}};AgentBusinessCore.Prepare(explicitOptions,library,AppDomain.CurrentDomain.BaseDirectory);check(AgentToolProfiles.ForTask(explicitOptions,library).SequenceEqual(new[]{"calculate"}),"v16: TypeScript tool routing cannot widen a user-picked capability list");
        var state=new AgentTaskState{id=Id(),options=options};
        var unread=AgentReading.ReviewSources(library,state,new[]{source.id}).Single();check(Convert.ToBoolean(unread["parsed"])&&!Convert.ToBoolean(unread["processed"])&&Convert.ToString(unread["status"])=="unread","v16: a parsed source is not presented as actually processed");
        var inspection=await library.ReadAgentTool(state,new AgentCall{tool="inspect_document",args=D(new{id=source.id,required_sections=new[]{"风险"}})},CancellationToken.None);
        state.calls.Add(new AgentCall{tool="inspect_document",status="done",result=inspection});string detail;
        check(!AgentReading.FullCoverage(library,state,new[]{source.id},out detail)&&AgentTaskState.Get<AgentArtifactReport>(inspection,"artifactReview").findings.Any(row=>row.code=="missing_section"),"v16: document inspection finds missing structure without claiming full source reading");
        async Task Read(int offset,int max){var call=new AgentCall{id=Id(),tool="read_items",status="done",args=D(new{ids=new[]{source.id},offset,max_chars=max})};call.result=await library.ReadAgentTool(state,call,CancellationToken.None);state.calls.Add(call);}
        await Read(0,200);check(Convert.ToString(AgentReading.ReviewSources(library,state,new[]{source.id}).Single()["status"])=="partial","v16: an actual partial source read remains partial");
        await Read(0,12000);check(Convert.ToString(AgentReading.ReviewSources(library,state,new[]{source.id}).Single()["status"])=="complete","v16: current native full-reading receipts are reflected by the TypeScript source review");
        File.AppendAllText(source.path,"\nUPDATED_AFTER_READ");check(Convert.ToString(AgentReading.ReviewSources(library,state,new[]{source.id}).Single()["status"])=="stale","v16: changing a source invalidates the reported reading status");
        var docReview=AgentArtifactReview.Document(library,source.id,new[]{"风险"});check(docReview.findings.Any(row=>row.code=="missing_section")&&!docReview.rendered&&!docReview.visualReviewed,"v16: saved-document review comes from the business core without false visual or factual certification");
        check(docReview.measurements["hanzi"]==AgentDeliveryRules.MeasuredLength(library.DocumentSource(source.id).text,AgentMeasureUnit.Hanzi)&&docReview.measurements["characters"]>docReview.measurements["hanzi"],"v16: document review exposes actual native counts in separate units to avoid unnecessary rewrites");

        var slides=new[]{new{title="第一页",bullets=new[]{"保留第一页文字"},layout="content"},new{title="第二页",bullets=new[]{"只调整第二页这个文本框"},layout="content"}};
        var deck=library.CreatePresentation("定向排版","",AgentSlides.Build(D(new{title="定向排版",slides})));
        var deckState=new AgentTaskState{id=Id(),options=new AgentTaskOptions{goal="检查所选PPT排版",allowed=new List<string>{deck.id},scope=new AgentScope{mode="selected",ids=new List<string>{deck.id}}}};
        var original=PresentationFiles.Read(deck.path,false);var target=original.slides[1].shapes.Last(shape=>!String.IsNullOrWhiteSpace(shape.text));string targetText=target.text;byte[] firstPage=Part(deck.path,"ppt/slides/slide1.xml");
        void Edit(object edits){var call=library.PrepareAgentCall(deckState,new AgentAction{tool="edit_presentation",args=D(new{id=deck.id,edits})},"turn",null);call.sequence=deckState.calls.Count+1;var receipt=library.WriteAgentTool(deckState,call,CancellationToken.None);call.result=receipt.result;call.status="done";call.stamp=receipt.stamp;deckState.calls.Add(call);}
        Edit(new[]{new{action="bounds",page=2,shape_id=target.id,x=20,y=1,w=4,h=1}});
        var broken=AgentArtifactReview.Presentation(library,deck.id);check(broken.findings.Any(row=>row.code=="outside_canvas"&&row.page==2&&row.shapeId==target.id),"v16: actual PPTX inspection locates the exact off-canvas page and native object");
        Edit(new[]{new{action="bounds",page=2,shape_id=target.id,x=target.x,y=target.y,w=target.w,h=target.h}});
        var repaired=AgentArtifactReview.Presentation(library,deck.id);var current=PresentationFiles.Read(deck.path,false);
        check(!repaired.findings.Any(row=>row.code=="outside_canvas")&&current.slides[1].shapes.Any(shape=>shape.id==target.id&&shape.text==targetText),"v16: targeted geometry repair preserves shape identity and text");
        check(Part(deck.path,"ppt/slides/slide1.xml").SequenceEqual(firstPage),"v16: repairing another page preserves the untouched OOXML slide bytes");
        Edit(new[]{new{action="font_size",page=2,shape_id=target.id,font_size=24}});check(PresentationFiles.Read(deck.path,false).slides[1].shapes.Single(shape=>shape.id==target.id).fontSize==24,"v16: targeted font editing saves the actual native font size");
        string dangerous=Path.Combine(root,"macro.pptx");File.Copy(deck.path,dangerous);using(var zip=ZipFile.Open(dangerous,ZipArchiveMode.Update)){using(var stream=zip.CreateEntry("ppt/vbaProject.bin").Open())stream.WriteByte(1);}
        bool macroDenied=false;try{PresentationRenderer.CheckStaticPackage(dangerous);}catch(InvalidDataException){macroDenied=true;}check(macroDenied,"v16: Office rendering refuses active-content packages before opening an application");

        // A native page window can emit only a prefix of an unusually long shape. Its page must not
        // enter exact coverage; a complete read_items pass remains an explicit recovery path.
        var longDeck=library.CreatePresentation("长文本阅读","",AgentSlides.Build(D(new{title="长文本阅读",slides=new[]{new{title="长文本",bullets=new[]{"短正文"},layout="content"}}})));
        using(var zip=ZipFile.Open(longDeck.path,ZipArchiveMode.Update)){
            var xml=AgentPresentationXml.Read(zip,"ppt/slides/slide1.xml");
            xml.Descendants(AgentPresentationXml.A+"t").First().Value=new string('长',7000);
            AgentPresentationXml.Write(zip,"ppt/slides/slide1.xml",xml);
        }
        var longState=new AgentTaskState{id=Id(),options=new AgentTaskOptions{allowed=new List<string>{longDeck.id}}};
        var longCall=new AgentCall{id=Id(),sequence=1,tool="read_presentation",status="done",args=D(new{id=longDeck.id,max_pages=1})};
        longCall.result=await library.ReadAgentTool(longState,longCall,CancellationToken.None);longState.calls.Add(longCall);
        check(AgentTaskState.Get<List<int>>(longCall.result,"incomplete_pages").SequenceEqual(new[]{1})&&!AgentReading.FullCoverage(library,longState,new[]{longDeck.id},out detail),"v16: a clipped PPT text box never proves complete source or page reading");
        var longText=new AgentCall{id=Id(),sequence=2,tool="read_items",status="done",args=D(new{ids=new[]{longDeck.id},max_chars=12000})};
        longText.result=await library.ReadAgentTool(longState,longText,CancellationToken.None);longState.calls.Add(longText);
        check(AgentReading.FullCoverage(library,longState,new[]{longDeck.id},out detail),"v16: exact text paging can recover complete reading of a clipped presentation page");

        string node=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtime","research","node.exe");File.WriteAllText(Path.Combine(workspace,"sum.ts"),"export const value = 5;\n");
        var jobCall=new AgentCall{id=Id(),tool="start_command_job",args=D(new{command=node,args=new[]{"-e","process.stdout.write('no file created')"},outputs=new[]{"missing.json"},inputs=new[]{"sum.ts"},timeoutSeconds=10})};
        var jobs=AgentCommandJobs.For(library);var started=jobs.Start(context,state,jobCall);var done=await jobs.Wait(Convert.ToString(started["jobId"]),state.id,10,CancellationToken.None);var jobResult=AgentTaskState.Get<Dictionary<string,object>>(done,"result");
        check(Convert.ToString(done["state"])=="failed"&&Convert.ToInt32(jobResult["exitCode"])==0&&!Convert.ToBoolean(jobResult["outputsVerified"]),"v16: exit zero cannot turn a missing declared output into successful work");
        check(AgentTaskState.Get<List<AgentAssetRef>>(jobResult,"assets").Single().available==false,"v16: a missing result is unavailable instead of a fake downloadable artifact");
        bool credentialOutput=false;try{AgentCommandJobTools.ValidateStart(new AgentAction{args=D(new{command=node,outputs=new[]{".env"}})},id=>true);}catch(InvalidDataException){credentialOutput=true;}check(credentialOutput,"v16: command output declarations cannot publish workspace credential paths");

        string sessionId=Id(),priorId=Id();var priorOptions=new AgentTaskOptions{sessionId=sessionId,goal="修复工作区 sum.ts 并运行测试",workspaceVersion=1,scope=new AgentScope{mode="all"},allowed=library.ResolveAgentScope(new AgentScope())};
        var journal=new AgentJournal(library.DataDir);var events=new List<AgentEvent>();
        void Emit(string kind,object data,string status="running"){events.Add(journal.Append(priorId,kind,data,sessionId,"code follow-up",status));}
        Emit("task.created",new{options=priorOptions,protocol="tools"},"created");var readCall=new AgentCall{id=Id(),tool="read_workspace_file",args=D(new{path="sum.ts"})};Emit("tool.proposed",new{call=readCall});Emit("tool.completed",new{callId=readCall.id,result=AgentWorkspaceTools.Read(library,readCall.args)});Emit("assistant.reply",new{text="已完成代码任务。"});Emit("task.completed",new{limited=false},"completed");
        service.SessionStore.Ensure(sessionId,"代码任务");
        AgentTaskOptions Follow(string goal,string sid=null){var next=new AgentTaskOptions{sessionId=sid??sessionId,goal=goal,scope=new AgentScope{mode="all"},allowed=library.ResolveAgentScope(new AgentScope()),delivery=new AgentDeliveryRequirement()};service.PrepareContinuation(next);AgentBusinessCore.Prepare(next,library,AppDomain.CurrentDomain.BaseDirectory);return next;}
        var follow=Follow("继续优化这个函数");check(follow.continuationTaskId==priorId&&follow.continuationFiles.Contains("sum.ts")&&AgentBusinessCore.Plan(follow,null).coding,"v16: vague follow-up resolves a real same-branch code task through the TypeScript core");
        check(AgentToolProfiles.ForTask(follow,library).Contains("apply_workspace_patch")&&!AgentToolProfiles.ForTask(follow,library).Contains("create_document"),"v16: continuation keeps relevant workspace tools without opening unrelated report creation");
        string previousLeaf=service.SessionStore.Leaf(sessionId),other=Id();service.SessionStore.Append(sessionId,"message",null,new{role="user",text="另一个无关主题"},other);service.SessionStore.SetLeaf(sessionId,other);
        check(Follow("继续优化").continuationTaskId=="","v16: switching conversation branches cannot inherit an unrelated code task");
        var replay=Follow("继续优化");replay.continuationTaskId=priorId;replay.continuationDomain="coding";replay.history.Add(new ChatTurn{role="user",content="修复工作区sum.ts"});service.PrepareContinuation(replay);check(replay.continuationTaskId=="","v16: caller history and forged continuation fields do not supply a trusted task anchor");
        service.SessionStore.SetLeaf(sessionId,previousLeaf);string alternate=Path.Combine(root,"other-workspace");Directory.CreateDirectory(alternate);AgentExecution.Save(library,true,alternate,false);check(Follow("继续优化").continuationTaskId=="","v16: changing workspaces blocks reuse of prior file context even in the same chat");
        AgentCommandJobs.Stop(library);service.Stop();
    }
}
