using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace kok {
    public partial class TranslationService {
        static void AddTokenLimit(Dictionary<string,object> body,AiConfig c,int limit){string parameter=c.tokenParameter;if(parameter=="auto"||String.IsNullOrEmpty(parameter))parameter=Endpoint(c.endpoint).Host.Equals("api.openai.com",StringComparison.OrdinalIgnoreCase)?"max_completion_tokens":"max_tokens";body.Remove("max_tokens");body.Remove("max_completion_tokens");body[parameter]=limit;}
        AiConfig RequestConfig(Dictionary<string,object> values) {
            AiConfig current=ConfigSnapshot(),next=ConfigSnapshot();next.endpoint=Value(values,"endpoint",current.endpoint).Trim();Uri endpoint=Endpoint(next.endpoint);
            string key=Value(values,"apiKey","").Trim();bool clear=values.ContainsKey("clearKey")&&Convert.ToBoolean(values["clearKey"]);
            if(clear)next.protectedKey="";
            if(key.Length>0){if(key.Length>4096||key.Any(Char.IsControl))throw new InvalidDataException("API 密钥格式无效。");next.protectedKey=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key),null,DataProtectionScope.CurrentUser));}
            else if(!clear&&!String.IsNullOrEmpty(current.protectedKey)&&endpoint!=Endpoint(current.endpoint))throw new InvalidDataException("更换接口地址时，请重新填写对应服务的密钥。");
            return next;
        }
        public async Task<List<string>> ModelsAsync(Dictionary<string,object> values,CancellationToken token){
            AiConfig c=RequestConfig(values);string complete=Endpoint(c.endpoint).AbsoluteUri;var endpoint=new Uri(complete.Substring(0,complete.Length-"/chat/completions".Length)+"/models");string key=ReadKey(c);
            using(var handler=handlerFactory!=null?handlerFactory():new HttpClientHandler{AllowAutoRedirect=false,UseProxy=!endpoint.IsLoopback})using(var client=new HttpClient(handler))using(var request=new HttpRequestMessage(HttpMethod.Get,endpoint))using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
                timeout.CancelAfter(TimeSpan.FromSeconds(25));client.Timeout=TimeSpan.FromSeconds(30);if(key!="")request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
                try{using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token)){
                    if(!response.IsSuccessStatusCode){if((int)response.StatusCode==404|| (int)response.StatusCode==405)throw new InvalidDataException("此服务不提供模型列表，请手动填写模型名称。");throw new InvalidDataException(HttpError((int)response.StatusCode));}
                    using(var stream=await response.Content.ReadAsStreamAsync())using(var buffer=new MemoryStream()){var bytes=new byte[8192];int count;while((count=await stream.ReadAsync(bytes,0,bytes.Length,timeout.Token))>0){if(buffer.Length+count>2*1024*1024)throw new InvalidDataException("模型列表过大，请手动填写模型名称。");buffer.Write(bytes,0,count);}
                        Dictionary<string,object> root;try{root=Library.Json.DeserializeObject(Encoding.UTF8.GetString(buffer.ToArray())) as Dictionary<string,object>;}catch{throw new InvalidDataException("模型列表格式无效，请手动填写模型名称。");}var rows=root!=null&&root.ContainsKey("data")?root["data"] as object[]:null;if(rows==null)throw new InvalidDataException("模型列表格式无效，请手动填写模型名称。");
                        return rows.OfType<Dictionary<string,object>>().Where(x=>x.ContainsKey("id")&&x["id"] is string).Select(x=>(string)x["id"]).Where(x=>x.Length>0&&x.Length<=200&&!x.Any(Char.IsControl)).Distinct().OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).Take(500).ToList();
                    }
                }}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;throw new InvalidDataException("读取模型列表超时，请稍后再试。");}catch(HttpRequestException){throw new InvalidDataException("无法连接翻译服务，请检查接口地址与网络。");}
            }
        }
    }
}
