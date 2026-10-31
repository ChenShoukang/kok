using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace kok {
 public sealed class ContentPage {public int page;public double time;public string text;public string method="text";}
 public sealed class ContentHit {public string itemId,chunkId,name,text,kind,clipId;public SourceAnchor anchor;public double score;}
 public partial class Library {
  public Entry[] ContentEntries(){lock(gate)return Db.items.Select(e=>Json.Deserialize<Entry>(Json.Serialize(e))).ToArray();}
  public string ContentStamp(string id){lock(gate){var e=Find(id);if(e==null)return "";var f=new FileInfo(e.path);return StudyHash(new{version=1,e.id,e.name,e.path,exists=f.Exists,size=f.Exists?f.Length:0,ticks=f.Exists?f.LastWriteTimeUtc.Ticks:0,metadata=SearchContent.Metadata(e),e.chapterTexts,e.editDraft,e.generatedSubtitles,sidecars=CaptionSources(e),clips=Db.workspace.clips.Where(c=>c.source!=null&&c.source.itemId==id).OrderBy(c=>c.id).ToArray()});}}
  void EnsureNormalContent(){if(IsPrivate)throw new InvalidOperationException("Private libraries do not use the shared content index.");}
  public List<ContentIndexStatus> ContentStatus(){EnsureNormalContent();lock(gate){InvalidateChangedContent();var indexed=store.ContentStatuses().ToDictionary(s=>s.itemId);return Db.items.Select(e=>indexed.ContainsKey(e.id)?indexed[e.id]:new ContentIndexStatus{itemId=e.id,state="pending",detail="",chunks=0}).ToList();}}
  void InvalidateChangedContent(){var changed=store.ContentStatuses().Where(s=>s.state!="stale"&&s.stamp!=ContentStamp(s.itemId)).Select(s=>s.itemId).ToArray();if(changed.Length>0)store.InvalidateContent(changed);}
  public List<ContentChunk> ReadContentChunks(IEnumerable<string> ids){EnsureNormalContent();lock(gate){InvalidateChangedContent();return store.ContentChunks(ids);}}
  internal string IndexedBody(string id){if(IsPrivate)return "";return String.Join("\n\n",ReadContentChunks(new[]{id}).Where(c=>c.kind!="metadata"&&c.kind!="clip").Select(c=>c.text));}
  internal void FailContentSource(string id,string expected){EnsureNormalContent();lock(gate){if(ContentStamp(id)!=expected)return;var chunks=store.ContentChunks(new[]{id});store.ReplaceContent(new ContentIndexStatus{itemId=id,stamp=expected,state="failed",detail="PDF extraction failed or exceeded its limit. Retry this source or run OCR.",chunks=chunks.Count},chunks);}}
  static void AddChunks(List<ContentChunk> result,Entry e,string stamp,string kind,string text,SourceAnchor at,string clipId=""){
   text=text??"";for(int start=0;start<text.Length&&result.Count<2000;){int length=Math.Min(1200,text.Length-start);if(length==0)break;if(start+length<text.Length&&Char.IsHighSurrogate(text[start+length-1]))length--;var anchor=Json.Deserialize<SourceAnchor>(Json.Serialize(at));anchor.itemId=e.id;if(kind!="clip"){anchor.start=at.start+start;anchor.end=at.start+start+length;anchor.text=text.Substring(start,Math.Min(length,200));}var chunk=new ContentChunk{itemId=e.id,stamp=stamp,kind=kind,text=text.Substring(start,length),anchor=anchor,clipId=clipId};chunk.id=StudyHash(new{e.id,stamp,kind,anchor,clipId,index=result.Count}).Substring(0,32);result.Add(chunk);if(start+length>=text.Length)break;start+=Math.Max(1,length-100);}
  }
  public ContentIndexStatus IndexContentSource(string id,List<ContentPage> pages=null,string expectedStamp=null){
   EnsureNormalContent();Entry e;string stamp;ClipRecord[] clips;lock(gate){e=Find(id);if(e==null)throw new FileNotFoundException("Source was removed.");e=Json.Deserialize<Entry>(Json.Serialize(e));stamp=ContentStamp(id);clips=Db.workspace.clips.Where(c=>c.source!=null&&c.source.itemId==id).Select(c=>Json.Deserialize<ClipRecord>(Json.Serialize(c))).ToArray();}
   if(expectedStamp!=null&&stamp!=expectedStamp)throw new InvalidDataException("Source changed during extraction; rebuild its index.");
   var result=new List<ContentChunk>();var status=new ContentIndexStatus{itemId=id,stamp=stamp,state="ready",detail=""};
   AddChunks(result,e,stamp,"metadata",e.name+"\n"+SearchContent.Metadata(e),new SourceAnchor());
   foreach(var c in clips)AddChunks(result,e,stamp,"clip",c.title+"\n"+c.text,c.source,c.id);
   try{
    if(!File.Exists(e.path)){status.state="missing";status.detail="Original file is missing.";}
    else if(e.ext=="PDF"||e.kind=="photo"){
     if(pages==null){status.state=e.ext=="PDF"?"needs_extraction":"needs_ocr";status.detail=e.ext=="PDF"?"Extract PDF text to index its pages.":"OCR is required for image text.";}
     else{if(pages.Count>2000||pages.Any(p=>p==null||p.page<1||p.text==null)||pages.Sum(p=>(long)p.text.Length)>2000000)throw new InvalidDataException("Extracted text is too large.");foreach(var p in pages)AddChunks(result,e,stamp,e.ext=="PDF"?(p.method=="ocr"?"ocr_page":"page"):"ocr",p.text,new SourceAnchor{page=p.page});if(!pages.Any(p=>!String.IsNullOrWhiteSpace(p.text))){status.state="needs_ocr";status.detail="No text layer was found. Run OCR.";}}
    }else if(e.kind=="video"||e.kind=="music"){
     var tracks=e.generatedSubtitles!=null?new List<SubtitleTrack>{e.generatedSubtitles}:Subtitles.Discover(e.path);foreach(var cue in tracks.Take(1).SelectMany(t=>t.cues))AddChunks(result,e,stamp,"subtitle",cue.text,new SourceAnchor{time=cue.start,endTime=cue.end});if(pages!=null){if(pages.Count>2000||pages.Any(p=>p==null||p.text==null)||pages.Sum(p=>(long)p.text.Length)>2000000)throw new InvalidDataException("Extracted frame text is too large.");foreach(var p in pages.Where(p=>!String.IsNullOrWhiteSpace(p.text)))AddChunks(result,e,stamp,"ocr_frame",p.text,new SourceAnchor{time=p.time});if(pages.Any(p=>!String.IsNullOrWhiteSpace(p.text))){status.state="ready";status.detail="OCR frame text indexed.";}else if(!tracks.Any()){status.state="metadata_only";status.detail="No captions or frame text available.";}}else if(!tracks.Any()){status.state="metadata_only";status.detail="No captions available.";}
    }else if(e.ext=="PPTX"){
     int n=0;foreach(var slide in PresentationFiles.Read(e.path,false).slides)AddChunks(result,e,stamp,"slide",slide.title+"\n"+slide.notes+"\n"+String.Join("\n",slide.shapes.Select(s=>s.text)),new SourceAnchor{page=++n});
    }else if(new[]{"TXT","MD","MARKDOWN","CSV","JSON","LOG","HTML","HTM","DOCX","EPUB"}.Contains(e.ext)){
     if(new FileInfo(e.path).Length>32*1024*1024)throw new InvalidDataException("Source exceeds the 32 MB extraction limit.");int chapter=0;foreach(var part in ReadBook(e).chapters){if(!part.placeholder)AddChunks(result,e,stamp,"chapter",part.text,new SourceAnchor{chapter=chapter});chapter++;}
    }else{status.state="metadata_only";status.detail="This format has no text extractor.";}
   }catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException||ex is InvalidDataException||ex is System.Xml.XmlException){status.state="failed";status.detail="Text extraction failed; source files were preserved.";}
   status.partial=result.Count>=2000;if(status.partial)status.detail="Source reached the 2,000 chunk limit; only part was indexed.";status.chunks=result.Count;
   lock(gate){if(ContentStamp(id)!=stamp)throw new InvalidDataException("Source changed during extraction; rebuild its index.");store.ReplaceContent(status,result);}return status;
  }
  public ContentChunk ReadContentChunk(string chunkId,IEnumerable<string> allowed){var chunk=ReadContentChunks(allowed).FirstOrDefault(c=>c.id==chunkId);if(chunk==null)throw new InvalidDataException("The source excerpt is stale or outside the allowed scope.");return chunk;}
  internal List<ContentVector> ReadVectors(string model,IEnumerable<string> ids){lock(gate)return store.ContentVectors(model,ids);}
  internal void WriteVectors(List<ContentVector> vectors,Dictionary<string,string> stamps){lock(gate){if(stamps.Any(p=>ContentStamp(p.Key)!=p.Value))throw new InvalidDataException("Source changed during embedding; rebuild its index.");store.SaveContentVectors(vectors);}}
 }
}
