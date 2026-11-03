using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;

namespace kok {
 // Patch the package rather than flattening a deck through the preview model.
 // Untouched slides, media, charts, masters and relationships remain intact.
 internal static class AgentPresentationXml {
  internal static readonly XNamespace P="http://schemas.openxmlformats.org/presentationml/2006/main",A="http://schemas.openxmlformats.org/drawingml/2006/main",R="http://schemas.openxmlformats.org/officeDocument/2006/relationships",K="http://schemas.openxmlformats.org/package/2006/relationships",C="http://schemas.openxmlformats.org/package/2006/content-types";
  internal static XElement Read(ZipArchive zip,string part){var entry=zip.GetEntry(part);if(entry==null)throw new InvalidDataException("PPTX 文件结构无效。");using(var input=entry.Open())using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024}))return XElement.Load(reader);}
  internal static void Write(ZipArchive zip,string part,XElement xml){var old=zip.GetEntry(part);if(old!=null)old.Delete();using(var output=zip.CreateEntry(part,CompressionLevel.Optimal).Open())using(var writer=new StreamWriter(output,new UTF8Encoding(false)))writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"+xml.ToString(SaveOptions.DisableFormatting));}
  static string RelPath(string part){return Path.GetDirectoryName(part).Replace('\\','/')+"/_rels/"+Path.GetFileName(part)+".rels";}
  static XElement Relationships(ZipArchive zip,string part){return zip.GetEntry(RelPath(part))==null?new XElement(K+"Relationships"):Read(zip,RelPath(part));}
  static string Resolve(string part,XElement relationship){Uri target;if(relationship==null||(string)relationship.Attribute("TargetMode")=="External"||!Uri.TryCreate(new Uri("https://presentation.local/"+part),(string)relationship.Attribute("Target"),out target)||target.Host!="presentation.local"||target.Scheme!="https")throw new InvalidDataException("PPTX 文件结构无效。");return Uri.UnescapeDataString(target.AbsolutePath.TrimStart('/'));}
  static void Override(ZipArchive zip,string part,string type){var xml=Read(zip,"[Content_Types].xml");if(!xml.Elements().Any(e=>(string)e.Attribute("PartName")=="/"+part)){xml.Add(new XElement(C+"Override",new XAttribute("PartName","/"+part),new XAttribute("ContentType",type)));Write(zip,"[Content_Types].xml",xml);}}
  static string Plain(XElement shape){return String.Join("\n",shape.Descendants(A+"p").Select(p=>String.Concat(p.Descendants().Where(e=>e.Name==A+"t"||e.Name==A+"br").Select(e=>e.Name==A+"br"?"\n":e.Value))));}
  static void SetText(XElement shape,string text){
   var body=shape.Element(P+"txBody");if(body==null)throw new InvalidDataException("指定的元素不包含可编辑文字。");var paragraphs=body.Elements(A+"p").ToList();var first=paragraphs.FirstOrDefault();var run=first==null?null:first.Descendants(A+"rPr").FirstOrDefault();var ppr=first==null?null:first.Element(A+"pPr");
   foreach(var paragraph in paragraphs)paragraph.Remove();foreach(string line in text.Split('\n'))body.Add(new XElement(A+"p",ppr==null?null:new XElement(ppr),new XElement(A+"r",run==null?null:new XElement(run),new XElement(A+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line))));
  }
  static void ReplaceText(XElement shape,string before,string after){
   string text=Plain(shape);int at=text.IndexOf(before,StringComparison.Ordinal);if(at<0||text.IndexOf(before,at+1,StringComparison.Ordinal)>=0)throw new InvalidOperationException("原文片段未找到或不唯一，请重新读取幻灯片。");
   // Single-line replacements retain all unaffected runs and their formatting.
   if(before.IndexOf('\n')<0&&after.IndexOf('\n')<0&&!shape.Descendants(A+"br").Any()){
    int offset=0;var nodes=shape.Descendants(A+"t").ToList();foreach(var paragraph in shape.Descendants(A+"p")){
     var runs=paragraph.Descendants(A+"t").ToList();int length=runs.Sum(e=>e.Value.Length);if(at>=offset&&at+before.Length<=offset+length){int local=at-offset,end=local+before.Length,index=0;bool wrote=false;foreach(var node in runs){string value=node.Value;int from=index,to=index+value.Length;index=to;if(to<=local||from>=end)continue;string left=from<local?value.Substring(0,local-from):"",right=to>end?value.Substring(end-from):"";node.Value=left+(wrote?"":after)+right;wrote=true;}if(wrote)return;}offset+=length+1;
    }
   }
   SetText(shape,text.Substring(0,at)+after+text.Substring(at+before.Length));
  }
  internal static void Notes(ZipArchive zip,string slidePart,string text){
   var links=Relationships(zip,slidePart);var relationship=links.Elements().FirstOrDefault(e=>((string)e.Attribute("Type")??"").EndsWith("/notesSlide",StringComparison.Ordinal));if(relationship==null&&String.IsNullOrEmpty(text))return;
   string part;if(relationship==null){int n=1;while(zip.GetEntry("ppt/notesSlides/notesSlide"+n+".xml")!=null)n++;part="ppt/notesSlides/notesSlide"+n+".xml";string id="kokNotes";while(links.Elements().Any(e=>(string)e.Attribute("Id")==id))id+="x";links.Add(new XElement(K+"Relationship",new XAttribute("Id",id),new XAttribute("Type",R.NamespaceName+"/notesSlide"),new XAttribute("Target","../notesSlides/notesSlide"+n+".xml")));Write(zip,RelPath(slidePart),links);Write(zip,RelPath(part),new XElement(K+"Relationships",new XElement(K+"Relationship",new XAttribute("Id","slide"),new XAttribute("Type",R.NamespaceName+"/slide"),new XAttribute("Target","../slides/"+Path.GetFileName(slidePart)))));Override(zip,part,"application/vnd.openxmlformats-officedocument.presentationml.notesSlide+xml");
    var shape=new XElement(P+"sp",new XElement(P+"nvSpPr",new XElement(P+"cNvPr",new XAttribute("id",2),new XAttribute("name","Notes")),new XElement(P+"cNvSpPr"),new XElement(P+"nvPr",new XElement(P+"ph",new XAttribute("type","body"),new XAttribute("idx",1)))),new XElement(P+"spPr"),new XElement(P+"txBody",new XElement(A+"bodyPr"),new XElement(A+"lstStyle"),new XElement(A+"p")));
    SetText(shape,text);Write(zip,part,new XElement(P+"notes",new XAttribute(XNamespace.Xmlns+"p",P),new XAttribute(XNamespace.Xmlns+"a",A),new XElement(P+"cSld",new XElement(P+"spTree",new XElement(P+"nvGrpSpPr",new XElement(P+"cNvPr",new XAttribute("id",1),new XAttribute("name","")),new XElement(P+"cNvGrpSpPr"),new XElement(P+"nvPr")),new XElement(P+"grpSpPr"),shape)),new XElement(P+"clrMapOvr",new XElement(A+"masterClrMapping"))));
   }else{part=Resolve(slidePart,relationship);var xml=Read(zip,part);var body=xml.Descendants(P+"sp").FirstOrDefault(e=>e.Descendants(P+"ph").Any(ph=>(string)ph.Attribute("type")=="body"));if(body==null)throw new InvalidDataException("演讲备注结构无法编辑。");SetText(body,text);Write(zip,part,xml);}
  }
  internal static byte[] Edit(byte[] original,Dictionary<string,object> args){
   var edits=AgentTaskState.Get<List<Dictionary<string,object>>>(args,"edits");string theme=AgentSlideDesign.Value(args,"theme");if(theme=="")theme="editorial";
   PresentationDeck deck;using(var input=new MemoryStream(original))deck=PresentationFiles.Read(input,false);if(!args.ContainsKey("theme"))theme=deck.slides[0].background=="111218"?"midnight":deck.slides[0].background=="F7F5FF"?"violet":deck.slides[0].background=="FFF9F0"?"editorial":"paper";
   using(var memory=new MemoryStream()){memory.Write(original,0,original.Length);using(var zip=new ZipArchive(memory,ZipArchiveMode.Update,true)){
    var presentation=Read(zip,"ppt/presentation.xml");var order=presentation.Element(P+"sldIdLst");if(order==null)throw new InvalidDataException("PPTX 文件结构无效。");var links=Relationships(zip,"ppt/presentation.xml");
    foreach(var edit in edits){string action=AgentSlideDesign.Value(edit,"action");var rows=order.Elements(P+"sldId").ToList();int page=Convert.ToInt32(edit["page"]);if(action=="insert_slide"){if(page<0||page>rows.Count||rows.Count>=100)throw new InvalidDataException("幻灯片页码超出范围。");int n=1;while(zip.GetEntry("ppt/slides/slide"+n+".xml")!=null)n++;string part="ppt/slides/slide"+n+".xml",rel="kokSlide"+n;var layout=zip.Entries.FirstOrDefault(e=>e.FullName.StartsWith("ppt/slideLayouts/",StringComparison.Ordinal)&&e.FullName.EndsWith(".xml",StringComparison.Ordinal));if(layout==null)throw new InvalidDataException("演示文稿缺少可用于新页面的版式。");while(links.Elements().Any(e=>(string)e.Attribute("Id")==rel))rel+="x";long id=rows.Select(e=>(long?)e.Attribute("id")??255).Max()+1;var row=new XElement(P+"sldId",new XAttribute("id",id),new XAttribute(R+"id",rel));if(page==0)order.AddFirst(row);else rows[page-1].AddAfterSelf(row);links.Add(new XElement(K+"Relationship",new XAttribute("Id",rel),new XAttribute("Type",R.NamespaceName+"/slide"),new XAttribute("Target","slides/slide"+n+".xml")));Write(zip,RelPath("ppt/presentation.xml"),links);Write(zip,part,AgentSlideDesign.Slide(AgentTaskState.Get<Dictionary<string,object>>(edit,"slide"),page+1,theme,deck.width,deck.height));Write(zip,RelPath(part),new XElement(K+"Relationships",new XElement(K+"Relationship",new XAttribute("Id","layout"),new XAttribute("Type",R.NamespaceName+"/slideLayout"),new XAttribute("Target","../slideLayouts/"+Path.GetFileName(layout.FullName)))));Override(zip,part,"application/vnd.openxmlformats-officedocument.presentationml.slide+xml");Notes(zip,part,AgentSlideDesign.Value(AgentTaskState.Get<Dictionary<string,object>>(edit,"slide"),"notes"));continue;}
     if(page<1||page>rows.Count)throw new InvalidDataException("幻灯片页码超出范围。");var current=rows[page-1];string source=Resolve("ppt/presentation.xml",links.Elements().FirstOrDefault(e=>(string)e.Attribute("Id")== (string)current.Attribute(R+"id")));
     if(action=="delete_slide"){if(rows.Count<=1)throw new InvalidDataException("演示文稿至少保留一页。");current.Remove();continue;}
     if(action=="move_slide"){int to=Convert.ToInt32(edit["to"]);if(to<1||to>rows.Count||to==page)throw new InvalidDataException("幻灯片页码超出范围。");current.Remove();if(to==1)order.AddFirst(current);else order.Elements(P+"sldId").ElementAt(to-2).AddAfterSelf(current);continue;}
     if(action=="replace_slide"){var value=AgentTaskState.Get<Dictionary<string,object>>(edit,"slide");Write(zip,source,AgentSlideDesign.Slide(value,page,theme,deck.width,deck.height));Notes(zip,source,AgentSlideDesign.Value(value,"notes"));continue;}
     if(action=="notes"){Notes(zip,source,AgentSlideDesign.Value(edit,"new_text"));continue;}
     var xml=Read(zip,source);var shapes=xml.Descendants(P+"spTree").First().Elements().Where(e=>e.Name==P+"sp"||e.Name==P+"pic").ToList();string prefix="slide-"+(string)current.Attribute("id")+"-",shapeId=AgentSlideDesign.Value(edit,"shape_id");int index;if(!shapeId.StartsWith(prefix,StringComparison.Ordinal)||!Int32.TryParse(shapeId.Substring(prefix.Length),out index)||index<0||index>=shapes.Count||shapes[index].Name!=P+"sp")throw new InvalidDataException("指定的幻灯片元素已变化，请重新读取。");ReplaceText(shapes[index],AgentSlideDesign.Value(edit,"old_text"),AgentSlideDesign.Value(edit,"new_text"));Write(zip,source,xml);
    }
    Write(zip,"ppt/presentation.xml",presentation);Write(zip,RelPath("ppt/presentation.xml"),links);
   }byte[] bytes=memory.ToArray();using(var input=new MemoryStream(bytes))PresentationFiles.Read(input,false);return bytes;}
  }
 }
}
