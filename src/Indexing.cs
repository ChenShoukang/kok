using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace kok {
 public partial class Library {
  internal static string IndexText(Entry item){
   if(item.kind=="video"||item.kind=="music")return String.Join("\n",Subtitles.Discover(item.path).SelectMany(t=>t.cues).Select(c=>c.text));
   if(new FileInfo(item.path).Length>32*1024*1024)return null;
   if(item.ext=="PPTX")return String.Join("\n\n",PresentationFiles.Read(item.path,false).slides.Select(s=>s.title+"\n"+s.notes+"\n"+String.Join("\n",s.shapes.Select(shape=>shape.text))));
   if(!new[]{"TXT","MD","MARKDOWN","CSV","JSON","LOG","HTML","HTM","DOCX","EPUB"}.Contains(item.ext))return null;
   return String.Join("\n\n",ReadBook(item).chapters.Select(c=>c.text));
  }
 }
 internal static class SearchContent {
  internal static string Metadata(Entry item){
   object text;string draft=item.ownedDocument&&item.editDraft!=null&&item.editDraft.TryGetValue("text",out text)?Convert.ToString(text):"";
   return String.Join("\n",new[]{item.sourceInfo==null?"":item.sourceInfo.url,item.notes,item.collection,item.aiSummary,item.album,item.artist,draft,String.Join(" ",item.tags??new List<string>()),String.Join("\n",(item.chapterTexts??new Dictionary<string,string>()).Values),String.Join("\n",(item.annotations??new List<Annotation>()).Select(a=>a.text+" "+a.note)),String.Join("\n",(item.generatedSubtitles==null?new List<Cue>():item.generatedSubtitles.cues).Select(c=>c.text)),String.Join("\n",(item.videoOutline==null?new List<VideoChapter>():item.videoOutline.chapters).Select(c=>c.title+" "+c.summary))});
  }
 }
}
