using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using kok;
class PresentationRulesTests {
 static void Put(ZipArchive zip,string name,string text){using(var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false)))writer.Write(text);}
 public static byte[] Fixture(bool external=false){using(var memory=new MemoryStream()){using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)){
  Put(zip,"[Content_Types].xml","<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'/>");
  Put(zip,"ppt/presentation.xml","<p:presentation xmlns:p='http://schemas.openxmlformats.org/presentationml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><p:sldIdLst><p:sldId id='256' r:id='rId1'/></p:sldIdLst><p:sldSz cx='12192000' cy='6858000'/></p:presentation>");
  Put(zip,"ppt/_rels/presentation.xml.rels","<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Target='slides/slide1.xml'/></Relationships>");
  Put(zip,"ppt/slides/slide1.xml","<p:sld xmlns:p='http://schemas.openxmlformats.org/presentationml/2006/main' xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><p:cSld><p:spTree><p:sp><p:nvSpPr><p:nvPr><p:ph type='title'/></p:nvPr></p:nvSpPr><p:spPr><a:xfrm><a:off x='914400' y='457200'/><a:ext cx='9144000' cy='914400'/></a:xfrm></p:spPr><p:txBody><a:p><a:r><a:rPr sz='3000' b='1'/><a:t>中文标题</a:t></a:r></a:p></p:txBody></p:sp>"+(external?"<p:pic><p:blipFill><a:blip r:embed='image1'/></p:blipFill></p:pic>":"")+"</p:spTree></p:cSld></p:sld>");
  if(external)Put(zip,"ppt/slides/_rels/slide1.xml.rels","<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='image1' Target='https://example.invalid/image.png' TargetMode='External'/></Relationships>");
 }return memory.ToArray();}}
 public static void Run(string root,Action<bool,string> check){Directory.CreateDirectory(root);var bytes=Fixture();PresentationDeck deck;using(var data=new MemoryStream(bytes))deck=PresentationFiles.Read(data);
  check(deck.slides.Count==1&&deck.slides[0].title=="中文标题"&&deck.slides[0].shapes[0].bold&&deck.slides[0].shapes[0].fontSize==30,"PPTX preview extracts Unicode text, geometry and basic formatting");
  check(Math.Abs(deck.width-13.333333)<.001&&deck.slides[0].shapes[0].x==1,"PPTX geometry retains its source aspect ratio and positions");
  using(var data=new MemoryStream(Fixture(true)))check(PresentationFiles.Read(data).unsupported==1,"PPTX external image relationships are skipped without a network request");
  bool invalid=false;try{PresentationFiles.Decode("not a package");}catch{invalid=true;}check(invalid,"invalid PPTX exports are rejected before any destination file is opened");
  var library=new Library(Path.Combine(root,"data"));Entry first=library.CreatePresentation("Presentation","Slides",bytes),second=library.CreatePresentation("Presentation","Slides",bytes);
  check(first.ext=="PPTX"&&second.name=="Presentation (2)"&&File.ReadAllBytes(first.path).SequenceEqual(bytes),"new PPTX files are classified and duplicate names never replace originals");
  invalid=false;try{library.CreatePresentation("../escape","",bytes);}catch(InvalidDataException){invalid=true;}check(invalid,"presentation creation rejects paths outside the managed document directory");
  check(library.BuildKnowledgeIndex().nodes.Any(n=>n.id==first.id),"PPTX readable text participates in the local knowledge index");
  check(VisualEditRules.Parse("{\"text\":\"Changed selection\"}")=="Changed selection","visual edits accept a bounded text replacement without tool commands");
  invalid=false;try{VisualEditRules.Parse("{\"text\":\"\"}");}catch(InvalidDataException){invalid=true;}check(invalid,"empty visual edits cannot silently delete a selection");
  var blocks=new List<VisualEditBlock>{new VisualEditBlock{id="title",text="Title"},new VisualEditBlock{id="body",text="Body"}};
  check(VisualEditRules.ParseBlocks("{\"edits\":[{\"id\":\"body\",\"text\":\"New body\"},{\"id\":\"title\",\"text\":\"New title\"}]}",blocks).Count==2,"whole-page edits retain exact object IDs before application");
  foreach(string answer in new[]{"{\"edits\":[{\"id\":\"title\",\"text\":\"A\"},{\"id\":\"title\",\"text\":\"B\"}]}","{\"edits\":[{\"id\":\"title\",\"text\":\"A\"},{\"id\":\"outside\",\"text\":\"B\"}]}"}){invalid=false;try{VisualEditRules.ParseBlocks(answer,blocks);}catch(InvalidDataException){invalid=true;}check(invalid,"duplicate and out-of-page AI object IDs are rejected atomically");}
 }
}
