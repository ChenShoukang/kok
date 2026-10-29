using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace kok {
    public class AgentTaskOptions {
        public string sessionId,goal,intent="assist",protocol="auto",workflow="",instruction="",workflowGuidance="";
        public AgentScope scope=new AgentScope();public List<string> allowed=new List<string>();public List<ChatTurn> history=new List<ChatTurn>();
        public int budget=14000,requestLimit=6;public double costLimit,inputRate,outputRate;public AgentHandoff handoff;
    }
    public class AgentHandoff {public string taskId,eventId,note;public List<string> callIds=new List<string>(),ids=new List<string>();}
    public class AgentCall {
        public string id,providerId,turnId,tool,why,status="queued",error="",errorKind="",resultEventId,stamp;
        public Dictionary<string,object> args=new Dictionary<string,object>(),result;
        public List<AgentChange> changes;public string beforeText,afterText;
        public int sequence,attempt;public bool retryable;public Dictionary<string,string> sources=new Dictionary<string,string>();
    }
    public class AgentDialogue {public string id,role,text,turnId;public int sequence;public bool meta;public List<string> sourceIds=new List<string>();}
    public class AgentTaskState {
        public string id,status="created",error="",errorKind="",phase="",protocol="json";public bool retryable,needsModel=true,modelDone,estimated,cancelRequested;
        public AgentTaskOptions options;public int sequence,requests,round,inputTokens,outputTokens,cachedTokens,repairs,steerVersion,replySteering;
        public double cost;public List<AgentCall> calls=new List<AgentCall>();public List<AgentDialogue> dialogue=new List<AgentDialogue>();public List<string> adjustments=new List<string>();
        public List<AgentEvent> events=new List<AgentEvent>();
        public static T Get<T>(Dictionary<string,object> data,string key){return data.ContainsKey(key)?Library.Json.Deserialize<T>(Library.Json.Serialize(data[key])):default(T);}
        public static AgentTaskState Replay(string id,List<AgentEvent> events){var s=new AgentTaskState{id=id};foreach(var e in events)s.Apply(e);return s;}
        public void Apply(AgentEvent e){
            events.Add(e);sequence=e.sequence;var d=e.data;
            switch(e.kind){
                case "task.created":options=Get<AgentTaskOptions>(d,"options");protocol=Get<string>(d,"protocol");dialogue.Add(new AgentDialogue{id=e.id,role="user",text=options.goal,sequence=e.sequence});break;
                case "task.running":status="running";error="";errorKind="";retryable=false;break;
                case "task.phase":phase=Get<string>(d,"phase");break;
                case "task.protocol":protocol=Get<string>(d,"protocol");break;
                case "request.repair":repairs++;break;
                case "task.adjusted":adjustments.Add(Get<string>(d,"text"));steerVersion++;dialogue.Add(new AgentDialogue{id=e.id,role="user",text=Get<string>(d,"text"),sequence=e.sequence});break;
                case "request.started":requests++;cost+=Get<double>(d,"reserve");break;
                case "request.usage":cost+=Get<double>(d,"cost")-Get<double>(d,"reserve");inputTokens+=Get<int>(d,"inputTokens");outputTokens+=Get<int>(d,"outputTokens");cachedTokens+=d.ContainsKey("cachedTokens")?Get<int>(d,"cachedTokens"):0;estimated|=Get<bool>(d,"estimated");break;
                case "assistant.reply":round++;replySteering=Get<int>(d,"steering");modelDone=Get<bool>(d,"done");needsModel=!modelDone;dialogue.Add(new AgentDialogue{id=e.id,role="agent",text=Get<string>(d,"text"),turnId=Get<string>(d,"turnId"),sequence=e.sequence,sourceIds=Get<List<string>>(d,"sources")??new List<string>()});break;
                case "task.note":dialogue.Add(new AgentDialogue{id=e.id,role="agent",meta=true,text=Get<string>(d,"text"),sequence=e.sequence});break;
                case "tool.proposed":var c=Get<AgentCall>(d,"call");c.sequence=e.sequence;calls.Add(c);break;
                case "tool.started":case "tool.retry":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status=e.kind=="tool.retry"?"queued":"running";c1.error="";c1.attempt++;break;}
                case "tool.completed":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status="done";c1.result=Get<Dictionary<string,object>>(d,"result");c1.resultEventId=e.id;c1.stamp=Get<string>(d,"stamp");break;}
                case "tool.reused":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));var previous=calls.First(c=>c.resultEventId==Get<string>(d,"resultRef")&&c.result!=null);c1.status="done";c1.result=previous.result;c1.resultEventId=previous.resultEventId;break;}
                case "tool.sources":calls.First(c=>c.id==Get<string>(d,"callId")).sources=Get<Dictionary<string,string>>(d,"sources");break;
                case "tool.skipped":case "tool.undone":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status=e.kind=="tool.skipped"?"skipped":"undone";if(e.kind=="tool.skipped")c1.error=d.ContainsKey("reason")?Get<string>(d,"reason"):"user";break;}
                case "tool.failed":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status="failed";c1.error=Get<string>(d,"message");c1.errorKind=Get<string>(d,"kind");c1.retryable=Get<bool>(d,"retryable");break;}
                case "task.waiting":status="waiting";phase="等待确认";break;
                case "task.cancel_requested":case "task.cancelled":cancelRequested=true;status="cancelled";phase="任务已取消";break;
                case "task.paused":case "task.stopped":status=cancelRequested?"cancelled":e.kind=="task.paused"?"paused":"stopped";phase=cancelRequested?"任务已取消":"任务已暂停";break;
                case "task.failed":status="failed";error=Get<string>(d,"message");errorKind=Get<string>(d,"kind");retryable=Get<bool>(d,"retryable");break;
                case "task.completed":status=Get<bool>(d,"limited")?"limited":"completed";phase=status=="completed"?"已完成":"已达到本次请求上限。";break;
            }
        }
        public bool Terminal {get{return cancelRequested||new[]{"completed","limited","cancelled"}.Contains(status);}}
    }
    public class AgentFailure:Exception {
        public readonly string kind;public readonly bool retryable;
        public AgentFailure(string kind,string text,bool retryable=false):base(text){this.kind=kind;this.retryable=retryable;}
        public static AgentFailure Classify(Exception e){var f=e as AgentFailure;if(f!=null)return f;string m=e.Message??"";if(e is FileNotFoundException)return new AgentFailure("missing_source",m);if(m.Contains("内容已被修改"))return new AgentFailure("conflict",m);if(m.Contains("无法连接")||m.Contains("超时"))return new AgentFailure("network",m,true);if(m.Contains("请求过于频繁")||m.Contains("服务暂时"))return new AgentFailure("service",m,true);if(m.Contains("格式无效")||m.Contains("被截断")||m.Contains("未完成"))return new AgentFailure("response",m,true);if(m.Contains("额度")||m.Contains("上限"))return new AgentFailure("budget",m);return new AgentFailure("validation",m);}
    }
    public class AgentModelTurn {public AgentReply reply;public List<string> providerIds=new List<string>();public string protocol;}
}
