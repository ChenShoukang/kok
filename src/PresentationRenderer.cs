using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    internal static class PresentationRenderer {
        sealed class RenderRequest {
            public RenderRequest(){}
            public string id="",signature="";
            public int start=1,count=8,width=1600,height=900;
        }
        internal static bool Available {get{try{return Type.GetTypeFromProgID("PowerPoint.Application")!=null;}catch{return false;}}}
        internal static bool Busy {get{return Process.GetProcessesByName("POWERPNT").Length>0;}}
        static string Root(Library library){return Path.Combine(library.DataDir,"agent-presentation-renders");}
        static string Hash(string path){return AgentWorkspaceTools.Hash(File.ReadAllBytes(path));}
        static object Com(object target,string name,BindingFlags operation,params object[] arguments){try{return target.GetType().InvokeMember(name,operation|BindingFlags.IgnoreCase,null,target,arguments);}catch(Exception error){throw new InvalidOperationException(name+": "+error.GetBaseException().Message);}}
        static object Get(object target,string name){return Com(target,name,BindingFlags.GetProperty);}
        static object Call(object target,string name,params object[] arguments){return Com(target,name,BindingFlags.InvokeMethod,arguments);}
        static void Release(object target){try{if(target!=null&&Marshal.IsComObject(target))Marshal.FinalReleaseComObject(target);}catch(ArgumentException){}}

        internal static void CheckStaticPackage(string path){
            using(var zip=ZipFile.OpenRead(path))foreach(var entry in zip.Entries){
                string name=entry.FullName.ToLowerInvariant();
                if(name.Contains("vbaproject")||name.Contains("activex/")||name.Contains("embeddings/")||name.EndsWith(".bin"))throw new InvalidDataException("渲染不支持含宏、ActiveX 或嵌入对象的 PPTX。");
                if(!name.EndsWith(".rels"))continue;
                using(var stream=entry.Open()){
                    var settings=new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null};
                    using(var reader=System.Xml.XmlReader.Create(stream,settings))foreach(var relationship in XDocument.Load(reader).Descendants()){
                        if((string)relationship.Attribute("TargetMode")=="External"&&!((string)relationship.Attribute("Type")??"").EndsWith("/hyperlink",StringComparison.Ordinal))throw new InvalidDataException("渲染不支持会自动加载外部资源的 PPTX。");
                    }
                }
            }
        }

        internal static void Prepare(Library library,AgentCall call){
            string id=ModuleContext.Text(call.args,"id");var item=library.Find(id);
            if(item==null||!item.ownedDocument||item.ext!="PPTX")throw new InvalidDataException("请先将 PPTX 复制为资料库内可编辑的演示文稿，再进行渲染。");
            if(!Available)throw new InvalidDataException("未检测到本机 PowerPoint；可继续使用结构检查，实际渲染需要安装并配置 PowerPoint。");
            if(Busy)throw new InvalidDataException("PowerPoint 正在使用中，请关闭自己的演示会话后再渲染。");
            var source=PresentationFiles.Read(item.path,false);int start=AgentDocumentTools.Number(call.args,"start_page",1,1,100);
            if(start>source.slides.Count)throw new InvalidDataException("幻灯片页码超出范围。");
            CheckStaticPackage(item.path);call.documentSignature=library.PresentationCoverageDigest(id);
            call.beforeText=item.name;call.afterText="PowerPoint PNG · "+start+"–"+Math.Min(source.slides.Count,start+AgentDocumentTools.Number(call.args,"max_pages",8,1,8)-1);
        }

        internal static AgentToolReceipt Render(ModuleContext context,AgentTaskState state,AgentCall call,CancellationToken token){
            AgentBusinessCore.Configure(context.AppDirectory);
            var library=context.Library;string id=ModuleContext.Text(call.args,"id");var item=library.Find(id);
            if(item==null||!item.ownedDocument||item.ext!="PPTX"||String.IsNullOrEmpty(call.documentSignature)||library.PresentationCoverageDigest(id)!=call.documentSignature)throw new InvalidOperationException("PPTX 在审批后已变化，请重新读取并批准渲染。");
            if(Busy)throw new InvalidOperationException("PowerPoint 正在使用中，请关闭自己的演示会话后再渲染。");
            token.ThrowIfCancellationRequested();CheckStaticPackage(item.path);
            var deck=PresentationFiles.Read(item.path,false);int start=AgentDocumentTools.Number(call.args,"start_page",1,1,100),count=AgentDocumentTools.Number(call.args,"max_pages",8,1,8);
            count=Math.Min(count,deck.slides.Count-start+1);if(count<1)throw new InvalidDataException("幻灯片页码超出范围。");
            double scale=Math.Min(1600/deck.width,1200/deck.height);
            var request=new RenderRequest{id=id,signature=call.documentSignature,start=start,count=count,width=Math.Max(1,(int)Math.Round(deck.width*scale)),height=Math.Max(1,(int)Math.Round(deck.height*scale))};
            string jobId=Guid.NewGuid().ToString("N"),directory=Path.Combine(Root(library),jobId);Directory.CreateDirectory(directory);
            File.Copy(item.path,Path.Combine(directory,"input.pptx"));
            if("sha256:"+Hash(Path.Combine(directory,"input.pptx"))!=request.signature)throw new InvalidOperationException("PPTX 在读取时已变化，请重新渲染。");
            File.WriteAllText(Path.Combine(directory,"request.json"),Library.Json.Serialize(request),new UTF8Encoding(false));
            var startInfo=new ProcessStartInfo{FileName=Path.Combine(context.AppDirectory,"kok.exe"),Arguments="--render-ppt-job "+AgentExecution.QuoteArgument(directory),UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=context.AppDirectory};
            using(var worker=Process.Start(startInfo)){
                try{var timer=Stopwatch.StartNew();while(!worker.WaitForExit(100)){token.ThrowIfCancellationRequested();if(timer.Elapsed.TotalSeconds>90)throw new TimeoutException("PowerPoint 渲染超时，请检查本机 Office 状态后重试。");}}
                finally{if(!worker.HasExited)try{worker.Kill();}catch(InvalidOperationException){}}
                string resultPath=Path.Combine(directory,"result.json");if(!File.Exists(resultPath))throw new InvalidDataException("渲染未返回可验证结果，请检查本机 PowerPoint。");
                var result=Library.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(resultPath));
                if(worker.ExitCode!=0||!AgentTaskState.Get<bool>(result,"rendered"))throw new InvalidDataException(ModuleContext.Text(result,"error"));
                if(library.PresentationCoverageDigest(id)!=request.signature)throw new InvalidOperationException("PPTX 在渲染期间已变化，旧页面不能证明当前版本。");
                var frames=new List<object>();
                for(int page=start;page<start+count;page++){
                    string image=Path.Combine(directory,"page-"+page.ToString("D3")+".png");var info=new FileInfo(image);
                    if(!info.Exists||info.Length<32||info.Length>8*1024*1024)throw new InvalidDataException("渲染页面缺失或大小异常。");
                    using(var bitmap=Image.FromFile(image)){
                        if(bitmap.Width!=request.width||bitmap.Height!=request.height)throw new InvalidDataException("渲染页面尺寸不匹配。");
                        frames.Add(new{page=page,width=bitmap.Width,height=bitmap.Height,sha256=Hash(image)});
                    }
                }
                var render=new{id=id,jobId=jobId,sourceVersion=request.signature,engine="PowerPoint",engineVersion=ModuleContext.Text(result,"engineVersion"),rendered=true,visualReviewed=false,frames=frames};
                return new AgentToolReceipt{result=new Dictionary<string,object>{{"id",id},{"text",Library.Json.Serialize(render)},{"render",render}}};
            }
        }

        // Separate STA worker: Office automation can block, so the desktop host stays responsive and bounded.
        internal static int RunWorker(string directory){
            object application=null,presentations=null,presentation=null;bool owned=false;Mutex mutex=null;bool locked=false;
            try{
                directory=Path.GetFullPath(directory);var request=Library.Json.Deserialize<RenderRequest>(File.ReadAllText(Path.Combine(directory,"request.json")));
                if(!AgentJournal.ValidId(request.id)||request.start<1||request.count<1||request.count>8||request.width<1||request.width>1600||request.height<1||request.height>1200)throw new InvalidDataException("Invalid render job");
                string input=Path.Combine(directory,"input.pptx");if("sha256:"+Hash(input)!=request.signature)throw new InvalidDataException("Render input version mismatch");CheckStaticPackage(input);
                mutex=new Mutex(false,"Local\\kok-powerpoint-render");locked=mutex.WaitOne(0);if(!locked||Busy)throw new InvalidOperationException("PowerPoint 正在使用中，请关闭自己的演示会话后再渲染。");
                DateTime started=DateTime.UtcNow;application=Activator.CreateInstance(Type.GetTypeFromProgID("PowerPoint.Application",true));
                var processes=Process.GetProcessesByName("POWERPNT");
                try{owned=processes.Length==1&&processes[0].StartTime.ToUniversalTime()>=started.AddMilliseconds(-100);}finally{foreach(var process in processes)process.Dispose();}
                if(!owned)throw new InvalidOperationException("无法确认独立的 PowerPoint 渲染进程，未操作现有会话。");
                presentations=Get(application,"Presentations");if(Convert.ToInt32(Get(presentations,"Count"))!=0)throw new InvalidOperationException("PowerPoint 会话已被使用，未操作其他演示文稿。");
                Com(application,"AutomationSecurity",BindingFlags.SetProperty,3);
                presentation=Call(presentations,"Open",input,-1,0,0);
                object slides=Get(presentation,"Slides");
                try{
                    if(request.start+request.count-1>Convert.ToInt32(Get(slides,"Count")))throw new InvalidDataException("Render page range changed");
                    for(int page=request.start;page<request.start+request.count;page++){
                        object slide=Call(slides,"Item",page);
                        try{Call(slide,"Export",Path.Combine(directory,"page-"+page.ToString("D3")+".png"),"PNG",request.width,request.height);}finally{Release(slide);}
                    }
                }finally{Release(slides);}
                File.WriteAllText(Path.Combine(directory,"result.json"),Library.Json.Serialize(new{rendered=true,engineVersion=Convert.ToString(Get(application,"Version")),visualReviewed=false}),new UTF8Encoding(false));return 0;
            }catch(Exception error){
                try{File.WriteAllText(Path.Combine(directory,"result.json"),Library.Json.Serialize(new{rendered=false,error=AgentWorkspaceTools.Redact(error.GetBaseException().Message)}),new UTF8Encoding(false));}catch(IOException){}return 1;
            }finally{
                // Never quit a pre-existing instance, or an instance the user has started using meanwhile.
                try{
                    if(owned&&presentation!=null){object windows=Get(presentation,"Windows");try{if(Convert.ToInt32(Get(windows,"Count"))==0)Call(presentation,"Close");}finally{Release(windows);}}
                    if(owned&&presentations!=null&&Convert.ToInt32(Get(presentations,"Count"))==0)Call(application,"Quit");
                }catch(Exception){}finally{Release(presentation);Release(presentations);Release(application);if(locked)mutex.ReleaseMutex();if(mutex!=null)mutex.Dispose();}
            }
        }

        internal static Dictionary<string,object> ReadFrame(Library library,AgentCall call,int page){
            var render=AgentTaskState.Get<Dictionary<string,object>>(call.result,"render");string id=ModuleContext.Text(render,"id"),jobId=ModuleContext.Text(render,"jobId");
            if(!AgentJournal.ValidId(jobId)||library.PresentationCoverageDigest(id)!=ModuleContext.Text(render,"sourceVersion"))throw new InvalidOperationException("渲染后的文件已变化，请重新渲染当前版本。");
            var frames=AgentTaskState.Get<List<Dictionary<string,object>>>(render,"frames");var frame=frames.FirstOrDefault(row=>Convert.ToInt32(row["page"])==page);if(frame==null)throw new InvalidDataException("渲染页面不在本次结果中。");
            string path=Path.Combine(Root(library),jobId,"page-"+page.ToString("D3")+".png");if(!File.Exists(path)||new FileInfo(path).Length>8*1024*1024||Hash(path)!=ModuleContext.Text(frame,"sha256"))throw new InvalidDataException("渲染页面缺失或已变化，请重新渲染。");
            return new Dictionary<string,object>{{"page",page},{"pages",frames.Select(row=>Convert.ToInt32(row["page"])).ToArray()},{"image","data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(path))},{"engine","PowerPoint"},{"visualReviewed",false}};
        }

        internal static AgentToolSpec Tool(){return new AgentToolSpec{Name="render_presentation",Title="渲染演示文稿",Write=true,Requires=Need.NormalLibrary,CapabilityId="presentations.readPresentation",
            Signature="render_presentation{id,start_page?,max_pages?}",Description="PROPOSE exporting up to 8 actual pages of a managed PPTX to PNG using an installed, idle PowerPoint. Exact file version and pages require approval. Never uses a running personal Office session. Static local content only; macros/OLE/external resources are refused. No Office is bundled. Returns actual rendered page hashes and dimensions; rendering alone is not a visual or factual quality verdict. This tool does not increase source reading coverage.",
            Properties=AgentRules.Props("id",AgentRules.Id,"start_page",new Dictionary<string,object>{{"type","integer"},{"minimum",1},{"maximum",100}},"max_pages",new Dictionary<string,object>{{"type","integer"},{"minimum",1},{"maximum",8}}),Required=new[]{"id"},
            Validate=(action,exists)=>{string id=ModuleContext.Text(action.args,"id");if(!AgentJournal.ValidId(id)||!exists(id))throw new FormatException("unknown or out-of-scope file ID");AgentDocumentTools.Number(action.args,"start_page",1,1,100);AgentDocumentTools.Number(action.args,"max_pages",8,1,8);},
            ExecuteWithContext=(context,state,call,token)=>Task.Run(()=>Render(context,state,call,token),token)};}
    }
}
