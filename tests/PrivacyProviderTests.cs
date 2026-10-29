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
        PrivateTransfer(Path.Combine(root,"transfer"),check);
    }
    static void PrivateTransfer(string root,Action<bool,string> check){
        Directory.CreateDirectory(root);string folder=Path.Combine(root,"normal"),vaultDir=Path.Combine(root,"vault"),password="test-only-transfer-password";
        string movie=Path.Combine(root,"movie.webm"),other=Path.Combine(root,"other.txt"),secret="private-transfer-"+Guid.NewGuid().ToString("N"),keep="keep-other-"+Guid.NewGuid().ToString("N"),oldIndex="old-index-"+Guid.NewGuid().ToString("N");
        File.WriteAllText(movie,"original video bytes");File.WriteAllText(other,"unrelated original text");var normal=new Library(folder);normal.Import(new[]{movie,other},null);
        var entry=normal.Db.items.Single(i=>i.kind=="video");var unrelated=normal.Db.items.Single(i=>i.path==other);
        normal.Update(entry.id,new Dictionary<string,object>{{"notes",secret}});normal.Update(unrelated.id,new Dictionary<string,object>{{"notes",keep}});
        byte[] jpeg;using(var image=new System.Drawing.Bitmap(17,11))using(var bytes=new MemoryStream()){image.Save(bytes,System.Drawing.Imaging.ImageFormat.Jpeg);jpeg=bytes.ToArray();}
        normal.SaveVideoCover(entry.id,"data:image/jpeg;base64,"+Convert.ToBase64String(jpeg));byte[] originalCover=File.ReadAllBytes(normal.ThumbnailPath(entry));
        normal.IndexItem(entry,oldIndex);normal.IndexItem(entry,secret);normal.CacheRecipeSummary("unrelated-cache",keep);
        string clipId=Guid.NewGuid().ToString("N"),spaceId=Guid.NewGuid().ToString("N");normal.OperationApply(normal.ProposeResource("clip",clipId,Library.Json.Serialize(new ClipRecord{id=clipId,title="clip",text=secret,source=new SourceAnchor{itemId=entry.id}})).id);normal.OperationApply(normal.ProposeResource("space",spaceId,Library.Json.Serialize(new SpaceRecord{id=spaceId,name="mixed",members=new List<string>{entry.id,unrelated.id,clipId}})).id);
        string view;using(var hash=System.Security.Cryptography.SHA256.Create())view=Path.Combine(folder,"translations","view-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(entry.id))).Replace("-","").ToLowerInvariant()+".json");Directory.CreateDirectory(Path.GetDirectoryName(view));File.WriteAllText(view,secret);
        var legacy=Library.Json.Deserialize<Dictionary<string,object>>(normal.Snapshot());legacy["unknownMetadata"]="must survive";foreach(var name in new[]{"library.json","library.json.bak","library.json.pre-0.8.1"})File.WriteAllText(Path.Combine(folder,name),Library.Json.Serialize(legacy));
        new Library(folder).Save(false); // a backup containing the old private note
        using(var session=VaultSession.Create(vaultDir,password)){
            var target=new Library(vaultDir,session);normal.TransferToPrivate(target,new[]{entry.id});var copy=target.Db.items.Single();
            check(copy.id!=entry.id&&copy.notes==secret&&copy.hasCover,"private transfer retains metadata with a new vault-local identity");
            using(var bytes=new MemoryStream()){using(var cover=target.OpenThumbnail(copy))cover.CopyTo(bytes);check(bytes.ToArray().SequenceEqual(originalCover)&&!File.ReadAllBytes(target.ThumbnailPath(copy)).SequenceEqual(originalCover),"private transfer encrypts the exact existing cover instead of discarding it");}
            check(target.Db.workspace.clips.Single().text==secret&&target.Db.workspace.clips.Single().source.itemId==copy.id,"private transfer remaps associated clips into the encrypted library");
        }
        var reopened=new Library(folder);check(reopened.Find(entry.id)==null&&reopened.Find(unrelated.id).notes==keep&&reopened.CachedRecipeSummary("unrelated-cache")==keep,"private cleanup preserves unrelated entries and AI caches");
        check(!File.Exists(view)&&!File.Exists(normal.ThumbnailPath(entry))&&!File.Exists(Path.Combine(folder,"private-move.pending.json"))&&reopened.Db.workspace.spaces.Single().members.SequenceEqual(new[]{unrelated.id}),"private cleanup removes only transferred covers, saved translations and space memberships");
        check(reopened.OperationList().Any(o=>o.patches.Any(p=>p.id==unrelated.id)||o.resources.Any(p=>p.kind=="item"&&p.id==unrelated.id)),"private history redaction preserves unrelated edits and mixed import history");
        check(reopened.FullTextSearch(secret).Count==0&&reopened.FullTextSearch(oldIndex).Count==0&&!reopened.Snapshot().Contains(secret)&&reopened.Snapshot().Contains(keep),"private cleanup redacts transferred history and both full-text indexes while keeping unrelated history");
        var managed=new[]{"library.sqlite","library.sqlite.bak","library.json","library.json.bak","library.json.pre-0.8.1"};check(managed.All(name=>!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder,name))).Contains(secret)),"current database, compact backup and migration snapshots contain no transferred note plaintext");
        check(new[]{"library.json","library.json.bak","library.json.pre-0.8.1"}.All(name=>File.ReadAllText(Path.Combine(folder,name)).Contains(keep)&&File.ReadAllText(Path.Combine(folder,name)).Contains("must survive")),"migration snapshot cleanup keeps unrelated records and unknown metadata");
        check(File.ReadAllText(movie)=="original video bytes"&&File.ReadAllText(other)=="unrelated original text","private transfer never edits original media or unrelated source files");
        using(var session=VaultSession.Open(vaultDir,password)){var target=new Library(vaultDir,session);check(target.Db.items.Single().notes==secret,"transferred metadata survives locking and reopening the encrypted vault");
            var duplicate=new Library(Path.Combine(root,"duplicate"));duplicate.Import(new[]{movie},null);var item=duplicate.Db.items.Single();duplicate.Update(item.id,new Dictionary<string,object>{{"notes","different normal version"}});bool rejected=false;try{duplicate.TransferToPrivate(target,new[]{item.id});}catch(InvalidDataException){rejected=true;}
            check(rejected&&new Library(duplicate.DataDir).Find(item.id).notes=="different normal version"&&target.Db.items.Single().notes==secret,"duplicate private paths reject the whole move without losing either version");
        }
        string blocked=Path.Combine(root,"blocked.webm");File.WriteAllText(blocked,"blocked cover fixture");reopened.Import(new[]{blocked},null);var interrupted=reopened.Db.items.Single(i=>i.path==blocked);reopened.Update(interrupted.id,new Dictionary<string,object>{{"notes","resume-cleanup-note"}});reopened.SaveVideoCover(interrupted.id,"data:image/jpeg;base64,"+Convert.ToBase64String(jpeg));bool failed=false;
        using(var session=VaultSession.Open(vaultDir,password))using(var held=File.Open(reopened.ThumbnailPath(interrupted),FileMode.Open,FileAccess.Read,FileShare.Read)){try{reopened.TransferToPrivate(new Library(vaultDir,session),new[]{interrupted.id});}catch(IOException){failed=true;}check(failed&&File.Exists(Path.Combine(folder,"private-move.pending.json")),"a locked cover reports incomplete cleanup and retains an ID-only retry journal");}
        var recovered=new Library(folder);using(var session=VaultSession.Open(vaultDir,password)){var target=new Library(vaultDir,session);check(recovered.Find(interrupted.id)==null&&!File.Exists(reopened.ThumbnailPath(interrupted))&&!File.Exists(Path.Combine(folder,"private-move.pending.json"))&&target.Db.items.Single(i=>i.path==blocked).notes=="resume-cleanup-note"&&recovered.Find(unrelated.id).notes==keep,"restart finishes interrupted cleanup after the encrypted copy was saved and preserves unrelated data");}
    }
}
