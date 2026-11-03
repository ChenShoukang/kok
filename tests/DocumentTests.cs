using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading;
using System.IO.Compression;
using System.Collections.Generic;
using kok;

class DocumentTests {
    public static void Run(string root,Action<bool,string> check){
        var library=new Library(root);var md=library.CreateDocument("学习笔记","md","学习");check(md.ownedDocument&&md.kind=="document"&&md.collection=="学习"&&md.ext=="MD"&&File.Exists(md.path),"new Markdown creates a managed document in its collection");
        string text="# 标题\n\n**重点** 与 *想法*\n- 项目\n> 引用\n";var source=library.DocumentSource(md.id);var saved=library.SaveDocument(md.id,text,source.signature,null);check(File.ReadAllText(md.path,Encoding.UTF8)==text&&library.DocumentSource(md.id).text==text&&File.Exists(md.path+".bak"),"Markdown saves UTF-8 source exactly with an atomic previous-version backup");
        var duplicate=library.CreateDocument("学习笔记.md","md","学习");check(duplicate.path!=md.path&&duplicate.name=="学习笔记 (2)"&&File.ReadAllText(md.path)==text,"duplicate creation never overwrites the first document");
        int before=library.Db.items.Count;foreach(string name in new[]{"../escape","CON","NUL.txt","","bad:name",new string('x',101)}){bool rejected=false;try{library.CreateDocument(name,"md","");}catch(InvalidDataException){rejected=true;}check(rejected,"invalid document name is rejected: "+name.Substring(0,Math.Min(name.Length,20)));}check(library.Db.items.Count==before,"failed creation leaves no library entries");
        bool badFormat=false;try{library.CreateDocument("unsupported","html","");}catch(InvalidDataException){badFormat=true;}check(badFormat,"only supported creation formats are accepted");
        var docx=library.CreateDocument("中文 Word","docx","");library.SaveDocument(docx.id,text,library.DocumentSource(docx.id).signature,null);using(var zip=ZipFile.OpenRead(docx.path))using(var reader=new StreamReader(zip.GetEntry("word/document.xml").Open())){var xml=XDocument.Parse(reader.ReadToEnd());XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";check(zip.GetEntry("[Content_Types].xml")!=null&&xml.Descendants(w+"b").Any()&&xml.Descendants(w+"pStyle").Any(p=>(string)p.Attribute(w+"val")=="Heading1")&&xml.Root.Value.Contains("重点"),"DOCX is a real OpenXML package with Unicode headings and bold text");}
        var reload=new Library(root);check(reload.Find(docx.id).ownedDocument&&reload.DocumentSource(docx.id).text==text,"DOCX editing source and managed ownership survive reload");
        bool badRecipe=false;try{library.SaveDocument(md.id,"invalid replacement",saved.signature,new Dictionary<string,object>{{"fontSize",900}});}catch(InvalidDataException){badRecipe=true;}check(badRecipe&&File.ReadAllText(md.path)==text,"invalid save settings fail before changing the file");
        File.WriteAllText(md.path,"external edit",new UTF8Encoding(false));bool conflict=false;try{library.SaveDocument(md.id,"local edit",saved.signature,null);}catch(InvalidOperationException){conflict=true;}check(conflict&&File.ReadAllText(md.path)=="external edit","external modifications are not overwritten by stale saves");
        library.SaveDocumentSession(md.id,new Dictionary<string,object>{{"kind","text"},{"text","recover this"},{"signature",saved.signature},{"columns",1},{"fontSize",17}});var recovered=new Library(root).DocumentSource(md.id);check(recovered.recovery&&recovered.text=="recover this"&&File.ReadAllText(md.path)=="external edit","closing during a conflict preserves a recoverable local draft without replacing the file");
        var txt=library.CreateDocument("Plain","txt","");library.SaveDocument(txt.id,"plain\n文字",library.DocumentSource(txt.id).signature,null);check(File.ReadAllText(txt.path)=="plain\n文字","plain-text creation and direct save work offline");
        string imported=Path.Combine(root,"imported.md");File.WriteAllText(imported,"keep imported");library.Import(new[]{imported},null);var entry=library.Db.items.First(e=>e.path==imported);bool protectedFile=false;try{library.SaveDocument(entry.id,"overwrite","",null);}catch(InvalidDataException){protectedFile=true;}check(protectedFile&&File.ReadAllText(imported)=="keep imported","direct save cannot overwrite imported originals");
        AgentBodyEdits(library,root,check);
        VersionHistory(Path.Combine(root,"version-history"),check);
        DocumentVersionApi(Path.Combine(root,"version-actions"),check);
        VersionLabelCases(Path.Combine(root,"version-labels"),check);
    }
    static bool Blocked(Action action){try{action();return false;}catch(InvalidDataException){return true;}catch(InvalidOperationException){return true;}catch(FormatException){return true;}}
    static void AgentBodyEdits(Library library,string root,Action<bool,string> check){
        library.SetAppMode("smart",true);var token=CancellationToken.None;
        string original="# 小说\n\n第一章：保留手动文字。\n第二章：原来的场景。\n第三章：仍需保留。";
        var state=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{budget=14000}};
        Entry docxDocument=null;
        foreach(string format in new[]{"md","txt","docx"}){
            var document=library.CreateDocument("正文修改 "+format,format,"",original);state.options.allowed.Add(document.id);
            Func<string,string,AgentCall> prepare=(oldText,newText)=>{
                var call=library.PrepareAgentCall(state,new AgentAction{tool="replace_document_text",args=new Dictionary<string,object>{{"id",document.id},{"old_text",oldText},{"new_text",newText}}},"turn",null);
                call.sequence=state.calls.Count+1;state.calls.Add(call);return call;
            };
            var first=prepare("第二章：原来的场景。","第二章：新的悬念。");
            var second=prepare("第三章：仍需保留。","第三章：新的结尾。");
            check(library.DocumentSource(document.id).text==original&&first.beforeText=="第二章：原来的场景。"&&first.afterText=="第二章：新的悬念。","body edit is only a preview until approval: "+format);
            var done=library.WriteAgentTool(state,first,token);first.status="done";first.result=done.result;first.stamp=done.stamp;
            var doneSecond=library.WriteAgentTool(state,second,token);second.status="done";second.result=doneSecond.result;second.stamp=doneSecond.stamp;
            string both=original.Replace("第二章：原来的场景。","第二章：新的悬念。").Replace("第三章：仍需保留。","第三章：新的结尾。");
            check(library.DocumentSource(document.id).text==both&&library.Find(document.id).path==document.path&&library.Db.items.Count(item=>item.id==document.id)==1,"approved fragment edits preserve other chapters, identity and CRLF: "+format);
            check(Convert.ToString(doneSecond.result["documentBeforeStamp"])==Convert.ToString(done.stamp)&&Convert.ToBoolean(done.result["readBackVerified"])&&Convert.ToBoolean(doneSecond.result["readBackVerified"]),"same-task approved edits have a continuous receipt chain: "+format);
            library.WriteAgentTool(state,second,token);
            check(library.DocumentSource(document.id).text==both&&library.OperationList().Count(operation=>operation.id==second.id)==1,"the same body operation is not replayed: "+format);
            library.OperationUndo(second.id);var afterUndo=library.DocumentSource(document.id).text;
            check(afterUndo.Contains("第二章：新的悬念。")&&afterUndo.Contains("第三章：仍需保留。")&&!afterUndo.Contains("第三章：新的结尾。"),"body undo survives library reload and restores only the latest edit: "+format);
            library.OperationUndo(first.id);
            check(library.DocumentSource(document.id).text==original&&File.Exists(document.path),"body undo keeps the document and restores its prior content: "+format);
            if(format=="docx")docxDocument=document;
        }
        using(var zip=ZipFile.OpenRead(docxDocument.path))check(docxDocument.ext=="DOCX"&&zip.GetEntry("word/document.xml")!=null,"body editing retains a real DOCX package");
        var doc=library.CreateDocument("正文边界","md","","唯一片段\n重复\n重复\n😀结束");state.options.allowed.Add(doc.id);
        Func<string,string,AgentCall> boundary=(oldText,newText)=>{
            var call=library.PrepareAgentCall(state,new AgentAction{tool="replace_document_text",args=new Dictionary<string,object>{{"id",doc.id},{"old_text",oldText},{"new_text",newText}}},"turn",null);
            call.sequence=state.calls.Count+1;state.calls.Add(call);return call;
        };
        check(Blocked(()=>boundary("重复","x"))&&Blocked(()=>boundary("不存在","x")),"missing and ambiguous source fragments never become body edits");
        var stale=boundary("唯一片段","Agent 修改");
        File.AppendAllText(doc.path,"\nMANUAL_CHANGE",new UTF8Encoding(false));
        check(Blocked(()=>library.WriteAgentTool(state,stale,token))&&File.ReadAllText(doc.path).Contains("MANUAL_CHANGE")&&!File.ReadAllText(doc.path).Contains("Agent 修改"),"a stale body approval preserves intervening manual file edits");
        var deletion=boundary("唯一片段","");library.WriteAgentTool(state,deletion,token);
        check(library.DocumentSource(doc.id).text.StartsWith("\n重复")&&library.DocumentSource(doc.id).text.Contains("MANUAL_CHANGE"),"empty replacement deletes only the chosen fragment");
        var read=library.PrepareAgentCall(state,new AgentAction{tool="read_document",args=new Dictionary<string,object>{{"id",doc.id},{"offset",0},{"max_chars",8}}},"read",null);var result=library.ReadAgentTool(state,read,token).GetAwaiter().GetResult();var window=Library.Json.Deserialize<Dictionary<string,object>>(Convert.ToString(result["text"]));
        check(Convert.ToString(window["text"])==library.DocumentSource(doc.id).text.Substring(0,7)&&Convert.ToInt32(window["next_offset"])==7,"exact source reads return bounded windows without splitting a Unicode pair");
        var importedItem=library.Db.items.First(item=>!item.ownedDocument&&item.path.EndsWith("imported.md"));state.options.allowed.Add(importedItem.id);
        check(Blocked(()=>library.PrepareAgentCall(state,new AgentAction{tool="replace_document_text",args=new Dictionary<string,object>{{"id",importedItem.id},{"old_text","keep imported"},{"new_text","replace"}}},"turn",null))&&File.ReadAllText(importedItem.path)=="keep imported","Agent body editing cannot overwrite imported originals");
        check(Blocked(()=>AgentRules.ValidateAction(new AgentAction{tool="replace_document_text",args=new Dictionary<string,object>{{"id",Guid.NewGuid().ToString("N")},{"old_text","a"},{"new_text","b"}}},id=>false)),"body edit tools reject IDs outside the task scope");
        library.SetAppMode("pure",false);check(Blocked(()=>library.ReadAgentTool(state,read,token).GetAwaiter().GetResult()),"Pure mode blocks Agent source reads");
    }
    static void VersionHistory(string dataDir,Action<bool,string> check){
        var library=new Library(dataDir);
        var doc=library.CreateDocument("版本笔记","md","","V1 内容");
        library.SaveDocument(doc.id,"V2 内容",library.DocumentSource(doc.id).signature,null);var op1=library.Db.operations.Last().id;
        library.SaveDocument(doc.id,"V3 内容",library.DocumentSource(doc.id).signature,null);var op2=library.Db.operations.Last().id;
        library.SaveDocument(doc.id,"V4 内容",library.DocumentSource(doc.id).signature,null);var op3=library.Db.operations.Last().id;
        var versions=library.ListVersions(doc.id);
        var rows=versions.Select(row=>Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(row))).ToList();
        var ids=rows.Select(row=>Convert.ToString(row["operationId"])).ToList();
        check(rows.Count==4&&rows.Count(row=>Convert.ToBoolean(row["current"]))==1&&Convert.ToBoolean(rows[0]["current"])&&Convert.ToString(rows[0]["title"])=="当前版本"&&ids.Skip(1).SequenceEqual(new[]{op3,op2,op1})&&rows.Skip(1).All(row=>Convert.ToBoolean(row["available"])),"version list is newest-first from accepted saves with one synthetic current entry");
        var middle=library.ReadVersion(doc.id,op2);
        check(Convert.ToString(middle["itemId"])==doc.id&&Convert.ToString(middle["operationId"])==op2&&Convert.ToString(middle["beforeText"])=="V2 内容"&&Convert.ToString(middle["afterText"])=="V3 内容"&&Convert.ToString(middle["currentText"])=="V4 内容"&&Convert.ToBoolean(middle["textAvailable"])&&Convert.ToString(middle["beforeHash"]).StartsWith("sha256:")&&Convert.ToString(middle["afterHash"]).StartsWith("sha256:")&&Convert.ToInt32(middle["size"])>0,"version read returns before, after and current text plus hashes for a text document");
        var oldest=library.ReadVersion(doc.id,op1);
        check(Convert.ToString(oldest["beforeText"])=="V1 内容"&&Convert.ToString(oldest["afterText"])=="V2 内容","the after text of a version is the next version's before content");
        var restored=library.RestoreVersion(doc.id,op1);string compensation=Convert.ToString(restored["operationId"]);
        var compensationOp=library.Db.operations.FirstOrDefault(op=>op.id==compensation);
        check(AgentJournal.ValidId(compensation)&&Convert.ToString(restored["restoredFrom"])==op1&&File.ReadAllText(doc.path,Encoding.UTF8)=="V1 内容"&&library.DocumentSource(doc.id).text=="V1 内容"&&compensationOp!=null&&compensationOp.title=="恢复版本"&&compensationOp.state=="applied"&&compensationOp.fileVersion==compensationOp.id,"restoring an old version writes its bytes through a compensating operation");
        var afterRestore=library.ListVersions(doc.id).Select(row=>Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(row))).ToList();
        check(afterRestore.Count==5&&Convert.ToInt32(restored["size"])==Encoding.UTF8.GetByteCount("V1 内容"),"the restore is listed as a new version with the restored size");
        library.OperationUndo(compensation);
        check(library.DocumentSource(doc.id).text=="V4 内容"&&File.ReadAllText(doc.path,Encoding.UTF8)=="V4 内容","undoing the restore operation brings back the previously current content");
        string sealedPath=Path.Combine(library.DataDir,"versions",op2+".sealed");File.Delete(sealedPath);
        var unavailable=library.ListVersions(doc.id).Select(row=>Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(row))).ToList();
        var missingRow=unavailable.FirstOrDefault(row=>Convert.ToString(row["operationId"])==op2);
        check(missingRow!=null&&!Convert.ToBoolean(missingRow["available"])&&Blocked(()=>library.ReadVersion(doc.id,op2)),"a missing sealed version is reported unavailable instead of failing the whole list");
        check(Blocked(()=>library.ListVersions("../escape"))&&Blocked(()=>library.ListVersions(Guid.NewGuid().ToString("N")))&&Blocked(()=>library.ReadVersion(doc.id,"../escape"))&&Blocked(()=>library.ReadVersion(doc.id,Guid.NewGuid().ToString("N")))&&Blocked(()=>library.RestoreVersion(doc.id,Guid.NewGuid().ToString("N"))),"invalid item, operation and traversal ids are rejected");
        var txt=library.CreateDocument("版本文本","txt","","T1 内容");
        library.SaveDocument(txt.id,"T2 内容",library.DocumentSource(txt.id).signature,null);
        string txtVersion=library.Db.operations.Last().id;
        var txtList=library.ListVersions(txt.id).Select(row=>Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(row))).ToList();
        var txtRestore=library.RestoreVersion(txt.id,txtVersion);
        check(txtList.Count==2&&File.ReadAllText(txt.path,Encoding.UTF8)=="T1 内容","plain-text versions list and restore with exact bytes");
        library.OperationUndo(Convert.ToString(txtRestore["operationId"]));
        check(File.ReadAllText(txt.path,Encoding.UTF8)=="T2 内容","plain-text restore undo returns the previous content");
        var docx=library.CreateDocument("版本文档","docx","","Word 一");
        library.SaveDocument(docx.id,"Word 二",library.DocumentSource(docx.id).signature,null);
        var docxRead=library.ReadVersion(docx.id,library.Db.operations.Last().id);
        check(Convert.ToBoolean(docxRead["textAvailable"])&&Convert.ToString(docxRead["beforeText"]).Contains("Word 一")&&Convert.ToString(docxRead["afterText"]).Contains("Word 二"),"DOCX versions expose plain text through the existing document reader");
    }
    static Dictionary<string,object> Payload(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static List<Dictionary<string,object>> PayloadRows(object value){return Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(value));}
    static bool SchemaAccepts(string name,Dictionary<string,object> args){try{CapabilitySchema.Check(ActionSchemas.For(name),args);return true;}catch(InvalidDataException){return false;}}
    static void DocumentVersionApi(string dataDir,Action<bool,string> check){
        var library=new Library(dataDir);
        var doc=library.CreateDocument("操作版本","md","","A1 内容");
        library.SaveDocument(doc.id,"A2 内容",library.DocumentSource(doc.id).signature,null);var op1=library.Db.operations.Last().id;
        library.SaveDocument(doc.id,"A3 内容",library.DocumentSource(doc.id).signature,null);var op2=library.Db.operations.Last().id;
        var context=new ModuleContext(library,null,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
        Func<string,Dictionary<string,object>,Dictionary<string,object>> call=(name,args)=>Payload(NativeDocuments.Handle(context,name,args).GetAwaiter().GetResult());
        var list=call("documentVersions",new Dictionary<string,object>{{"id",doc.id}});
        var listed=PayloadRows(list["versions"]);
        check(listed.Count==3&&Convert.ToBoolean(listed[0]["current"])&&listed.Count(row=>!Convert.ToBoolean(row["current"]))==2&&Convert.ToString(listed[1]["operationId"])==op2,"the documents module lists the current entry plus every saved version newest-first");
        var read=call("documentVersion",new Dictionary<string,object>{{"id",doc.id},{"operationId",op1}});
        check(Convert.ToString(read["beforeText"])=="A1 内容"&&Convert.ToString(read["afterText"])=="A2 内容"&&Convert.ToString(read["currentText"])=="A3 内容"&&Convert.ToBoolean(read["textAvailable"]),"documentVersion returns before, after and current text for one operation");
        var current=call("documentVersion",new Dictionary<string,object>{{"id",doc.id},{"operationId","current"}});
        check(Convert.ToBoolean(current["current"])&&Convert.ToString(current["operationId"])=="current"&&Convert.ToString(current["currentText"])=="A3 内容"&&Convert.ToBoolean(current["textAvailable"]),"documentVersion accepts the current sentinel and returns the live document text");
        var restored=call("documentVersionRestore",new Dictionary<string,object>{{"id",doc.id},{"operationId",op1}});
        check(Convert.ToString(restored["restoredFrom"])==op1&&Convert.ToString(restored["currentText"])=="A1 内容"&&Convert.ToBoolean(restored["textAvailable"])&&File.ReadAllText(doc.path,Encoding.UTF8)=="A1 内容"&&library.ListVersions(doc.id).Count==4,"documentVersionRestore restores the old content and returns the new current text");
        check(Blocked(()=>call("documentVersion",new Dictionary<string,object>{{"id",doc.id},{"operationId",Guid.NewGuid().ToString("N")}}))&&Blocked(()=>call("documentVersionRestore",new Dictionary<string,object>{{"id",doc.id},{"operationId","current"}}))&&Blocked(()=>call("documentVersions",new Dictionary<string,object>{{"id","../escape"}}))&&Blocked(()=>call("documentVersion",new Dictionary<string,object>{{"id",doc.id},{"operationId","../escape"}})),"unknown or traversal version ids are rejected and restore refuses the current sentinel");
        var documentsModule=ModuleRegistry.Builtins.Modules.Single(module=>module.Name=="documents");var names=documentsModule.Actions.Select(action=>action.Name).ToList();
        var listSpec=documentsModule.Actions.Single(action=>action.Name=="documentVersions");var restoreSpec=documentsModule.Actions.Single(action=>action.Name=="documentVersionRestore");
        bool schemas=SchemaAccepts("documentVersions",new Dictionary<string,object>{{"id",doc.id}})&&SchemaAccepts("documentVersion",new Dictionary<string,object>{{"id",doc.id},{"operationId",op1}})&&SchemaAccepts("documentVersionRestore",new Dictionary<string,object>{{"id",doc.id},{"operationId",op1}})&&Blocked(()=>CapabilitySchema.Check(ActionSchemas.For("documentVersions"),new Dictionary<string,object>()))&&Blocked(()=>CapabilitySchema.Check(ActionSchemas.For("documentVersion"),new Dictionary<string,object>{{"id",doc.id}}));
        check(names.Contains("documentVersions")&&names.Contains("documentVersion")&&names.Contains("documentVersionRestore")&&listSpec.Title=="版本记录"&&restoreSpec.Title=="恢复版本"&&restoreSpec.Writes.Value&&!listSpec.Writes.Value&&restoreSpec.Requires.Value==Need.NormalLibrary&&listSpec.Requires.Value==Need.NormalLibrary&&schemas,"the documents catalog exposes the three version actions with titles, write classification and exact schemas");
        string privateRoot=Path.Combine(dataDir,"private-versions");
        using(var vault=VaultSession.Create(privateRoot,"OnlyForVersionApi928!")){
            var secret=new Library(privateRoot,vault);
            var privateContext=new ModuleContext(secret,null,new ModulePolicy(secret),null,CancellationToken.None,null,new Operations(secret,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
            string message="";
            try{NativeDocuments.Handle(privateContext,"documentVersions",new Dictionary<string,object>{{"id",Guid.NewGuid().ToString("N")}}).GetAwaiter().GetResult();}catch(InvalidDataException error){message=error.Message;}
            check(message.Contains("私密文件夹不保存版本"),"a private vault still rejects version history with the unchanged private message");
        }
    }
    // P3-C: durable named checkpoints (version labels) stored in a self-contained sealed file.
    static void VersionLabelCases(string dataDir,Action<bool,string> check){
        var library=new Library(dataDir);library.SetAppMode("smart",true);
        var doc=library.CreateDocument("命名检查点文档","md","","L1 内容");
        library.SaveDocument(doc.id,"L2 内容",library.DocumentSource(doc.id).signature,null);string first=library.Db.operations.Last().id;
        library.SaveDocument(doc.id,"L3 内容",library.DocumentSource(doc.id).signature,null);string second=library.Db.operations.Last().id;
        var labeled=library.LabelVersion(doc.id,first,"  初稿  ");
        var rows=PayloadRows(library.ListVersions(doc.id));
        bool listed=rows.Any(row=>Convert.ToString(row["operationId"])==first&&Convert.ToString(row["label"])=="初稿")&&rows.All(row=>row.ContainsKey("label"))&&rows.Where(row=>Convert.ToString(row["operationId"])!=first).All(row=>Convert.ToString(row["label"])=="");
        var reloaded=new Library(dataDir);var reloadRows=PayloadRows(reloaded.ListVersions(doc.id));
        check(Convert.ToString(labeled["label"])=="初稿"&&listed&&reloadRows.Any(row=>Convert.ToString(row["operationId"])==first&&Convert.ToString(row["label"])=="初稿"),"version labels roundtrip through the sealed label store and appear in ListVersions after reload");
        reloaded.LabelVersion(doc.id,first,"定稿");reloaded.LabelVersion(doc.id,second,"第二版");
        var relabelRows=PayloadRows(reloaded.ListVersions(doc.id));
        var unlabeled=reloaded.UnlabelVersion(doc.id,first);var afterUnlabel=PayloadRows(reloaded.ListVersions(doc.id));
        var unknownUnlabel=reloaded.UnlabelVersion(doc.id,Guid.NewGuid().ToString("N"));
        check(relabelRows.Any(row=>Convert.ToString(row["operationId"])==first&&Convert.ToString(row["label"])=="定稿")&&relabelRows.Any(row=>Convert.ToString(row["operationId"])==second&&Convert.ToString(row["label"])=="第二版")&&Convert.ToString(unlabeled["label"])==""&&afterUnlabel.All(row=>Convert.ToString(row["operationId"])!=first||Convert.ToString(row["label"])=="")&&Convert.ToString(unknownUnlabel["label"])=="","labels can be replaced and removed, and removing an unknown label is a no-op");
        bool currentLabel=Blocked(()=>library.LabelVersion(doc.id,"current","x"));
        bool unknownVersion=Blocked(()=>library.LabelVersion(doc.id,Guid.NewGuid().ToString("N"),"x"));
        bool badIds=Blocked(()=>library.LabelVersion("../escape",first,"x"))&&Blocked(()=>library.LabelVersion(doc.id,"../escape","x"));
        bool badLabels=Blocked(()=>library.LabelVersion(doc.id,first,""))&&Blocked(()=>library.LabelVersion(doc.id,first,new string('x',65)))&&Blocked(()=>library.LabelVersion(doc.id,first,"bad\nlabel"))&&Blocked(()=>library.LabelVersion(doc.id,first,"bad\u0001"));
        bool currentUnlabel=Blocked(()=>library.UnlabelVersion(doc.id,"current"));
        check(currentLabel&&unknownVersion&&badIds&&badLabels&&currentUnlabel,"version labels reject the current sentinel, unknown versions, malformed ids and invalid label text");
        var context=new ModuleContext(library,null,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
        var documentsModule=ModuleRegistry.Builtins.Modules.Single(module=>module.Name=="documents");
        var labelSpec=documentsModule.Actions.Single(action=>action.Name=="documentVersionLabel");var unlabelSpec=documentsModule.Actions.Single(action=>action.Name=="documentVersionUnlabel");
        var dispatched=Payload(ModuleRegistry.Builtins.Dispatch(context,"documentVersionLabel",new Dictionary<string,object>{{"id",doc.id},{"operationId",second},{"label","发布稿"}}).GetAwaiter().GetResult());
        var afterDispatch=PayloadRows(library.ListVersions(doc.id));
        var dispatchedUnlabel=Payload(ModuleRegistry.Builtins.Dispatch(context,"documentVersionUnlabel",new Dictionary<string,object>{{"id",doc.id},{"operationId",second}}).GetAwaiter().GetResult());
        var afterDispatchUnlabel=PayloadRows(library.ListVersions(doc.id));
        bool schemaOk=SchemaAccepts("documentVersionLabel",new Dictionary<string,object>{{"id",doc.id},{"operationId",first},{"label","检查点"}})&&SchemaAccepts("documentVersionUnlabel",new Dictionary<string,object>{{"id",doc.id},{"operationId",first}})&&!SchemaAccepts("documentVersionLabel",new Dictionary<string,object>{{"id",doc.id},{"operationId",first},{"label",new string('y',65)}});
        check(labelSpec.Writes.Value&&unlabelSpec.Writes.Value&&labelSpec.Requires.Value==Need.NormalLibrary&&labelSpec.Title=="命名检查点"&&unlabelSpec.Title=="移除检查点"&&Convert.ToString(dispatched["label"])=="发布稿"&&afterDispatch.Any(row=>Convert.ToString(row["operationId"])==second&&Convert.ToString(row["label"])=="发布稿")&&Convert.ToString(dispatchedUnlabel["label"])==""&&afterDispatchUnlabel.All(row=>Convert.ToString(row["operationId"])!=second||Convert.ToString(row["label"])=="")&&schemaOk,"documentVersionLabel and documentVersionUnlabel are registered write version actions with bounded schemas and dispatch through the documents module");
        string corruptDir=Path.Combine(dataDir,"corrupt");Directory.CreateDirectory(corruptDir);
        var corruptLibrary=new Library(corruptDir);var corruptDoc=corruptLibrary.CreateDocument("损坏测试","md","","C1");
        corruptLibrary.SaveDocument(corruptDoc.id,"C2",corruptLibrary.DocumentSource(corruptDoc.id).signature,null);string corruptOp=corruptLibrary.Db.operations.Last().id;
        File.WriteAllBytes(Path.Combine(corruptDir,"version-labels.sealed"),new byte[]{1,2,3,4,5});
        var corruptRows=PayloadRows(corruptLibrary.ListVersions(corruptDoc.id));
        var afterCorrupt=corruptLibrary.LabelVersion(corruptDoc.id,corruptOp,"恢复标签");
        check(corruptRows.Count>=2&&corruptRows.All(row=>Convert.ToString(row["label"])=="")&&Convert.ToString(afterCorrupt["label"])=="恢复标签"&&new FileInfo(Path.Combine(corruptDir,"version-labels.sealed")).Length>5,"a corrupt label file is tolerated as empty and replaced by the next successful label");
        string labelsPath=Path.Combine(dataDir,"version-labels.sealed");
        library.LabelVersion(doc.id,first,"已记录检查点");bool failedLabel=false,failedUnlabel=false;
        using(var held=new FileStream(labelsPath,FileMode.Open,FileAccess.Read,FileShare.Read)){
            try{library.LabelVersion(doc.id,first,"不应保留的名称");}catch(IOException){failedLabel=true;}
            try{library.UnlabelVersion(doc.id,first);}catch(IOException){failedUnlabel=true;}
        }
        check(failedLabel&&failedUnlabel&&PayloadRows(library.ListVersions(doc.id)).Any(row=>Convert.ToString(row["operationId"])==first&&Convert.ToString(row["label"])=="已记录检查点")&&PayloadRows(new Library(dataDir).ListVersions(doc.id)).Any(row=>Convert.ToString(row["operationId"])==first&&Convert.ToString(row["label"])=="已记录检查点"),"failed label writes and removals preserve both cached and persisted checkpoint names");
        check(File.ReadAllBytes(Path.Combine(corruptDir,"version-labels.sealed.bak")).SequenceEqual(new byte[]{1,2,3,4,5}),"repairing checkpoint metadata retains the corrupt original in its backup");
        string privateRoot=Path.Combine(dataDir,"private-labels");
        var many=new Library(Path.Combine(dataDir,"many-saves"));var milestone=many.CreateDocument("长期创作","md","","第一稿");
        many.SaveDocument(milestone.id,"第 2 稿",many.DocumentSource(milestone.id).signature,null);string pinned=many.Db.operations.Last().id;many.LabelVersion(milestone.id,pinned,"第一稿检查点");
        for(int n=3;n<=105;n++)many.SaveDocument(milestone.id,"第 "+n+" 稿",many.DocumentSource(milestone.id).signature,null);
        var visible=PayloadRows(many.ListVersions(milestone.id));
        check(visible.Count==Library.MaxVersionEntries+1&&visible.Any(row=>Convert.ToString(row["operationId"])==pinned&&Convert.ToString(row["label"])=="第一稿检查点"),"named checkpoints stay visible across more than 100 saves while the version panel remains bounded");
        many.RestoreVersion(milestone.id,pinned);check(many.DocumentSource(milestone.id).text=="第一稿","a pinned checkpoint remains restorable after many later saves");
        using(var vault=VaultSession.Create(privateRoot,"OnlyForVersionLabels928!")){
            var secret=new Library(privateRoot,vault);string labelMessage="",unlabelMessage="";
            try{secret.LabelVersion(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),"x");}catch(InvalidDataException error){labelMessage=error.Message;}
            try{secret.UnlabelVersion(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));}catch(InvalidDataException error){unlabelMessage=error.Message;}
            check(labelMessage.Contains("私密文件夹不保存版本")&&unlabelMessage.Contains("私密文件夹不保存版本"),"version labels stay unavailable in a private vault with the unchanged private message");
        }
    }
}
