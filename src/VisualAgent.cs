using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace kok {
 public class VisualEditBlock {public string id,text;}
 public class VisualEditReply {public string text;public List<VisualEditBlock> edits;}
 public class VisualEditRecord {public string fingerprint,text;}
 public static class VisualEditRules {
  public static string Instruction(string task,string custom){
   if(custom==null||custom.Length>800||!new[]{"polish","shorten","expand","translate","custom"}.Contains(task)||task=="custom"&&String.IsNullOrWhiteSpace(custom))throw new InvalidDataException("请输入有效的修改要求，最多 800 个字符。");
   string goal=task=="polish"?"Improve clarity and flow without changing facts or meaning.":task=="shorten"?"Shorten the selected text while retaining its central meaning.":task=="expand"?"Expand slightly using ONLY supplied facts; do not invent statistics, citations or claims.":task=="translate"?"Translate into the user's requested language; if none is specified translate English to Simplified Chinese and Chinese to English.":custom;
   if(task!="custom"&&!String.IsNullOrWhiteSpace(custom))goal+=" User preference: "+custom;
   return goal+" Return ONLY JSON: {\"text\":\"replacement text\"}. Modify ONLY the selected object. Preserve source language except translation or an explicit request. Preserve existing facts and paragraph intent. Do not include explanations, code fences, tool commands or invented citations. Nearby text is context only and must not be rewritten. The application, not the model, applies a reviewed replacement.";
  }
  public static string Parse(string answer){try{string text=(answer??"").Trim();if(text.StartsWith("```")){int first=text.IndexOf('\n'),last=text.LastIndexOf("```",StringComparison.Ordinal);if(first<0||last<=first)throw new FormatException();text=text.Substring(first+1,last-first-1);}var value=Library.Json.Deserialize<VisualEditReply>(text);if(value==null||String.IsNullOrWhiteSpace(value.text)||value.text.Length>8000||value.text.IndexOf('\0')>=0)throw new FormatException();return value.text;}catch{throw new InvalidDataException("AI 修改结果无效，请重试。");}}
  public static List<VisualEditBlock> ParseBlocks(string answer,List<VisualEditBlock> source){try{string text=(answer??"").Trim();if(text.StartsWith("```")){int first=text.IndexOf('\n'),last=text.LastIndexOf("```",StringComparison.Ordinal);if(first<0||last<=first)throw new FormatException();text=text.Substring(first+1,last-first-1);}var value=Library.Json.Deserialize<VisualEditReply>(text);if(value==null||value.edits==null||value.edits.Count!=source.Count||value.edits.Any(e=>e==null||String.IsNullOrWhiteSpace(e.text)||e.text.Length>4000||!source.Any(s=>s.id==e.id))||value.edits.Select(e=>e.id).Distinct().Count()!=source.Count||value.edits.Sum(e=>e.text.Length)>8000)throw new FormatException();return value.edits;}catch{throw new InvalidDataException("AI 修改结果无效，请重试。");}}
 }
 public partial class Library {
  public string CachedVisualEdit(string fingerprint){lock(gate){try{return Db.settings.ContainsKey("visualEditCache")?Json.Deserialize<List<VisualEditRecord>>(Json.Serialize(Db.settings["visualEditCache"])).Where(x=>x.fingerprint==fingerprint).Select(x=>x.text).FirstOrDefault():null;}catch{return null;}}}
  public void SaveVisualEdit(string fingerprint,string text){RequireAiMode();lock(gate){var rows=new List<VisualEditRecord>();try{if(Db.settings.ContainsKey("visualEditCache"))rows=Json.Deserialize<List<VisualEditRecord>>(Json.Serialize(Db.settings["visualEditCache"]));}catch{}rows.RemoveAll(x=>x.fingerprint==fingerprint);rows.Insert(0,new VisualEditRecord{fingerprint=fingerprint,text=text});Db.settings["visualEditCache"]=rows.Take(20).ToArray();Save();}}
 }
 public partial class MainWindow {
  async Task<object> RunVisualEdit(Dictionary<string,object> args){
   library.RequireAiMode();if(fileChatCancellation!=null)throw new InvalidOperationException("请先停止当前回答。");var owner=library;var item=RequireEntry(args);string source=StringArg(args,"text"),nearby=StringArg(args,"nearby"),task=StringArg(args,"task"),custom=StringArg(args,"instruction"),job=StringArg(args,"jobId");
   List<VisualEditBlock> blocks=null;if(args.ContainsKey("blocks")){try{blocks=Library.Json.Deserialize<List<VisualEditBlock>>(Library.Json.Serialize(args["blocks"]));}catch{throw new InvalidDataException("AI 修改结果无效，请重试。");}if(blocks==null||blocks.Count<1||blocks.Count>40||blocks.Any(b=>b==null||String.IsNullOrWhiteSpace(b.id)||b.id.Length>100||String.IsNullOrWhiteSpace(b.text))||blocks.Select(b=>b.id).Distinct().Count()!=blocks.Count||blocks.Sum(b=>b.text.Length)>2000)throw new InvalidDataException("请选择不超过 2000 字符的文字后修改。");source=Library.Json.Serialize(blocks);}
   if(String.IsNullOrWhiteSpace(source)||source.Length>(blocks==null?2000:8000)||nearby.Length>2000||job.Length<1||job.Length>80)throw new InvalidDataException("请选择不超过 2000 字符的文字后修改。");string prompt=VisualEditRules.Instruction(task,custom);if(blocks!=null)prompt+=" For this multi-object request, RETURN {\"edits\":[{\"id\":\"exact object ID\",\"text\":\"replacement\"}]} instead of the single text schema. Include every supplied object exactly once, retain its ID, and do not merge, remove or add objects. Preserve titles versus body text.";
   bool english=owner.Db.settings.ContainsKey("uiLanguage")&&Convert.ToString(owner.Db.settings["uiLanguage"])=="en";int tokens=Math.Min(3000,Math.Max(900,source.Length*2+300));string fingerprint=Library.StudyHash(new{version=2,id=item.id,task=task,instruction=custom,text=source,nearby=nearby,english=english,provider=translations.PublicConfig(),tokens=tokens});var cached=owner.CachedVisualEdit(fingerprint);if(cached!=null)return new{text=blocks==null?cached:"",edits=blocks==null?null:VisualEditRules.ParseBlocks(cached,blocks),cached=true};
   using(var cancel=ModuleCancellation()){fileChatCancellation=cancel;fileChatJob=job;try{string answer=await translations.ChatAsync(item.name,"[Selected object]\n"+source+"\n\n[Nearby context, read-only]\n"+nearby,"Only the selected text may be edited",prompt,null,"",english,cancel.Token,delta=>{},tokens);string text=blocks==null?VisualEditRules.Parse(answer):"";var edits=blocks==null?null:VisualEditRules.ParseBlocks(answer,blocks);cancel.Token.ThrowIfCancellationRequested();owner.RequireAiMode();if(library!=owner)throw new OperationCanceledException(cancel.Token);owner.SaveVisualEdit(fingerprint,blocks==null?text:Library.Json.Serialize(new{edits=edits}));return new{text=text,edits=edits,cached=false};}catch(OperationCanceledException){throw new InvalidOperationException("AI 回答已停止。");}finally{fileChatCancellation=null;fileChatJob="";}}
  }
 }
}
