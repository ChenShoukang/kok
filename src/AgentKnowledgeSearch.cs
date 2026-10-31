using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace kok {
 public partial class Library {
  async Task<object> AgentSearchCurrent(Dictionary<string,object> args,List<string> allowed,CancellationToken token){
   RequireAiMode();string query=ModuleContext.Text(args,"query");if(String.IsNullOrWhiteSpace(query))return AgentSearch(args,allowed);int limit=args.ContainsKey("limit")?Convert.ToInt32(args["limit"]):15;string kind=ModuleContext.Text(args,"kind"),collection=ModuleContext.Text(args,"collection"),tag=ModuleContext.Text(args,"tag");var scope=new HashSet<string>(allowed);string[] ids;lock(gate)ids=Db.items.Where(e=>scope.Contains(e.id)&&(kind==""||e.kind==kind)&&(collection==""||e.collection==collection)&&(tag==""||e.tags.Contains(tag))).Select(e=>e.id).ToArray();
   var cfg=ReadEmbeddingConfig();var result=await SearchKnowledge(query,ids,cfg.enabled?"hybrid":"lexical",limit,token,validate:RequireAiMode);token.ThrowIfCancellationRequested();RequireAiMode();lock(gate){var hits=result.hits.Where(h=>Find(h.itemId)!=null).GroupBy(h=>h.itemId).Select(g=>g.First()).ToArray();var refs=hits.Select((h,n)=>new ChatReference{id="S"+(n+1),fileId=h.itemId,label=h.name,excerpt=h.text.Substring(0,Math.Min(240,h.text.Length)),page=h.anchor==null||h.anchor.page==0?(int?)null:h.anchor.page,chapter=h.anchor==null?(int?)null:h.anchor.chapter,time=h.kind=="subtitle"?(double?)h.anchor.time:null}).ToList();return new Dictionary<string,object>{{"text",(hits.Length==0?"No matching sources.":String.Join("\n\n",hits.Select(h=>AgentLine(Find(h.itemId))+"\nMatch ("+h.kind+"): "+h.text.Substring(0,Math.Min(240,h.text.Length)))))+(result.notice==""?"":"\n"+result.notice)},{"ids",hits.Select(h=>h.itemId).ToArray()},{"references",refs},{"_sourceStamps",AgentSourceStamps(hits.Select(h=>h.itemId))}};}
  }
 }
}
