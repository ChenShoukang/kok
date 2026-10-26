using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.IO.Compression;

namespace kok {
    public static class TextDocumentExport {
        static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        static readonly XNamespace H="http://www.w3.org/1999/xhtml";
        static void Write(ZipArchive zip,string name,string text,CompressionLevel compression=CompressionLevel.Optimal){var entry=zip.CreateEntry(name,compression);using(var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false)))writer.Write(text);}
        static string Xml(XElement element){return new XDocument(new XDeclaration("1.0","utf-8","yes"),element).ToString();}
        static XElement Run(string text,bool bold=false,bool italic=false){return new XElement(W+"r",new XElement(W+"rPr",bold?new XElement(W+"b"):null,italic?new XElement(W+"i"):null),new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),text));}
        static XElement Paragraph(string line){
            string style="Normal";var heading=Regex.Match(line,@"^(#{1,3})\s+(.+)$");if(heading.Success){style="Heading"+heading.Groups[1].Value.Length;line=heading.Groups[2].Value;}if(line.StartsWith("- ")||line.StartsWith("* "))line="• "+line.Substring(2);
            var p=new XElement(W+"p",new XElement(W+"pPr",new XElement(W+"pStyle",new XAttribute(W+"val",style))));int at=0;
            foreach(Match match in Regex.Matches(line,@"\*\*([^*]+)\*\*|\*([^*]+)\*")){if(match.Index>at)p.Add(Run(line.Substring(at,match.Index-at)));p.Add(Run(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value,match.Groups[1].Success,match.Groups[2].Success));at=match.Index+match.Length;}if(at<line.Length)p.Add(Run(line.Substring(at)));return p;
        }
        static XElement HtmlParagraph(string tag,string text){var paragraph=new XElement(H+tag);int at=0;foreach(Match match in Regex.Matches(text,@"\*\*([^*]+)\*\*|\*([^*]+)\*")){if(match.Index>at)paragraph.Add(text.Substring(at,match.Index-at));paragraph.Add(new XElement(H+(match.Groups[1].Success?"strong":"em"),match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value));at=match.Index+match.Length;}if(at<text.Length)paragraph.Add(text.Substring(at));return paragraph;}
        public static byte[] Export(string text,string title,string format,int columns=1){
            if(text==null||text.Length>16*1024*1024||columns<1||columns>2)throw new InvalidDataException("编辑正文过大。");
            using(var output=new MemoryStream()){
                using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){
                    if(format=="docx"){
                        Write(zip,"[Content_Types].xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/></Types>");
                        Write(zip,"_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
                        Write(zip,"word/_rels/document.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                        var body=new XElement(W+"body",text.Replace("\r\n","\n").Split('\n').Select(Paragraph));body.Add(new XElement(W+"sectPr",new XElement(W+"pgSz",new XAttribute(W+"w",11906),new XAttribute(W+"h",16838)),new XElement(W+"pgMar",new XAttribute(W+"top",1440),new XAttribute(W+"bottom",1440),new XAttribute(W+"left",1440),new XAttribute(W+"right",1440)),new XElement(W+"cols",new XAttribute(W+"num",columns),new XAttribute(W+"space",720))));Write(zip,"word/document.xml",Xml(new XElement(W+"document",new XAttribute(XNamespace.Xmlns+"w",W),body)));
                        var styles=new XElement(W+"styles",new XAttribute(XNamespace.Xmlns+"w",W),new XElement(W+"docDefaults",new XElement(W+"rPrDefault",new XElement(W+"rPr",new XElement(W+"rFonts",new XAttribute(W+"ascii","Calibri"),new XAttribute(W+"eastAsia","Microsoft YaHei")),new XElement(W+"sz",new XAttribute(W+"val",24))))));
                        foreach(var pair in new[]{new{Id="Normal",Size=24},new{Id="Heading1",Size=36},new{Id="Heading2",Size=30},new{Id="Heading3",Size=26}})styles.Add(new XElement(W+"style",new XAttribute(W+"type","paragraph"),new XAttribute(W+"styleId",pair.Id),new XElement(W+"name",new XAttribute(W+"val",pair.Id)),new XElement(W+"pPr",new XElement(W+"spacing",new XAttribute(W+"after",160),new XAttribute(W+"line",360),new XAttribute(W+"lineRule","auto"))),new XElement(W+"rPr",pair.Id!="Normal"?new XElement(W+"b"):null,new XElement(W+"sz",new XAttribute(W+"val",pair.Size)))));Write(zip,"word/styles.xml",Xml(styles));
                    }else if(format=="epub"){
                        Write(zip,"mimetype","application/epub+zip",CompressionLevel.NoCompression);Write(zip,"META-INF/container.xml","<?xml version=\"1.0\"?><container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>");
                        XNamespace opf="http://www.idpf.org/2007/opf",dc="http://purl.org/dc/elements/1.1/";
                        Write(zip,"OEBPS/content.opf",Xml(new XElement(opf+"package",new XAttribute("version","3.0"),new XAttribute("unique-identifier","bookid"),new XElement(opf+"metadata",new XAttribute(XNamespace.Xmlns+"dc",dc),new XElement(dc+"identifier",new XAttribute("id","bookid"),"urn:uuid:"+Guid.NewGuid()),new XElement(dc+"title",title),new XElement(dc+"language","zh-CN"),new XElement(opf+"meta",new XAttribute("property","dcterms:modified"),DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))),new XElement(opf+"manifest",new XElement(opf+"item",new XAttribute("id","chapter"),new XAttribute("href","content.xhtml"),new XAttribute("media-type","application/xhtml+xml")),new XElement(opf+"item",new XAttribute("id","nav"),new XAttribute("href","nav.xhtml"),new XAttribute("media-type","application/xhtml+xml"),new XAttribute("properties","nav"))),new XElement(opf+"spine",new XElement(opf+"itemref",new XAttribute("idref","chapter"))))));
                        var body=new XElement(H+"body");foreach(string original in text.Replace("\r\n","\n").Split('\n')){var heading=Regex.Match(original,@"^(#{1,3})\s+(.+)$");body.Add(HtmlParagraph(heading.Success?"h"+heading.Groups[1].Value.Length:"p",heading.Success?heading.Groups[2].Value:original));}
                        Write(zip,"OEBPS/content.xhtml",Xml(new XElement(H+"html",new XElement(H+"head",new XElement(H+"title",title),new XElement(H+"style","body{font-family:serif;line-height:1.8;margin:6%;}p{white-space:pre-wrap;}h1,h2,h3{line-height:1.4;}")),body)));
                        XNamespace epub="http://www.idpf.org/2007/ops";Write(zip,"OEBPS/nav.xhtml",Xml(new XElement(H+"html",new XAttribute(XNamespace.Xmlns+"epub",epub),new XElement(H+"head",new XElement(H+"title",title)),new XElement(H+"body",new XElement(H+"nav",new XAttribute(epub+"type","toc"),new XElement(H+"ol",new XElement(H+"li",new XElement(H+"a",new XAttribute("href","content.xhtml"),title))))))));
                    }else throw new InvalidDataException("请选择支持的导出格式。");
                }return output.ToArray();
            }
        }
    }
}
