using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
 internal static class AgentDocumentTools {
  internal static AgentToolSpec[] ConcatDocumentEdits(this AgentToolSpec[] tools){return tools.Concat(new[]{Read(),Replace()}).ToArray();}
  static object Integer(string description,int minimum,int maximum){return new Dictionary<string,object>{{"type","integer"},{"description",description},{"minimum",minimum},{"maximum",maximum}};}
  static AgentToolSpec Read(){return new AgentToolSpec{Name="read_document",Title="读取文档原文",CapabilityId="documents.documentSource",Requires=Need.NormalLibrary,Write=false,
   Signature="read_document{id,offset?,max_chars?}",Description="Read exact source of a kok-created MD/TXT/DOCX before editing its body. Returns a bounded text window, total_chars and next_offset; notes are not document body.",
   Properties=AgentRules.Props("id",AgentRules.Id,"offset",Integer("UTF-16 source offset; default 0",0,380000),"max_chars",Integer("Source characters; default 6000",2,12000)),Required=new[]{"id"},Validate=Validate,
   Read=(lib,state,call,token)=>Task.FromResult(lib.ReadAgentDocument(call,token))};}
  static AgentToolSpec Replace(){return new AgentToolSpec{Name="replace_document_text",Title="修改文档正文",CapabilityId="documents.saveDocument",Requires=Need.NormalLibrary,Write=true,
   Signature="replace_document_text{id,old_text,new_text}",Description="PROPOSE one exact, unique text replacement in an existing kok-created MD/TXT/DOCX. Read with read_document first. Preserves other text, file identity and format. Edits document BODY, not notes. Approval required; stale content is rejected. Imported originals cannot be overwritten.",
   Properties=AgentRules.Props("id",AgentRules.Id,"old_text",AgentRules.Str("Exact nonempty source fragment, with original whitespace; must match once",12000),"new_text",AgentRules.Str("Replacement; empty deletes this fragment",20000)),Required=new[]{"id","old_text","new_text"},Validate=Validate};}
  static string Text(Dictionary<string,object> args,string key,int max,bool empty=false){object value;if(!args.TryGetValue(key,out value)||!(value is string))throw new FormatException("missing string "+key);var text=(string)value;if(text.Length>max||!empty&&String.IsNullOrWhiteSpace(text)||text.Any(c=>Char.IsControl(c)&&c!='\r'&&c!='\n'&&c!='\t'))throw new FormatException("invalid "+key);return text;}
  static void Validate(AgentAction action,Func<string,bool> exists){
   string id=Text(action.args,"id",32);if(!AgentJournal.ValidId(id)||!exists(id))throw new FormatException("unknown or out-of-scope file ID");
   if(action.tool=="replace_document_text"){if(Text(action.args,"old_text",12000)==Text(action.args,"new_text",20000,true))throw new FormatException("old_text and new_text are identical");}
   else {Number(action.args,"offset",0,0,380000);Number(action.args,"max_chars",6000,2,12000);}
  }
  internal static int Number(Dictionary<string,object> args,string key,int fallback,int min,int max){object value;if(!args.TryGetValue(key,out value))return fallback;if(!(value is int)&&!(value is long)&&!(value is decimal)&&!(value is double))throw new FormatException("invalid "+key);double number=Convert.ToDouble(value);if(Double.IsNaN(number)||Double.IsInfinity(number)||number<min||number>max||number!=Math.Floor(number))throw new FormatException("invalid "+key);return (int)number;}
 }
 public partial class Library {
  static int UniqueDocumentMatch(string text,string old){int at=text.IndexOf(old,StringComparison.Ordinal);if(at<0)throw new InvalidOperationException("原文片段未找到，请重新读取文档后再修改。");if(text.IndexOf(old,at+1,StringComparison.Ordinal)>=0)throw new InvalidOperationException("原文片段出现多次，请提供更完整的唯一片段。");return at;}
  internal void PrepareAgentDocument(AgentCall call){
   string id=Convert.ToString(call.args["id"]),old=Convert.ToString(call.args["old_text"]),replacement=Convert.ToString(call.args["new_text"]);ManagedDocument(id);var source=DocumentSource(id);
   if(source.recovery)throw new InvalidOperationException("文档有待恢复的修改，请先在编辑器中保存或另存副本。");UniqueDocumentMatch(source.text,old);
   if(source.text.Length-old.Length+replacement.Length>DocumentTextLimit)throw new InvalidDataException("文档超过轻量编辑的大小限制。");
   call.documentSignature=source.signature;call.documentBeforeStamp=AgentCreatedStamp(id);call.beforeText=old;call.afterText=replacement;
  }
  internal Dictionary<string,object> ReadAgentDocument(AgentCall call,CancellationToken token){
   token.ThrowIfCancellationRequested();lock(gate){string id=Convert.ToString(call.args["id"]);var item=ManagedDocument(id);var source=DocumentSource(id);int offset=AgentDocumentTools.Number(call.args,"offset",0,0,DocumentTextLimit),max=AgentDocumentTools.Number(call.args,"max_chars",6000,2,12000);
    if(offset>source.text.Length)throw new InvalidDataException("文档原文偏移超出范围。");if(offset>0&&offset<source.text.Length&&Char.IsLowSurrogate(source.text[offset])&&Char.IsHighSurrogate(source.text[offset-1]))offset--;
    int end=Math.Min(source.text.Length,offset+max);if(end>offset&&end<source.text.Length&&Char.IsHighSurrogate(source.text[end-1])&&Char.IsLowSurrogate(source.text[end]))end--;
    string text=source.text.Substring(offset,end-offset);return new Dictionary<string,object>{{"text",Json.Serialize(new{id=item.id,name=item.name,offset=offset,total_chars=source.text.Length,next_offset=end<source.text.Length?end:-1,text=text,signature=source.signature,recovery=source.recovery})},{"references",new List<ChatReference>{new ChatReference{id=item.id,fileId=item.id,label=item.name,excerpt=text.Substring(0,Math.Min(160,text.Length))}}}};
   }
  }
  internal AgentToolReceipt ReplaceAgentDocument(AgentTaskState state,AgentCall call,CancellationToken token){
   lock(gate){token.ThrowIfCancellationRequested();string id=Convert.ToString(call.args["id"]);var item=ManagedDocument(id);var source=DocumentSource(id);var operation=FindOperation(call.id);
    if(operation!=null){
     if(operation.state!="applied"||operation.module!="documents"||operation.patches.Count!=1||operation.patches[0].id!=id)throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");
     var draft=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(operation.patches[0].after["editDraft"]));if(source.recovery||DraftValue(draft,"signature")!=source.signature||Json.Serialize(item.editDraft)!=Json.Serialize(operation.patches[0].after["editDraft"]))throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");
    }else{
     string expected=call.documentBeforeStamp;
     foreach(var prior in state.calls.Where(c=>c.sequence<call.sequence&&c.status=="done"&&c.tool=="replace_document_text"&&c.result!=null&&Convert.ToString(c.result["id"])==id).OrderBy(c=>c.sequence))if(Convert.ToString(prior.result["documentBeforeStamp"])==expected)expected=prior.stamp;
     if(source.recovery||String.IsNullOrEmpty(call.documentSignature)||AgentCreatedStamp(id)!=expected)throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");
     string old=Convert.ToString(call.args["old_text"]),replacement=Convert.ToString(call.args["new_text"]);int at=UniqueDocumentMatch(source.text,old);string next=source.text.Substring(0,at)+replacement+source.text.Substring(at+old.Length);
     token.ThrowIfCancellationRequested();SaveDocument(id,next,source.signature,item.editDraft,call.id,"agent:"+state.id,expected);
     var verified=DocumentSource(id);if(verified.recovery||verified.text!=next)throw new AgentFailure("uncertain_write","文档修改后的读取核对失败，请检查文件后再继续。");
    }
    var receipt=AgentReceipt(call.id);return new AgentToolReceipt{result=receipt,stamp=Convert.ToString(receipt["stamp"])};
   }
  }
 }
}
