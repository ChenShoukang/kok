using System;
using System.Text;
using System.Collections.Generic;

namespace kok {
 internal static class AgentPartialReply {
  // Only a top-level JSON reply string is displayable. Tool names/arguments never become prose.
  internal static string Visible(string raw,string protocol){
   if(protocol=="tools")return raw.Length>24000?raw.Substring(0,24000):raw;
   int start=raw.IndexOf('{'),depth=0;bool quoted=false,escape=false;string key="";int keyStart=-1;
   for(int i=start;i>=0&&i<raw.Length;i++){
    char c=raw[i];if(quoted){if(escape){escape=false;continue;}if(c=='\\'){escape=true;continue;}if(c=='"'){quoted=false;if(depth==1&&keyStart>=0){key=raw.Substring(keyStart,i-keyStart);int p=i+1;while(p<raw.Length&&Char.IsWhiteSpace(raw[p]))p++;if(p<raw.Length&&raw[p]==':'&&key=="reply"){p++;while(p<raw.Length&&Char.IsWhiteSpace(raw[p]))p++;if(p<raw.Length&&raw[p]=='"')return Decode(raw,p+1);}keyStart=-1;}}continue;}
    if(c=='{')depth++;else if(c=='}')depth--;else if(c=='"'){quoted=true;keyStart=depth==1?i+1:-1;}
   }return "";
  }
  static string Decode(string raw,int start){var text=new StringBuilder();for(int i=start;i<raw.Length&&text.Length<24000;i++){char c=raw[i];if(c=='"')break;if(c=='\\'){if(++i>=raw.Length)break;c=raw[i];switch(c){case '"':case '\\':case '/':break;case 'n':c='\n';break;case 'r':c='\r';break;case 't':c='\t';break;case 'b':c='\b';break;case 'f':c='\f';break;case 'u':int code;if(i+4>=raw.Length||!Int32.TryParse(raw.Substring(i+1,4),System.Globalization.NumberStyles.HexNumber,null,out code))return text.ToString();c=(char)code;i+=4;break;default:return text.ToString();}}if(c>=32||c=='\n'||c=='\r'||c=='\t')text.Append(c);}if(text.Length>0&&Char.IsHighSurrogate(text[text.Length-1]))text.Length--;return text.ToString();}
 }
 public sealed partial class AgentTaskEngine {
  readonly StringBuilder delivered=new StringBuilder();string streamRequest,streamProtocol;int streamSteering;
  internal void BeginStream(string request,string protocol){lock(gate){InterruptStream("replaced");streamRequest=request;streamProtocol=protocol;streamSteering=State.steerVersion;delivered.Clear();}}
  internal void StreamDelta(string text){lock(gate){if(streamRequest!=null&&delivered.Length<64000)delivered.Append(text,0,Math.Min(text.Length,64000-delivered.Length));}}
  void ClearStream(){streamRequest=null;delivered.Clear();}
  internal void InterruptStream(string reason){lock(gate){if(streamRequest==null)return;string text=AgentPartialReply.Visible(delivered.ToString(),streamProtocol),request=streamRequest;ClearStream();if(!String.IsNullOrWhiteSpace(text))Emit("assistant.interrupted",new{requestId=request,text,reason,steering=streamSteering,protocol=streamProtocol});}}
 }
}
