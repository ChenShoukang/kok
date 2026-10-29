using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace kok {
 public class PresentationShape {
  public string id,kind="text",text="",fontFamily="Microsoft YaHei",color="222222",fill="",line="",image="",align="left";
  public double x,y,w=5,h=1,fontSize=24,rotation;
  public bool bold,italic;
 }
 public class PresentationSlide {public string id,title="",notes="",background="FFFFFF";public List<PresentationShape> shapes=new List<PresentationShape>();}
 public class PresentationDeck {public string name="";public double width=13.333333,height=7.5;public int unsupported;public List<PresentationSlide> slides=new List<PresentationSlide>();}
 public static class PresentationFiles {
  static readonly XNamespace P="http://schemas.openxmlformats.org/presentationml/2006/main",A="http://schemas.openxmlformats.org/drawingml/2006/main",R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
  const int Limit=32*1024*1024;
  static string Attr(XElement element,string name,string fallback=""){return element==null?fallback:(string)element.Attribute(name)??fallback;}
  static double Number(XElement element,string name,double fallback=0){double value;return Double.TryParse(Attr(element,name),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value)&&!Double.IsInfinity(value)&&!Double.IsNaN(value)?value:fallback;}
  static XDocument Xml(ZipArchive zip,string name){var entry=zip.GetEntry(name);if(entry==null)return null;if(entry.Length>2*1024*1024)throw new InvalidDataException("幻灯片 XML 超过轻量预览限制。");using(var stream=entry.Open())using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024}))return XDocument.Load(reader);}
  static Dictionary<string,string> Relations(ZipArchive zip,string part){string folder=Path.GetDirectoryName(part).Replace('\\','/'),file=Path.GetFileName(part);var xml=Xml(zip,folder+"/_rels/"+file+".rels");var map=new Dictionary<string,string>();if(xml!=null)foreach(var e in xml.Root.Elements())if(Attr(e,"TargetMode")!="External"){Uri target;if(Uri.TryCreate(new Uri("https://presentation.local/"+part),Attr(e,"Target"),out target)&&target.Host=="presentation.local"&&target.Scheme=="https")map[Attr(e,"Id")]=Uri.UnescapeDataString(target.AbsolutePath.TrimStart('/'));}return map;}
  static string Hex(XElement element,Dictionary<string,string> colors,string fallback=""){
   if(element==null)return fallback;var raw=element.DescendantsAndSelf().FirstOrDefault(e=>e.Name==A+"srgbClr"||e.Name==A+"schemeClr"||e.Name==A+"sysClr");if(raw==null)return fallback;
   string value=Attr(raw,raw.Name==A+"sysClr"?"lastClr":"val");if(raw.Name==A+"schemeClr"){if(value=="tx1")value="dk1";if(value=="bg1")value="lt1";if(value=="tx2")value="dk2";if(value=="bg2")value="lt2";value=colors.ContainsKey(value)?colors[value]:fallback;}
   return Regex.IsMatch(value??"","^[0-9A-Fa-f]{6}$")?value.ToUpperInvariant():fallback;
  }
  static string Text(XElement shape){return String.Join("\n",shape.Descendants(A+"p").Select(p=>String.Concat(p.Descendants().Where(n=>n.Name==A+"t"||n.Name==A+"br").Select(n=>n.Name==A+"br"?"\n":n.Value))));}
  public static PresentationDeck Read(string path,bool images=true){using(var stream=File.OpenRead(path)){if(stream.Length>Limit)throw new InvalidDataException("PPTX 超过 32 MB，请使用系统应用。");return Read(stream,images);}}
  public static PresentationDeck Read(Stream stream,bool images=true){
   using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true)){
    if(zip.Entries.Count>6000||zip.Entries.Sum(e=>e.Length)>128L*1024*1024||zip.GetEntry("ppt/vbaProject.bin")!=null)throw new InvalidDataException("PPTX 结构超过轻量预览限制。");
    var presentation=Xml(zip,"ppt/presentation.xml");if(presentation==null||zip.GetEntry("[Content_Types].xml")==null)throw new InvalidDataException("PPTX 文件结构无效。");
    var deck=new PresentationDeck();var dimensions=presentation.Root.Element(P+"sldSz");deck.width=Number(dimensions,"cx",12192000)/914400;deck.height=Number(dimensions,"cy",6858000)/914400;if(deck.width<1||deck.width>60||deck.height<1||deck.height>60)throw new InvalidDataException("幻灯片尺寸无效。");
    var theme=Xml(zip,"ppt/theme/theme1.xml");var colors=new Dictionary<string,string>{{"dk1","000000"},{"lt1","FFFFFF"},{"accent1","4472C4"}};if(theme!=null)foreach(var color in theme.Descendants(A+"clrScheme").Elements())colors[color.Name.LocalName]=Hex(color,colors,"222222");
    var relations=Relations(zip,"ppt/presentation.xml");var ids=presentation.Descendants(P+"sldId").ToList();if(ids.Count<1||ids.Count>100)throw new InvalidDataException("轻量演示支持 1 至 100 张幻灯片。");long imageBytes=0;int textCharacters=0;
    foreach(var row in ids){string source;if(!relations.TryGetValue((string)row.Attribute(R+"id")??"",out source))throw new InvalidDataException("PPTX 文件结构无效。");var xml=Xml(zip,source);if(xml==null)throw new InvalidDataException("PPTX 文件结构无效。");var links=Relations(zip,source);var slide=new PresentationSlide{id="slide-"+Attr(row,"id")};slide.background=Hex(xml.Descendants(P+"bgPr").FirstOrDefault(),colors,"FFFFFF");
     var tree=xml.Descendants(P+"spTree").FirstOrDefault();if(tree==null)throw new InvalidDataException("PPTX 文件结构无效。");deck.unsupported+=xml.Descendants(P+"timing").Count()+xml.Descendants(P+"transition").Count();var elements=tree.Elements().Where(e=>e.Name==P+"sp"||e.Name==P+"pic").ToList();deck.unsupported+=tree.Elements().Count(e=>e.Name==P+"graphicFrame"||e.Name==P+"grpSp");if(elements.Count>500)throw new InvalidDataException("幻灯片元素过多，请使用系统应用。");int index=0;
     foreach(var element in elements){var shape=new PresentationShape{id=slide.id+"-"+index++};XElement transform=element.Descendants(A+"xfrm").FirstOrDefault(),off=transform==null?null:transform.Element(A+"off"),ext=transform==null?null:transform.Element(A+"ext");bool title=element.Descendants(P+"ph").Any(e=>new[]{"title","ctrTitle"}.Contains(Attr(e,"type")));shape.x=Number(off,"x",548640)/914400;shape.y=Number(off,"y",title?365760:1645920)/914400;shape.w=Math.Min(60,Math.Max(.05,Number(ext,"cx",deck.width*914400-1097280)/914400));shape.h=Math.Min(60,Math.Max(.05,Number(ext,"cy",title?914400:deck.height*914400-2194560)/914400));shape.rotation=Number(transform,"rot")/60000;
      var properties=element.Element(P+"spPr");shape.fill=Hex(properties==null?null:properties.Element(A+"solidFill"),colors);shape.line=Hex(properties==null?null:properties.Element(A+"ln"),colors);var geometry=properties==null?null:properties.Element(A+"prstGeom");if(Attr(geometry,"prst")=="ellipse")shape.kind="ellipse";else if(Attr(geometry,"prst")=="roundRect")shape.kind="roundRect";
      if(element.Name==P+"pic"){
       shape.kind="image";var blip=element.Descendants(A+"blip").FirstOrDefault();string target;if(!links.TryGetValue((string)(blip==null?null:blip.Attribute(R+"embed"))??"",out target)){deck.unsupported++;continue;}var entry=zip.GetEntry(target);string type=Path.GetExtension(target).ToLowerInvariant(),mime=type==".png"?"image/png":type==".jpg"||type==".jpeg"?"image/jpeg":type==".gif"?"image/gif":type==".bmp"?"image/bmp":"";if(entry==null||entry.Length>8*1024*1024||mime==""){deck.unsupported++;continue;}
       if(images){imageBytes+=entry.Length;if(imageBytes>20*1024*1024)throw new InvalidDataException("PPTX 图片超过轻量预览限制。");using(var data=entry.Open())using(var buffer=new MemoryStream()){data.CopyTo(buffer);shape.image="data:"+mime+";base64,"+Convert.ToBase64String(buffer.ToArray());}}
      }else{
       shape.text=Text(element);textCharacters+=shape.text.Length;if(shape.text.Length>50000||textCharacters>2000000)throw new InvalidDataException("幻灯片文字过长。");var run=element.Descendants().FirstOrDefault(e=>e.Name==A+"rPr"||e.Name==A+"defRPr");shape.fontSize=Math.Max(6,Math.Min(160,Number(run,"sz",title?3200:2400)/100));shape.bold=Attr(run,"b")=="1";shape.italic=Attr(run,"i")=="1";shape.color=Hex(run,colors,"222222");var face=run==null?null:run.Element(A+"ea")??run.Element(A+"latin");shape.fontFamily=Attr(face,"typeface","Microsoft YaHei");if(shape.fontFamily.StartsWith("+"))shape.fontFamily="Microsoft YaHei";string alignment=Attr(element.Descendants(A+"pPr").FirstOrDefault(),"algn");shape.align=alignment=="ctr"?"center":alignment=="r"?"right":"left";if(title)slide.title=shape.text;
      }slide.shapes.Add(shape);
     }
     foreach(string notesPart in links.Values.Where(v=>v.StartsWith("ppt/notesSlides/",StringComparison.Ordinal)&&v.EndsWith(".xml",StringComparison.OrdinalIgnoreCase))){var notes=Xml(zip,notesPart);if(notes!=null)slide.notes=String.Join("\n",notes.Descendants(P+"sp").Where(sp=>sp.Descendants(P+"ph").Any(ph=>Attr(ph,"type")=="body")).Select(Text));}
     textCharacters+=slide.notes.Length;if(slide.notes.Length>50000||textCharacters>2000000)throw new InvalidDataException("幻灯片文字过长。");if(slide.title=="")slide.title=slide.shapes.Where(s=>s.kind!="image"&&!String.IsNullOrWhiteSpace(s.text)).Select(s=>s.text.Split('\n')[0]).FirstOrDefault()??"";deck.slides.Add(slide);
    }return deck;
   }
  }
  public static byte[] Decode(string encoded){if(encoded==null||encoded.Length>44*1024*1024)throw new InvalidDataException("PPTX 超过 32 MB，请使用系统应用。");byte[] bytes;try{bytes=Convert.FromBase64String(encoded);}catch{throw new InvalidDataException("PPTX 文件结构无效。");}if(bytes.Length>Limit)throw new InvalidDataException("PPTX 超过 32 MB，请使用系统应用。");using(var stream=new MemoryStream(bytes))Read(stream,false);return bytes;}
 }
 public partial class Library {
  public Entry CreatePresentation(string name,string collection,byte[] bytes,string operationId="",string operationSource=""){
   if(IsPrivate)throw new InvalidOperationException("请先返回普通资料库新建或保存文档。");name=(name??"").Trim();if(name.EndsWith(".pptx",StringComparison.OrdinalIgnoreCase))name=name.Substring(0,name.Length-5);collection=collection??"";
   if(name.Length<1||name.Length>100||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith(".")||Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase)||collection.Length>100||collection.Any(Char.IsControl))throw new InvalidDataException("请输入有效的文档名称，最多 100 个字符。");
   lock(gate){CheckDocumentDirectory();string file=null;for(int n=1;n<10000;n++){var candidate=Path.Combine(DocumentDirectory,name+(n==1?"":" ("+n+")")+".pptx");try{using(var stream=new FileStream(candidate,FileMode.CreateNew,FileAccess.Write))stream.Write(bytes,0,bytes.Length);file=candidate;break;}catch(IOException){if(!File.Exists(candidate))throw;}}if(file==null)throw new IOException("同名文档过多，请更换名称。");var info=new FileInfo(file);var entry=new Entry{id=Guid.NewGuid().ToString("N"),name=Path.GetFileNameWithoutExtension(file),path=file,agentOperationId=operationId,ext="PPTX",kind="document",collection=collection,size=info.Length,added=DateTime.UtcNow.ToString("o"),modified=info.LastWriteTimeUtc.ToString("o")};Db.items.Add(entry);bool added=collection!=""&&!Db.collections.Contains(collection);if(added)Db.collections.Add(collection);Operation createdOp=null;try{if(operationSource!="")createdOp=TrackCreatedOperation(entry,operationId,operationSource,"presentations");Save();}catch{if(createdOp!=null)Db.operations.Remove(createdOp);Db.items.Remove(entry);if(added)Db.collections.Remove(collection);File.Delete(file);throw;}return entry;}
  }
 }
 public partial class MainWindow {
  async Task<object> HandlePresentation(string action,Dictionary<string,object> args,System.Windows.Forms.Form owner){
   var target=library;
   if(action=="readPresentation"){var entry=RequireEntry(args);if(entry.ext!="PPTX")throw new InvalidDataException("请使用 PPTX 格式；旧版 PPT 请先转换。");var deck=await Task.Run(()=>PresentationFiles.Read(entry.path));if(library!=target)throw new InvalidOperationException("文件已切换。");deck.name=entry.name;return deck;}
   byte[] bytes=PresentationFiles.Decode(StringArg(args,"base64"));
   if(action=="newPresentation"){backgroundLibraryWrites++;try{var item=await Task.Run(()=>target.CreatePresentation(StringArg(args,"name"),StringArg(args,"collection"),bytes));Notify("libraryChanged",null);return item;}finally{backgroundLibraryWrites--;}}
   var original=RequireEntry(args);string destination=ChooseEditDestination(owner,original,".pptx","PowerPoint|*.pptx");if(destination==null)return new{cancelled=true};backgroundLibraryWrites++;
   try{string temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}finally{if(File.Exists(temp))File.Delete(temp);}target.Import(new[]{destination},null);Notify("libraryChanged",null);return new{saved=true,name=Path.GetFileName(destination)};}finally{backgroundLibraryWrites--;}
  }
 }
}
