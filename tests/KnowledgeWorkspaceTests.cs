using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

class KnowledgeWorkspaceTests {
    public static void Run(string root,Action<bool,string> check){
        var library=new Library(root);library.SetAppMode("smart",true);
        var items=Enumerable.Range(0,24).Select(n=>library.CreateDocument("Scope "+n,"md","Scope","# Scope "+n+"\n\n"+new string((char)('a'+n),2500)+"\nLocal evidence "+n)).ToList();
        var ids=items.Select(i=>i.id).ToList();var scope=new KnowledgeScope{id=Guid.NewGuid().ToString("N"),name="Reading",scope="manual",kind="",ids=ids.Take(2).ToList(),limit=100,from="2026-01-01",to="2026-12-31"};
        library.Settings(new Dictionary<string,object>{{"knowledgeViews",new[]{scope}}});var reopened=new Library(root);var views=Library.ValidateKnowledgeScopes(reopened.Db.settings["knowledgeViews"]);
        check(views.Count==1&&views[0].ids.SequenceEqual(scope.ids),"saved graph scopes retain explicit IDs and date boundaries across restart");
        scope.to="2025-01-01";bool invalid=false;try{library.Settings(new Dictionary<string,object>{{"knowledgeViews",new[]{scope}}});}catch(InvalidDataException){invalid=true;}
        check(invalid&&Library.ValidateKnowledgeScopes(library.Db.settings["knowledgeViews"])[0].to=="2026-12-31","invalid date ranges cannot replace a saved graph scope");
        var context=library.BuildKnowledgeContext(ids,"connect","",6000);
        check(context.ids.Count==24&&context.characters<=6000&&context.outputTokens==900&&context.sampled,"economy recipe bounds all 24 selected source excerpts and the output budget");
        check(!context.context.Contains(root)&&context.references.All(r=>ids.Contains(r.fileId)),"AI preparation excludes file paths and restricts references to selected files");
        var repeat=library.BuildKnowledgeContext(ids.AsEnumerable().Reverse().ToList(),"connect","",6000);
        check(context.fingerprint==repeat.fingerprint,"source selection order does not invalidate an identical analysis cache");
        var partial=library.BuildKnowledgeContext(ids.Take(2).ToList(),"connect","",6000);
        var parsed=KnowledgeRecipes.Parse("{\"summary\":\"Evidence [S1] and [S2]\",\"connections\":[{\"source\":\"S1\",\"target\":\"S2\",\"reason\":\"Shared evidence\"}]}",partial);
        check(parsed.connections.Count==1,"a recipe accepts source-bound suggested connections");
        foreach(string answer in new[]{"{\"summary\":\"Unknown [S99]\",\"connections\":[]}","{\"summary\":\"Test\",\"connections\":[{\"source\":\"S1\",\"target\":\"S1\",\"reason\":\"Self\"}]}","{\"summary\":\"Test\",\"connections\":[{\"source\":\"S1\",\"target\":\"S99\",\"reason\":\"Outside\"}]}"}){
            bool rejected=false;try{KnowledgeRecipes.Parse(answer,partial);}catch(InvalidDataException){rejected=true;}check(rejected,"AI results reject invented citations, self connections or out-of-scope endpoints");
        }
        var report=new KnowledgeReport{id=Guid.NewGuid().ToString("N"),fingerprint="test-fingerprint",created=DateTime.UtcNow.ToString("o"),recipe="connect",summary=parsed.summary,connections=parsed.connections,references=partial.references,ids=partial.ids};library.SaveKnowledgeReport(report);
        check(new Library(root).CachedKnowledgeReport("test-fingerprint").id==report.id,"bounded analysis results persist locally for reuse");
        library.SaveDocument(items[0].id,"Changed evidence",library.DocumentSource(items[0].id).signature,null);
        check(library.BuildKnowledgeContext(ids,"connect","",6000).fingerprint!=context.fingerprint,"editing a source invalidates the prepared context fingerprint");
        library.SetAppMode("pure",false);bool pure=false;try{library.BuildKnowledgeContext(ids,"connect","",6000);}catch(InvalidOperationException){pure=true;}check(pure,"pure mode rejects AI context preparation before any model call");
        ProjectCases(Path.Combine(root,"creative-projects"),check);
    }
 // P1 creative projects: native project model, audited artifact/session links, projectId task pipeline.
 sealed class ProjectProvider:HttpMessageHandler {
  public int requests;public List<Dictionary<string,object>> bodies=new List<Dictionary<string,object>>();
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
   bodies.Add(Library.Json.Deserialize<Dictionary<string,object>>(await request.Content.ReadAsStringAsync()));requests++;
   var body=new{choices=new[]{new{message=new{role="assistant",content="Project fixture",tool_calls=new[]{new{id="call_finish",type="function",function=new{name="finish_task",arguments=Library.Json.Serialize(new{status="partial",summary="Project fixture outcome",checks=new object[0]})}}}},finish_reason="tool_calls"}},usage=new{prompt_tokens=100,completion_tokens=20}};
   return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Library.Json.Serialize(body),Encoding.UTF8,"application/json")};
  }
 }
 static void ProjectCases(string root,Action<bool,string> check){ProjectCasesAsync(root,check).GetAwaiter().GetResult();}
 static async Task ProjectCasesAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  var library=new Library(Path.Combine(root,"library"));library.SetAppMode("smart",true);
  var materialA=library.CreateDocument("Material A","md","","MATERIAL_A");var materialB=library.CreateDocument("Material B","md","","MATERIAL_B");var outside=library.CreateDocument("Outside","md","","OUTSIDE");
  string timestamp=DateTime.UtcNow.ToString("o");
  Func<string,SpaceRecord> projectRecord=id=>new SpaceRecord{id=id,name="秋季画册",created=timestamp,updated=timestamp,view="grid",kind="project",goal="完成一本秋季画册",requirements="温暖色调，共 8 页",status="active",members=new List<string>{materialA.id,materialB.id}};
  string projectId=Guid.NewGuid().ToString("N");
  var createProject=library.ProposeResource("space",projectId,Library.Json.Serialize(projectRecord(projectId)),"user");library.OperationApply(createProject.id,true);
  Func<Library,List<SpaceRecord>> spaces=target=>Library.Json.Deserialize<WorkspaceState>(Library.Json.Serialize(target.WorkspaceSnapshot())).spaces;
  string matrixId=Guid.NewGuid().ToString("N");
  Func<SpaceRecord> matrix=()=>new SpaceRecord{id=matrixId,name="矩阵项目",created=timestamp,view="grid",kind="project",goal="目标",requirements="要求",status="active",members=new List<string>()};
  bool Accepts(SpaceRecord record){try{library.ProposeResource("space",matrixId,Library.Json.Serialize(record),"user");return true;}catch(InvalidDataException){return false;}}
  var validProject=matrix();var fullProject=matrix();fullProject.artifacts=new List<string>{materialA.id};fullProject.sessions=new List<string>{Guid.NewGuid().ToString("N")};
  var badKind=matrix();badKind.kind="deck";var badStatus=matrix();badStatus.status="paused";var longGoal=matrix();longGoal.goal=new string('g',1001);var longRequirements=matrix();longRequirements.requirements=new string('r',4001);var longUpdated=matrix();longUpdated.updated=new string('u',41);
  var spaceWithGoal=matrix();spaceWithGoal.kind="space";var spaceWithArtifacts=matrix();spaceWithArtifacts.kind="space";spaceWithArtifacts.artifacts=new List<string>{materialA.id};var badArtifact=matrix();badArtifact.artifacts=new List<string>{"not-an-id"};var duplicateArtifacts=matrix();duplicateArtifacts.artifacts=new List<string>{materialA.id,materialA.id};var badSession=matrix();badSession.sessions=new List<string>{new string('z',32)};
  check(Accepts(validProject)&&Accepts(fullProject)&&!Accepts(badKind)&&!Accepts(badStatus)&&!Accepts(longGoal)&&!Accepts(longRequirements)&&!Accepts(longUpdated)&&!Accepts(spaceWithGoal)&&!Accepts(spaceWithArtifacts)&&!Accepts(badArtifact)&&!Accepts(duplicateArtifacts)&&!Accepts(badSession),"creative project validation accepts valid projects and rejects bad kind/status/limits/artifact/session data plus plain spaces carrying project fields");
  string legacyId=Guid.NewGuid().ToString("N");string legacyJson="{\"id\":\""+legacyId+"\",\"name\":\"旧空间\",\"created\":\""+timestamp+"\",\"view\":\"grid\",\"members\":[],\"positions\":{}}";
  var legacyOp=library.ProposeResource("space",legacyId,legacyJson,"user");library.OperationApply(legacyOp.id,true);
  var legacy=spaces(library).FirstOrDefault(space=>space.id==legacyId);bool legacyResave=false;try{library.ProposeResource("space",legacyId,Library.Json.Serialize(legacy),"user");legacyResave=true;}catch(InvalidDataException){}
  check(legacy!=null&&legacy.kind=="space"&&legacy.status=="active"&&legacy.artifacts.Count==0&&legacy.sessions.Count==0&&legacyResave,"legacy space JSON without project fields loads and re-saves as a plain space");
  string linkProjectId=Guid.NewGuid().ToString("N");var linkCreate=library.ProposeResource("space",linkProjectId,Library.Json.Serialize(projectRecord(linkProjectId)),"user");library.OperationApply(linkCreate.id,true);
  var deliverable=library.CreateDocument("报告成果","md","","DELIVERABLE");string linkSession=Guid.NewGuid().ToString("N");int opsBeforeLinks=new Operations(library,null).List().Count;
  bool addedArtifact=library.ProjectLinkArtifact(linkProjectId,deliverable.id);bool addedSession=library.ProjectLinkSession(linkProjectId,linkSession);
  var linked=spaces(library).First(space=>space.id==linkProjectId);int opsAfterLinks=new Operations(library,null).List().Count;
  bool repeatArtifact=library.ProjectLinkArtifact(linkProjectId,deliverable.id),repeatSession=library.ProjectLinkSession(linkProjectId,linkSession);
  bool missingProject=library.ProjectLinkArtifact(Guid.NewGuid().ToString("N"),deliverable.id)||library.ProjectLinkSession(linkProjectId,"bad-id");
  var operations=new Operations(library,null);var sessionLinkOperation=operations.List().Last(operation=>operation.module=="workspace"&&operation.resources!=null&&operation.resources.Any(patch=>patch.kind=="space"&&patch.id==linkProjectId&&patch.after!=null&&patch.after.Contains(linkSession)));
  operations.Undo(sessionLinkOperation.id);var unlinked=spaces(library).First(space=>space.id==linkProjectId);
  check(addedArtifact&&addedSession&&linked.artifacts.Contains(deliverable.id)&&linked.sessions.Contains(linkSession)&&linked.updated!=""&&!repeatArtifact&&!repeatSession&&!missingProject&&opsAfterLinks==opsBeforeLinks+2&&unlinked.artifacts.Contains(deliverable.id)&&!unlinked.sessions.Contains(linkSession),"project artifact/session links persist through audited workspace operations, are idempotent, refuse missing projects and undo cleanly");
  var provider=new ProjectProvider();var ai=new TranslationService(library.DataDir,()=>provider);ai.SaveConfig(new Dictionary<string,object>{{"endpoint","http://127.0.0.1:1234/v1"},{"model","fixture"},{"apiKey","synthetic-key-only"}});
  var context=new ModuleContext(library,ai,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
  var service=new AgentService(context);
  Dictionary<string,object> Map(object value)=>Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));
  async Task<Dictionary<string,object>> StartAndSettle(Dictionary<string,object> args){var snapshot=Map(await service.HandleAgentTaskAction("agentTaskStart",args));string taskId=Convert.ToString(snapshot["id"]);for(int n=0;n<400&&Convert.ToBoolean(snapshot["running"]);n++){await Task.Delay(10);snapshot=Map(await service.HandleAgentTaskAction("agentTaskRead",new Dictionary<string,object>{{"taskId",taskId}}));}return snapshot;}
  string sessionId=Guid.NewGuid().ToString("N");
  var started=await StartAndSettle(new Dictionary<string,object>{{"sessionId",sessionId},{"goal","继续画册"},{"projectId",projectId},{"requestLimit",2}});
  var startedOptions=Library.Json.Deserialize<AgentTaskOptions>(Library.Json.Serialize(started["options"]));
  var linkedProject=spaces(library).First(space=>space.id==projectId);
  var guidanceMessage=provider.bodies.SelectMany(body=>AgentTaskState.Get<List<Dictionary<string,object>>>(body,"messages")??new List<Dictionary<string,object>>()).Select(message=>Convert.ToString(message.ContainsKey("content")?message["content"]:"")).Where(content=>content!=null&&content.Contains("[Task guidance selected by the user")).FirstOrDefault();
  check(Convert.ToString(started["projectId"])==projectId&&startedOptions.projectId==projectId&&startedOptions.projectBrief.Contains("秋季画册")&&startedOptions.projectBrief.Contains("完成一本秋季画册")&&startedOptions.projectBrief.Contains("温暖色调")&&startedOptions.projectBrief.Length<=4000&&guidanceMessage!=null&&guidanceMessage.Contains("秋季画册")&&guidanceMessage.Contains("完成一本秋季画册")&&linkedProject.sessions.Contains(sessionId),"agentTaskStart validates the project, injects a stable projectBrief guidance prefix, exposes snapshot projectId and links the session");
  check(startedOptions.allowed.Count==2&&startedOptions.allowed.Contains(materialA.id)&&startedOptions.allowed.Contains(materialB.id)&&!startedOptions.allowed.Contains(outside.id),"an omitted scope defaults to valid project members while unknown materials are ignored");
  string explicitSession=Guid.NewGuid().ToString("N");
  var explicitStarted=await StartAndSettle(new Dictionary<string,object>{{"sessionId",explicitSession},{"goal","显式范围"},{"projectId",projectId},{"scope",new{mode="selected",ids=new[]{outside.id}}},{"requestLimit",2}});
  var explicitOptions=Library.Json.Deserialize<AgentTaskOptions>(Library.Json.Serialize(explicitStarted["options"]));
  check(explicitOptions.allowed.Count==1&&explicitOptions.allowed[0]==outside.id&&explicitOptions.projectBrief.Contains("秋季画册")&&spaces(library).First(space=>space.id==projectId).sessions.Contains(explicitSession),"an explicit caller scope is preserved for project tasks while the project brief is still injected");
  bool unknownProject=false;try{await service.HandleAgentTaskAction("agentTaskStart",new Dictionary<string,object>{{"sessionId",Guid.NewGuid().ToString("N")},{"goal","unknown"},{"projectId",Guid.NewGuid().ToString("N")}});}catch(InvalidDataException){unknownProject=true;}
  check(unknownProject,"agentTaskStart rejects an unknown project id");
  library.ProjectLinkArtifact(projectId,deliverable.id);
  var resumed=await StartAndSettle(new Dictionary<string,object>{{"sessionId",Guid.NewGuid().ToString("N")},{"goal","修改已有报告"},{"projectId",projectId},{"requestLimit",2}});
  var resumedOptions=Library.Json.Deserialize<AgentTaskOptions>(Library.Json.Serialize(resumed["options"]));
  check(resumedOptions.allowed.SequenceEqual(new[]{deliverable.id,materialA.id,materialB.id})&&!resumedOptions.allowed.Contains(outside.id),"a fresh project task can read/edit existing artifacts alongside materials without widening to unrelated files");
  var narrow=await StartAndSettle(new Dictionary<string,object>{{"sessionId",Guid.NewGuid().ToString("N")},{"goal","仅指定资料"},{"projectId",projectId},{"scope",new{mode="selected",ids=new[]{materialA.id}}},{"requestLimit",2}});
  check(Library.Json.Deserialize<AgentTaskOptions>(Library.Json.Serialize(narrow["options"])).allowed.SequenceEqual(new[]{materialA.id}),"explicit project scope never silently adds artifact permissions");
  // A terminal task can later continue: synchronize only its new journal events, preserving earlier artifact nodes.
  string journalSession=Guid.NewGuid().ToString("N"),journalTask=Guid.NewGuid().ToString("N");var journal=new AgentJournal(library.DataDir);
  var journalOptions=new AgentTaskOptions{sessionId=journalSession,goal="原始需求"};
  journal.Append(journalTask,"task.created",new{options=journalOptions,protocol="tools"},journalSession,"续作历史","paused");
  journal.Append(journalTask,"assistant.reply",new{text="请补充受众",done=true},journalSession,"续作历史","needs_input");
  await service.HandleAgentTaskAction("agentSessionProject",new Dictionary<string,object>{{"sessionId",journalSession}});
  var anchor=service.SessionStore.Append(journalSession,"artifact",service.SessionStore.Leaf(journalSession),new Dictionary<string,object>{{"itemId",deliverable.id},{"taskId",journalTask},{"name","阶段稿"}});service.SessionStore.SetLeaf(journalSession,anchor.id);
  journalOptions.sessionLeafId=service.SessionStore.Entries(journalSession).First(entry=>entry.type=="message").id;
  journal.Append(journalTask,"task.continued",new{text="面向初学者"},journalSession,"续作历史","paused");
  journal.Append(journalTask,"assistant.reply",new{text="补充后的报告已完成",done=true},journalSession,"续作历史","completed");
  await service.HandleAgentTaskAction("agentSessionProject",new Dictionary<string,object>{{"sessionId",journalSession}});
  var continuedHistory=service.SessionStore.Projection(journalSession);
  check(continuedHistory.Select(turn=>turn.content).SequenceEqual(new[]{"原始需求","请补充受众","面向初学者","补充后的报告已完成"})&&service.SessionStore.Artifacts(journalSession).Any(row=>Convert.ToString(row["itemId"])==deliverable.id),"terminal task continuation appends both messages after its prior artifact instead of losing history or branch outputs");
  int syncedCount=service.SessionStore.Entries(journalSession).Count;string syncedLeaf=service.SessionStore.Leaf(journalSession);
  await service.HandleAgentTaskAction("agentSessionProject",new Dictionary<string,object>{{"sessionId",journalSession}});
  check(service.SessionStore.Entries(journalSession).Count==syncedCount&&service.SessionStore.Leaf(journalSession)==syncedLeaf,"repeated sequence-aware session sync neither duplicates messages nor resets the selected leaf");
  string writeProjectId=Guid.NewGuid().ToString("N");var writeCreate=library.ProposeResource("space",writeProjectId,Library.Json.Serialize(projectRecord(writeProjectId)),"user");library.OperationApply(writeCreate.id,true);
  var agentCreated=library.CreateDocument("Agent 报告","md","","AGENT_REPORT");var operationCreated=library.CreateDocument("Agent 演示","md","","AGENT_DECK");
  string syntheticTaskId=Guid.NewGuid().ToString("N");var operationStore=new Operations(library,null);operationStore.Created(Guid.NewGuid().ToString("N"),syntheticTaskId,"documents",operationCreated);
  var synthetic=new AgentTaskState{id=syntheticTaskId,status="completed",options=new AgentTaskOptions{projectId=writeProjectId,sessionId=Guid.NewGuid().ToString("N"),goal="fixture"},calls=new List<AgentCall>{new AgentCall{id=Guid.NewGuid().ToString("N"),tool="create_document",status="done",result=new Dictionary<string,object>{{"id",agentCreated.id}}}}};
  int opsBeforeWriteback=operationStore.List().Count;service.WriteProjectArtifacts(synthetic);var afterWriteback=spaces(library).First(space=>space.id==writeProjectId);service.WriteProjectArtifacts(synthetic);int opsAfterWriteback=operationStore.List().Count;
  check(afterWriteback.artifacts.Contains(agentCreated.id)&&afterWriteback.artifacts.Contains(operationCreated.id)&&opsAfterWriteback==opsBeforeWriteback+2,"terminal artifact writeback links done artifact-call results and agent-created operations into the project exactly once");
  service.SessionStore.Ensure(synthetic.options.sessionId,"持续创作成果");service.WriteSessionArtifacts(synthetic);
  var continued=library.CreateDocument("续作成果","md","","FOLLOW_UP_ARTIFACT");synthetic.sequence++;
  synthetic.calls.Add(new AgentCall{id=Guid.NewGuid().ToString("N"),tool="create_document",status="done",result=new Dictionary<string,object>{{"id",continued.id}}});
  service.WriteProjectArtifacts(synthetic);service.WriteSessionArtifacts(synthetic);
  check(spaces(library).First(space=>space.id==writeProjectId).artifacts.Contains(continued.id)&&service.SessionStore.Artifacts(synthetic.options.sessionId).Any(row=>Convert.ToString(row["itemId"])==continued.id),"a continued task registers its later artifacts in both the project and durable session index");
  int continuedOps=operationStore.List().Count,continuedEntries=service.SessionStore.Entries(synthetic.options.sessionId).Count;service.WriteProjectArtifacts(synthetic);service.WriteSessionArtifacts(synthetic);
  check(operationStore.List().Count==continuedOps&&service.SessionStore.Entries(synthetic.options.sessionId).Count==continuedEntries&&service.SessionStore.Artifacts(synthetic.options.sessionId).Count==3,"repeated terminal callbacks dedupe artifacts after continuation without extra operations or session entries");
  string copyPath=Path.Combine(root,"delivery-copy.md");
  byte[] copyBytes=Encoding.UTF8.GetBytes("Exported deliverable");
  File.WriteAllBytes(copyPath,copyBytes);
  library.Import(new[]{copyPath},null);
  var copy=library.Db.items.Single(item=>item.path==copyPath);
  check(library.TryRecordProjectDelivery(deliverable,copy,copyBytes),"a successful export of a linked project artifact records a receipt");
  List<Dictionary<string,object>> Deliveries(Library target,string id)=>Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(target.ReadProjectDeliveries(id)));
  var restarted=new Library(library.DataDir);
  var delivery=Deliveries(restarted,linkProjectId).Single();
  check(Convert.ToString(delivery["path"])==copyPath&&Convert.ToString(delivery["exportId"])==copy.id&&Convert.ToBoolean(delivery["available"])&&Deliveries(restarted,writeProjectId).Count==0,"delivery history survives reload and is scoped to the project's artifacts");
  string privateFolder=Path.Combine(root,"delivery-private");
  using(var vault=VaultSession.Create(privateFolder,"delivery-test-password")){
   var privateLibrary=new Library(privateFolder,vault);
   restarted.TransferToPrivate(privateLibrary,new[]{copy.id});
   check(Deliveries(restarted,linkProjectId).Count==0&&File.Exists(copyPath),"moving a delivered copy into the private folder removes its ordinary project receipt without deleting the exported file");
  }
 }
}
