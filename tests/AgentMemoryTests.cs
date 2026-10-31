using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

static class AgentMemoryTests {
 sealed class FakeEmbedding:HttpMessageHandler {
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
   string raw=await request.Content.ReadAsStringAsync();var data=Library.Json.Deserialize<Dictionary<string,object>>(raw);
   var inputs=AgentTaskState.Get<List<string>>(data,"input");var vectors=new List<double[]>();
   foreach(var text in inputs){
    if(text.Contains("咖啡")||text.Contains("提神")||text.Contains("清醒"))vectors.Add(new double[]{1,0,0});
    else if(text.Contains("摄影")||text.Contains("相机"))vectors.Add(new double[]{0,1,0});
    else vectors.Add(new double[]{0,0,1});
   }
   string json=Library.Json.Serialize(new{embeddings=vectors});
   return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
  }
 }
 sealed class ThrowingEmbedding:HttpMessageHandler {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){throw new HttpRequestException("offline");}
 }
 public static void Run(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  var lib=new Library(Path.Combine(root,"library"));lib.SetAppMode("smart",true);
  var doc=lib.CreateDocument("Coffee notes","md","","豆子与相机");
  lib.SaveEmbeddingConfig(new EmbeddingConfig{enabled=true,model="fixture",endpoint="http://127.0.0.1:11434",revision="1",minimumSimilarity=.35});
  var store=new AgentMemoryStore(lib);
  var coffee=store.Save(new AgentMemoryRecord{title="咖啡偏好",text="咖啡因让人保持清醒",ids=new List<string>{doc.id}},"");
  var photo=store.Save(new AgentMemoryRecord{title="摄影偏好",text="相机与摄影灯",ids=new List<string>{doc.id}},"");
  check(store.Search("提神饮品",new[]{doc.id}).Count==0,"lexical memory search does not match a paraphrase");
  var hybrid=store.SearchHybrid("提神饮品",new[]{doc.id},3,new LocalEmbedder(()=>new FakeEmbedding())).GetAwaiter().GetResult();
  check(hybrid.Count==1&&hybrid[0].id==coffee.id,"semantic memory recall finds a paraphrase using the local embedding model");
  var duplicate=store.Save(new AgentMemoryRecord{title="咖啡偏好",text="咖啡因让人保持清醒！",ids=new List<string>{doc.id}},"");
  check(duplicate.id==coffee.id&&store.List().Count==2,"near-duplicate approved memories merge into the existing record instead of accumulating copies");
  lib.SaveEmbeddingConfig(new EmbeddingConfig{enabled=false,model="fixture",endpoint="http://127.0.0.1:11434",revision="1"});
  var fallback=store.SearchHybrid("提神饮品",new[]{doc.id},3,new LocalEmbedder(()=>new FakeEmbedding())).GetAwaiter().GetResult();
  check(fallback.Count==0,"semantic memory recall falls back to lexical results when no embedding model is enabled");
  lib.SaveEmbeddingConfig(new EmbeddingConfig{enabled=true,model="fixture",endpoint="http://127.0.0.1:11434",revision="1",minimumSimilarity=.35});
  lib.Update(doc.id,new Dictionary<string,object>{{"notes","changed"}});
  var stale=store.SearchHybrid("咖啡",new[]{doc.id},3,new LocalEmbedder(()=>new FakeEmbedding())).GetAwaiter().GetResult();
  check(stale.All(r=>r.id!=coffee.id)&&stale.All(r=>r.id!=photo.id),"memories whose sources changed stop being recalled");
  bool survived=true;try{store.SearchHybrid("咖啡",new[]{doc.id},3,new LocalEmbedder(()=>new ThrowingEmbedding())).GetAwaiter().GetResult();}catch{survived=false;}check(survived,"embedding failures do not break memory recall");
 }
}
