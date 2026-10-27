using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace kok {
    public class KnowledgeNode {
        public string id,name,kind,ext,collection,key;
        public bool managed,missing,ambiguous,eligible;
        public int tasks,completed;
        public List<string> tags=new List<string>(),aliases=new List<string>(),candidates=new List<string>();
    }
    public class KnowledgeLink { public string source,target,kind,title,section,context; public int start,count=1; }
    public class KnowledgeIndex { public List<KnowledgeNode> nodes=new List<KnowledgeNode>();public List<KnowledgeLink> links=new List<KnowledgeLink>();public int total,skipped;public bool limited; }
    public class KnowledgeReference { public string title,section,context;public int start; }
    public class KnowledgeText {
        public List<KnowledgeReference> references=new List<KnowledgeReference>();public List<string> tags=new List<string>(),aliases=new List<string>();public int tasks,completed;
        public static string Key(string text){string value=(text??"").Trim().Replace('\\','/').Normalize(NormalizationForm.FormKC);while(value.StartsWith("./"))value=value.Substring(2);return value.ToLowerInvariant();}
        public static KnowledgeText Parse(string text){
            var result=new KnowledgeText();text=(text??"").Replace("\r","");string fence=null;int offset=0,lineNo=0;bool frontMatter=false;
            foreach(string line in text.Split('\n')){
                if(lineNo++==0&&line.Trim()=="---"&&Regex.IsMatch(text.Substring(Math.Min(line.Length+1,text.Length)),@"(?m)^---\s*$")){frontMatter=true;offset+=line.Length+1;continue;}
                if(frontMatter){if(line.Trim()=="---")frontMatter=false;else{var field=Regex.Match(line,@"^(tags|aliases):\s*(.*)$",RegexOptions.IgnoreCase);if(field.Success)foreach(string item in field.Groups[2].Value.Trim().Trim('[',']').Split(',')){string value=item.Trim().Trim('\'', '"');if(value.Length>0&&value.Length<=100)(field.Groups[1].Value.ToLowerInvariant()=="tags"?result.tags:result.aliases).Add(value.TrimStart('#'));}}offset+=line.Length+1;continue;}
                var fenceMark=Regex.Match(line,@"^\s{0,3}(`{3,}|~{3,})");if(fenceMark.Success){string mark=fenceMark.Groups[1].Value;if(fence==null)fence=mark;else if(mark[0]==fence[0]&&mark.Length>=fence.Length)fence=null;offset+=line.Length+1;continue;}
                if(fence!=null){offset+=line.Length+1;continue;}
                string plain=Regex.Replace(line,@"(`+).*?\1",match=>new string(' ',match.Length));
                foreach(Match match in Regex.Matches(plain,@"(?<![\\!])\[\[([^\]\r\n]{1,200})\]\]|(?<!!)\[[^\]\r\n]*\]\(([^\s)]+\.(?:md|markdown|docx|txt|epub|pdf)(?:#[^\s)]*)?)\)",RegexOptions.IgnoreCase)){
                    string value=match.Groups[1].Success?match.Groups[1].Value.Split('|')[0]:match.Groups[2].Value;try{value=Uri.UnescapeDataString(value);}catch(UriFormatException){}value=value.Trim();if(value.Contains("://")||value.StartsWith("#")||value.Length==0)continue;string[] pieces=value.Split(new[]{'#'},2);if(result.references.Count<250)result.references.Add(new KnowledgeReference{title=pieces[0].Trim(),section=pieces.Length>1?pieces[1]:"",context=line.Length>180?line.Substring(0,180):line,start=offset+match.Index});
                }
                string tagText=Regex.Replace(plain,@"!?\[\[[^\]\r\n]*\]\]|!?\[[^\]\r\n]*\]\([^)]*\)|https?://\S+",match=>new string(' ',match.Length));
                foreach(Match match in Regex.Matches(tagText,@"(?<![\\\p{L}\p{N}_/#])#([\p{L}\p{N}_\-/]{1,60})"))result.tags.Add(match.Groups[1].Value);
                var task=Regex.Match(plain,@"^\s*[-*+]\s+\[([ xX])\]\s+");if(task.Success){result.tasks++;if(task.Groups[1].Value!=" ")result.completed++;}
                offset+=line.Length+1;
            }
            result.tags=result.tags.Where(t=>t.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToList();result.aliases=result.aliases.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();return result;
        }
    }
    public partial class Library {
        readonly object knowledgeGate=new object();
        sealed class KnowledgeBody { public string stamp,text; }
        readonly Dictionary<string,KnowledgeBody> knowledgeBodies=new Dictionary<string,KnowledgeBody>();
        string ReadKnowledgeBody(Entry entry,ref int skipped){
            bool readable=new[]{"MD","MARKDOWN","TXT","DOCX","EPUB"}.Contains(entry.ext);if(!readable)return "";
            try{var info=new FileInfo(entry.path);if(!info.Exists||info.Length>(entry.ext=="DOCX"||entry.ext=="EPUB"?2*1024*1024:1024*1024)){skipped++;return "";}
                string stamp=info.Length+"|"+info.LastWriteTimeUtc.Ticks+"|"+DraftValue(entry.editDraft,"signature")+"|"+DraftValue(entry.editDraft,"recovery")+"|"+DraftValue(entry.editDraft,"text").GetHashCode();KnowledgeBody cached;if(knowledgeBodies.TryGetValue(entry.id,out cached)&&cached.stamp==stamp)return cached.text;
                string text=entry.ownedDocument?DocumentSource(entry.id).text:entry.ext=="MD"||entry.ext=="MARKDOWN"?File.ReadAllText(entry.path,Encoding.UTF8):String.Join("\n\n",ReadBook(entry).chapters.Select(c=>c.text));
                if(text.Length>120000){text=text.Substring(0,120000);skipped++;}knowledgeBodies[entry.id]=new KnowledgeBody{stamp=stamp,text=text};return text;
            }catch{skipped++;return "";}
        }
        public KnowledgeIndex BuildKnowledgeIndex(){
            if(IsPrivate)throw new InvalidOperationException("私密内容不参与知识图谱。");
            lock(knowledgeGate){
                Entry[] entries;int total;lock(gate){total=Db.items.Count;entries=Db.items.OrderByDescending(e=>e.ownedDocument).ThenBy(e=>e.kind=="document"||e.kind=="book"?0:1).Take(2000).Select(e=>new Entry{id=e.id,path=e.path,name=e.name,kind=e.kind,ext=e.ext,collection=e.collection,notes=e.notes,ownedDocument=e.ownedDocument,tags=(e.tags??new List<string>()).ToList(),editDraft=e.editDraft==null?null:new Dictionary<string,object>(e.editDraft),annotations=(e.annotations??new List<Annotation>()).Where(a=>!String.IsNullOrWhiteSpace(a.note)).Take(40).Select(a=>new Annotation{note=a.note}).ToList()}).ToArray();}
                var result=new KnowledgeIndex{total=total,limited=total>entries.Length};var parsed=new Dictionary<string,KnowledgeText>();var lookup=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);int budget=6*1024*1024;
                Action<string,string> addName=(name,id)=>{string key=KnowledgeText.Key(name);if(key.Length==0)return;List<string> list;if(!lookup.TryGetValue(key,out list)){list=new List<string>();lookup[key]=list;}if(!list.Contains(id))list.Add(id);};
                foreach(var entry in entries){
                    string text="";if(budget>0){text=ReadKnowledgeBody(entry,ref result.skipped);if(text.Length>budget){text=text.Substring(0,budget);result.limited=true;}budget-=text.Length;}else{result.skipped++;result.limited=true;}
                    string notes=(entry.notes??"")+"\n"+String.Join("\n",entry.annotations.Select(a=>a.note));if(notes.Length>32000)notes=notes.Substring(0,32000);var content=KnowledgeText.Parse(text+"\n"+notes);parsed[entry.id]=content;
                    var node=new KnowledgeNode{id=entry.id,name=entry.name,kind=entry.kind,ext=entry.ext,collection=entry.collection,managed=entry.ownedDocument,eligible=entry.kind=="document"||entry.kind=="book"||notes.Trim().Length>0,tags=content.tags.Concat(entry.tags??new List<string>()).Where(t=>!String.IsNullOrWhiteSpace(t)).Select(t=>t.TrimStart('#')).Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToList(),aliases=content.aliases,tasks=content.tasks,completed=content.completed};result.nodes.Add(node);
                    foreach(string name in new[]{entry.id,entry.name,Path.GetFileName(entry.path),Path.GetFileNameWithoutExtension(entry.path),entry.collection+"/"+entry.name,entry.collection+"/"+Path.GetFileName(entry.path)}.Concat(content.aliases))addName(name,entry.id);
                }
                var known=result.nodes.ToDictionary(n=>n.id);var edgeMap=new Dictionary<string,KnowledgeLink>();
                foreach(var entry in entries){
                    foreach(var reference in parsed[entry.id].references){
                        string key=KnowledgeText.Key(reference.title);List<string> candidates;if(!lookup.TryGetValue(key,out candidates))candidates=new List<string>();
                        if(candidates.Count>1){var same=candidates.Where(id=>known[id].collection==entry.collection).ToList();if(same.Count==1)candidates=same;}
                        string target=candidates.Count==1?candidates[0]:"missing:"+key;
                        if(candidates.Count!=1&&!known.ContainsKey(target)){if(known.Count>=3000){result.limited=true;continue;}var missing=new KnowledgeNode{id=target,name=reference.title,kind="missing",collection="",key=reference.title,missing=true,ambiguous=candidates.Count>1,candidates=candidates.ToList(),eligible=true};known[target]=missing;result.nodes.Add(missing);}
                        string edgeKey=entry.id+"|"+target;if(edgeMap.ContainsKey(edgeKey)){edgeMap[edgeKey].count++;continue;}if(result.links.Count>=12000){result.limited=true;break;}var edge=new KnowledgeLink{source=entry.id,target=target,kind="link",title=reference.title,section=reference.section,context=reference.context,start=reference.start};edgeMap[edgeKey]=edge;result.links.Add(edge);
                    }
                    foreach(string tag in known[entry.id].tags){string target="tag:"+KnowledgeText.Key(tag);if(!known.ContainsKey(target)){if(known.Count>=3000){result.limited=true;continue;}var node=new KnowledgeNode{id=target,name="#"+tag,kind="tag",key=tag,collection="",eligible=true};known[target]=node;result.nodes.Add(node);}if(result.links.Count<12000)result.links.Add(new KnowledgeLink{source=entry.id,target=target,kind="tag",title=tag,context="",section=""});else result.limited=true;}
                }
                HashSet<string> live;lock(gate)live=new HashSet<string>(Db.items.Select(e=>e.id));var removed=new HashSet<string>(entries.Where(e=>!live.Contains(e.id)).Select(e=>e.id));result.nodes.RemoveAll(n=>removed.Contains(n.id));result.links.RemoveAll(e=>removed.Contains(e.source)||removed.Contains(e.target));var used=new HashSet<string>(result.links.SelectMany(e=>new[]{e.source,e.target}));result.nodes.RemoveAll(n=>(n.missing||n.kind=="tag")&&!used.Contains(n.id));foreach(string id in knowledgeBodies.Keys.Where(id=>!live.Contains(id)).ToArray())knowledgeBodies.Remove(id);int cachedCharacters=knowledgeBodies.Values.Sum(b=>b.text.Length);while(knowledgeBodies.Count>1024||cachedCharacters>8*1024*1024){string id=knowledgeBodies.Keys.First();cachedCharacters-=knowledgeBodies[id].text.Length;knowledgeBodies.Remove(id);}return result;
            }
        }
    }
    public partial class MainWindow {
        async Task<object> ReadKnowledgeIndex(){var target=library;var result=await Task.Run(()=>target.BuildKnowledgeIndex());if(library!=target||target.IsPrivate)throw new InvalidOperationException("私密内容不参与知识图谱。");return result;}
    }
}
