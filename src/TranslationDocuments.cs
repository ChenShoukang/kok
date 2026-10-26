using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.IO.Compression;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace kok {
    // Translate text nodes while keeping the original archive, relationships and styles.
    public sealed class TranslationDocument {
        public string format,signature;
        public List<string> parts=new List<string>(),displayParts=new List<string>();
        public List<int> sections=new List<int>();
        internal Dictionary<string,XDocument> documents=new Dictionary<string,XDocument>();
        internal List<List<int>> slots=new List<List<int>>();
        internal List<Tuple<XText,string>> texts=new List<Tuple<XText,string>>();
        internal Dictionary<XElement,int> blocks=new Dictionary<XElement,int>();
    }
    public static class TranslationDocuments {
        static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        static XDocument Xml(ZipArchiveEntry entry){if(entry==null||entry.Length>32*1024*1024)throw new InvalidDataException("文档内容过大或不完整。");using(var input=entry.Open())using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32*1024*1024}))return XDocument.Load(reader,LoadOptions.PreserveWhitespace);}
        static string Escape(string value){return System.Security.SecurityElement.Escape(value);}
        static void EpubIds(XElement right){var map=new Dictionary<string,string>();foreach(var attr in right.DescendantsAndSelf().Attributes().Where(a=>a.Name.LocalName=="id")){string next="kok-translated-"+attr.Value;map[attr.Value]=next;attr.Value=next;}foreach(var href in right.DescendantsAndSelf().Attributes().Where(a=>a.Name.LocalName=="href"&&a.Value.StartsWith("#"))){string value;if(map.TryGetValue(href.Value.Substring(1),out value))href.Value="#"+value;}}
        static void WordIds(XElement document){
            var table=document.Descendants(W+"tbl").FirstOrDefault();if(table==null)return;long last=1000;foreach(var attribute in document.Descendants().Attributes().Where(a=>a.Name.LocalName=="id")){long value;if(Int64.TryParse(attribute.Value,out value)&&value>=last&&value<Int32.MaxValue)last=value+1;}
            var cells=table.Elements(W+"tr").Select(row=>row.Elements(W+"tc").Skip(1).FirstOrDefault()).Where(c=>c!=null).ToList();var bookmarks=new Dictionary<string,string>();
            foreach(var cell in cells)foreach(var start in cell.Descendants(W+"bookmarkStart")){var id=start.Attribute(W+"id");if(id!=null){string old=id.Value;id.Value=(last++).ToString();bookmarks[old]=id.Value;}var name=start.Attribute(W+"name");if(name!=null)name.Value="kok_tr_"+last;}
            foreach(var cell in cells){
                foreach(var end in cell.Descendants(W+"bookmarkEnd")){var id=end.Attribute(W+"id");string mapped;if(id!=null&&bookmarks.TryGetValue(id.Value,out mapped))id.Value=mapped;}
                foreach(var element in cell.Descendants().Where(e=>e.Name.LocalName=="docPr"||e.Name==W+"id")){var id=element.Attribute("id");if(id!=null)id.Value=(last++).ToString();else if(element.Attribute(W+"val")!=null)element.SetAttributeValue(W+"val",last++);}
                foreach(var comment in cell.Descendants().Where(e=>new[]{"commentRangeStart","commentRangeEnd","commentReference"}.Contains(e.Name.LocalName)).ToList())comment.Remove();
            }
        }
        public static Dictionary<int,string> ValidatePart(string source,string translated){
            try{var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024};XElement before,after;using(var reader=XmlReader.Create(new StringReader("<kok>"+source+"</kok>"),settings))before=XElement.Load(reader);using(var reader=XmlReader.Create(new StringReader("<kok>"+translated+"</kok>"),settings))after=XElement.Load(reader);var expected=before.Elements().Select(e=>e.Name.LocalName).ToList();if(after.Nodes().OfType<XText>().Any(t=>!String.IsNullOrWhiteSpace(t.Value))||after.Elements().Count()!=expected.Count)throw new FormatException();var values=new Dictionary<int,string>();foreach(var element in after.Elements()){int slot;if(!expected.Contains(element.Name.LocalName)||!element.Name.LocalName.StartsWith("kok-r-")||!Int32.TryParse(element.Name.LocalName.Substring(6),out slot)||values.ContainsKey(slot)||element.HasElements)throw new FormatException();values.Add(slot,element.Value);}return values;}catch{throw new InvalidDataException("译文的格式标记不完整，请重试该段翻译。");}
        }
        static string Signature(string file){using(var sha=SHA256.Create())using(var input=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
        static void AddBlock(TranslationDocument result,XElement block,IEnumerable<XText> source,int section){
            var nodes=source.Where(t=>!String.IsNullOrWhiteSpace(t.Value)).ToList();if(nodes.Count==0)return;result.blocks[block]=section;var slots=new List<int>();var model=new StringBuilder();var display=new StringBuilder();
            Action flush=()=>{if(slots.Count==0)return;result.parts.Add(model.ToString());result.displayParts.Add(display.ToString());result.sections.Add(section);result.slots.Add(slots);slots=new List<int>();model.Clear();display.Clear();};
            foreach(var node in nodes){foreach(string chunk in TranslationService.SplitText(node.Value)){int slot=result.texts.Count;result.texts.Add(Tuple.Create(node,chunk));string tagged="<kok-r-"+slot+">"+Escape(chunk)+"</kok-r-"+slot+">";if(tagged.Length>6000)throw new InvalidDataException("单段文档内容过大。");if(model.Length+tagged.Length>2600)flush();model.Append(tagged);display.Append(chunk);slots.Add(slot);}}flush();
        }
        public static TranslationDocument Prepare(string file){
            var info=new FileInfo(file);if(info.Length>64*1024*1024)throw new InvalidDataException("原格式翻译支持 64 MiB 以内的 DOCX / EPUB。");var result=new TranslationDocument{format=info.Extension.TrimStart('.').ToLowerInvariant(),signature=Signature(file)};
            using(var zip=ZipFile.OpenRead(file)){
                if(zip.Entries.Count>20000||zip.Entries.Sum(e=>e.Length)>128*1024*1024)throw new InvalidDataException("文档内容过大或不完整。");
                if(result.format=="docx"){
                    var doc=Xml(zip.GetEntry("word/document.xml"));result.documents.Add("word/document.xml",doc);
                    foreach(var p in doc.Descendants(W+"p").ToList())AddBlock(result,p,p.Descendants(W+"t").Where(t=>t.Ancestors(W+"p").FirstOrDefault()==p).SelectMany(t=>t.Nodes().OfType<XText>()),0);
                }else if(result.format=="epub"){
                    XNamespace c="urn:oasis:names:tc:opendocument:xmlns:container",p="http://www.idpf.org/2007/opf";string opf=(string)Xml(zip.GetEntry("META-INF/container.xml")).Descendants(c+"rootfile").First().Attribute("full-path");var package=Xml(zip.GetEntry(opf));string baseDir=opf.Contains("/")?opf.Substring(0,opf.LastIndexOf('/')+1):"";var manifest=package.Descendants(p+"item").Where(e=>e.Attribute("id")!=null).ToDictionary(e=>(string)e.Attribute("id"));int section=0;
                    foreach(var itemref in package.Descendants(p+"spine").Elements(p+"itemref")){
                        XElement item;if(!manifest.TryGetValue((string)itemref.Attribute("idref"),out item))continue;string href=(string)item.Attribute("href");if(String.IsNullOrEmpty(href))continue;var uri=new Uri(new Uri("https://book.local/"+baseDir),href);if(uri.Host!="book.local")continue;string name=Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));if(result.documents.ContainsKey(name))continue;var doc=Xml(zip.GetEntry(name));var body=doc.Descendants().FirstOrDefault(e=>e.Name.LocalName=="body");if(body==null)continue;result.documents.Add(name,doc);
                        var blockNames=new[]{"p","h1","h2","h3","h4","h5","h6","li","blockquote","pre","td","th","div"};var blocks=body.Descendants().Where(e=>blockNames.Contains(e.Name.LocalName)&&!e.Descendants().Any(d=>blockNames.Contains(d.Name.LocalName))).ToList();
                        if(blocks.Count==0)blocks.Add(body);int before=result.parts.Count;foreach(var block in blocks)AddBlock(result,block,block.DescendantNodes().OfType<XText>().Where(t=>!t.Ancestors().Any(e=>new[]{"script","style","head"}.Contains(e.Name.LocalName))),section);if(result.parts.Count>before)section++;
                    }
                }else throw new InvalidDataException("原格式翻译支持 DOCX / EPUB。");
            }
            if(result.parts.Count==0||result.parts.Count>20000||result.parts.Sum(s=>(long)s.Length)>2000000)throw new InvalidDataException("翻译内容为空或过大，请按章节或页面翻译。");return result;
        }
        public static byte[] Export(string file,string signature,List<int> indices,List<string> translated,bool bilingual){
            var prepared=Prepare(file);if(prepared.signature!=signature)throw new InvalidDataException("原文件已改变，请重新翻译后导出。");
            if(indices==null||translated==null||indices.Count==0||indices.Count!=translated.Count||indices.Distinct().Count()!=indices.Count||indices.Any(i=>i<0||i>=prepared.parts.Count)||translated.Sum(t=>(long)(t??"").Length)>16*1024*1024)throw new InvalidDataException("译文与原文件不匹配。");
            var originals=prepared.documents.ToDictionary(p=>p.Key,p=>new XDocument(p.Value));var values=new Dictionary<int,string>();
            for(int n=0;n<indices.Count;n++){
                foreach(var value in ValidatePart(prepared.parts[indices[n]],translated[n]))values.Add(value.Key,value.Value);
            }
            foreach(var group in prepared.texts.Select((node,index)=>new{node,index}).GroupBy(x=>x.node.Item1)){group.Key.Value=String.Concat(group.Select(x=>values.ContainsKey(x.index)?values[x.index]:x.node.Item2));}
            if(bilingual)foreach(var pair in prepared.documents){var original=originals[pair.Key];if(prepared.format=="docx"){
                    XElement body=pair.Value.Root.Element(W+"body"),source=original.Root.Element(W+"body");var a=source.Elements().Where(e=>e.Name!=W+"sectPr").ToList();var b=body.Elements().Where(e=>e.Name!=W+"sectPr").ToList();var table=new XElement(W+"tbl",new XElement(W+"tblPr",new XElement(W+"tblW",new XAttribute(W+"w",21000),new XAttribute(W+"type","dxa")),new XElement(W+"tblLayout",new XAttribute(W+"type","fixed"))),new XElement(W+"tblGrid",new XElement(W+"gridCol",new XAttribute(W+"w",10500)),new XElement(W+"gridCol",new XAttribute(W+"w",10500))));
                    for(int n=0;n<b.Count;n++){var row=new XElement(W+"tr");foreach(var element in new[]{a[n],b[n]}){var cell=new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"tcW",new XAttribute(W+"w",10500),new XAttribute(W+"type","dxa"))),new XElement(element));if(element.Name!=W+"p")cell.Add(new XElement(W+"p"));row.Add(cell);}table.Add(row);}var section=body.Element(W+"sectPr");if(section==null)section=new XElement(W+"sectPr");else section=new XElement(section);section.Elements(W+"cols").Remove();section.Elements(W+"pgSz").Remove();section.Add(new XElement(W+"pgSz",new XAttribute(W+"w",23812),new XAttribute(W+"h",16838),new XAttribute(W+"orient","landscape")));body.ReplaceNodes(table,section);WordIds(pair.Value.Root);
                }else{
                    XElement body=pair.Value.Descendants().First(e=>e.Name.LocalName=="body"),old=original.Descendants().First(e=>e.Name.LocalName=="body");XNamespace h=body.Name.Namespace;var left=new XElement(h+"section",new XAttribute("class","kok-original"),old.Nodes().Select(Clone));var right=new XElement(h+"section",new XAttribute("class","kok-translated"),body.Nodes().Select(Clone).ToList());EpubIds(right);body.ReplaceNodes(new XElement(h+"div",new XAttribute("class","kok-bilingual"),left,right));var head=pair.Value.Descendants().FirstOrDefault(e=>e.Name.LocalName=="head");if(head!=null)head.Add(new XElement(h+"style",".kok-bilingual{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:2em;}.kok-bilingual>section{min-width:0;overflow-wrap:anywhere;}.kok-translated{border-left:1px solid #8885;padding-left:1.5em;}"));
                }}
            using(var output=new MemoryStream()){using(var source=ZipFile.OpenRead(file))using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))foreach(var entry in source.Entries.OrderBy(e=>e.FullName=="mimetype"?0:1)){
                    var copy=zip.CreateEntry(entry.FullName,entry.FullName=="mimetype"?CompressionLevel.NoCompression:CompressionLevel.Optimal);using(var dest=copy.Open()){XDocument xml;if(prepared.documents.TryGetValue(entry.FullName,out xml)){using(var writer=XmlWriter.Create(dest,new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false}))xml.Save(writer);}else using(var input=entry.Open())input.CopyTo(dest);}
                }return output.ToArray();}
        }
        static XNode Clone(XNode node){var element=node as XElement;if(element!=null)return new XElement(element);var text=node as XText;if(text!=null)return new XText(text.Value);var comment=node as XComment;if(comment!=null)return new XComment(comment.Value);return new XText("");}
    }
    public partial class MainWindow {
        static readonly string[] DocumentTranslationActions={"translationDocument","exportTranslationDocument","saveTranslationArtifact"};
        async Task<object> HandleDocumentTranslationAction(string action,Dictionary<string,object> args,Form owner){
            var entry=RequireEntry(args);var target=library;
            if(action=="translationDocument"){var prepared=await Task.Run(()=>TranslationDocuments.Prepare(entry.path));return new{format=prepared.format,signature=prepared.signature,parts=prepared.parts,displayParts=prepared.displayParts,sections=prepared.sections};}
            string format=StringArg(args,"format");byte[] bytes;
            if(action=="exportTranslationDocument"){
                var indices=Library.Json.Deserialize<List<int>>(Library.Json.Serialize(args["indices"]));var parts=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(args["parts"]));bool bilingual=args.ContainsKey("bilingual")&&Convert.ToBoolean(args["bilingual"]);backgroundLibraryWrites++;try{bytes=await Task.Run(()=>TranslationDocuments.Export(entry.path,StringArg(args,"signature"),indices,parts,bilingual));}finally{backgroundLibraryWrites--;}format=Path.GetExtension(entry.path).TrimStart('.').ToLowerInvariant();
            }else if(format=="pdf"){
                string encoded=StringArg(args,"base64");if(encoded.Length>44*1024*1024)throw new InvalidDataException("译文文件过大，请按章节或页面导出。");bytes=Convert.FromBase64String(encoded);if(bytes.Length<8||Encoding.ASCII.GetString(bytes,0,5)!="%PDF-")throw new InvalidDataException("PDF 内容无效。");
            }else if(new[]{"txt","md","markdown","html","htm","csv","json","log","srt"}.Contains(format)){string text=StringArg(args,"text");if(text.Length>16*1024*1024)throw new InvalidDataException("译文过大。");bytes=new UTF8Encoding(false).GetBytes(text);}
            else throw new InvalidDataException("请选择支持的导出格式。");
            if(library!=target)throw new OperationCanceledException();nativeFileDialogOpen=true;try{using(var dialog=new SaveFileDialog{Title=L("导出译文"),Filter=format.ToUpperInvariant()+"|*."+format,DefaultExt=format,AddExtension=true,FileName=entry.name+L("-译文")+"."+format}){if(dialog.ShowDialog(owner)!=DialogResult.OK)return false;EnsureExportDestination(dialog.FileName);if(!String.Equals(Path.GetExtension(dialog.FileName),"."+format,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("请选择支持的导出格式。");string temp=dialog.FileName+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);if(File.Exists(dialog.FileName))File.Replace(temp,dialog.FileName,null);else File.Move(temp,dialog.FileName);}finally{if(File.Exists(temp))File.Delete(temp);}return true;}}finally{nativeFileDialogOpen=false;}
        }
    }
}
