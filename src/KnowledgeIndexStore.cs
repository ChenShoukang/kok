using System;
using System.Linq;
using System.Collections.Generic;
namespace kok {
 public sealed class ContentChunk {
  public string id,itemId,kind,text,stamp,clipId="";
  public SourceAnchor anchor;
 }
 public sealed class ContentIndexStatus {
  public string itemId,stamp,state,detail,updated;public int chunks;public bool partial;
 }
 public sealed class ContentVector {public string chunkId,model;public double[] values;}
 public sealed partial class LibraryStore {
  static void KnowledgeSchema(SqlDb db){
   Schema(db);
   db.Exec("CREATE TABLE IF NOT EXISTS knowledge_sources(item_id TEXT PRIMARY KEY REFERENCES items(id) ON DELETE CASCADE,stamp TEXT NOT NULL,state TEXT NOT NULL,detail TEXT NOT NULL,updated TEXT NOT NULL,chunks INTEGER NOT NULL,partial INTEGER NOT NULL)");
   db.Exec("CREATE TABLE IF NOT EXISTS knowledge_chunks(id TEXT PRIMARY KEY,item_id TEXT NOT NULL REFERENCES items(id) ON DELETE CASCADE,kind TEXT NOT NULL,body TEXT NOT NULL,stamp TEXT NOT NULL,anchor TEXT NOT NULL,clip_id TEXT NOT NULL)");
   db.Exec("CREATE INDEX IF NOT EXISTS knowledge_item ON knowledge_chunks(item_id)");
   db.Exec("CREATE VIRTUAL TABLE IF NOT EXISTS knowledge_text USING fts5(id UNINDEXED,item_id UNINDEXED,body,tokenize='trigram')");
   db.Exec("CREATE TRIGGER IF NOT EXISTS knowledge_insert AFTER INSERT ON knowledge_chunks BEGIN INSERT INTO knowledge_text(id,item_id,body) VALUES(new.id,new.item_id,new.body); END");
   db.Exec("CREATE TRIGGER IF NOT EXISTS knowledge_delete AFTER DELETE ON knowledge_chunks BEGIN DELETE FROM knowledge_text WHERE id=old.id; END");
   db.Exec("INSERT INTO knowledge_text(knowledge_text,rank) VALUES('secure-delete',1)");
   db.Exec("CREATE TABLE IF NOT EXISTS knowledge_vectors(chunk_id TEXT PRIMARY KEY REFERENCES knowledge_chunks(id) ON DELETE CASCADE,model TEXT NOT NULL,data TEXT NOT NULL)");
  }
  public string ContentRevision(string id){using(var db=Open()){KnowledgeSchema(db);var rows=db.Query("SELECT updated FROM knowledge_sources WHERE item_id=?",id);return rows.Count==0?"":rows[0][0];}}
  public List<ContentIndexStatus> ContentStatuses(){using(var db=Open()){KnowledgeSchema(db);return db.Query("SELECT item_id,stamp,state,detail,updated,chunks,partial FROM knowledge_sources").Select(r=>new ContentIndexStatus{itemId=r[0],stamp=r[1],state=r[2],detail=r[3],updated=r[4],chunks=Int32.Parse(r[5]),partial=r[6]=="1"}).ToList();}}
  public List<ContentChunk> ContentChunks(IEnumerable<string> ids){using(var db=Open()){KnowledgeSchema(db);return Scoped(db,"SELECT id,item_id,kind,body,stamp,anchor,clip_id FROM knowledge_chunks","item_id",ids==null?null:new HashSet<string>(ids)).Select(r=>new ContentChunk{id=r[0],itemId=r[1],kind=r[2],text=r[3],stamp=r[4],anchor=Library.Json.Deserialize<SourceAnchor>(r[5]),clipId=r[6]}).ToList();}}
  public List<ContentChunk> MatchContent(string query,IEnumerable<string> ids,int limit){using(var db=Open()){KnowledgeSchema(db);var all=ids.Distinct().ToArray();var results=new List<ContentChunk>();string[] terms=System.Text.RegularExpressions.Regex.Matches(query,@"[\p{L}\p{N}_-]+").Cast<System.Text.RegularExpressions.Match>().SelectMany(m=>m.Value.Any(c=>c>127)&&m.Length>2?Enumerable.Range(0,m.Length-1).Select(n=>m.Value.Substring(n,2)):new[]{m.Value}).Distinct().Take(20).ToArray();if(terms.Length==0)return results;string match=String.Join(" OR ",terms.Select(t=>"\""+t.Replace("\"","\"\"")+"\""));for(int n=0;n<all.Length;n+=400){var scope=all.Skip(n).Take(400).ToArray();string where=" c.item_id IN ("+String.Join(",",scope.Select(i=>"?"))+")";string sql="SELECT c.id,c.item_id,c.kind,c.body,c.stamp,c.anchor,c.clip_id FROM knowledge_chunks c JOIN knowledge_text f ON f.id=c.id WHERE knowledge_text MATCH ? AND"+where+" ORDER BY rank LIMIT ?";var args=new[]{match}.Concat(scope).Concat(new[]{limit.ToString()}).ToArray();if(terms.All(t=>t.Length<3)){sql="SELECT c.id,c.item_id,c.kind,c.body,c.stamp,c.anchor,c.clip_id FROM knowledge_chunks c WHERE instr(lower(c.body),lower(?))>0 AND"+where+" LIMIT ?";args=new[]{query}.Concat(scope).Concat(new[]{limit.ToString()}).ToArray();}results.AddRange(db.Query(sql,args).Select(r=>new ContentChunk{id=r[0],itemId=r[1],kind=r[2],text=r[3],stamp=r[4],anchor=Library.Json.Deserialize<SourceAnchor>(r[5]),clipId=r[6]}));}return results.Take(limit).ToList();}}
  public void ReplaceContent(ContentIndexStatus status,List<ContentChunk> chunks){using(var db=Open()){KnowledgeSchema(db);db.Exec("BEGIN IMMEDIATE");try{
   db.Exec("DELETE FROM knowledge_chunks WHERE item_id=?",status.itemId);
   foreach(var c in chunks)db.Exec("INSERT INTO knowledge_chunks(id,item_id,kind,body,stamp,anchor,clip_id) VALUES(?,?,?,?,?,?,?)",c.id,c.itemId,c.kind,c.text,c.stamp,Library.Json.Serialize(c.anchor),c.clipId??"");
   db.Exec("INSERT INTO knowledge_sources(item_id,stamp,state,detail,updated,chunks,partial) VALUES(?,?,?,?,?,?,?) ON CONFLICT(item_id) DO UPDATE SET stamp=excluded.stamp,state=excluded.state,detail=excluded.detail,updated=excluded.updated,chunks=excluded.chunks,partial=excluded.partial",status.itemId,status.stamp,status.state,status.detail??"",DateTime.UtcNow.ToString("o"),chunks.Count.ToString(),status.partial?"1":"0");
   db.Exec("INSERT INTO contents(id,body,stamp) VALUES(?,?,?) ON CONFLICT(id) DO UPDATE SET body=excluded.body,stamp=excluded.stamp",status.itemId,String.Join("\n",chunks.Select(c=>c.text)),status.stamp);
   db.Exec("COMMIT");}catch{db.Exec("ROLLBACK");throw;}}
  }
  public void InvalidateContent(IEnumerable<string> ids){using(var db=Open()){KnowledgeSchema(db);db.Exec("BEGIN IMMEDIATE");try{foreach(var id in ids){db.Exec("DELETE FROM knowledge_chunks WHERE item_id=?",id);db.Exec("DELETE FROM contents WHERE id=?",id);db.Exec("UPDATE knowledge_sources SET state='stale',chunks=0 WHERE item_id=?",id);}db.Exec("COMMIT");}catch{db.Exec("ROLLBACK");throw;}}}
  public List<ContentVector> ContentVectors(string model,IEnumerable<string> ids){using(var db=Open()){KnowledgeSchema(db);return Scoped(db,"SELECT v.chunk_id,v.model,v.data,c.item_id FROM knowledge_vectors v JOIN knowledge_chunks c ON c.id=v.chunk_id","c.item_id",new HashSet<string>(ids)).Where(r=>r[1]==model).Select(r=>new ContentVector{chunkId=r[0],model=r[1],values=Library.Json.Deserialize<double[]>(r[2])}).ToList();}}
  public void SaveContentVectors(IEnumerable<ContentVector> vectors){using(var db=Open()){KnowledgeSchema(db);db.Exec("BEGIN IMMEDIATE");try{foreach(var v in vectors)db.Exec("INSERT INTO knowledge_vectors(chunk_id,model,data) VALUES(?,?,?) ON CONFLICT(chunk_id) DO UPDATE SET model=excluded.model,data=excluded.data",v.chunkId,v.model,Library.Json.Serialize(v.values));db.Exec("COMMIT");}catch{db.Exec("ROLLBACK");throw;}}}
 }
}
