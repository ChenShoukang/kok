using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
namespace kok {
 internal static class SpeechToolDefinitions {
  internal static AgentToolSpec[] Tools(){return new[]{new AgentToolSpec{
   Name="generate_subtitles",Title="生成字幕",CapabilityId="speech.generateSubtitles",Write=true,Requires=Need.Ai|Need.Network|Need.NormalLibrary,
   Description="PROPOSE generating subtitles for one allowed video with the user's configured speech engine. Requires local media tools and a configured local model or speech API. May incur speech-provider charges. Never install components or change providers automatically. The user approves first.",Signature="generate_subtitles{id}",Properties=AgentRules.Props("id",AgentRules.Id),Required=new[]{"id"},
   Validate=(action,exists)=>{object id;if(action.args==null||action.args.Count!=1||!action.args.TryGetValue("id",out id)||!(id is string)||!AgentJournal.ValidId((string)id)||!exists((string)id))throw new FormatException("id must identify one video in the allowed scope");},
   ExecuteWithContext=async(c,state,call,token)=>{
    string id=ModuleContext.Text(call.args,"id");var item=c.Library.Find(id);if(!state.options.allowed.Contains(id)||item==null||item.kind!="video")throw new InvalidDataException("请先打开视频。");c.Policy.Ensure(Need.Ai|Need.Network|Need.NormalLibrary,"speech");
    var previous=c.Library.AgentReceipt(call.id);if(previous!=null)return new AgentToolReceipt{result=previous};
    if(c.Library.OperationStatus(call.id)!="")throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");
    if(call.sources.ContainsKey(id)&&call.sources[id]!=c.Library.AgentSourceStamps(new[]{id})[id])throw new InvalidOperationException("文件已变化，请重新分析后应用。");
    object result;using(OperationOrigin.Use("agent:"+state.id,call.id))result=await c.Jobs.WithCancellation(()=>c.Invoke("generateSubtitles",new Dictionary<string,object>{{"id",id},{"jobId",call.id}}),token);
    token.ThrowIfCancellationRequested();var track=result as SubtitleTrack;if(track==null)throw new OperationCanceledException(token);
    if(c.Library.OperationStatus(call.id)==""){c.Ops.Propose("生成字幕","speech","agent:"+state.id,new List<AgentChange>{new AgentChange{id=id,before=new Dictionary<string,object>{{"generatedSubtitles",track}},after=new Dictionary<string,object>{{"generatedSubtitles",track}}}},call.id);c.Ops.Apply(call.id,true);}
    return new AgentToolReceipt{result=new Dictionary<string,object>{{"id",id},{"cues",track.cues.Count},{"text","Generated "+track.cues.Count+" subtitle cues. Source video bytes were preserved."}}};
   }
  }};}
 }
}
