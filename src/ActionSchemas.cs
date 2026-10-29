using System;
using System.Linq;
using System.Collections.Generic;
namespace kok {
 internal static class ActionSchemas {
  static Dictionary<string,object> S(int n){return CapabilitySchema.Str(n);}static Dictionary<string,object> B(){return new Dictionary<string,object>{{"type","boolean"}};}
  static Dictionary<string,object> N(double min,double max,bool integer=false){return new Dictionary<string,object>{{"type",integer?"integer":"number"},{"minimum",min},{"maximum",max}};}
  static Dictionary<string,object> E(params string[] values){var s=S(100);s["enum"]=values;return s;}
  static Dictionary<string,object> A(int max,object items){return new Dictionary<string,object>{{"type","array"},{"maxItems",max},{"items",items}};}
  static Dictionary<string,object> O(string[] required,params object[] fields){return CapabilitySchema.Obj(CapabilitySchema.Fields(fields),required);}
  public static Dictionary<string,object> For(string name){
   if(name.StartsWith("command",StringComparison.Ordinal)||new[]{"moduleCatalog","agentTaskList","agentSessions","operationList","jobList"}.Contains(name))return CapabilitySchema.Obj();
   if(name=="agentTaskStart")return O(new[]{"sessionId","goal"},"sessionId",S(32),"goal",S(2000),"intent",E("assist","discuss"),"protocol",E("auto","tools","json"),"workflow",S(64),"instruction",S(800),"budget",new Dictionary<string,object>{{"type","integer"},{"enum",new[]{6000,14000,24000}}},"requestLimit",N(1,12,true),"costLimit",N(0,10000),"inputRate",N(0,10000),"outputRate",N(0,10000),"scope",O(new[]{"mode"},"mode",E("all","selected","collection"),"ids",A(24,S(32)),"collection",S(100)),"history",A(10,O(new[]{"role","content"},"role",E("user","assistant"),"content",S(6000))),"handoff",new Dictionary<string,object>{{"type","object"},{"additionalProperties",true},{"nullable",true}});
   if(name=="saveAgentSessions")return O(new[]{"text"},"text",S(1500000));
   if(name=="agentTaskStop")return O(new string[0],"taskId",S(32),"cancel",B());
   if(name=="agentTaskDecide")return O(new[]{"taskId","callId","approve"},"taskId",S(32),"callId",S(32),"approve",B());
   if(name=="agentTaskAdjust")return O(new[]{"taskId","text"},"taskId",S(32),"text",S(400));
   if(name=="agentTaskUndo")return O(new[]{"taskId","callId"},"taskId",S(32),"callId",S(32));
   if(name=="agentTaskEvents")return O(new[]{"taskId"},"taskId",S(32),"eventId",S(32));
   if(new[]{"agentTaskRead","agentTaskResume","agentTaskDelete","agentHandoffPreview"}.Contains(name))return O(new[]{"taskId"},"taskId",S(32));
   if(name=="agentHandoffPrepare")return O(new[]{"taskId","callIds"},"taskId",S(32),"callIds",A(24,S(32)),"note",S(800));
   if(name=="agentHandoffRead")return O(new[]{"handoff"},"handoff",CapabilitySchema.Obj(additional:true));
   if(new[]{"documentSource","read","subtitles","readPresentation","agentCreatedStamp"}.Contains(name))return O(new[]{"id"},"id",S(32));
   if(name=="agentUndoCreated")return O(new[]{"id","stamp"},"id",S(32),"stamp",S(100));
   if(name=="createDocument")return O(new[]{"name","format"},"name",S(100),"format",E("md","docx","txt"),"collection",S(100),"text",S(380000));
   if(name=="saveDocument")return O(new[]{"id","text","signature"},"id",S(32),"text",S(380000),"signature",S(100),"recipe",CapabilitySchema.Obj(additional:true));
   if(name=="newPresentation")return O(new[]{"name","base64"},"name",S(100),"collection",S(100),"base64",S(44*1024*1024));
   if(name=="exportPresentation")return O(new[]{"id","base64"},"id",S(32),"base64",S(44*1024*1024),"name",S(100));
   if(name=="bulkClassify")return O(new[]{"ids","collection"},"ids",A(1000,S(32)),"collection",S(100));
   if(name=="applyOrganization")return O(new[]{"ids"},"ids",A(50,S(32)));
   if(name=="editOrganization")return O(new[]{"id","category","tags","summary"},"id",S(32),"category",S(60),"tags",A(8,S(32)),"summary",S(400));
   if(new[]{"operationApply","operationUndo","operationSkip"}.Contains(name))return O(new[]{"id"},"id",S(32));
   if(name=="jobAction")return O(new[]{"id","action"},"id",S(80),"action",E("resume","pause","cancel"));
   // Compatibility actions retain their original domain validators until each is migrated.
   return CapabilitySchema.Obj(additional:true);
  }
 }
}
