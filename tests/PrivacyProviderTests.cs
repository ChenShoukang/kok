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

static class PrivacyProviderTests {
    class Handler:HttpMessageHandler {
        public static int calls;public static string address,mode="ok";public static bool authenticated;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){calls++;address=request.RequestUri.AbsoluteUri;authenticated=request.Headers.Authorization!=null;
            if(mode=="redirect")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(mode=="bad"?"untrusted server text":Library.Json.Serialize(new{data=new[]{new{id="vision-model"},new{id="text-model"},new{id="vision-model"}}}),Encoding.UTF8,"application/json")});}
    }
    public static void Run(string root,Action<bool,string> check){
        Directory.CreateDirectory(root);var service=new TranslationService(Path.Combine(root,"ai"),()=>new Handler());service.SaveConfig(new Dictionary<string,object>{{"endpoint","http://127.0.0.1:1234/v1"},{"model","test-model"},{"apiKey","synthetic-test-key"}});string config=File.ReadAllText(Path.Combine(root,"ai","ai-settings.json"));
        var models=service.ModelsAsync(new Dictionary<string,object>(),CancellationToken.None).GetAwaiter().GetResult();check(models.SequenceEqual(new[]{"text-model","vision-model"})&&Handler.address=="http://127.0.0.1:1234/v1/models"&&Handler.authenticated,"model discovery uses the selected API, authenticates natively and deduplicates IDs");
        bool rejected=false;int before=Handler.calls;try{service.ModelsAsync(new Dictionary<string,object>{{"endpoint","https://other.example/v1"}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){rejected=true;}check(rejected&&Handler.calls==before,"model discovery never forwards an existing credential to a changed endpoint");
        service.ModelsAsync(new Dictionary<string,object>{{"endpoint","http://localhost:11434/v1"},{"clearKey",true}},CancellationToken.None).GetAwaiter().GetResult();check(!Handler.authenticated&&File.ReadAllText(Path.Combine(root,"ai","ai-settings.json"))==config,"local model discovery can omit authentication without overwriting saved configuration");
        Handler.mode="bad";rejected=false;try{service.ModelsAsync(new Dictionary<string,object>(),CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException ex){rejected=!ex.Message.Contains("untrusted");}check(rejected,"invalid model-list responses do not expose server content");Handler.mode="ok";
        var normal=new Library(Path.Combine(root,"normal"));rejected=false;try{normal.RequireAiMode();}catch(InvalidOperationException){rejected=true;}check(rejected,"an unconfigured application never enables AI implicitly");normal.SetAppMode("smart",true);normal.RequireAiMode();normal.SetAppMode("pure",false);check(!new Library(normal.DataDir).AiEnabled,"pure mode and AI request restrictions persist after restart");
        string vaultDir=Path.Combine(root,"private"),password="test-only-password-"+Guid.NewGuid().ToString("N"),marker="private-note-"+Guid.NewGuid().ToString("N");string source=Path.Combine(root,"original.txt");File.WriteAllText(source,"Original text remains in place.");byte[] saved;
        using(var session=VaultSession.Create(vaultDir,password)){
            var lib=new Library(vaultDir,session);lib.Import(new[]{source},null);var item=lib.Db.items[0];lib.Update(item.id,new Dictionary<string,object>{{"notes",marker}});saved=File.ReadAllBytes(Path.Combine(vaultDir,"library.vault"));
            check(lib.IsPrivate&&!lib.AiEnabled&&!File.Exists(Path.Combine(vaultDir,"library.json"))&&!Encoding.UTF8.GetString(saved).Contains(marker),"private indexes and notes are encrypted and never written as library.json");
            string video=Path.Combine(root,"original.webm");File.WriteAllText(video,"original movie");lib.Import(new[]{video},null);var entry=lib.Db.items.First(x=>x.kind=="video");byte[] jpeg;using(var image=new System.Drawing.Bitmap(12,8))using(var buffer=new MemoryStream()){image.Save(buffer,System.Drawing.Imaging.ImageFormat.Jpeg);jpeg=buffer.ToArray();}
            lib.SaveVideoCover(entry.id,"data:image/jpeg;base64,"+Convert.ToBase64String(jpeg));var encrypted=File.ReadAllBytes(lib.ThumbnailPath(entry));using(var clear=lib.OpenThumbnail(entry))using(var decoded=System.Drawing.Image.FromStream(clear))check(decoded.Width==12&&Encoding.ASCII.GetString(encrypted,0,5)=="KOKV1","private video covers are encrypted on disk and decode only through the unlocked library");
            rejected=false;try{lib.SetAppMode("smart",true);}catch(InvalidOperationException){rejected=true;}check(rejected&&normal.Db.items.Count==0,"private and normal libraries stay separate and private mode cannot enable AI");
        }
        rejected=false;try{using(var wrong=VaultSession.Open(vaultDir,"wrong-test-password")){}}catch(InvalidDataException){rejected=true;}check(rejected,"incorrect private passwords never unlock the vault");
        using(var session=VaultSession.Open(vaultDir,password)){var lib=new Library(vaultDir,session);check(lib.Db.items.Any(x=>x.notes==marker)&&File.ReadAllText(source)=="Original text remains in place.","private notes survive locking and unlocking while originals remain unchanged");var bytes=File.ReadAllBytes(Path.Combine(vaultDir,"library.vault"));bytes[30]^=1;rejected=false;try{session.Unseal(bytes,"library");}catch(InvalidDataException){rejected=true;}check(rejected,"authenticated encryption detects private-data tampering");rejected=false;try{session.Unseal(saved,"another-purpose");}catch(InvalidDataException){rejected=true;}check(rejected,"private encrypted data cannot be swapped between purposes");}
        var locked=VaultSession.Open(vaultDir,password);locked.Dispose();rejected=false;try{locked.Unseal(saved,"library");}catch(InvalidOperationException){rejected=true;}check(rejected,"locking disposes encryption keys and rejects later decryption");
        check(!File.ReadAllText(Path.Combine(vaultDir,"vault-info.json")).Contains(password),"private passwords are absent from vault metadata");
    }
}
