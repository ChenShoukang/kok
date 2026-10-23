using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using kok;

static class TranslationTests {
    class Handler:HttpMessageHandler {
        public static int calls;public static bool authorized;public static string mode="ok";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
            calls++;authorized=request.Headers.Authorization!=null;
            if(mode=="wait")await Task.Delay(10000,token);
            if(mode=="429")return new HttpResponseMessage((HttpStatusCode)429){Content=new StringContent("untrusted server detail")};
            var body=(Dictionary<string,object>)Library.Json.DeserializeObject(await request.Content.ReadAsStringAsync());var messages=(object[])body["messages"];var input=(object[])Library.Json.DeserializeObject(Convert.ToString(((Dictionary<string,object>)messages[1])["content"]));
            var rows=input.Cast<Dictionary<string,object>>().Reverse().Select(row=>new{id=row["id"],text="译文："+row["text"]}).ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Library.Json.Serialize(new{choices=new[]{new{finish_reason="stop",message=new{content=Library.Json.Serialize(new{translations=rows})}}}}),Encoding.UTF8,"application/json")};
        }
    }
    public static void Run(string directory,Action<bool,string> check) {
        var service=new TranslationService(directory,()=>new Handler());string marker=Guid.NewGuid().ToString("N");
        service.SaveConfig(new Dictionary<string,object>{{"endpoint","http://127.0.0.1:1234/v1"},{"model","test-model"},{"apiKey",marker},{"language","简体中文"}});
        check(!File.ReadAllText(Path.Combine(directory,"ai-settings.json")).Contains(marker)&&!Library.Json.Serialize(service.PublicConfig()).Contains(marker),"AI credentials encrypted at rest and omitted from UI configuration");
        service.SaveConfig(new Dictionary<string,object>{{"model","test-model"},{"apiKey",""}});
        var input=Enumerable.Range(0,70).Select(n=>"字幕 "+n).ToList();int completed=0;var result=service.TranslateAsync(input,CancellationToken.None,(done,total)=>completed=done).GetAwaiter().GetResult();
        check(result.parts.Count==70&&result.parts[31]=="译文：字幕 31"&&Handler.calls==3&&completed==70&&Handler.authorized,"batched translations retain order and report progress with native authentication");
        var again=new TranslationService(directory,()=>new Handler()).TranslateAsync(input,CancellationToken.None,(a,b)=>{}).GetAwaiter().GetResult();
        check(again.cached&&Handler.calls==3,"persistent translation cache avoids duplicate API requests");
        var changed=new List<string>{"new source"};service.TranslateAsync(changed,CancellationToken.None,(a,b)=>{}).GetAwaiter().GetResult();check(Handler.calls==4,"edited source invalidates translation cache");
        bool rejected=false;try{service.SaveConfig(new Dictionary<string,object>{{"endpoint","https://other.example/v1"}});}catch(InvalidDataException){rejected=true;}check(rejected,"changing API destination requires re-entering the corresponding credential");
        rejected=false;try{TranslationService.Endpoint("http://untrusted.example/v1");}catch(InvalidDataException){rejected=true;}check(rejected&&TranslationService.Endpoint("http://localhost:1234/v1").AbsolutePath=="/v1/chat/completions","remote HTTP rejected and local model endpoint supported");
        string unicode=String.Concat(Enumerable.Repeat("Hello 😀\n",800));var split=TranslationService.SplitText(unicode);check(String.Concat(split)==unicode&&split.All(p=>p.Length<=1800&&!Char.IsHighSurrogate(p[p.Length-1])),"long documents split without losing text or breaking Unicode characters");
        rejected=false;try{TranslationService.ParseResponse("{\"choices\":[{\"message\":{\"content\":\"{\\\"translations\\\":[{\\\"id\\\":0,\\\"text\\\":\\\"a\\\"},{\\\"id\\\":0,\\\"text\\\":\\\"b\\\"}]}\"}}]}",2);}catch(InvalidDataException){rejected=true;}check(rejected,"incomplete or duplicate model results rejected before caching");
        Handler.mode="429";rejected=false;try{service.TranslateAsync(new List<string>{"rate test"},CancellationToken.None,(a,b)=>{}).GetAwaiter().GetResult();}catch(InvalidDataException ex){rejected=ex.Message.Contains("额度")&&!ex.Message.Contains("untrusted");}check(rejected,"rate and quota failures show a safe actionable error without server body");
        Handler.mode="wait";using(var cancel=new CancellationTokenSource()){cancel.CancelAfter(30);rejected=false;try{service.TranslateAsync(new List<string>{"cancel test"},cancel.Token,(a,b)=>{}).GetAwaiter().GetResult();}catch(OperationCanceledException){rejected=true;}check(rejected,"cancellation interrupts in-flight model requests");}Handler.mode="ok";
        service.SaveConfig(new Dictionary<string,object>{{"clearKey",true}});check(!Library.Json.Serialize(service.PublicConfig()).Contains("\"hasKey\":true"),"stored API credential can be removed");
    }
}
