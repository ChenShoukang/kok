using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    // A provider-side refusal of the tools parameter; the host switches the task to the JSON protocol once.
    public sealed class AgentToolsUnsupported:AgentFailure {public AgentToolsUnsupported(string text,int status=400):base("tools_unsupported",text,false,status){}}
    // The model called tools with invalid arguments. Messages answer every call with an error so the next round can repair it.
    public sealed class AgentRepair:Exception {
        public readonly List<object> messages;public AgentRepair(string problem,List<object> messages):base(problem){this.messages=messages;}
        public static AgentRepair ForTurn(AgentModelTurn turn,string[] problems){
            var rows=new List<object>();if(turn.protocol=="tools"){
                rows.Add(new{role="assistant",content=turn.reply.reply,tool_calls=turn.reply.actions.Select((action,n)=>{var args=new Dictionary<string,object>(action.args);if(!String.IsNullOrEmpty(action.why))args["why"]=action.why;return new{id=turn.providerIds[n],type="function",function=new{name=action.tool,arguments=Library.Json.Serialize(args)}};}).ToArray()});
                for(int n=0;n<turn.reply.actions.Count;n++)rows.Add(new{role="tool",tool_call_id=turn.providerIds[n],content=problems[n]==null?"Not executed because another call was invalid. Repeat it if needed.":"Error: "+problems[n]+". Nothing was executed. Inspect list_mcp_tools for the approved schema and correct the call."});
            }return new AgentRepair(String.Join("; ",problems.Where(p=>p!=null)),rows.Count==0?null:rows);
        }
    }
    public partial class TranslationService {
        sealed class ToolCallDraft {public string id="",name="";public StringBuilder arguments=new StringBuilder();}
        // OpenAI-compatible function calling (DeepSeek, OpenAI, Qwen, SiliconFlow, OpenRouter, Ollama, LM Studio ...).
        // Messages are built by the host in a stable order so providers with automatic prefix caching can reuse earlier rounds.
        public async Task<AgentModelTurn> AgentToolChat(List<object> messages,object[] tools,Func<string,bool> exists,CancellationToken token,Action<string> onDelta,Action<ChatUsage> onUsage,Action<int> beforeRequest,int maxTokens,Action<int> onStatus=null,bool requireTools=false){
            if(maxTokens<128||maxTokens>3000)throw new ArgumentOutOfRangeException("maxTokens");
            AiConfig c=ConfigSnapshot();if(String.IsNullOrWhiteSpace(c.model))throw new InvalidDataException("请先在设置中配置 AI 翻译接口和模型。");Uri endpoint=Endpoint(c.endpoint);string key=ReadKey(c);
            var body=new Dictionary<string,object>{{"model",c.model},{"messages",messages},{"tools",tools},{"tool_choice",requireTools?"required":"auto"},{"stream",true}};AddTokenLimit(body,c,maxTokens);
            bool deepseek=endpoint.Host.Equals("api.deepseek.com",StringComparison.OrdinalIgnoreCase),openai=endpoint.Host.Equals("api.openai.com",StringComparison.OrdinalIgnoreCase);
            if(deepseek)body["thinking"]=new{type="disabled"};if(openai)body["stream_options"]=new{include_usage=true};
            string json=Library.Json.Serialize(body);if(json.Length>400000)throw new InvalidDataException("Agent 任务过长或已达到步骤上限，请缩小范围。");
            // Estimate the entire serialized request, including tool definitions.
            if(beforeRequest!=null)beforeRequest(AgentTokenBudget.Estimate(json));
            var text=new StringBuilder();var calls=new SortedDictionary<int,ToolCallDraft>();string finish="";
            using(var handler=handlerFactory!=null?handlerFactory():new HttpClientHandler{AllowAutoRedirect=false,UseProxy=!endpoint.IsLoopback})using(var client=new HttpClient(handler))using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token))using(var request=new HttpRequestMessage(HttpMethod.Post,endpoint)){
                client.Timeout=TimeSpan.FromSeconds(185);timeout.CancelAfter(TimeSpan.FromSeconds(180));if(key!="")request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);request.Content=new StringContent(json,Encoding.UTF8,"application/json");
                try{using(var reply=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token)){
                    if(onStatus!=null)onStatus((int)reply.StatusCode);if(!reply.IsSuccessStatusCode){var failure=await AgentHttpFailure.Read(reply,timeout.Token);if(failure.kind=="tools_unsupported")throw new AgentToolsUnsupported(failure.Message,failure.httpStatus);throw failure;}
                    using(var stream=await reply.Content.ReadAsStreamAsync())using(var registration=timeout.Token.Register(()=>stream.Dispose()))using(var reader=new StreamReader(stream,Encoding.UTF8)){
                        bool plain=reply.Content.Headers.ContentType!=null&&reply.Content.Headers.ContentType.MediaType=="application/json";
                        if(plain){
                            var raw=new StringBuilder();var chars=new char[2048];int read;while((read=await reader.ReadAsync(chars,0,chars.Length))>0){if(raw.Length+read>512000)throw new InvalidDataException("AI 返回内容过长，请缩小提问范围。");raw.Append(chars,0,read);}
                            var root=(Dictionary<string,object>)Library.Json.DeserializeObject(raw.ToString());ReportChatUsage(root,onUsage);var choice=(Dictionary<string,object>)((object[])root["choices"])[0];finish=choice.ContainsKey("finish_reason")?Convert.ToString(choice["finish_reason"]):"stop";
                            Absorb(choice["message"] as Dictionary<string,object>,text,calls,onDelta);
                        }else{
                            string line;bool complete=false;int received=0;
                            while((line=await reader.ReadLineAsync())!=null){
                                timeout.Token.ThrowIfCancellationRequested();received+=line.Length;if(received>1024*1024)throw new InvalidDataException("AI 返回内容过长，请缩小提问范围。");if(!line.StartsWith("data:",StringComparison.Ordinal))continue;string data=line.Substring(5).Trim();if(data=="[DONE]"){complete=true;break;}if(data=="")continue;
                                var root=(Dictionary<string,object>)Library.Json.DeserializeObject(data);ReportChatUsage(root,onUsage);if(root.ContainsKey("error"))throw new InvalidDataException("AI 未返回有效回答，请重试。");
                                var choices=root.ContainsKey("choices")?root["choices"] as object[]:null;if(choices==null||choices.Length==0)continue;var choice=(Dictionary<string,object>)choices[0];
                                string f=choice.ContainsKey("finish_reason")&&choice["finish_reason"]!=null?Convert.ToString(choice["finish_reason"]):"";if(f!=""){finish=f;complete=true;}
                                Absorb(choice.ContainsKey("delta")?choice["delta"] as Dictionary<string,object>:null,text,calls,onDelta);
                            }
                            if(!complete)throw new InvalidDataException("AI 回答未完成，请重试。");
                        }
                    }
                }}catch(Exception ex){if(token.IsCancellationRequested)throw new OperationCanceledException(token);if(timeout.IsCancellationRequested)throw new TimeoutException("翻译请求超时，请稍后重试或更换模型。");if(ex is AgentToolsUnsupported||ex is AgentFailure)throw;if(ex is HttpRequestException||ex is IOException&&!(ex is InvalidDataException))throw new HttpRequestException("无法连接翻译服务，请检查接口地址与网络。");throw new AgentFailure("response","AI 未返回有效回答，请重试。",true);}
            }
            token.ThrowIfCancellationRequested();if(finish=="length")throw new AgentFailure("context","AI 回答被截断，请缩小提问范围后重试。");
            return ToTurn(text.ToString().Trim(),calls.Values.ToList(),exists);
        }
        static void Absorb(Dictionary<string,object> part,StringBuilder text,SortedDictionary<int,ToolCallDraft> calls,Action<string> onDelta){
            if(part==null)return;
            var content=part.ContainsKey("content")?part["content"] as string:null;if(!String.IsNullOrEmpty(content)){if(text.Length+content.Length>24000)throw new InvalidDataException("AI 返回内容过长，请缩小提问范围。");text.Append(content);onDelta(content);}
            var rows=part.ContainsKey("tool_calls")?part["tool_calls"] as object[]:null;if(rows==null)return;
            for(int n=0;n<rows.Length;n++){
                var row=rows[n] as Dictionary<string,object>;if(row==null)continue;int index=row.ContainsKey("index")?Convert.ToInt32(row["index"]):n;if(index<0||index>15)throw new InvalidDataException("AI 未返回有效回答，请重试。");
                ToolCallDraft call;if(!calls.TryGetValue(index,out call))calls[index]=call=new ToolCallDraft();
                if(row.ContainsKey("id")&&row["id"]!=null)call.id=Convert.ToString(row["id"]);
                var fn=row.ContainsKey("function")?row["function"] as Dictionary<string,object>:null;if(fn==null)continue;
                if(fn.ContainsKey("name")&&fn["name"]!=null)call.name+=Convert.ToString(fn["name"]);
                // Ollama sends complete arguments as an object; streaming providers send string fragments.
                if(fn.ContainsKey("arguments")&&fn["arguments"]!=null){var value=fn["arguments"] as string??Library.Json.Serialize(fn["arguments"]);if(call.arguments.Length+value.Length>60000)throw new InvalidDataException("AI 返回内容过长，请缩小提问范围。");call.arguments.Append(value);}
            }
        }
        static AgentModelTurn ToTurn(string text,List<ToolCallDraft> drafts,Func<string,bool> exists){
            for(int n=0;n<drafts.Count;n++)if(drafts[n].id=="")drafts[n].id="call_"+Guid.NewGuid().ToString("N").Substring(0,20);
            if(drafts.Any(d=>!System.Text.RegularExpressions.Regex.IsMatch(d.id,"^[A-Za-z0-9_-]{1,128}$"))||drafts.Select(d=>d.id).Distinct().Count()!=drafts.Count)throw new InvalidDataException("AI 未返回有效回答，请重试。");
            var turn=new AgentModelTurn{reply=new AgentReply{reply=text},protocol="tools"};var problems=new string[drafts.Count];
            for(int n=0;n<drafts.Count;n++){
                var d=drafts[n];var action=new AgentAction{tool=d.name.Trim()};
                try{
                    string raw=d.arguments.ToString().Trim();Dictionary<string,object> args;try{args=raw==""?new Dictionary<string,object>():Library.Json.Deserialize<Dictionary<string,object>>(raw);}catch{throw new FormatException("arguments are not a valid JSON object");}
                    args=args??new Dictionary<string,object>();object why;if(args.TryGetValue("why",out why)){action.why=why==null?null:Convert.ToString(why);args.Remove("why");}action.args=args;
                    if(n>=AgentRules.MaxActions)throw new FormatException("at most "+AgentRules.MaxActions+" tool calls are allowed per round");AgentRules.ValidateAction(action,exists);
                }catch(FormatException e){problems[n]=e.Message;}
                turn.reply.actions.Add(action);turn.providerIds.Add(d.id);
            }
            if(problems.Any(p=>p!=null)){
                var repair=new List<object>{new Dictionary<string,object>{{"role","assistant"},{"content",text},{"tool_calls",drafts.Select(d=>new{id=d.id,type="function",function=new{name=d.name,arguments=d.arguments.ToString()}}).ToArray()}}};
                for(int n=0;n<drafts.Count;n++)repair.Add(new{role="tool",tool_call_id=drafts[n].id,content=problems[n]!=null?"Error: "+problems[n]+". Nothing was executed; call the tool again with corrected arguments.":"Not executed because another call in this round was invalid. Repeat it if still needed."});
                throw new AgentRepair(String.Join("; ",problems.Where(p=>p!=null)),repair);
            }
            bool reads=turn.reply.actions.Any(a=>AgentRules.ReadTools.Contains(a.tool));turn.reply.done=!reads;
            if(turn.reply.reply==""&&turn.reply.actions.Count==0)throw new InvalidDataException("AI 未返回有效回答，请重试。");
            return turn;
        }
    }
}
