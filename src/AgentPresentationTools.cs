using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace kok {public partial class Library {
 Entry PresentationItem(string id){var item=Find(id);if(item==null||item.ext!="PPTX")throw new InvalidDataException("请使用 PPTX 格式；旧版 PPT 请先转换。");return item;}
 internal string PresentationSignature(string id){lock(gate){return DocumentHash(ManagedOwnedFile(id,"PPTX").path);}}
 // Page coverage needs a version token for EVERY readable deck (an imported PPTX source cannot be edited through
 // kok but its file bytes can still change on disk). Managed decks use the same PresentationSignature as edits;
 // imported originals fall back to the raw file hash. DeckCoverage compares this token to invalidate old reads.
 internal string PresentationCoverageDigest(string id){lock(gate){var item=PresentationItem(id);try{return PresentationSignature(id);}catch{return DocumentHash(item.path);}}}
 internal int PresentationPageCount(string id){lock(gate){return PresentationFiles.Read(PresentationItem(id).path,false).slides.Count;}}
 internal Dictionary<string,object> ReadAgentPresentation(AgentCall call){lock(gate){
  var item=PresentationItem(Convert.ToString(call.args["id"]));var deck=PresentationFiles.Read(item.path,false);
  int start=AgentDocumentTools.Number(call.args,"start_page",1,1,100),max=AgentDocumentTools.Number(call.args,"max_pages",4,1,8);
  if(start>deck.slides.Count)throw new InvalidDataException("幻灯片页码超出范围。");
  int left=16000;var pages=new List<object>();var selected=deck.slides.Skip(start-1).Take(max).ToList();
  var complete=new List<int>();var incomplete=new List<int>();
  foreach(var slide in selected){
   int page=deck.slides.IndexOf(slide)+1;bool full=true;var shapes=new List<object>();
   foreach(var shape in slide.shapes){
    string text=shape.text??"";int take=Math.Min(left,Math.Min(6000,text.Length));
    if(take>0&&take<text.Length&&Char.IsHighSurrogate(text[take-1]))take--;
    left-=take;full&=take==text.Length;
    shapes.Add(new{id=shape.id,kind=shape.kind,text=text.Substring(0,take),truncated=take<text.Length,x=shape.x,y=shape.y,w=shape.w,h=shape.h,fontSize=shape.fontSize,color=shape.color});
   }
   int notes=Math.Min(left,Math.Min(4000,slide.notes.Length));
   if(notes>0&&notes<slide.notes.Length&&Char.IsHighSurrogate(slide.notes[notes-1]))notes--;
   left-=notes;full&=notes==slide.notes.Length;
   (full?complete:incomplete).Add(page);
   pages.Add(new{page=page,id=slide.id,title=slide.title,notes=slide.notes.Substring(0,notes),notes_truncated=notes<slide.notes.Length,background=slide.background,shapes=shapes});
  }
  int pagesRead=selected.Count,totalPages=deck.slides.Count,next=start+pagesRead<=totalPages?start+pagesRead:-1;
  string digest=PresentationCoverageDigest(item.id);
  // Only pages whose emitted text and notes are complete prove page coverage. Adjacent complete pages
  // share a row; clipped pages need a smaller page window or exact read_items text paging.
  var coverage=new List<Dictionary<string,object>>();
  foreach(int page in complete){
   var previous=coverage.LastOrDefault();
   if(previous!=null&&Convert.ToInt32(previous["readEnd"])==page-1){previous["readEnd"]=page;previous["readChars"]=Convert.ToInt32(previous["readChars"])+1;previous["full"]=Convert.ToInt32(previous["readStart"])==0&&page==totalPages;continue;}
   coverage.Add(new Dictionary<string,object>{{"id",item.id},{"name",item.name},{"totalChars",totalPages},
    {"readStart",page-1},{"readEnd",page},{"readChars",1},{"full",page==1&&totalPages==1},
    {"hasMore",next>=0||incomplete.Count>0},{"nextOffset",next},{"digest",digest},{"mode","exact"},
    {"sampled",false},{"capped",false},{"domain",AgentReading.DomainPresentation},{"startPage",start},{"pageMax",max}});
  }
  string hint=incomplete.Count==0?"":"Some page text or notes were clipped. Retry these pages with max_pages=1; if still clipped, use read_items with ids, offset and max_chars until has_more=false. Clipped pages do not prove complete reading.";
  return new Dictionary<string,object>{
   {"text",Json.Serialize(new{id=item.id,name=item.name,total_pages=totalPages,next_page=next,incomplete_pages=incomplete,reading_hint=hint,editable=item.ownedDocument,has_draft=DraftValue(item.editDraft,"kind")=="presentation",width=deck.width,height=deck.height,unsupported=deck.unsupported,slides=pages})},
   {"mode","exact-read"},{"domain",AgentReading.DomainPresentation},{"coverage",coverage},
   {"references",new List<ChatReference>{new ChatReference{id=item.id,fileId=item.id,label=item.name,page=start,excerpt=deck.slides[start-1].title}}},
   {"start_page",start},{"total_pages",totalPages},{"pages_read",pagesRead},{"next_page",next},{"incomplete_pages",incomplete},{"reading_hint",hint}};
 }}
 internal byte[] PresentationEditPreview(Dictionary<string,object> args){var item=ManagedOwnedFile(Convert.ToString(args["id"]),"PPTX");if(DraftValue(item.editDraft,"kind")=="presentation")throw new InvalidOperationException("演示文稿有未写入文件的草稿，请先保存 PPTX 后再交给 Agent。");if(new FileInfo(item.path).Length>32*1024*1024)throw new InvalidDataException("PPTX 超过 32 MB，请使用系统应用。");return AgentPresentationXml.Edit(File.ReadAllBytes(item.path),args);}
 internal void PrepareAgentPresentation(AgentCall call){PresentationEditPreview(call.args);string id=Convert.ToString(call.args["id"]);call.documentSignature=PresentationSignature(id);call.documentBeforeStamp=AgentCreatedStamp(id);call.beforeText=String.Join("\n",PresentationFiles.Read(Find(id).path,false).slides.Select((s,n)=>(n+1)+". "+s.title));call.afterText=Json.Serialize(call.args["edits"]);}
 internal AgentToolReceipt CopyAgentPresentation(AgentTaskState state,AgentCall call,CancellationToken token){lock(gate){token.ThrowIfCancellationRequested();var source=PresentationItem(Convert.ToString(call.args["id"]));var bytes=PresentationFiles.Decode(Convert.ToBase64String(File.ReadAllBytes(source.path)));var item=CreatePresentation(Convert.ToString(call.args["name"]),source.collection,bytes,call.id,"agent:"+state.id);return new AgentToolReceipt{result=AgentReceipt(call.id),stamp=AgentCreatedStamp(item.id)};}}
 internal DocumentSaved SavePresentation(string id,byte[] bytes,string expectedSignature,string operationId=null,string operationSource=null,string beforeStamp=null){lock(gate){var item=ManagedOwnedFile(id,"PPTX");if(DocumentHash(item.path)!=expectedSignature)throw new InvalidOperationException("文件已在其他位置修改，请另存副本以保留修改。");using(var memory=new MemoryStream(bytes))PresentationFiles.Read(memory,false);if(operationId!=null&&(!AgentJournal.ValidId(operationId)||FindOperation(operationId)!=null))throw new InvalidDataException("操作记录无效。");var before=Json.Deserialize<Entry>(Json.Serialize(item));byte[] old=File.ReadAllBytes(item.path);string hash=BytesHash(bytes);var op=new Operation{id=operationId??Guid.NewGuid().ToString("N"),module="presentations",source=operationSource??OperationOrigin.Source??"user",title="保存演示文稿",created=DateTime.UtcNow.ToString("o"),state="applied",documentBeforeStamp=beforeStamp};var draft=new Dictionary<string,object>();op.patches.Add(new AgentChange{id=id,before=new Dictionary<string,object>{{"editDraft",Json.Deserialize<Dictionary<string,object>>(Json.Serialize(item.editDraft))}},after=new Dictionary<string,object>{{"editDraft",draft}}});CheckOpCapacity(op);if(hash!=BytesHash(old)){op.fileVersion=op.id;SaveFileVersion(op.id,new FileVersion{itemId=id,before=Convert.ToBase64String(old),afterHash=hash});}
  try{if(op.fileVersion!=null)ReplaceOwnedBytes(item.path,bytes);item.editDraft=draft;item.size=bytes.LongLength;item.modified=File.GetLastWriteTimeUtc(item.path).ToString("o");if(beforeStamp!=null)op.documentAfterStamp=AgentCreatedStamp(id);OperationRows.Add(op);Save(false,new[]{id},new[]{op.id});return new DocumentSaved{signature=hash,modified=item.modified,size=item.size};}catch{OperationRows.Remove(op);if(op.fileVersion!=null)ReplaceOwnedBytes(item.path,old);foreach(var field in typeof(Entry).GetFields())field.SetValue(item,field.GetValue(before));throw;}
 }}
 internal AgentToolReceipt EditAgentPresentation(AgentTaskState state,AgentCall call,CancellationToken token){lock(gate){token.ThrowIfCancellationRequested();string id=Convert.ToString(call.args["id"]);ManagedOwnedFile(id,"PPTX");var op=FindOperation(call.id);if(op!=null){if(op.state!="applied"||op.module!="presentations"||op.patches.Count!=1||op.patches[0].id!=id||AgentCreatedStamp(id)!=op.documentAfterStamp)throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");}else{
  string expected=call.documentBeforeStamp;foreach(var prior in state.calls.Where(c=>c.sequence<call.sequence&&c.status=="done"&&c.tool=="edit_presentation"&&c.result!=null&&Convert.ToString(c.result["id"])==id).OrderBy(c=>c.sequence))if(Convert.ToString(prior.result["documentBeforeStamp"])==expected)expected=prior.stamp;
  if(String.IsNullOrEmpty(call.documentSignature)||AgentCreatedStamp(id)!=expected)throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");byte[] bytes=PresentationEditPreview(call.args);token.ThrowIfCancellationRequested();SavePresentation(id,bytes,PresentationSignature(id),call.id,"agent:"+state.id,expected);if(PresentationSignature(id)!=BytesHash(bytes))throw new AgentFailure("uncertain_write","演示文稿修改后的读取核对失败，请检查文件后再继续。");}
  var result=AgentReceipt(call.id);return new AgentToolReceipt{result=result,stamp=Convert.ToString(result["stamp"])};
 }}
}}
