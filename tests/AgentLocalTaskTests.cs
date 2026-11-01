using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

static class AgentLocalTaskTests {
    static Dictionary<string,object> Obj(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static Dictionary<string,object> ScopeObj(string mode,string collection="",string[] ids=null){return Obj(new{mode,ids=ids??new string[0],collection});}
    static bool Rejects(Action action){try{action();return false;}catch(InvalidDataException){return true;}catch(InvalidOperationException){return true;}}
    static Entry Add(Library lib,string name,string kind,string ext,long size,string collection,DateTime added,params string[] tags){
        var entry=new Entry{id=Guid.NewGuid().ToString("N"),name=name,kind=kind,ext=ext,path=Path.Combine(lib.DataDir,name+"."+ext.ToLowerInvariant()),size=size,added=added.ToString("o"),collection=collection};
        entry.tags=tags.Where(x=>!String.IsNullOrEmpty(x)).ToList();
        lib.Db.items.Add(entry);
        if(!String.IsNullOrEmpty(collection)&&!lib.Db.collections.Contains(collection))lib.Db.collections.Add(collection);
        return entry;
    }
    static string Row(AgentLocalTask plan,string label){
        return Convert.ToString(plan.stats.First(x=>Convert.ToString(x["label"])==label)["value"]);
    }
    public static void Run(string root,Action<bool,string> check){
        Directory.CreateDirectory(root);
        var lib=new Library(Path.Combine(root,"library"));lib.SetAppMode("smart",true);
        DateTime now=DateTime.Now;
        var v1=Add(lib,"旅行片段一","video","MP4",2*1024*1024,"旅行",now.AddDays(-1),"设计");
        var v2=Add(lib,"旅行片段二","video","MP4",12*1024*1024,"旅行",now.AddDays(-5),"设计","风景");
        var v3=Add(lib,"工作录像","video","MP4",20*1024*1024,"工作",now.AddDays(-20));
        var p1=Add(lib,"海报","photo","JPG",11*1024*1024,"设计",now.AddDays(-2),"设计");
        var p2=Add(lib,"截图","photo","PNG",1*1024*1024,"",now.AddDays(-1));
        var p3=Add(lib,"原图","photo","JPG",10*1024*1024,"设计",now.AddDays(-6));
        var d1=Add(lib,"报告","document","PDF",3*1024*1024,"工作",now.AddDays(-3));
        var d2=Add(lib,"演示","document","PPTX",5*1024*1024,"工作",now.AddDays(-4));
        var m1=Add(lib,"背景音乐","music","MP3",4*1024*1024,"旅行",now.AddDays(-10));
        var b1=Add(lib,"电子书","book","EPUB",1*1024*1024,"",now.AddDays(-8));
        lib.Db.items.Single(x=>x.id==v3.id).favorite=true;

        var allScope=lib.ResolveAgentScope(new AgentScope{mode="all"});
        var travelScope=lib.ResolveAgentScope(new AgentScope{mode="collection",collection="旅行"});

        // 1. Count by kind inside the current collection.
        var plan=AgentLocalTasks.Plan(lib,travelScope,"分类「旅行」","统计这个分类里的视频数量");
        check(plan.handled&&plan.kind=="count"&&plan.total==2&&plan.scopeLabel=="分类「旅行」","local count by kind honours the collection scope");
        check(plan.title=="本地统计"&&plan.summary.Contains("视频共 2 份"),"count response carries a localized title and a concrete summary");
        check(Row(plan,"范围")=="分类「旅行」"&&Row(plan,"条件")=="视频"&&Row(plan,"匹配")=="2","count stats expose scope, filter and match count");
        check(plan.items.Count==0&&!plan.truncated&&plan.followups.Count==2,"count answers stay compact but keep deterministic and AI followups");
        check(plan.followups[0]=="列出视频"&&plan.followups[1].StartsWith("用 AI 深入分析："),"count followups chain into a local list and an explicit AI action");

        // 2. Count by extension across the whole library.
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","资料库里有多少 PDF");
        check(plan.handled&&plan.kind=="count"&&plan.total==1&&plan.summary.Contains("PDF 共 1 份"),"local count by extension answers a whole-library PDF question");
        check(plan.stats.Count==3&&Convert.ToInt32(plan.stats[2]["value"])==1,"count stats serialize numeric values");

        // 3. Count by an explicitly named collection.
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","统计分类「工作」里的视频数量");
        check(plan.handled&&plan.kind=="count"&&plan.total==1,"an explicitly quoted collection name narrows the local count");

        // 4. List inside a time window with a kind filter.
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","列出最近三天的视频");
        check(plan.handled&&plan.kind=="list"&&plan.total==1&&plan.items.Count==1&&Convert.ToString(plan.items[0]["id"])==v1.id,"time-window list returns only recent matches");
        var item=plan.items[0];
        check(item.ContainsKey("id")&&item.ContainsKey("name")&&item.ContainsKey("kind")&&item.ContainsKey("ext")&&item.ContainsKey("collection")&&item.ContainsKey("added")&&item.ContainsKey("size")&&item.ContainsKey("tags")&&item.ContainsKey("favorite"),"listed items expose the documented metadata fields");
        check(AgentTaskState.Get<List<string>>(item,"tags").SequenceEqual(new[]{"设计"})&&Convert.ToBoolean(item["favorite"])==false,"listed item tags and favorite flags survive JSON serialization");

        // 5. Imported-within window without an explicit list verb.
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","最近一周导入的文件");
        check(plan.handled&&plan.kind=="list"&&plan.total==7&&plan.items.Count==7&&!plan.truncated,"recent-import questions fall back to a local list without a verb");

        // 6. Tag filter and tag overview.
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","标了 #设计 的资料有哪些");
        check(plan.handled&&plan.kind=="list"&&plan.total==3&&plan.summary.Contains("#设计")&&plan.items.All(x=>AgentTaskState.Get<List<string>>(x,"tags").Contains("设计")),"a #tag filter lists only tagged entries");
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","有哪些标签");
        check(plan.handled&&plan.kind=="tags"&&plan.total==2&&Row(plan,"#设计")=="3"&&Row(plan,"#风景")=="1","tag overview counts every tag in scope");

        // 7. Kinds distribution and collection overview.
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","各类文件数量");
        check(plan.handled&&plan.kind=="kinds"&&plan.total==10&&Row(plan,"视频")=="3"&&Row(plan,"文档")=="2"&&Row(plan,"图片")=="3"&&Row(plan,"音乐")=="1"&&Row(plan,"电子书")=="1","kind distribution counts the whole scope");
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","有哪些分类，各自多少份资料");
        check(plan.handled&&plan.kind=="collections"&&plan.total==3&&Row(plan,"工作")=="3"&&Row(plan,"旅行")=="3"&&Row(plan,"设计")=="2"&&Row(plan,"未分类")=="2","collection overview reports every category and uncategorized items");

        // 8. Size thresholds are strict or inclusive as written.
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","找出大于 10MB 的图片");
        check(plan.handled&&plan.kind=="list"&&plan.total==1&&Convert.ToString(plan.items[0]["id"])==p1.id,"strict size threshold excludes an exactly-sized file");
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","找出至少 10MB 的图片");
        check(plan.handled&&plan.total==2,"inclusive size threshold keeps an exactly-sized file");

        // 9. Scope isolation: only IDs in the resolved scope are ever read.
        var selected=lib.ResolveAgentScope(new AgentScope{mode="selected",ids=new List<string>{v1.id,v2.id,p1.id}});
        plan=AgentLocalTasks.Plan(lib,selected,"所选 3 份资料","统计视频数量");
        check(plan.handled&&plan.total==2,"selected scope only counts its own entries");
        plan=AgentLocalTasks.Plan(lib,selected,"所选 3 份资料","资料库里有多少 PDF");
        check(plan.handled&&plan.total==0&&!plan.items.Any(x=>Convert.ToString(x["id"])==d1.id),"a matching entry outside the scope is never counted");
        plan=AgentLocalTasks.Plan(lib,selected,"所选 3 份资料","列出最近一周导入的资料");
        check(plan.handled&&plan.kind=="list"&&plan.total==3&&plan.items.All(x=>selected.Contains(Convert.ToString(x["id"]))),"listed results stay inside the resolved scope");

        // 10. Favorites and explicit no-match fallbacks.
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","列出收藏的资料");
        check(plan.handled&&plan.kind=="list"&&plan.total==1&&Convert.ToString(plan.items[0]["id"])==v3.id,"favorite filter lists only starred items");
        foreach(var goal in new[]{"总结这些视频的共同观点","把选中的资料做一份演示文稿","根据内容建议分类和标签","帮我想想怎么整理","给这些视频写一份介绍"}){
            plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库",goal);
            check(!plan.handled&&plan.reason=="generative_or_write","generative request stays on the model path: "+goal);
        }
        foreach(var goal in new[]{"列出资料","统计一下","有多少文件","帮我看看"}){
            plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库",goal);
            check(!plan.handled&&plan.reason=="no_explicit_constraint","a request without an explicit constraint is not answered locally: "+goal);
        }
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","这个分类里的视频");
        check(!plan.handled&&plan.reason=="ambiguous_scope","an unresolvable current-collection wording is not answered locally");
        plan=AgentLocalTasks.Plan(lib,allScope,"整个资料库","统计这个分类里的视频数量");
        check(!plan.handled&&plan.reason=="ambiguous_scope","current-collection wording without a collection scope falls back instead of guessing");

        // 11. English goals parse with the same constraints.
        plan=AgentLocalTasks.Plan(lib,allScope,"the entire library","how many videos are in the library");
        check(plan.handled&&plan.kind=="count"&&plan.total==3&&plan.title=="Local count","English count phrasing is parsed locally");
        plan=AgentLocalTasks.Plan(lib,allScope,"the entire library","list photos larger than 5 MB");
        check(plan.handled&&plan.kind=="list"&&plan.total==2,"English size and kind phrasing is parsed locally");
        plan=AgentLocalTasks.Plan(lib,allScope,"the entire library","summarize these videos");
        check(!plan.handled,"English generative phrasing stays on the model path");

        // 12. The planner is strictly read-only.
        string before=Library.Json.Serialize(new{lib.Db.version,lib.Db.items,lib.Db.collections,lib.Db.settings});
        AgentLocalTasks.Plan(lib,allScope,"整个资料库","有哪些分类，各自多少份资料");
        AgentLocalTasks.Plan(lib,allScope,"整个资料库","列出最近一周导入的文件");
        AgentLocalTasks.Plan(lib,allScope,"整个资料库","统计视频数量");
        string after=Library.Json.Serialize(new{lib.Db.version,lib.Db.items,lib.Db.collections,lib.Db.settings});
        check(before==after,"local planning never changes the library database");

        // 13. The native action is registered as an AI-free read action and dispatches without any API key.
        var registry=ModuleRegistry.Builtins;
        ActionSpec action=registry.Modules.Single(m=>m.Name=="agent").Actions.Single(a=>a.Name=="agentLocalPlan");
        check(action.Requires.Value==Need.NormalLibrary&&action.Writes.Value==false&&action.Kind=="read","agentLocalPlan is a NormalLibrary read action with no AI or network requirement");
        check(action.Schema!=null&&Library.Json.Serialize(action.Schema).Contains("\"goal\"")&&Library.Json.Serialize(action.Schema).Contains("\"scope\""),"agentLocalPlan declares an explicit goal and scope schema");
        var capabilities=registry.Capabilities.Where(c=>c.id=="agent.agentLocalPlan").ToList();
        check(capabilities.Count==1&&capabilities[0].requires==Need.NormalLibrary&&capabilities[0].kind=="read","the module catalog advertises agentLocalPlan as an available read capability");
        var baseContext=new ModuleContext(lib,null,new ModulePolicy(lib),null,CancellationToken.None,null,new Operations(lib,null),null);
        var service=new AgentService(baseContext);
        var context=new ModuleContext(lib,null,new ModulePolicy(lib),null,CancellationToken.None,null,new Operations(lib,null),null,service);
        check(Library.Json.Serialize(registry.Catalog(context)).Contains("\"agentLocalPlan\""),"the module catalog lists the agentLocalPlan action");
        Func<string,string,Dictionary<string,object>,Dictionary<string,object>> call=(goal,mode,scope)=>Obj(registry.Dispatch(context,"agentLocalPlan",Obj(new{goal,scope=scope??ScopeObj(mode)})).GetAwaiter().GetResult());
        var response=call("资料库里有多少 PDF","all",null);
        check(Convert.ToBoolean(response["handled"])==true&&Convert.ToBoolean(response["local"])==true&&Convert.ToString(response["kind"])=="count"&&Convert.ToInt32(response["total"])==1,"agentLocalPlan returns the documented handled/local/count contract");
        check(response.ContainsKey("stats")&&response.ContainsKey("items")&&response.ContainsKey("truncated")&&response.ContainsKey("followups")&&response.ContainsKey("title")&&response.ContainsKey("summary"),"agentLocalPlan response keeps every documented field");
        var query=AgentTaskState.Get<Dictionary<string,object>>(response,"query");
        check(Convert.ToString(query["text"])=="资料库里有多少 PDF"&&Convert.ToString(query["scopeLabel"])=="整个资料库","agentLocalPlan echoes the goal and the resolved scope label");
        response=call("总结这些视频","all",null);
        check(Convert.ToBoolean(response["handled"])==false&&Convert.ToBoolean(response["local"])==true&&Convert.ToString(response["reason"])=="generative_or_write","agentLocalPlan answers generative requests with an explicit local=false decision");
        response=call("统计视频数量","collection",ScopeObj("collection","旅行"));
        check(Convert.ToBoolean(response["handled"])==true&&Convert.ToInt32(response["total"])==2&&Convert.ToString(AgentTaskState.Get<Dictionary<string,object>>(response,"query")["scopeLabel"])=="分类「旅行」","agentLocalPlan resolves a collection scope through ResolveAgentScope");
        check(Rejects(()=>call("统计视频数量","selected",ScopeObj("selected","",new[]{new string('a',32)}))),"agentLocalPlan rejects unknown selected IDs before reading entries");
        check(Rejects(()=>registry.Dispatch(context,"agentLocalPlan",Obj(new{goal="统计视频数量"})).GetAwaiter().GetResult()),"agentLocalPlan requires a scope object");
        check(Rejects(()=>registry.Dispatch(context,"agentLocalPlan",Obj(new{goal="统计视频数量",scope=ScopeObj("everything")})).GetAwaiter().GetResult()),"agentLocalPlan rejects an invalid scope mode");
        check(Rejects(()=>registry.Dispatch(context,"agentLocalPlan",Obj(new{goal="",scope=ScopeObj("all")})).GetAwaiter().GetResult()),"agentLocalPlan rejects an empty goal");
        lib.SetAppMode("pure",false);
        bool pure=false;try{call("统计视频数量","all",null);}catch(InvalidOperationException error){pure=error.Message.Contains("纯净模式");}
        check(pure,"Pure mode keeps the existing localized AI-mode error for agentLocalPlan");
        lib.SetAppMode("smart",true);
        string privateRoot=Path.Combine(root,"private");
        using(var vault=VaultSession.Create(privateRoot,"OnlyForLocalFixture928!")){
            var secret=new Library(privateRoot,vault);
            var privateContext=new ModuleContext(secret,null,new ModulePolicy(secret),null,CancellationToken.None,null,new Operations(secret,null),null);
            check(Rejects(()=>registry.Dispatch(privateContext,"agentLocalPlan",Obj(new{goal="统计视频数量",scope=ScopeObj("all")})).GetAwaiter().GetResult()),"a private library keeps rejecting agentLocalPlan before any read");
        }
    }
}
