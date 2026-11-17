using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using kok;

static class AgentQualityV162Tests {
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> D(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static Dictionary<string,object> First(Dictionary<string,object> result){return AgentTaskState.Get<List<Dictionary<string,object>>>(AgentTaskState.Get<Dictionary<string,object>>(result,"claimReview"),"claims").Single();}
    public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
    static async Task RunAsync(string root,Action<bool,string> check){
        var library=new Library(root);library.SetAppMode("smart",true);AgentBusinessCore.Configure(AppDomain.CurrentDomain.BaseDirectory);
        var source=library.CreateDocument("最终修订","md","","# 最终修订\n"+new string('前',300)+"\n预算8万元，60人。\n日期待确认。");
        var artifact=library.CreateDocument("已保存报告","docx","","# 活动报告\n## 目标\n依照最终修订执行。\n## 风险\n预算为8万元。\n");
        var options=new AgentTaskOptions{runtime="research",goal="创建DOCX报告，章节：目标、风险。",artifactReviewVersion=2,sessionId=Id(),allowed=new List<string>{source.id,artifact.id},scope=new AgentScope{mode="selected",ids=new List<string>{source.id,artifact.id}}};
        AgentBusinessCore.Prepare(options,library,AppDomain.CurrentDomain.BaseDirectory);
        var state=new AgentTaskState{id=Id(),options=options};
        object Claim(string quote="预算8万元，60人。",string version=""){return new{claim="预算为8万元。",sourceId=source.id,quote=quote,relationship="supports",sourceVersion=version};}
        async Task<Dictionary<string,object>> Review(object claim=null){var call=new AgentCall{id=Id(),tool="review_claims",args=D(new{claims=new[]{claim??Claim()},artifact_id=artifact.id})};AgentClaimReview.Tool().Validate(new AgentAction{tool=call.tool,args=call.args},id=>options.allowed.Contains(id));return await library.ReadAgentTool(state,call,CancellationToken.None);}
        async Task Read(int offset,int max){var call=new AgentCall{id=Id(),sequence=state.calls.Count+1,tool="read_document",status="done",args=D(new{id=source.id,offset,max_chars=max})};call.result=await library.ReadAgentTool(state,call,CancellationToken.None);state.calls.Add(call);}
        var unread=await Review();check(Convert.ToString(First(unread)["status"])=="unread"&&Convert.ToBoolean(First(unread)["quoteVerified"]),"v162: exact quote location alone does not invent reading coverage");
        check(!unread.ContainsKey("coverage")&&unread.ContainsKey("_sourceStamps")&&Convert.ToString(First(unread)["artifactStatus"])=="located","v162: claim review binds source and actual saved DOCX versions without granting reading");
        state.createdArtifactIds.Add(artifact.id);var self=First(await Review(new{claim="预算为8万元。",sourceId=artifact.id,quote="预算为8万元。",relationship="supports"}));check(Convert.ToString(self["sourceRole"])=="artifact"&&AgentTaskState.Get<List<string>>(self,"warnings").Contains("artifact_used_as_source"),"v162: the native host labels current-task artifact sources and warns against report self-citation");
        await Read(0,200);check(Convert.ToString(First(await Review())["status"])=="unread","v162: reading the opening does not cover a later quoted conclusion");
        await Read(0,12000);var anchored=First(await Review());string version=Convert.ToString(anchored["sourceVersion"]);
        check(Convert.ToString(anchored["status"])=="anchored"&&Convert.ToInt32(anchored["startLine"])==3&&AgentTaskState.Get<List<string>>(anchored,"readCallIds").Contains(state.calls.Last().id),"v162: native read ranges anchor the exact current quotation with line and original call identity");
        check(!Convert.ToBoolean(anchored["semanticVerified"])&&Convert.ToString(anchored["relationshipAuthority"])=="caller_proposed","v162: a located quote never certifies caller-proposed semantic support");
        File.AppendAllText(source.path,"\n用户修改资料。\n");
        check(Convert.ToString(First(await Review())["status"])=="stale_reading"&&Convert.ToString(First(await Review(Claim(version:version)))["status"])=="stale","v162: actual source changes invalidate old read ranges and explicitly supplied source versions");
        check(Convert.ToString(First(await Review(Claim("预算12万元")))["status"])=="not_found","v162: invented source quotations are reported as missing");
        bool denied=false;try{AgentClaimReview.Tool().Validate(new AgentAction{args=D(new{claims=new[]{new{claim="越界",sourceId=Id(),quote="片段",relationship="supports"}}})},id=>options.allowed.Contains(id));}catch(FormatException){denied=true;}check(denied,"v162: source IDs nested in claim rows are still checked against native task scope");
        var requirement=AgentDeliveryRules.Derive(options);check(requirement.requiredSections.SequenceEqual(new[]{"目标","风险"})&&!requirement.requiresTargetedEdit,"v162: explicit report section lists travel from TypeScript planning to native acceptance");
        var revised=AgentDeliveryRules.Derive(options,new[]{"栏目改为背景、结论"});check(revised.requiredSections.SequenceEqual(new[]{"背景","结论"})&&!revised.requiresTargetedEdit,"v162: changing report section requirements replaces only that specification");
        var codeState=new AgentTaskState{options=new AgentTaskOptions{goal="修复代码但不要运行测试"},adjustments=new List<string>{"现在运行测试"}};string codeInstructions=AgentCodeDelivery.Instructions(codeState);check(codeInstructions.Contains("[Later user adjustments, in chronological order]\n现在运行测试")&&!codeInstructions.Contains("Current goal:"),"v162: code instructions include actual later steering instead of restating an obsolete initial goal as current");
        string workspace=Path.Combine(root,"workspace");Directory.CreateDirectory(workspace);AgentExecution.Save(library,true,workspace,false);var info=D(Library.Json.DeserializeObject(ModuleContext.Text(AgentWorkspaceTools.Info(library),"text")));check(ModuleContext.Text(info,"bundledNode")==AgentBusinessCore.NodePath&&File.Exists(AgentBusinessCore.NodePath),"v162: workspace discovery uses the configured packaged Node path even when the native host is embedded elsewhere");
        var legacy=new AgentTaskOptions{runtime=options.runtime,businessVersion=options.businessVersion,businessEnvironment=options.businessEnvironment,goal=options.goal,artifactReviewVersion=1,allowed=options.allowed,scope=options.scope};check(AgentDeliveryRules.Derive(legacy).requiredSections.Count==0,"v162: old persisted review-version tasks do not acquire new section completion requirements");
        var before=library.DocumentSource(artifact.id);string changedText=before.text.Replace("## 风险\n预算为8万元。","## 风险\n");library.SaveDocument(artifact.id,changedText,before.signature,null);
        var broken=AgentArtifactReview.Document(library,artifact.id,requirement.requiredSections,requirement);check(broken.errors==1&&broken.findings.Single(f=>f.severity=="error").code=="empty_section","v162: actual saved DOCX with an empty mandatory section yields a definite local defect");
        var edit=library.PrepareAgentCall(state,new AgentAction{tool="replace_document_text",args=D(new{id=artifact.id,old_text="## 风险\n",new_text="## 风险\n预算为8万元。\n"})},"repair",null);
        edit.sequence=state.calls.Count+1;library.WriteAgentTool(state,edit,CancellationToken.None);
        var repaired=AgentArtifactReview.Document(new Library(root),artifact.id,requirement.requiredSections,requirement);
        check(repaired.errors==0&&library.DocumentSource(artifact.id).text.Contains("## 目标\n依照最终修订执行。"),"v162: exact section repair survives library reopen and preserves unrelated report text");
    }
}
