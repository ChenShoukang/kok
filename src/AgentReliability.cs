using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
 public sealed class AgentHttpFailure:AgentFailure {
  public AgentHttpFailure(string kind,string message,int status,bool retryable=false,int retryAfterMs=0):base(kind,message,retryable,status,retryAfterMs){}
  public static async Task<AgentHttpFailure> Read(HttpResponseMessage response,CancellationToken token){
   string code="",param="",message="";int status=(int)response.StatusCode,after=0;
   var retry=response.Headers.RetryAfter;if(retry!=null){double seconds=retry.Delta.HasValue?retry.Delta.Value.TotalSeconds:retry.Date.HasValue?(retry.Date.Value-DateTimeOffset.UtcNow).TotalSeconds:0;after=(int)Math.Max(0,Math.Min(3600000,seconds*1000));}
   // Provider error text is inspected in memory only. It may echo prompts, paths or secrets.
   try{using(var stream=await response.Content.ReadAsStreamAsync())using(token.Register(()=>stream.Dispose()))using(var reader=new StreamReader(stream,Encoding.UTF8)){
    var text=new StringBuilder();var buffer=new char[1024];while(text.Length<8192){int n=await reader.ReadAsync(buffer,0,Math.Min(buffer.Length,8192-text.Length));if(n==0)break;text.Append(buffer,0,n);}
    var root=Library.Json.Deserialize<Dictionary<string,object>>(text.ToString());object value;var error=root.TryGetValue("error",out value)?value as Dictionary<string,object>:null;
    if(error!=null){code=Value(error,"code").ToLowerInvariant();param=Value(error,"param").ToLowerInvariant();message=Value(error,"message").ToLowerInvariant();}
   }}catch(OperationCanceledException){throw;}catch{token.ThrowIfCancellationRequested();}
   if(code=="insufficient_quota"||code=="billing_hard_limit_reached")return new AgentHttpFailure("budget","翻译服务额度不足或请求过于频繁，请检查账户后重试。",status);
   if(status==401||status==403)return new AgentHttpFailure("authentication","翻译服务认证失败，请检查密钥、模型权限与账户状态。",status);
   if(code=="context_length_exceeded"||code=="max_tokens_exceeded")return new AgentHttpFailure("context","Agent 上下文超过模型限制，请缩小范围或开启新任务。",status);
   bool toolParam=param=="tools"||param=="tool_choice"||param=="functions";
   bool explicitUnsupported=(new[]{"unsupported_parameter","unknown_parameter","unrecognized_request_argument","unsupported_value"}.Contains(code)&&toolParam)||((toolParam||message.Contains("tools")||message.Contains("tool_choice"))&&(message.Contains("does not support")||message.Contains("not supported")||message.Contains("unsupported parameter")||message.Contains("unrecognized parameter")));
   if((status==400||status==422)&&explicitUnsupported)return new AgentHttpFailure("tools_unsupported","当前模型或接口不支持工具调用，已改用兼容协议。",status);
   string textError=status==429?"翻译服务额度不足或请求过于频繁，请检查账户后重试。":status==404?"未找到翻译接口或模型，请检查 API 地址和模型名称。":status>=300&&status<400?"翻译接口发生重定向，请在设置中填写最终接口地址。":"翻译服务请求失败（HTTP "+status+"），请检查接口配置或稍后重试。";
   return new AgentHttpFailure(status==429?"rate_limit":status>=500?"service":"request",textError,status,status==408||status==425||status==429||status>=500&&status<=599,after);
  }
  static string Value(Dictionary<string,object> row,string key){object value;return row.TryGetValue(key,out value)?Convert.ToString(value)??"":"";}
 }
 public static class AgentRetry {
  public const int MaxRetries=2;
  public static int Delay(AgentFailure failure,int retry){return Math.Max(failure.retryAfterMs,500*(1<<Math.Min(retry,3))+Math.Abs(Guid.NewGuid().GetHashCode()%151));}
  public static bool Allowed(AgentFailure failure,int retry,int requests,int limit){return failure.retryable&&retry<MaxRetries&&requests<limit&&failure.retryAfterMs<=30000;}
 }
 public sealed class AgentConflictException:InvalidOperationException {public AgentConflictException(string message):base(message){}}
}
