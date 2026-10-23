using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using kok;

static class OrganizationTests {
    class Handler:HttpMessageHandler {
        public static int calls;public static string payload="",mode="ok";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
            calls++;if(mode=="wait")await Task.Delay(10000,token);
            var body=(Dictionary<string,object>)Library.Json.DeserializeObject(await request.Content.ReadAsStringAsync());var messages=(object[])body["messages"];payload=Convert.ToString(((Dictionary<string,object>)messages[1])["content"]);
            var input=((object[])Library.Json.DeserializeObject(payload)).Cast<Dictionary<string,object>>().ToList();
            var rows=input.Select((x,n)=>new{id=mode=="bad"?0:n,category="学习资料",tags=new[]{"学习","资料"},summary="测试文件摘要"}).Reverse().ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Library.Json.Serialize(new{choices=new[]{new{finish_reason="stop",message=new{content=Library.Json.Serialize(new{items=rows})}}}}),Encoding.UTF8,"application/json")};
        }
    }
    public static void Run(string directory,Action<bool,string> check) {
        string files=Path.Combine(directory,"资料"),nested=Path.Combine(files,"子文件夹"),data=Path.Combine(files,"index-data");Directory.CreateDirectory(nested);
        string text=Path.Combine(files,"阅读.txt"),md=Path.Combine(nested,"笔记.md");File.WriteAllText(text,"阅读正文",Encoding.UTF8);File.WriteAllText(md,"# 学习笔记",Encoding.UTF8);File.WriteAllText(Path.Combine(files,"ignore.bin"),"x");
        var lib=new Library(data);var folder=lib.AddFolder(files);lib.AddFolder(nested);lib.AddFolder(files);
        var result=lib.ScanFolders(Path.Combine(directory,"app"),CancellationToken.None,null);
        check(lib.Folders().Count==2&&result.added==2&&lib.Db.items.Count==2,"watched folders recursively classify files, skip own data and deduplicate overlaps");
        string removed=lib.Db.items.First(x=>x.path==text).id;lib.Remove(removed);lib=new Library(data);
        check(lib.ScanFolders(Path.Combine(directory,"app"),CancellationToken.None,null).added==0&&File.Exists(text),"removed entries stay excluded from automatic import after restart");
        lib.Import(new[]{text},null);check(!lib.Db.ignoredAutoPaths.Contains(text),"manual import can restore a file excluded from automatic scans");
        foreach(var f in lib.Folders())lib.ChangeFolder(f.id,false);File.WriteAllText(Path.Combine(files,"新增.txt"),"new");
        check(lib.ScanFolders(Path.Combine(directory,"app"),CancellationToken.None,null).added==0,"paused folder sources do not import new files");
        lib.ChangeFolder(folder.id,true);check(lib.ScanFolders(Path.Combine(directory,"app"),CancellationToken.None,null).added==1,"resuming a source discovers new files without duplicates");
        var canceled=new CancellationTokenSource();canceled.Cancel();bool rejected=false;try{lib.ScanFolders(Path.Combine(directory,"app"),canceled.Token,null);}catch(OperationCanceledException){rejected=true;}check(rejected,"folder scanning honors cancellation");
        var ids=lib.Db.items.Take(2).Select(x=>x.id).ToList();var inputs=lib.OrganizeInputs(ids,false);var service=new TranslationService(Path.Combine(directory,"ai"),()=>new Handler());service.SaveConfig(new Dictionary<string,object>{{"endpoint","http://127.0.0.1:1234/v1"},{"model","test"}});
        var suggestions=service.OrganizeAsync(inputs,false,false,CancellationToken.None,(a,b)=>{}).GetAwaiter().GetResult();
        check(suggestions[0].id==ids[0]&&suggestions.Count==2&&!Handler.payload.Contains(directory)&&!Handler.payload.Contains("阅读正文")&&lib.Db.items.All(x=>x.aiCategory==""),"AI metadata request omits full paths and text, retains file order and leaves preview unapplied");
        service.OrganizeAsync(inputs,false,false,CancellationToken.None,(a,b)=>{}).GetAwaiter().GetResult();check(Handler.calls==1,"organization cache avoids repeated API requests");
        lib.ApplyOrganization(suggestions,ids);var restored=new Library(data);check(restored.Find(ids[0]).tags.Count==2&&restored.Find(ids[0]).collection=="学习资料"&&File.ReadAllText(text)=="阅读正文","applying AI categories and tags persists without modifying original files");
        lib.EditOrganization(ids[0],"手动分类",new List<string>{"个人标签"},"我的摘要");check(new Library(data).Find(ids[0]).tags[0]=="个人标签","AI metadata remains manually editable");
        var content=lib.OrganizeInputs(ids,true);check(content.Any(x=>!String.IsNullOrEmpty(x.excerpt))&&content.All(x=>x.excerpt.Length<=1200),"text excerpts require explicit content mode and are bounded");
        File.AppendAllText(lib.Find(ids[0]).path," changed");rejected=false;try{lib.ApplyOrganization(suggestions,ids);}catch(InvalidDataException){rejected=true;}check(rejected&&lib.Find(ids[0]).collection=="手动分类","changed source files invalidate previews before any categories are applied");
        Handler.mode="bad";rejected=false;try{service.OrganizeAsync(content,true,false,CancellationToken.None,(a,b)=>{}).GetAwaiter().GetResult();}catch(InvalidDataException){rejected=true;}check(rejected,"duplicate AI response IDs are rejected without applying results");
        Handler.mode="wait";var cancel=new CancellationTokenSource();cancel.CancelAfter(100);rejected=false;try{service.OrganizeAsync(content,true,true,cancel.Token,(a,b)=>{}).GetAwaiter().GetResult();}catch(OperationCanceledException){rejected=true;}check(rejected,"AI analysis cancellation prevents stale results");
    }
}
