using System;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using SharpToken;

namespace kok {
 // A script-aware estimate, not a provider tokenizer. All prompts/tools are included at preflight.
 internal static class AgentTokenBudget {
  internal const string Method="tiktoken-cl100k-v1";
  static GptEncoding encoder;static readonly object encoderGate=new object();
  static GptEncoding Encoder(){lock(encoderGate)return encoder??(encoder=GptEncoding.GetEncoding("cl100k_base"));}
  internal static int Estimate(string text){
   if(String.IsNullOrEmpty(text))return 0;
   try{var tokens=Encoder().Encode(text,new HashSet<string>(),new HashSet<string>());if(tokens!=null&&tokens.Count>0)return Math.Max(1,(int)Math.Ceiling(tokens.Count*1.4));}catch{}
   return EstimateFallback(text);
  }
  static int EstimateFallback(string text){
   if(String.IsNullOrEmpty(text))return 0;double count=0;int latin=0;
   Action flush=()=>{if(latin>0){count+=Math.Ceiling(latin/3.5d);latin=0;}};
   for(int i=0;i<text.Length;i++){
    char c=text[i];if(c<128&&Char.IsLetterOrDigit(c)){latin++;continue;}flush();
    if(Char.IsWhiteSpace(c)){count+=.15;continue;}
    if(c>=0x3400&&c<=0x9fff||c>=0xf900&&c<=0xfaff){count+=1.25;continue;}
    if(Char.IsHighSurrogate(c)&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1])){count+=3;i++;continue;}
    count+=c<128?.6:Char.IsLetterOrDigit(c)?1.25:1;
   }flush();return Math.Max(1,(int)Math.Ceiling(count));
  }
  internal static double Calibration(AgentTaskState task){
   var rows=AgentTrace.Requests(task).Where(r=>!r.estimated&&r.inputEstimate>0&&r.inputTokens>0&&(double)r.inputTokens/r.inputEstimate>=.4&&(double)r.inputTokens/r.inputEstimate<=2.5).TakeLastCompat(6).ToArray();
   if(rows.Length==0)return 1;
   return Math.Max(.5,Math.Min(2,rows.Average(r=>(double)r.inputTokens/r.inputEstimate)));
  }
  internal static int ForTask(AgentTaskState task,int estimate){return Math.Max(1,(int)Math.Ceiling(estimate*Calibration(task)*1.1));}
  // Framework 4.8 has no Enumerable.TakeLast.
  static System.Collections.Generic.IEnumerable<T> TakeLastCompat<T>(this System.Collections.Generic.IEnumerable<T> rows,int n){var a=rows.ToArray();return a.Skip(Math.Max(0,a.Length-n));}
 }
}
