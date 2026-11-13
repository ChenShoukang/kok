using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using kok;

// Real bundled worker + real SDK + native library/approval/journal. Model HTTP is loopback-only.
static class AgentResearchTests {
    static Dictionary<string,object> D(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    sealed class Server : IDisposable {
        readonly TcpListener listener;readonly Thread thread;volatile bool running=true;
        public readonly List<string> Bodies=new List<string>();public Func<int,object> Script;public int Port;
        public Server(){listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;thread=new Thread(Loop){IsBackground=true};thread.Start();}
        void Loop(){while(running){try{
            using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){
                stream.ReadTimeout=15000;var header=new List<byte>();int b;
                while((b=stream.ReadByte())>=0){header.Add((byte)b);int n=header.Count;if(n>4&&header[n-4]==13&&header[n-3]==10&&header[n-2]==13&&header[n-1]==10)break;if(n>64000)throw new IOException();}
                string h=Encoding.ASCII.GetString(header.ToArray());int length=0;
                foreach(string row in h.Split('\n'))if(row.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))length=Int32.Parse(row.Substring(15).Trim());
                if(length<1||length>1000000)throw new IOException();byte[] data=new byte[length];int read=0;
                while(read<length){int take=stream.Read(data,read,length-read);if(take==0)throw new IOException();read+=take;}
                int number;lock(Bodies){Bodies.Add(Encoding.UTF8.GetString(data));number=Bodies.Count;}
                object scripted=Script(number);var json=Library.Json.Serialize(scripted);
                byte[] bytes=Encoding.UTF8.GetBytes("data: "+json+"\n\ndata: [DONE]\n\n");
                byte[] response=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");
                stream.Write(response,0,response.Length);stream.Write(bytes,0,bytes.Length);stream.Flush();
            }
        }catch{if(!running)return;}}}
        public int Count{get{lock(Bodies)return Bodies.Count;}}
        public void Dispose(){running=false;listener.Stop();}
    }
    static object Reply(string tool=null,object args=null,string text=""){
        object delta=tool==null?(object)new{role="assistant",content=text}:new{role="assistant",tool_calls=new[]{new{index=0,id="call_"+Guid.NewGuid().ToString("N"),type="function",function=new{name=tool,arguments=Library.Json.Serialize(args)}}}};
        return new{id="fixture_"+Guid.NewGuid().ToString("N"),@object="chat.completion.chunk",model="kok-research-fixture",choices=new[]{new{index=0,delta,finish_reason=tool==null?"stop":"tool_calls"}},usage=new{prompt_tokens=100,completion_tokens=30}};
    }
    sealed class Fixture {
        public Library Lib;public AgentService Service;public string Source;
        public Fixture(string path,Server server){
            Lib=new Library(path);Lib.SetAppMode("smart",true);
            Source=Lib.CreateDocument("研究来源","md","","最终修订预算为8万元。禁止收集手机号。活动分三阶段开展。").id;
            var ai=new TranslationService(Lib.DataDir);ai.SaveConfig(D(new{endpoint="http://127.0.0.1:"+server.Port+"/v1",model="kok-research-fixture",clearKey=true}));
            Service=new AgentService(new ModuleContext(Lib,ai,new ModulePolicy(Lib),null,CancellationToken.None,null,new Operations(Lib,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory));
        }
        public async Task<Dictionary<string,object>> Start(int limit=6){return D(await Service.HandleAgentTaskAction("agentTaskStart",D(new{runtime="research",sessionId=Guid.NewGuid().ToString("N"),goal="根据这份资料创建100-300字的DOCX报告，采用最终修订预算，保留禁止事项。",scope=new{mode="selected",ids=new[]{Source}},requestLimit=limit,budget=14000,contextTokens=32000})));}
        public async Task<Dictionary<string,object>> Read(string id){return D(await Service.HandleAgentTaskAction("agentTaskRead",D(new{taskId=id})));}
        public async Task<Dictionary<string,object>> Wait(string id,bool pending=false){
            var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<30000){var state=await Read(id);if(!Convert.ToBoolean(state["running"])||pending&&AgentTaskState.Get<List<AgentCall>>(state,"calls").Any(c=>c.status=="pending"))return state;await Task.Delay(15);}
            Service.Stop();throw new Exception("Research runtime fixture timed out.");
        }
    }
    public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
    static async Task RunAsync(string root,Action<bool,string> check){
        check(AgentResearchKernel.Available(AppDomain.CurrentDomain.BaseDirectory),"research: packaged Node and worker are available without a system Node lookup");
        using(var server=new Server()){
            var fixture=new Fixture(Path.Combine(root,"restart"),server);
            string report="最终修订预算为8万元，禁止收集手机号。"+new string('研',160);
            server.Script=n=>n==1?Reply("read_items",new{ids=new[]{fixture.Source}}):n==2?Reply("create_document",new{name="研究报告",format="docx",text=report}):Reply(text:"已按来源生成DOCX报告，保留8万元预算和禁止收集手机号的限制。");
            string id=Convert.ToString((await fixture.Start())["id"]);var waiting=await fixture.Wait(id,true);
            var pending=AgentTaskState.Get<List<AgentCall>>(waiting,"calls").FirstOrDefault(c=>c.tool=="create_document"&&c.status=="pending");
            if(pending==null){var diagnostic=AgentTaskState.Replay(id,new AgentJournal(fixture.Lib.DataDir).Events(id));Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"failed-runtime.json"),Library.Json.Serialize(new{taskId=id,checkpoint=diagnostic.researchCheckpoint,bodies=server.Bodies}),Encoding.UTF8);throw new Exception("Research runtime did not propose the document: status="+Convert.ToString(waiting["status"])+", error="+Convert.ToString(waiting["error"])+", HTTP requests="+server.Count+", calls="+String.Join(";",diagnostic.calls.Select(c=>c.tool+":"+c.status+":"+c.error))+", replies="+String.Join(";",diagnostic.dialogue.Where(d=>d.role=="agent").Select(d=>d.text)));}
            check(pending!=null,"research: SDK file creation reaches the native approval gate");
            check(server.Count==2,"research: source read and draft use two actual model requests");
            bool manualRejected=false;try{await fixture.Service.HandleAgentTaskAction("agentTaskCompact",D(new{taskId=id}));}catch(InvalidOperationException){manualRejected=true;}
            check(manualRejected,"research: legacy manual compaction is rejected explicitly instead of silently ignored");
            check(server.Bodies[0].Contains("根据这份资料")&&server.Bodies[1].Contains("最终修订预算"),"research: UTF-8 preserves Chinese user goals and native source text across the worker boundary");
            check(!new AgentJournal(fixture.Lib.DataDir).Events(id).Any(e=>e.kind=="tool.completed"&&AgentTaskState.Get<string>(e.data,"callId")==pending.id),"research: a proposal does not write the file before approval");
            fixture.Service.Stop();await fixture.Wait(id);
            await fixture.Service.HandleAgentTaskAction("agentTaskResume",D(new{taskId=id}));
            await fixture.Wait(id,true);
            check(server.Count==2,"research: restarting a pending approval does not regenerate or spend another model request");
            await fixture.Service.HandleAgentTaskAction("agentTaskDecide",D(new{taskId=id,callId=pending.id,approve=true}));
            var done=await fixture.Wait(id);
            check(Convert.ToString(done["status"])=="completed","research: real SDK, approval, saved DOCX, read-back and host completion finish end to end");
            var state=AgentTaskState.Replay(id,new AgentJournal(fixture.Lib.DataDir).Events(id));
            var created=state.calls.Single(c=>c.tool=="create_document"&&c.status=="done");string artifact=Convert.ToString(created.result["id"]);
            check(fixture.Lib.Find(artifact).ext=="DOCX"&&fixture.Lib.DocumentSource(artifact).text==report,"research: final file is actual DOCX with the expected saved body");
            check(state.calls.Count(c=>c.tool=="create_document")==1,"research: recovery never duplicates the approved creation");
            check(state.plan.Count==0&&state.verification!=null,"research: formal model plan/finish calls are replaced by host verification");
            check(state.requests==3&&server.Count==3,"research: native request ledger counts all three framework HTTP requests");
            check(server.Bodies[2].Contains("hostReadBacks")&&server.Bodies[2].Contains("kok_call_id"),"research: actual automatic read-back receipts reach the SDK without another model read request");
            check(state.researchCheckpoint!=null&&state.researchCheckpoint.ContainsKey("snapshot"),"research: compressed SDK state persists in the encrypted native journal");
            fixture.Service.Stop();
        }
        using(var server=new Server()){
            var fixture=new Fixture(Path.Combine(root,"budget"),server);server.Script=n=>Reply("read_items",new{ids=new[]{fixture.Source}});
            string id=Convert.ToString((await fixture.Start(1))["id"]);var done=await fixture.Wait(id);
            check(Convert.ToString(done["status"])=="limited"&&server.Count==1,"research: request cap stops the SDK before its next HTTP call");fixture.Service.Stop();
        }
        var legacy=new AgentTaskState{options=new AgentTaskOptions()};
        check(!AgentService.ResearchRuntime(legacy),"research: pre-0.13 tasks retain the native execution semantics");
    }
}
