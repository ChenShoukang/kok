using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO.Compression;

namespace kok {
    public static class BookNavigation {
        public static XDocument ReadXml(ZipArchiveEntry entry){
            if(entry==null||entry.Length>32*1024*1024)throw new InvalidDataException("阅读内容过大或不完整。");string text;using(var input=new StreamReader(entry.Open(),Encoding.UTF8,true))text=input.ReadToEnd();
            text=Regex.Replace(text,@"&([A-Za-z][A-Za-z0-9]+);",m=>new[]{"amp","lt","gt","quot","apos"}.Contains(m.Groups[1].Value)?m.Value:System.Net.WebUtility.HtmlDecode(m.Value));
            using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,MaxCharactersInDocument=32*1024*1024}))return XDocument.Load(reader,LoadOptions.PreserveWhitespace);
        }
        static void Add(Book book,string title,int chapter,int start,int depth,string source){title=Regex.Replace(title??"",@"\s+"," ").Trim();if(title.Length==0||title.Length>240||chapter<0||chapter>=book.chapters.Count||book.toc.Count>=4000)return;start=Math.Max(0,Math.Min(book.chapters[chapter].text.Length,start));if(book.toc.Any(t=>t.chapter==chapter&&t.start==start&&t.title==title))return;book.toc.Add(new ReadingToc{title=title,chapter=chapter,start=start,depth=Math.Min(6,Math.Max(0,depth)),source=source});}
        static string Resolve(string file,string href,out string fragment){Uri uri;fragment="";if(!Uri.TryCreate(new Uri("https://book.local/"+file),href??"",out uri))return "";fragment=Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));return uri.Host=="book.local"?Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')):"";}
        static Dictionary<string,int> Anchors(ZipArchiveEntry entry){
            var doc=ReadXml(entry);var body=doc.Descendants().FirstOrDefault(e=>e.Name.LocalName=="body")??doc.Root;
            foreach(var node in body.Descendants().Where(e=>new[]{"script","style","iframe","object","head"}.Contains(e.Name.LocalName)).ToList())node.Remove();
            var blocks=body.Descendants().Where(e=>new[]{"p","h1","h2","h3","h4","li","blockquote","pre"}.Contains(e.Name.LocalName)&&!e.Ancestors().Any(a=>a!=body&&new[]{"p","li","blockquote","pre"}.Contains(a.Name.LocalName))).Where(e=>e.Value.Trim().Length>0).ToList();
            string title=body.Descendants().Where(e=>new[]{"h1","h2","h3"}.Contains(e.Name.LocalName)).Select(e=>e.Value.Trim()).FirstOrDefault();if(blocks.Count>1&&blocks[0].Value.Trim()==title)blocks.RemoveAt(0);if(blocks.Count==0)blocks.Add(body);
            var offsets=new Dictionary<string,int>();var positions=new Dictionary<XElement,int>();int offset=0;foreach(var block in blocks){positions[block]=offset;int trim=block.Value.Length-block.Value.TrimStart().Length;
                foreach(var element in block.DescendantsAndSelf().Where(e=>e.Attributes().Any(a=>a.Name.LocalName=="id"))){int before=block.DescendantNodes().OfType<XText>().TakeWhile(t=>XNode.DocumentOrderComparer.Compare(t,element)<0).Sum(t=>t.Value.Length);foreach(var id in element.Attributes().Where(a=>a.Name.LocalName=="id"))if(!offsets.ContainsKey(id.Value))offsets[id.Value]=offset+Math.Max(0,before-trim);}
                foreach(var parent in block.Ancestors().TakeWhile(e=>e!=body))foreach(var id in parent.Attributes().Where(a=>a.Name.LocalName=="id"))if(!offsets.ContainsKey(id.Value))offsets[id.Value]=offset;
                offset+=block.Value.Trim().Length+2;
            }foreach(var element in body.DescendantsAndSelf().Where(e=>e.Attributes().Any(a=>a.Name.LocalName=="id"))){var following=blocks.FirstOrDefault(b=>XNode.DocumentOrderComparer.Compare(b,element)>0);foreach(var id in element.Attributes().Where(a=>a.Name.LocalName=="id"))if(!offsets.ContainsKey(id.Value))offsets[id.Value]=following==null?Math.Max(0,offset-2):positions[following];}return offsets;
        }
        static void Link(ZipArchive zip,Book book,Dictionary<string,Dictionary<string,int>> cache,string document,string href,string title,int depth,string source){
            if(String.IsNullOrWhiteSpace(href))return;string fragment,path=Resolve(document,href,out fragment);int chapter=book.chapters.FindIndex(c=>c.sourcePath==path);if(chapter<0)return;int start=0;
            if(fragment.Length>0){Dictionary<string,int> anchors;if(!cache.TryGetValue(path,out anchors)){var entry=zip.GetEntry(path);anchors=entry==null?new Dictionary<string,int>():Anchors(entry);cache[path]=anchors;}int at;if(anchors.TryGetValue(fragment,out at))start=at;}
            Add(book,title,chapter,start,depth,source);
        }
        static void Epub(string file,Book book){using(var zip=ZipFile.OpenRead(file)){var cache=new Dictionary<string,Dictionary<string,int>>();
            var container=ReadXml(zip.GetEntry("META-INF/container.xml"));string opf=(string)container.Descendants().First(e=>e.Name.LocalName=="rootfile").Attribute("full-path");var package=ReadXml(zip.GetEntry(opf));var items=package.Descendants().Where(e=>e.Name.LocalName=="item").ToList();
            var nav=items.FirstOrDefault(e=>((string)e.Attribute("properties")??"").Split(' ').Contains("nav"));
            if(nav!=null){try{string unused,path=Resolve(opf,(string)nav.Attribute("href"),out unused);var navEntry=zip.GetEntry(path);if(navEntry!=null){var doc=ReadXml(navEntry);var toc=doc.Descendants().FirstOrDefault(e=>e.Name.LocalName=="nav"&&e.Attributes().Any(a=>a.Name.LocalName=="type"&&a.Value.Split(' ').Contains("toc")));if(toc!=null)foreach(var link in toc.Descendants().Where(e=>e.Name.LocalName=="a"))Link(zip,book,cache,path,(string)link.Attribute("href"),link.Value,Math.Max(0,link.Ancestors().Count(e=>e.Name.LocalName=="li")-1),"epub-nav");}}catch(XmlException){}}
            if(book.toc.Count==0){var ncx=items.FirstOrDefault(e=>(string)e.Attribute("media-type")=="application/x-dtbncx+xml");if(ncx!=null){string unused,path=Resolve(opf,(string)ncx.Attribute("href"),out unused);var doc=ReadXml(zip.GetEntry(path));foreach(var point in doc.Descendants().Where(e=>e.Name.LocalName=="navPoint")){XElement content=point.Elements().FirstOrDefault(e=>e.Name.LocalName=="content"),label=point.Elements().FirstOrDefault(e=>e.Name.LocalName=="navLabel");if(content!=null&&label!=null)Link(zip,book,cache,path,(string)content.Attribute("src"),label.Value,point.Ancestors().Count(e=>e.Name.LocalName=="navPoint"),"epub-ncx");}}}
        }}
        static void Word(string file,Book book){XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";using(var zip=ZipFile.OpenRead(file)){
            var styles=new Dictionary<string,int>();var styleEntry=zip.GetEntry("word/styles.xml");if(styleEntry!=null){var definitions=ReadXml(styleEntry).Descendants(w+"style").ToList();foreach(var style in definitions){string id=(string)style.Attribute(w+"styleId");if(id==null)continue;int level;var outline=style.Descendants(w+"outlineLvl").FirstOrDefault();string name=(string)style.Element(w+"name").AttributeOrNull(w+"val");var match=Regex.Match(name??id,@"^(?:heading|标题)\s*([1-6])$",RegexOptions.IgnoreCase);if(outline!=null&&Int32.TryParse((string)outline.Attribute(w+"val"),out level)&&level<6)styles[id]=level;else if(match.Success)styles[id]=Int32.Parse(match.Groups[1].Value)-1;}for(int pass=0;pass<8;pass++)foreach(var style in definitions){string id=(string)style.Attribute(w+"styleId"),parent=(string)style.Element(w+"basedOn").AttributeOrNull(w+"val");int inherited;if(id!=null&&!styles.ContainsKey(id)&&parent!=null&&styles.TryGetValue(parent,out inherited))styles[id]=inherited;}}
            int offset=0;foreach(var p in ReadXml(zip.GetEntry("word/document.xml")).Descendants(w+"p")){string text=String.Concat(p.Descendants(w+"t").Select(t=>t.Value));int level;var properties=p.Element(w+"pPr");var outline=properties==null?null:properties.Element(w+"outlineLvl");string style=properties==null?null:(string)properties.Element(w+"pStyle").AttributeOrNull(w+"val");if(outline!=null&&Int32.TryParse((string)outline.Attribute(w+"val"),out level)&&level<6)Add(book,text,0,offset,level,"word-outline");else if(style!=null&&styles.TryGetValue(style,out level))Add(book,text,0,offset,level,"word-style");offset+=text.Length+2;}
        }}
        static void Text(Book book,string ext){for(int chapter=0;chapter<book.chapters.Count;chapter++){string text=book.chapters[chapter].text;foreach(Match line in Regex.Matches(text,@"(?m)^([^\r\n]{1,100})\r?$")){string value=line.Groups[1].Value.Trim();var md=Regex.Match(value,@"^(#{1,6})\s+(.+?)\s*#*\s*$");if(md.Success&&(ext==".md"||ext==".markdown")){Add(book,md.Groups[2].Value,chapter,line.Index,md.Groups[1].Value.Length-1,"markdown");continue;}if(Regex.IsMatch(value,@"^(?:第[零〇一二三四五六七八九十百千万两0-9０-９]{1,14}[章节卷回部篇集](?:\s|[：:、.．]|[^。！？!?]{0,60}$)|(?:chapter|part|book)\s+(?:\d+|[ivxlcdm]+)\b|序章$|序言$|前言$|引子$|后记$|尾声$)",RegexOptions.IgnoreCase)&&!Regex.IsMatch(value,@"[。！？!?]$"))Add(book,value,chapter,line.Index,value.Contains("卷")?0:1,"text-heading");}}}
        public static void Populate(Entry entry,Book book){try{string ext=Path.GetExtension(entry.path).ToLowerInvariant();if(ext==".epub")Epub(entry.path,book);else if(ext==".docx")Word(entry.path,book);else Text(book,ext);}catch(InvalidDataException){throw;}catch{book.toc.Clear();}
            if(book.toc.Count==0)for(int n=0;n<book.chapters.Count;n++)Add(book,book.chapters[n].title,n,0,0,book.chapters[n].generatedTitle?"section":"spine");
        }
        static XAttribute AttributeOrNull(this XElement element,XName name){return element==null?null:element.Attribute(name);}
    }
}
