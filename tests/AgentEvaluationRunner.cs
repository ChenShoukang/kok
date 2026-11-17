using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using kok;

// Opt-in real-model evaluation, deliberately excluded from offline/CI native suites.
// Literal facts and saved-file checks are mechanical; this is not a semantic or visual quality judge.
class AgentEvaluationRunner {
    static string key="",root,app,model;static List<Dictionary<string,object>> reports=new List<Dictionary<string,object>>();
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> D(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static string Hash(string path){return AgentWorkspaceTools.Hash(File.ReadAllBytes(path));}
    static byte[] Part(string path,string name){using(var zip=ZipFile.OpenRead(path))using(var input=zip.GetEntry(name).Open())using(var bytes=new MemoryStream()){input.CopyTo(bytes);return bytes.ToArray();}}
    static void Save(string path,object value){File.WriteAllText(path,Library.Json.Serialize(value),new UTF8Encoding(false));}
    static async Task<Dictionary<string,object>> RunCase(Dictionary<string,object> sample){
        string label=ModuleContext.Text(sample,"id"),group=ModuleContext.Text(sample,"group"),profile=Path.Combine(root,label);Directory.CreateDirectory(profile);
        var library=new Library(profile);library.SetAppMode("smart",true);var ai=new TranslationService(profile);AgentService service=null;AgentTaskState state=null;
        var checks=new Dictionary<string,bool>();var watch=Stopwatch.StartNew();string taskId="",error="",answer="";int pages=0,hanzi=0;bool adjusted=false,stopped=false;
        string longText="# 长资料\n"+new string('甲',7500)+"\n中间限制：不收集手机号。\n"+new string('乙',7500)+"\n末尾修订：预算8万元，60人；日期和地点待确认。";
        var draft=library.CreateDocument("活动初稿","md","","初稿：预算12万元，80人。以最终修订为准。地点和日期未确认。");
        var revision=library.CreateDocument("最终修订","md","",label=="research-paging"?longText:"最终修订替代初稿：社区阅读活动，预算8万元，60人，不收集手机号。准备、实施、复盘三个阶段。地点、日期、负责人和预算分配待确认，不能编造。");
        var originalSources=new[]{draft,revision};var originals=originalSources.ToDictionary(item=>item.id,item=>Hash(item.path));
        string reportText="# 已保存报告\n## 目标\n采用最终预算8万元，60人，不收集手机号。\n## 流程\n准备、实施、复盘。\n## 风险\n资料未定部分保持待确认。\n## 待确认事项\n日期、地点、负责人和预算分配待确认。\n"+new string('研',400);
        Entry existingDoc=null,existingDeck=null;byte[][] unchangedPages=null;string existingHash="";
        if(label=="document-inspect-only"){existingDoc=library.CreateDocument("已生成报告","docx","",reportText);existingHash=Hash(existingDoc.path);}
        if(label=="presentation-target"||label=="boundary-component"){
            var slides=Enumerable.Range(1,8).Select(n=>new{title=n==3?"预算":"第"+n+"页",bullets=new[]{n==3?"旧预算12万元":"原文保留，第"+n+"页。"},layout="content"}).ToArray();
            existingDeck=library.CreatePresentation("已有8页PPT","",AgentSlides.Build(D(new{title="已有8页PPT",slides})));existingHash=Hash(existingDeck.path);
            unchangedPages=Enumerable.Range(1,8).Where(n=>n!=3).Select(n=>Part(existingDeck.path,"ppt/slides/slide"+n+".xml")).ToArray();
        }
        string workspace=Path.Combine(profile,"workspace"),code="// USER_COMMENT_MUST_STAY\nfunction add(a: number, b: number): number { return a - b; }\nif (add(2, 3) !== 5) process.exit(7);\nconsole.log('sum verified');\n";
        if(group=="coding"){Directory.CreateDirectory(workspace);File.WriteAllText(Path.Combine(workspace,"sum.ts"),code,new UTF8Encoding(false));AgentExecution.Save(library,true,workspace,false);}
        ai.SaveConfig(D(new{endpoint="https://api.deepseek.com",model=model,apiKey=key,automatic=false}));
        try{
            service=new AgentService(new ModuleContext(library,ai,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null,appDirectory:app));
            string sessionId=Id();
            if(label=="code-continuation"){
                var journal=new AgentJournal(profile);string prior=Id();var priorOptions=new AgentTaskOptions{sessionId=sessionId,goal="修复工作区sum.ts的加法错误并运行测试",workspaceVersion=1,scope=new AgentScope{mode="all"},allowed=library.ResolveAgentScope(new AgentScope())};
                journal.Append(prior,"task.created",new{options=priorOptions,protocol="tools"},sessionId,"历史代码读取","created");
                var read=new AgentCall{id=Id(),tool="read_workspace_file",args=D(new{path="sum.ts"})};journal.Append(prior,"tool.proposed",new{call=read},sessionId,"历史代码读取","running");journal.Append(prior,"tool.completed",new{callId=read.id,result=AgentWorkspaceTools.Read(library,read.args)},sessionId,"历史代码读取","running");
                journal.Append(prior,"assistant.reply",new{text="已读取sum.ts，当前加法仍待修复。"},sessionId,"历史代码读取","running");journal.Append(prior,"task.completed",new{limited=false},sessionId,"历史代码读取","completed");service.SessionStore.Ensure(sessionId,"代码任务");
            }
            string goal=ModuleContext.Text(sample,"goal"),suffix="\n保留原始资料和已有内容，不联网。以来源的最新修订为准，不确定信息如实说明。";
            if(group=="coding")suffix="\n使用已配置工作区中的sum.ts，保留USER_COMMENT_MUST_STAY注释；需要测试时可用实际Node运行sum.ts。不要访问网络或工作区外资料。";
            var ids=originalSources.Select(item=>item.id).ToList();if(existingDoc!=null)ids.Add(existingDoc.id);if(existingDeck!=null)ids.Add(existingDeck.id);
            var changes=AgentTaskState.Get<List<string>>(sample,"adjustments")??new List<string>();
            // Inspection is steered after a real read. Other revisions arrive before first approval, so canceled writes never run.
            int limit=label=="boundary-budget"?1:9;
            Save(Path.Combine(profile,"fixture.json"),new{label,goal=goal+suffix,adjustments=changes,originals,sourceTexts=originalSources.Select(item=>new{item.name,text=library.DocumentSource(item.id).text}),existingDoc=existingDoc==null?null:existingDoc.id,existingDeck=existingDeck==null?null:existingDeck.id,code=group=="coding"?code:null,limit,continuationSeed=label=="code-continuation"?"Host journal with a real native workspace read; unfinished fix":null,powerPointAvailable=PresentationRenderer.Available});
            var created=D(await service.HandleAgentTaskAction("agentTaskStart",D(new{runtime="research",sessionId,goal=goal+suffix,scope=new{mode=group=="coding"?"all":"selected",ids=ids.ToArray()},requestLimit=limit,budget=48000,contextTokens=64000,useMemory=false})));taskId=Convert.ToString(created["id"]);
            while(watch.Elapsed.TotalSeconds<360){
                var snapshot=D(await service.HandleAgentTaskAction("agentTaskRead",D(new{taskId})));state=AgentTaskState.Replay(taskId,new AgentJournal(profile).Events(taskId));
                var pending=state.calls.Where(call=>call.status=="pending").ToList();
                bool ready=label=="code-steering"?state.calls.Any(call=>call.tool=="apply_workspace_patch"&&call.status=="done"):label=="document-inspect-only"?state.calls.Any(call=>call.status=="done"&&call.tool=="read_items"):pending.Count>0;
                if(!adjusted&&changes.Count>0&&ready){
                    foreach(string change in changes)await service.HandleAgentTaskAction("agentTaskAdjust",D(new{taskId,text=change}));adjusted=true;
                    if(label=="boundary-stop"){await service.HandleAgentTaskAction("agentTaskStop",D(new{taskId}));stopped=true;}
                }else if(!stopped)foreach(var call in pending){
                    // The evaluator approves only the seeded task's native operations; no arbitrary writes, network or shells.
                    bool allowed=new[]{"create_document","replace_document_text","create_presentation","copy_presentation","edit_presentation","apply_workspace_patch","start_command_job","run_command"}.Contains(call.tool);
                    if(group!="coding"&&new[]{"apply_workspace_patch","start_command_job","run_command"}.Contains(call.tool))allowed=false;
                    if(!allowed)throw new InvalidOperationException("Unexpected approval requested: "+call.tool);
                    if(group=="coding"&&new[]{"start_command_job","run_command"}.Contains(call.tool)){
                        string command=ModuleContext.Text(call.args,"command"),installed=AgentExecution.ResolveExecutable("node.exe");if(!new[]{"node","node.exe"}.Contains(command)&&!String.Equals(command,installed,StringComparison.OrdinalIgnoreCase)&&!String.Equals(command,Path.Combine(app,"runtime","research","node.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Only the discovered Node executable is authorized by this fixture.");
                        var args=AgentTaskState.Get<List<string>>(call.args,"args")??new List<string>();bool versionOnly=args.SequenceEqual(new[]{"--version"})||args.SequenceEqual(new[]{"-v"});bool seededTest=args.Count>0&&new[]{"sum.ts","./sum.ts",".\\sum.ts"}.Contains(args.Last())&&args.Take(args.Count-1).All(arg=>new[]{"--experimental-strip-types","--no-warnings"}.Contains(arg));if(!versionOnly&&!seededTest)throw new InvalidOperationException("Only Node version discovery or the seeded sum.ts verification may be executed.");
                    }
                    await service.HandleAgentTaskAction("agentTaskDecide",D(new{taskId,callId=call.id,approve=true}));
                }
                if(!Convert.ToBoolean(snapshot["running"]))break;await Task.Delay(80);
            }
            if(state==null)throw new InvalidOperationException("No native task state was produced.");
            if(watch.Elapsed.TotalSeconds>=360){service.Stop();throw new TimeoutException("Evaluation case reached its finite deadline.");}
            state=AgentTaskState.Replay(taskId,new AgentJournal(profile).Events(taskId));
            answer=state.dialogue.LastOrDefault(row=>row.role=="agent"&&!row.meta)?.text??"";
            checks["originalSourcesPreserved"]=originalSources.All(item=>originals[item.id]==Hash(item.path));
            checks["adjustmentsApplied"]=changes.Count==0||adjusted;
            checks["requestLimitRespected"]=state.requests<=limit;
            var artifacts=state.calls.Where(call=>AgentTaskState.IsArtifactTool(call.tool)&&call.status=="done").ToList();var reopened=new Library(profile);
            string outputText=answer;
            foreach(var call in artifacts){var item=reopened.Find(ModuleContext.Text(call.result,"id"));checks["reopen-"+call.id]=item!=null&&File.Exists(item.path);if(item.ext=="PPTX"){var deck=PresentationFiles.Read(item.path,false);pages=deck.slides.Count;outputText+="\n"+String.Join("\n",deck.slides.SelectMany(slide=>slide.shapes.Select(shape=>shape.text)));}else{var body=reopened.DocumentSource(item.id);outputText+="\n"+body.text;hanzi=AgentDeliveryRules.MeasuredLength(body.text,AgentMeasureUnit.Hanzi);checks["saved-"+call.id]=!body.recovery;}}
            checks["terminalExpected"]=group=="boundary"?label=="boundary-budget"?state.status=="limited":label=="boundary-stop"?stopped&&state.status!="running":state.status=="completed":state.status=="completed";
            if(group!="boundary")checks["hostDeliveryAccepted"]=service.DeliveryProblem(state)=="";
            if(group=="research"){
                checks["noWrites"]=artifacts.Count==0&&!state.calls.Any(call=>AgentRules.Tools.Any(tool=>tool.Name==call.tool&&tool.Write)&&call.status=="done");
                if(label!="research-unknown"){string normalized=Regex.Replace(answer,@"\s+","");checks["finalFacts"]=normalized.Contains("8万")&&normalized.Contains("60")&&normalized.Contains("手机号");}
                if(label=="research-unknown")checks["unknownsHonest"]=answer.Contains("地点")&&answer.Contains("日期")&&Regex.IsMatch(answer,"待确认|未确定|未确认|尚未|不确定");
                if(label=="research-paging"){string detail;checks["actualFullCoverage"]=AgentReading.FullCoverage(library,state,ids,out detail);}
            }
            if(group=="document"){
                if(label=="document-inspect-only")checks["existingOnly"]=artifacts.Count==0&&Hash(existingDoc.path)==existingHash;
                else{checks["oneDocument"]=artifacts.Count==1&&artifacts[0].tool=="create_document";checks["formatAndLength"]=artifacts.Count==1&&reopened.Find(ModuleContext.Text(artifacts[0].result,"id")).ext==(label=="document-format"?"TXT":"DOCX")&&hanzi>=(label=="document-format"?500:400)&&hanzi<=(label=="document-format"?800:600);checks["finalFacts"]=outputText.Contains("8万")&&outputText.Contains("60")&&outputText.Contains("手机号");}
            }
            if(group=="presentation"){
                if(label=="presentation-target"){
                    var deck=PresentationFiles.Read(existingDeck.path,false);checks["sameDeckOnly"]=artifacts.Count==0&&deck.slides.Count==8;checks["targetChanged"]=String.Join("\n",deck.slides[2].shapes.Select(shape=>shape.text)).Contains("8万");
                    checks["otherPagesPreserved"]=Enumerable.Range(1,8).Where(n=>n!=3).Select((n,index)=>Part(existingDeck.path,"ppt/slides/slide"+n+".xml").SequenceEqual(unchangedPages[index])).All(ok=>ok);
                }else{checks["oneDeck"]=artifacts.Count==1&&artifacts[0].tool=="create_presentation";checks["actualPages"]=pages==(label=="presentation-pages"?6:8);}
            }
            if(group=="coding"){
                string current=File.ReadAllText(Path.Combine(workspace,"sum.ts"));checks["userCommentPreserved"]=current.Contains("// USER_COMMENT_MUST_STAY");checks["actualPatch"]=state.calls.Any(call=>call.tool=="apply_workspace_patch"&&call.status=="done");checks["actualSuccessfulTest"]=state.calls.Any(call=>new[]{"wait_command_job","read_command_job","start_command_job","run_command"}.Contains(call.tool)&&call.status=="done"&&Library.Json.Serialize(call.result).Contains("sum verified"));
                if(label=="code-continuation")checks["trustedContinuation"]=!String.IsNullOrEmpty(state.options.continuationTaskId)&&state.options.continuationDomain=="coding";
            }
            if(group=="combined"){
                if(label=="combined-cancel-report")checks["onlyDeck"]=artifacts.Count==1&&artifacts[0].tool=="create_presentation"&&pages==8;
                if(label=="combined-switch-report")checks["onlyReport"]=artifacts.Count==1&&artifacts[0].tool=="create_document"&&hanzi>=400&&hanzi<=600;
                if(label=="combined-chat")checks["chatOnly"]=artifacts.Count==0&&AgentDeliveryRules.MeasuredLength(answer,AgentMeasureUnit.Hanzi)==200;
            }
            if(group=="quality"){
                checks["oneSavedDocx"]=artifacts.Count==1&&reopened.Find(ModuleContext.Text(artifacts[0].result,"id")).ext=="DOCX"&&hanzi>=400&&hanzi<=600;
                var requirement=AgentDeliveryRules.Derive(state.options,state.adjustments);var expected=label=="quality-sections-change"?new[]{"背景","结论","待确认事项"}:new[]{"目标","流程","风险","待确认事项"};checks["currentRequiredSections"]=requirement.requiredSections.SequenceEqual(expected);
                if(artifacts.Count==1){var structural=AgentArtifactReview.Document(reopened,ModuleContext.Text(artifacts[0].result,"id"),expected,requirement);checks["actualSectionBodies"]=structural.errors==0&&structural.sectionChecks.Count==expected.Length;}
                var claims=state.calls.Where(call=>call.tool=="review_claims"&&call.status=="done"&&call.result!=null).SelectMany(call=>AgentTaskState.Get<List<Dictionary<string,object>>>(AgentTaskState.Get<Dictionary<string,object>>(call.result,"claimReview"),"claims")??new List<Dictionary<string,object>>()).ToList();
                string finalId=artifacts.Count==1?ModuleContext.Text(artifacts[0].result,"id"):"",signature=finalId==""?"":reopened.DocumentSource(finalId).signature;
                checks["savedClaimAnchoredToReadSource"]=claims.Any(row=>originals.ContainsKey(ModuleContext.Text(row,"sourceId"))&&ModuleContext.Text(row,"artifactId")==finalId&&ModuleContext.Text(row,"artifactVersion")==signature&&ModuleContext.Text(row,"status")=="anchored"&&ModuleContext.Text(row,"artifactStatus")=="located"&&!AgentTaskState.Get<bool>(row,"semanticVerified"));
                string normalized=Regex.Replace(outputText,@"\s+","");checks["literalFinalFacts"]=normalized.Contains("8万")&&normalized.Contains("60")&&normalized.Contains("手机号")&&normalized.Contains("待确认");
            }
            if(label=="boundary-budget")checks["noUnapprovedWrites"]=artifacts.Count==0;
            if(label=="boundary-stop")checks["stoppedBeforeWrite"]=artifacts.Count==0&&!state.calls.Any(call=>call.tool=="create_presentation"&&call.status=="done");
            if(label=="boundary-component")checks["noFalseRender"]=Hash(existingDeck.path)==existingHash&&artifacts.Count==0&&!state.calls.Any(call=>call.tool=="render_presentation"&&call.status=="done")&&Regex.IsMatch(answer,"渲染|PowerPoint",RegexOptions.IgnoreCase)&&!Regex.IsMatch(answer,"已(?:经)?(?:成功)?(?:完成)?渲染");
        }catch(Exception failure){error=AgentWorkspaceTools.Redact(failure.Message);checks["runnerCompleted"]=false;}
        finally{if(service!=null)service.Stop();AgentCommandJobs.Stop(library);foreach(string name in new[]{"ai-settings.json","ai-settings.json.bak"}){string path=Path.Combine(profile,name);if(File.Exists(path))File.Delete(path);}}
        var report=D(new{label,group,passed=checks.Count>0&&checks.Values.All(ok=>ok),checks,taskId,status=state==null?"runner_failed":state.status,error=error==""&&state!=null?state.error:error,requests=state==null?0:state.requests,seconds=Math.Round(watch.Elapsed.TotalSeconds,2),pages,hanzi,answer,adjusted,stopped,trace=state==null?null:AgentTrace.Export(state)});
        Save(Path.Combine(profile,"result.json"),report);Console.WriteLine(label+": "+(Convert.ToBoolean(report["passed"])?"PASS":"FAIL")+", status="+report["status"]+", requests="+report["requests"]+", checks="+String.Join(",",checks.Where(row=>!row.Value).Select(row=>row.Key)));return report;
    }
    static async Task<int> Run(string[] args){
        root=Path.GetFullPath(args[0]);app=Path.GetFullPath(args[1]);var manifest=File.ReadAllText(args[2]);model=args[4];key=Regex.Match(File.ReadAllText(args[3]),@"sk-[A-Za-z0-9_-]{16,}").Value;if(key=="")throw new InvalidOperationException("No usable DeepSeek key found.");
        var selected=args.Length>5?args[5].Split(new[]{','},StringSplitOptions.RemoveEmptyEntries):new string[0];var samples=AgentTaskState.Get<List<Dictionary<string,object>>>(Library.Json.Deserialize<Dictionary<string,object>>(manifest),"cases");
        if(selected.Any(id=>!samples.Any(row=>ModuleContext.Text(row,"id")==id)))throw new InvalidOperationException("Unknown evaluation case.");
        LiveEvaluationBudget.Initialize(new[]{"--debug","--test-silent"});
        foreach(var sample in samples.Where(row=>selected.Length==0||selected.Contains(ModuleContext.Text(row,"id")))){reports.Add(await RunCase(sample));Save(Path.Combine(root,"evaluation.json"),new{format="kok.agent.live-evaluation",version=1,model,sampleSha256=AgentWorkspaceTools.Hash(Encoding.UTF8.GetBytes(manifest)),scope="Development material, actual DeepSeek + bundled runtime + host approvals + saved artifacts. Literal fact/structure checks only; not blinded semantic or visual review. Boundary cases are counted separately. Continuation uses a seeded native journal, not a prior model run.",cases=reports});}
        key="";return reports.All(row=>Convert.ToBoolean(row["passed"]))?0:1;
    }
    static int Main(string[] args){try{return Run(args).GetAwaiter().GetResult();}catch(Exception failure){Console.Error.WriteLine(Regex.Replace(key==""?failure.Message:failure.Message.Replace(key,"[REDACTED]"),@"sk-[A-Za-z0-9_-]{16,}","[REDACTED]"));return 1;}finally{key="";}}
}
