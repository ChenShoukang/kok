using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    public partial class Library {
        static object CaptionSources(Entry e){if(e.kind!="video"&&e.kind!="music")return null;try{string stem=Path.GetFileNameWithoutExtension(e.path);return Directory.GetFiles(Path.GetDirectoryName(e.path)).Where(file=>new[]{".srt",".vtt",".ass",".ssa",".sub",".sbv",".lrc",".ttml",".dfxp",".smi"}.Contains(Path.GetExtension(file).ToLowerInvariant())&&(Path.GetFileNameWithoutExtension(file).Equals(stem,StringComparison.OrdinalIgnoreCase)||Path.GetFileNameWithoutExtension(file).StartsWith(stem+".",StringComparison.OrdinalIgnoreCase))).OrderBy(file=>file,StringComparer.OrdinalIgnoreCase).Take(20).Select(file=>{var info=new FileInfo(file);return new{name=info.Name,size=info.Length,ticks=info.LastWriteTimeUtc.Ticks};}).ToArray();}catch{return "unavailable";}}
        public Dictionary<string,string> AgentSourceStamps(IEnumerable<string> ids){lock(gate){var result=new Dictionary<string,string>();foreach(var id in ids.Distinct()){var e=Find(id);if(e==null)continue;var f=new FileInfo(e.path);result[id]=StudyHash(new{id=e.id,modified=f.Exists?f.LastWriteTimeUtc.Ticks:0,size=f.Exists?f.Length:0,extraction=e.ext=="PDF"||e.kind=="photo"?store.ContentRevision(e.id):"",metadata=SearchContent.Metadata(e),notes=e.notes,draft=e.editDraft,chapters=e.chapterTexts,annotations=e.annotations,generated=e.generatedSubtitles,sidecars=CaptionSources(e)});}return result;}}
        public AgentCall PrepareAgentCall(AgentTaskState state,AgentAction action,string turn,string provider){
            if(!ModuleRegistry.Builtins.ToolEnabled(action.tool,this))throw new InvalidOperationException("此模块已停用，请在设置中启用。");
            AgentRules.ValidateAction(action,id=>state.options.allowed.Contains(id)&&AgentHas(id));var c=new AgentCall{id=Guid.NewGuid().ToString("N"),providerId=provider,turnId=turn,tool=action.tool,args=action.args,why=action.why,status=AgentRules.ReadTools.Contains(action.tool)?"queued":"pending"};
            lock(gate){
                var ids=action.args.ContainsKey("ids")?AgentTaskState.Get<List<string>>(action.args,"ids"):action.args.ContainsKey("id")?new List<string>{Convert.ToString(action.args["id"])}:new List<string>();c.sources=AgentSourceStamps(ids);
                if(c.tool=="write_note"){var e=Find(ids[0]);c.beforeText=e.notes??"";string text=Convert.ToString(c.args["text"]),mode=c.args.ContainsKey("mode")?Convert.ToString(c.args["mode"]):"append";c.afterText=mode=="replace"?text:(c.beforeText.Length>0?c.beforeText.TrimEnd()+"\n\n":"")+text;c.changes=new List<AgentChange>{new AgentChange{id=e.id,before=new Dictionary<string,object>{{"notes",c.beforeText}},after=new Dictionary<string,object>{{"notes",c.afterText}}}};}
                if(c.tool=="set_collection"||c.tool=="add_tags"){c.changes=new List<AgentChange>();foreach(var id in ids){var e=Find(id);string key=c.tool=="set_collection"?"collection":"tags";object before=key=="collection"?(object)(e.collection??""):(object)(e.tags??new List<string>()).ToList(),after=key=="collection"?c.args["collection"]:(object)(e.tags??new List<string>()).Concat(AgentTaskState.Get<List<string>>(c.args,"tags")).Distinct().Take(40).ToList();c.changes.Add(new AgentChange{id=id,before=new Dictionary<string,object>{{key,before}},after=new Dictionary<string,object>{{key,after}}});}}
            }return c;
        }
        public Dictionary<string,object> AgentReceipt(string operation){lock(gate){var e=Db.items.FirstOrDefault(x=>x.agentOperationId==operation);if(e!=null)return new Dictionary<string,object>{{"id",e.id},{"name",e.name},{"text","Created "+e.name+" ("+e.id+")"}};
            if(OperationStatus(operation)=="applied")return new Dictionary<string,object>{{"text","Changes applied"}};
            if(Db.settings.ContainsKey("agentToolReceipts")){var rows=Json.Deserialize<Dictionary<string,Dictionary<string,object>>>(Json.Serialize(Db.settings["agentToolReceipts"]));Dictionary<string,object> r;if(rows.TryGetValue(operation,out r))return r;}return null;}}
        public async Task<Dictionary<string,object>> ReadAgentTool(AgentTaskState state,AgentCall call,CancellationToken token){
            if(!ModuleRegistry.Builtins.ToolEnabled(call.tool,this))throw new InvalidOperationException("此模块已停用，请在设置中启用。");
            RequireAiMode();AgentRules.ValidateAction(new AgentAction{tool=call.tool,args=call.args},id=>state.options.allowed.Contains(id)&&AgentHas(id));token.ThrowIfCancellationRequested();
            var spec=AgentRules.Tools.First(t=>t.Name==call.tool);if(spec.Read!=null)return await spec.Read(this,state,call,token);
            if(call.tool=="list_collections")return new Dictionary<string,object>{{"text","Collections: "+String.Join(", ",state.options.allowed.Select(Find).Where(e=>e!=null).Select(e=>e.collection).Distinct())}};
            if(call.tool=="search_library")return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(await AgentSearchCurrent(call.args,state.options.allowed,token)));
            var ids=AgentTaskState.Get<List<string>>(call.args,"ids");string question=call.args.ContainsKey("question")?Convert.ToString(call.args["question"]):"";
            var result=await Task.Run(()=>BuildKnowledgeContext(ids,"summarize",question,state.options.budget),token);token.ThrowIfCancellationRequested();RequireAiMode();return new Dictionary<string,object>{{"text",result.context},{"references",result.references},{"skipped",result.skipped}};
        }
        public AgentToolReceipt WriteAgentTool(AgentTaskState state,AgentCall call,CancellationToken token,Operations operations=null){
            if(!ModuleRegistry.Builtins.ToolEnabled(call.tool,this))throw new InvalidOperationException("此模块已停用，请在设置中启用。");
            RequireAiMode();token.ThrowIfCancellationRequested();AgentRules.ValidateAction(new AgentAction{tool=call.tool,args=call.args},id=>state.options.allowed.Contains(id)&&AgentHas(id));
            var previous=AgentReceipt(call.id);if(previous!=null)return new AgentToolReceipt{result=previous,stamp=previous.ContainsKey("id")?AgentCreatedStamp(Convert.ToString(previous["id"])):null};
            if(call.changes!=null){if(operations==null)ApplyAgentChanges(call.changes,call.id);else{operations.Propose(call.tool,"agent","agent:"+state.id,call.changes,call.id);operations.Apply(call.id,true);}var receipt=AgentReceipt(call.id);lock(gate){if(call.changes.Any(change=>change.after.Any(p=>Json.Serialize(typeof(Entry).GetField(p.Key).GetValue(Find(change.id)))!=Json.Serialize(p.Value))))throw new AgentFailure("uncertain_write","Applied fields could not be verified; inspect the source before retrying.");}receipt["readBackVerified"]=true;return new AgentToolReceipt{result=receipt};}
            var spec=AgentRules.Tools.First(t=>t.Name==call.tool);if(spec.Execute!=null)return spec.Execute(this,state,call,token);
            Entry entry;var a=call.args;
            if(call.tool=="create_document")entry=CreateDocument(Convert.ToString(a["name"]),Convert.ToString(a["format"]),a.ContainsKey("collection")?Convert.ToString(a["collection"]):"",Convert.ToString(a["text"]),call.id,"agent:"+state.id);
            else if(call.tool=="create_presentation")entry=CreatePresentation(Convert.ToString(a["title"]),"",AgentSlides.Build(a),call.id,"agent:"+state.id);
            else throw new InvalidOperationException("Agent 修改无效。");
            if(operations!=null)operations.Created(call.id,state.id,call.tool=="create_presentation"?"presentations":"documents",entry);
            return new AgentToolReceipt{result=AgentReceipt(call.id),stamp=AgentCreatedStamp(entry.id)};
        }
    }
}
