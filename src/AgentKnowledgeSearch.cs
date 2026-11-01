using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace kok {
 public partial class Library {
  // Fragment-level search: several anchored evidence snippets per source are returned so the
  // model can inspect different parts of one long file without a full read of every source.
  async Task<object> AgentSearchCurrent(Dictionary<string,object> args,List<string> allowed,CancellationToken token){
   RequireAiMode();string query=ModuleContext.Text(args,"query");if(String.IsNullOrWhiteSpace(query))return AgentSearch(args,allowed);
   int limit=args.ContainsKey("limit")?Convert.ToInt32(args["limit"]):12;limit=Math.Max(1,Math.Min(24,limit));
   string kind=ModuleContext.Text(args,"kind"),collection=ModuleContext.Text(args,"collection"),tag=ModuleContext.Text(args,"tag");
   var scope=new HashSet<string>(allowed);string[] ids;lock(gate)ids=Db.items.Where(e=>scope.Contains(e.id)&&(kind==""||e.kind==kind)&&(collection==""||e.collection==collection)&&(tag==""||e.tags.Contains(tag))).Select(e=>e.id).ToArray();
   var cfg=ReadEmbeddingConfig();var result=await SearchKnowledge(query,ids,cfg.enabled?"hybrid":"lexical",limit,token,validate:RequireAiMode,maxPerFile:3);
   token.ThrowIfCancellationRequested();RequireAiMode();
   lock(gate){
    var hits=result.hits.Where(h=>Find(h.itemId)!=null).ToArray();
    var counts=hits.GroupBy(h=>h.itemId).ToDictionary(g=>g.Key,g=>g.Count());
    var refs=hits.Select((h,n)=>new ChatReference{id="S"+(n+1),fileId=h.itemId,label=h.name,excerpt=SnippetText(h.text,240),page=h.anchor==null||h.anchor.page==0?(int?)null:h.anchor.page,chapter=h.anchor==null?(int?)null:h.anchor.chapter,time=h.kind=="subtitle"&&h.anchor!=null?(double?)h.anchor.time:null}).ToList();
    var seen=new Dictionary<string,int>();var lines=new List<string>();
    for(int n=0;n<hits.Length;n++){
     var h=hits[n];int ordinal;seen.TryGetValue(h.itemId,out ordinal);ordinal++;seen[h.itemId]=ordinal;int total=counts[h.itemId];string anchor=SnippetAnchor(h);
     lines.Add(AgentLine(Find(h.itemId))+"\nMatch S"+(n+1)+" ("+h.kind+(anchor==""?"":", "+anchor)+")"+(total>1?" snippet "+ordinal+"/"+total:"")+": "+SnippetText(h.text,360));
    }
    string text=hits.Length==0?"No matching sources.":String.Join("\n\n",lines);
    if(counts.Count>0&&counts.Values.Any(count=>count>1))text+="\nMore snippets: "+String.Join(", ",counts.Where(pair=>pair.Value>1).Take(6).Select(pair=>{var entry=Find(pair.Key);string name=entry==null?pair.Key:entry.name.Length>40?entry.name.Substring(0,40):entry.name;return name+" ("+pair.Value+" snippets)";}))+(counts.Count(pair=>pair.Value>1)>6?" …":"");
    if(result.notice!="")text+="\n"+result.notice;
    var itemIds=hits.Select(h=>h.itemId).Distinct().ToArray();
    return new Dictionary<string,object>{{"text",text},{"ids",itemIds},{"references",refs},{"_sourceStamps",AgentSourceStamps(itemIds)}};
   }
  }
  static string SnippetText(string text,int max){text=text??"";if(text.Length<=max)return text;int length=max;if(Char.IsHighSurrogate(text[length-1]))length--;return text.Substring(0,length);}
  static string SnippetAnchor(ContentHit h){
   var anchor=h.anchor;if(anchor==null)return "";
   if(h.kind=="subtitle"||h.kind=="ocr_frame")return "time "+SnippetTime(anchor.time);
   if(anchor.page>0)return "page "+anchor.page;
   if(h.kind=="chapter"||h.kind=="slide")return "chapter "+(Math.Max(0,anchor.chapter)+1);
   if(anchor.time>0)return "time "+SnippetTime(anchor.time);
   if(anchor.chapter>0)return "chapter "+(anchor.chapter+1);
   return "";
  }
  static string SnippetTime(double seconds){if(Double.IsNaN(seconds)||Double.IsInfinity(seconds)||seconds<0)seconds=0;return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");}
 }
}
