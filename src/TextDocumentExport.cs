using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.IO.Compression;

namespace kok {
    public static class TextDocumentExport {
        static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        static readonly XNamespace H="http://www.w3.org/1999/xhtml";
        static readonly Regex InlinePattern=new Regex(@"\[([^\]]+)\]\(([^)\s]+)\)|`([^`]+)`|\*\*([^*]+)\*\*|\*([^*]+)\*",RegexOptions.Compiled);
        static readonly Regex HeadingPattern=new Regex(@"^(#{1,4})\s+(.+)$",RegexOptions.Compiled);
        static readonly Regex OrderedPattern=new Regex(@"^(\s*)(\d+)\.\s+(.+)$",RegexOptions.Compiled);
        static readonly Regex BulletPattern=new Regex(@"^(\s*)[-*+]\s+(.+)$",RegexOptions.Compiled);
        static readonly Regex TaskPattern=new Regex(@"^\[( |x|X)\]\s+(.+)$",RegexOptions.Compiled);
        static readonly Regex RulePattern=new Regex(@"^\s*([-*_])\1{2,}\s*$",RegexOptions.Compiled);
        static readonly Regex TableSepPattern=new Regex(@"^\s*\|?(?:\s*:?-{2,}:?\s*\|)+\s*:?-{2,}:?\s*\|?\s*$",RegexOptions.Compiled);

        static void Write(ZipArchive zip,string name,string text,CompressionLevel compression=CompressionLevel.Optimal){
            var entry=zip.CreateEntry(name,compression);
            using(var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false)))writer.Write(text);
        }
        static string Xml(XElement element){return new XDocument(new XDeclaration("1.0","utf-8","yes"),element).ToString(SaveOptions.DisableFormatting);}

        static XElement Run(string text,bool bold=false,bool italic=false,bool code=false,string color=null,int? sizeHalfPt=null){
            var rpr=new XElement(W+"rPr",
                code?new XElement(W+"rFonts",new XAttribute(W+"ascii","Consolas"),new XAttribute(W+"hAnsi","Consolas"),new XAttribute(W+"eastAsia","Microsoft YaHei")):null,
                bold?new XElement(W+"b"):null,
                italic?new XElement(W+"i"):null,
                color!=null?new XElement(W+"color",new XAttribute(W+"val",color)):null,
                sizeHalfPt.HasValue?new XElement(W+"sz",new XAttribute(W+"val",sizeHalfPt.Value)):null,
                sizeHalfPt.HasValue?new XElement(W+"szCs",new XAttribute(W+"val",sizeHalfPt.Value)):null,
                code?new XElement(W+"shd",new XAttribute(W+"val","clear"),new XAttribute(W+"color","auto"),new XAttribute(W+"fill","F1F3F5")):null);
            return new XElement(W+"r",rpr.HasElements?rpr:null,new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),text??""));
        }

        static void AppendInline(XElement p,string line,bool forceBold=false,string baseColor=null,int? baseSize=null){
            if(String.IsNullOrEmpty(line))return;
            int at=0;
            foreach(Match m in InlinePattern.Matches(line)){
                if(m.Index>at)p.Add(Run(line.Substring(at,m.Index-at),forceBold,false,false,baseColor,baseSize));
                if(m.Groups[1].Success)p.Add(Run(m.Groups[1].Value,forceBold,false,false,baseColor??"1D4ED8",baseSize));
                else if(m.Groups[3].Success)p.Add(Run(" "+m.Groups[3].Value+" ",forceBold,false,true,baseColor??"1E293B",baseSize.HasValue?Math.Max(16,baseSize.Value-2):20));
                else if(m.Groups[4].Success)p.Add(Run(m.Groups[4].Value,true,false,false,baseColor,baseSize));
                else if(m.Groups[5].Success)p.Add(Run(m.Groups[5].Value,forceBold,true,false,baseColor,baseSize));
                at=m.Index+m.Length;
            }
            if(at<line.Length)p.Add(Run(line.Substring(at),forceBold,false,false,baseColor,baseSize));
        }

        static List<string> SplitTableRow(string line){
            string trimmed=(line??"").Trim();
            if(trimmed.StartsWith("|"))trimmed=trimmed.Substring(1);
            if(trimmed.EndsWith("|"))trimmed=trimmed.Substring(0,trimmed.Length-1);
            return trimmed.Split('|').Select(cell=>cell.Trim()).ToList();
        }
        static string[] TableAlignments(string separatorLine,int cols){
            var cells=SplitTableRow(separatorLine);
            var aligns=new string[cols];
            for(int i=0;i<cols;i++){
                string c=i<cells.Count?cells[i].Trim():"";
                if(c.StartsWith(":")&&c.EndsWith(":"))aligns[i]="center";
                else if(c.EndsWith(":"))aligns[i]="right";
                else aligns[i]="left";
            }
            return aligns;
        }

        static XElement BuildDocxTable(List<string> headers,string[] aligns,List<List<string>> rows){
            int cols=Math.Max(1,headers.Count);
            int totalWidth=9026; // A4 printable width (11906 - 2*1440)
            int colWidth=totalWidth/cols;
            var tbl=new XElement(W+"tbl",
                new XElement(W+"tblPr",
                    new XElement(W+"tblW",new XAttribute(W+"w",totalWidth),new XAttribute(W+"type","dxa")),
                    new XElement(W+"tblLayout",new XAttribute(W+"type","fixed")),
                    new XElement(W+"tblBorders",
                        new XElement(W+"top",new XAttribute(W+"val","single"),new XAttribute(W+"sz","8"),new XAttribute(W+"color","1E242D")),
                        new XElement(W+"bottom",new XAttribute(W+"val","single"),new XAttribute(W+"sz","8"),new XAttribute(W+"color","1E242D")),
                        new XElement(W+"insideH",new XAttribute(W+"val","single"),new XAttribute(W+"sz","4"),new XAttribute(W+"color","E2E6EC")),
                        new XElement(W+"left",new XAttribute(W+"val","none")),
                        new XElement(W+"right",new XAttribute(W+"val","none")),
                        new XElement(W+"insideV",new XAttribute(W+"val","none"))),
                    new XElement(W+"tblCellMar",
                        new XElement(W+"top",new XAttribute(W+"w","110"),new XAttribute(W+"type","dxa")),
                        new XElement(W+"bottom",new XAttribute(W+"w","110"),new XAttribute(W+"type","dxa")),
                        new XElement(W+"left",new XAttribute(W+"w","140"),new XAttribute(W+"type","dxa")),
                        new XElement(W+"right",new XAttribute(W+"w","140"),new XAttribute(W+"type","dxa")))),
                new XElement(W+"tblGrid",Enumerable.Range(0,cols).Select(_=>new XElement(W+"gridCol",new XAttribute(W+"w",colWidth)))));

            Func<List<string>,bool,int,XElement> buildRow=(cells,isHeader,rowIndex)=>{
                string fill=isHeader?"1E242D":(rowIndex%2==1?"F7F8FA":"FFFFFF");
                string textColor=isHeader?"FFFFFF":"1F2937";
                var tr=new XElement(W+"tr",isHeader?new XElement(W+"trPr",new XElement(W+"tblHeader"),new XElement(W+"cantSplit")):new XElement(W+"trPr",new XElement(W+"cantSplit")));
                for(int c=0;c<cols;c++){
                    string val=c<cells.Count?cells[c]:"";
                    string align=c<aligns.Length?aligns[c]:"left";
                    var p=new XElement(W+"p",
                        new XElement(W+"pPr",
                            new XElement(W+"spacing",new XAttribute(W+"before","0"),new XAttribute(W+"after","0"),new XAttribute(W+"line","280"),new XAttribute(W+"lineRule","auto")),
                            align!="left"?new XElement(W+"jc",new XAttribute(W+"val",align)):null));
                    AppendInline(p,val,isHeader,textColor,isHeader?20:19);
                    tr.Add(new XElement(W+"tc",
                        new XElement(W+"tcPr",
                            new XElement(W+"tcW",new XAttribute(W+"w",colWidth),new XAttribute(W+"type","dxa")),
                            new XElement(W+"shd",new XAttribute(W+"val","clear"),new XAttribute(W+"color","auto"),new XAttribute(W+"fill",fill)),
                            new XElement(W+"vAlign",new XAttribute(W+"val","center"))),
                        p));
                }
                return tr;
            };
            tbl.Add(buildRow(headers,true,0));
            for(int r=0;r<rows.Count;r++)tbl.Add(buildRow(rows[r],false,r));
            return tbl;
        }

        static XElement BuildCallout(List<string> quoteLines){
            var p=new XElement(W+"p",
                new XElement(W+"pPr",
                    new XElement(W+"pBdr",new XElement(W+"left",new XAttribute(W+"val","single"),new XAttribute(W+"sz","24"),new XAttribute(W+"space","12"),new XAttribute(W+"color","1D4ED8"))),
                    new XElement(W+"shd",new XAttribute(W+"val","clear"),new XAttribute(W+"color","auto"),new XAttribute(W+"fill","F4F6FA")),
                    new XElement(W+"ind",new XAttribute(W+"left","240"),new XAttribute(W+"right","180")),
                    new XElement(W+"spacing",new XAttribute(W+"before","140"),new XAttribute(W+"after","180"),new XAttribute(W+"line","340"),new XAttribute(W+"lineRule","auto"))));
            for(int i=0;i<quoteLines.Count;i++){
                if(i>0)p.Add(new XElement(W+"r",new XElement(W+"br")));
                AppendInline(p,quoteLines[i],false,"1E293B",21);
            }
            return p;
        }

        static XElement BuildCodeBlock(List<string> codeLines){
            var p=new XElement(W+"p",
                new XElement(W+"pPr",
                    new XElement(W+"pBdr",
                        new XElement(W+"top",new XAttribute(W+"val","single"),new XAttribute(W+"sz","4"),new XAttribute(W+"space","6"),new XAttribute(W+"color","E2E8F0")),
                        new XElement(W+"bottom",new XAttribute(W+"val","single"),new XAttribute(W+"sz","4"),new XAttribute(W+"space","6"),new XAttribute(W+"color","E2E8F0")),
                        new XElement(W+"left",new XAttribute(W+"val","single"),new XAttribute(W+"sz","4"),new XAttribute(W+"space","10"),new XAttribute(W+"color","E2E8F0")),
                        new XElement(W+"right",new XAttribute(W+"val","single"),new XAttribute(W+"sz","4"),new XAttribute(W+"space","10"),new XAttribute(W+"color","E2E8F0"))),
                    new XElement(W+"shd",new XAttribute(W+"val","clear"),new XAttribute(W+"color","auto"),new XAttribute(W+"fill","F8FAFC")),
                    new XElement(W+"ind",new XAttribute(W+"left","120"),new XAttribute(W+"right","120")),
                    new XElement(W+"spacing",new XAttribute(W+"before","120"),new XAttribute(W+"after","160"),new XAttribute(W+"line","260"),new XAttribute(W+"lineRule","auto"))));
            for(int i=0;i<codeLines.Count;i++){
                if(i>0)p.Add(new XElement(W+"r",new XElement(W+"br")));
                p.Add(Run(codeLines[i],false,false,true,"1E293B",18));
            }
            return p;
        }

        static XElement BuildRule(){
            return new XElement(W+"p",
                new XElement(W+"pPr",
                    new XElement(W+"keepNext"),
                    new XElement(W+"pBdr",new XElement(W+"bottom",new XAttribute(W+"val","single"),new XAttribute(W+"sz","6"),new XAttribute(W+"space","4"),new XAttribute(W+"color","D8DEE6"))),
                    new XElement(W+"spacing",new XAttribute(W+"before","120"),new XAttribute(W+"after","160"))));
        }

        static List<XElement> BuildDocxBlocks(string text){
            var blocks=new List<XElement>();
            var lines=(text??"").Replace("\r\n","\n").Replace("\r","\n").Split('\n');
            int i=0;
            while(i<lines.Length){
                string line=lines[i];
                if(String.IsNullOrWhiteSpace(line)){i++;continue;}
                if(line.TrimStart().StartsWith("```")){
                    i++;var code=new List<string>();
                    while(i<lines.Length&&!lines[i].TrimStart().StartsWith("```")){code.Add(lines[i]);i++;}
                    if(i<lines.Length)i++;
                    if(code.Count>0)blocks.Add(BuildCodeBlock(code));
                    continue;
                }
                if(RulePattern.IsMatch(line)){blocks.Add(BuildRule());i++;continue;}
                if(line.TrimStart().StartsWith(">")){
                    var quote=new List<string>();
                    while(i<lines.Length&&lines[i].TrimStart().StartsWith(">")){quote.Add(Regex.Replace(lines[i].TrimStart(),@"^>\s?",""));i++;}
                    blocks.Add(BuildCallout(quote));
                    continue;
                }
                if(line.Contains("|")&&i+1<lines.Length&&TableSepPattern.IsMatch(lines[i+1])){
                    var headers=SplitTableRow(line);
                    var aligns=TableAlignments(lines[i+1],headers.Count);
                    i+=2;
                    var rows=new List<List<string>>();
                    while(i<lines.Length&&lines[i].Contains("|")&&!String.IsNullOrWhiteSpace(lines[i])){rows.Add(SplitTableRow(lines[i]));i++;}
                    blocks.Add(BuildDocxTable(headers,aligns,rows));
                    blocks.Add(new XElement(W+"p",new XElement(W+"pPr",new XElement(W+"spacing",new XAttribute(W+"before","0"),new XAttribute(W+"after","100")))));
                    continue;
                }
                var heading=HeadingPattern.Match(line);
                if(heading.Success){
                    int level=heading.Groups[1].Value.Length;
                    var p=new XElement(W+"p",new XElement(W+"pPr",new XElement(W+"pStyle",new XAttribute(W+"val","Heading"+level))));
                    AppendInline(p,heading.Groups[2].Value.Trim());
                    blocks.Add(p);i++;continue;
                }
                var ordered=OrderedPattern.Match(line);
                if(ordered.Success){
                    int depth=Math.Min(3,ordered.Groups[1].Value.Length/2);
                    int left=360+depth*280;
                    var p=new XElement(W+"p",
                        new XElement(W+"pPr",
                            new XElement(W+"pStyle",new XAttribute(W+"val","Normal")),
                            new XElement(W+"ind",new XAttribute(W+"left",left),new XAttribute(W+"hanging","280")),
                            new XElement(W+"spacing",new XAttribute(W+"before","20"),new XAttribute(W+"after","80"),new XAttribute(W+"line","320"),new XAttribute(W+"lineRule","auto"))));
                    p.Add(Run(ordered.Groups[2].Value+". ",true,false,false,"1D4ED8",21));
                    AppendInline(p,ordered.Groups[3].Value.Trim());
                    blocks.Add(p);i++;continue;
                }
                var bullet=BulletPattern.Match(line);
                if(bullet.Success){
                    int depth=Math.Min(3,bullet.Groups[1].Value.Length/2);
                    int left=360+depth*280;
                    string bodyText=bullet.Groups[2].Value.Trim();
                    string marker=depth==0?"▪  ":"◦  ";
                    var task=TaskPattern.Match(bodyText);
                    if(task.Success){marker=task.Groups[1].Value!=" "?"☑  ":"☐  ";bodyText=task.Groups[2].Value.Trim();}
                    var p=new XElement(W+"p",
                        new XElement(W+"pPr",
                            new XElement(W+"pStyle",new XAttribute(W+"val","Normal")),
                            new XElement(W+"ind",new XAttribute(W+"left",left),new XAttribute(W+"hanging","240")),
                            new XElement(W+"spacing",new XAttribute(W+"before","20"),new XAttribute(W+"after","80"),new XAttribute(W+"line","320"),new XAttribute(W+"lineRule","auto"))));
                    p.Add(Run(marker,false,false,false,"1D4ED8",20));
                    AppendInline(p,bodyText);
                    blocks.Add(p);i++;continue;
                }
                var para=new XElement(W+"p",new XElement(W+"pPr",new XElement(W+"pStyle",new XAttribute(W+"val","Normal"))));
                AppendInline(para,line);
                blocks.Add(para);i++;
            }
            if(blocks.Count==0)blocks.Add(new XElement(W+"p"));
            return blocks;
        }

        static XElement BuildStyles(){
            var styles=new XElement(W+"styles",new XAttribute(XNamespace.Xmlns+"w",W),
                new XElement(W+"docDefaults",
                    new XElement(W+"rPrDefault",
                        new XElement(W+"rPr",
                            new XElement(W+"rFonts",new XAttribute(W+"ascii","Segoe UI"),new XAttribute(W+"hAnsi","Segoe UI"),new XAttribute(W+"eastAsia","Microsoft YaHei")),
                            new XElement(W+"color",new XAttribute(W+"val","1F2937")),
                            new XElement(W+"sz",new XAttribute(W+"val",21)),
                            new XElement(W+"szCs",new XAttribute(W+"val",21))))));

            styles.Add(new XElement(W+"style",new XAttribute(W+"type","paragraph"),new XAttribute(W+"styleId","Normal"),new XAttribute(W+"default","1"),
                new XElement(W+"name",new XAttribute(W+"val","Normal")),
                new XElement(W+"pPr",
                    new XElement(W+"widowControl"),
                    new XElement(W+"spacing",new XAttribute(W+"before","0"),new XAttribute(W+"after","120"),new XAttribute(W+"line","320"),new XAttribute(W+"lineRule","auto")))));

            styles.Add(new XElement(W+"style",new XAttribute(W+"type","paragraph"),new XAttribute(W+"styleId","Heading1"),
                new XElement(W+"name",new XAttribute(W+"val","heading 1")),
                new XElement(W+"pPr",
                    new XElement(W+"keepNext"),
                    new XElement(W+"pBdr",new XElement(W+"bottom",new XAttribute(W+"val","single"),new XAttribute(W+"sz","12"),new XAttribute(W+"space","8"),new XAttribute(W+"color","181817"))),
                    new XElement(W+"spacing",new XAttribute(W+"before","200"),new XAttribute(W+"after","220"),new XAttribute(W+"line","290"),new XAttribute(W+"lineRule","auto"))),
                new XElement(W+"rPr",new XElement(W+"b"),new XElement(W+"color",new XAttribute(W+"val","111827")),new XElement(W+"sz",new XAttribute(W+"val",42)))));

            styles.Add(new XElement(W+"style",new XAttribute(W+"type","paragraph"),new XAttribute(W+"styleId","Heading2"),
                new XElement(W+"name",new XAttribute(W+"val","heading 2")),
                new XElement(W+"pPr",
                    new XElement(W+"keepNext"),
                    new XElement(W+"pBdr",new XElement(W+"left",new XAttribute(W+"val","single"),new XAttribute(W+"sz","18"),new XAttribute(W+"space","8"),new XAttribute(W+"color","1D4ED8"))),
                    new XElement(W+"spacing",new XAttribute(W+"before","260"),new XAttribute(W+"after","120"),new XAttribute(W+"line","280"),new XAttribute(W+"lineRule","auto"))),
                new XElement(W+"rPr",new XElement(W+"b"),new XElement(W+"color",new XAttribute(W+"val","0F172A")),new XElement(W+"sz",new XAttribute(W+"val",30)))));

            styles.Add(new XElement(W+"style",new XAttribute(W+"type","paragraph"),new XAttribute(W+"styleId","Heading3"),
                new XElement(W+"name",new XAttribute(W+"val","heading 3")),
                new XElement(W+"pPr",
                    new XElement(W+"keepNext"),
                    new XElement(W+"spacing",new XAttribute(W+"before","220"),new XAttribute(W+"after","100"),new XAttribute(W+"line","280"),new XAttribute(W+"lineRule","auto"))),
                new XElement(W+"rPr",new XElement(W+"b"),new XElement(W+"color",new XAttribute(W+"val","1E293B")),new XElement(W+"sz",new XAttribute(W+"val",25)))));

            styles.Add(new XElement(W+"style",new XAttribute(W+"type","paragraph"),new XAttribute(W+"styleId","Heading4"),
                new XElement(W+"name",new XAttribute(W+"val","heading 4")),
                new XElement(W+"pPr",
                    new XElement(W+"keepNext"),
                    new XElement(W+"spacing",new XAttribute(W+"before","160"),new XAttribute(W+"after","80"),new XAttribute(W+"line","280"),new XAttribute(W+"lineRule","auto"))),
                new XElement(W+"rPr",new XElement(W+"b"),new XElement(W+"color",new XAttribute(W+"val","475569")),new XElement(W+"sz",new XAttribute(W+"val",22)))));
            return styles;
        }

        static XElement BuildHeader(string title){
            return new XElement(W+"hdr",new XAttribute(XNamespace.Xmlns+"w",W),
                new XElement(W+"p",
                    new XElement(W+"pPr",
                        new XElement(W+"jc",new XAttribute(W+"val","right")),
                        new XElement(W+"pBdr",new XElement(W+"bottom",new XAttribute(W+"val","single"),new XAttribute(W+"sz","4"),new XAttribute(W+"space","4"),new XAttribute(W+"color","E2E8F0")))),
                    Run(String.IsNullOrWhiteSpace(title)?"":title,false,false,false,"94A3B8",17)));
        }

        static XElement BuildFooter(){
            return new XElement(W+"ftr",new XAttribute(XNamespace.Xmlns+"w",W),
                new XElement(W+"p",
                    new XElement(W+"pPr",
                        new XElement(W+"jc",new XAttribute(W+"val","center")),
                        new XElement(W+"pBdr",new XElement(W+"top",new XAttribute(W+"val","single"),new XAttribute(W+"sz","4"),new XAttribute(W+"space","6"),new XAttribute(W+"color","E2E8F0")))),
                    Run("第 ",false,false,false,"94A3B8",17),
                    new XElement(W+"fldSimple",new XAttribute(W+"instr","PAGE")),
                    Run(" 页 / 共 ",false,false,false,"94A3B8",17),
                    new XElement(W+"fldSimple",new XAttribute(W+"instr","NUMPAGES")),
                    Run(" 页",false,false,false,"94A3B8",17)));
        }

        static XElement HtmlParagraph(string tag,string text){
            var paragraph=new XElement(H+tag);int at=0;
            foreach(Match match in InlinePattern.Matches(text??"")){
                if(match.Index>at)paragraph.Add(text.Substring(at,match.Index-at));
                if(match.Groups[1].Success)paragraph.Add(new XElement(H+"span",match.Groups[1].Value));
                else if(match.Groups[3].Success)paragraph.Add(new XElement(H+"code",match.Groups[3].Value));
                else if(match.Groups[4].Success)paragraph.Add(new XElement(H+"strong",match.Groups[4].Value));
                else if(match.Groups[5].Success)paragraph.Add(new XElement(H+"em",match.Groups[5].Value));
                at=match.Index+match.Length;
            }
            if(at<(text??"").Length)paragraph.Add(text.Substring(at));
            return paragraph;
        }

        public static byte[] Export(string text,string title,string format,int columns=1){
            if(text==null||text.Length>16*1024*1024||columns<1||columns>2)throw new InvalidDataException("编辑正文过大。");
            using(var output=new MemoryStream()){
                using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){
                    if(format=="docx"){
                        Write(zip,"[Content_Types].xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/><Override PartName=\"/word/header1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml\"/><Override PartName=\"/word/footer1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml\"/></Types>");
                        Write(zip,"_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
                        Write(zip,"word/_rels/document.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/header\" Target=\"header1.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer\" Target=\"footer1.xml\"/></Relationships>");
                        var body=new XElement(W+"body",BuildDocxBlocks(text));
                        body.Add(new XElement(W+"sectPr",
                            new XElement(W+"headerReference",new XAttribute(W+"type","default"),new XAttribute(R+"id","rId2")),
                            new XElement(W+"footerReference",new XAttribute(W+"type","default"),new XAttribute(R+"id","rId3")),
                            new XElement(W+"pgSz",new XAttribute(W+"w",11906),new XAttribute(W+"h",16838)),
                            new XElement(W+"pgMar",new XAttribute(W+"top",1360),new XAttribute(W+"bottom",1360),new XAttribute(W+"left",1440),new XAttribute(W+"right",1440),new XAttribute(W+"header",680),new XAttribute(W+"footer",680)),
                            new XElement(W+"cols",new XAttribute(W+"num",columns),new XAttribute(W+"space",720))));
                        Write(zip,"word/document.xml",Xml(new XElement(W+"document",new XAttribute(XNamespace.Xmlns+"w",W),new XAttribute(XNamespace.Xmlns+"r",R),body)));
                        Write(zip,"word/styles.xml",Xml(BuildStyles()));
                        Write(zip,"word/header1.xml",Xml(BuildHeader(title)));
                        Write(zip,"word/footer1.xml",Xml(BuildFooter()));
                    }else if(format=="epub"){
                        Write(zip,"mimetype","application/epub+zip",CompressionLevel.NoCompression);Write(zip,"META-INF/container.xml","<?xml version=\"1.0\"?><container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>");
                        XNamespace opf="http://www.idpf.org/2007/opf",dc="http://purl.org/dc/elements/1.1/";
                        Write(zip,"OEBPS/content.opf",Xml(new XElement(opf+"package",new XAttribute("version","3.0"),new XAttribute("unique-identifier","bookid"),new XElement(opf+"metadata",new XAttribute(XNamespace.Xmlns+"dc",dc),new XElement(dc+"identifier",new XAttribute("id","bookid"),"urn:uuid:"+Guid.NewGuid()),new XElement(dc+"title",title),new XElement(dc+"language","zh-CN"),new XElement(opf+"meta",new XAttribute("property","dcterms:modified"),DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))),new XElement(opf+"manifest",new XElement(opf+"item",new XAttribute("id","chapter"),new XAttribute("href","content.xhtml"),new XAttribute("media-type","application/xhtml+xml")),new XElement(opf+"item",new XAttribute("id","nav"),new XAttribute("href","nav.xhtml"),new XAttribute("media-type","application/xhtml+xml"),new XAttribute("properties","nav"))),new XElement(opf+"spine",new XElement(opf+"itemref",new XAttribute("idref","chapter"))))));
                        var body=new XElement(H+"body");
                        var lines=text.Replace("\r\n","\n").Split('\n');
                        for(int idx=0;idx<lines.Length;idx++){
                            string original=lines[idx];
                            if(String.IsNullOrWhiteSpace(original))continue;
                            if(original.Contains("|")&&idx+1<lines.Length&&TableSepPattern.IsMatch(lines[idx+1])){
                                var headers=SplitTableRow(original);idx+=2;
                                var table=new XElement(H+"table",new XElement(H+"thead",new XElement(H+"tr",headers.Select(h=>new XElement(H+"th",h)))));
                                var tbody=new XElement(H+"tbody");
                                while(idx<lines.Length&&lines[idx].Contains("|")&&!String.IsNullOrWhiteSpace(lines[idx])){tbody.Add(new XElement(H+"tr",SplitTableRow(lines[idx]).Select(c=>new XElement(H+"td",c))));idx++;}
                                idx--;table.Add(tbody);body.Add(table);continue;
                            }
                            if(original.TrimStart().StartsWith(">")){body.Add(HtmlParagraph("blockquote",Regex.Replace(original.TrimStart(),@"^>\s?","")));continue;}
                            var heading=HeadingPattern.Match(original);
                            body.Add(HtmlParagraph(heading.Success?"h"+heading.Groups[1].Value.Length:"p",heading.Success?heading.Groups[2].Value:original));
                        }
                        Write(zip,"OEBPS/content.xhtml",Xml(new XElement(H+"html",new XElement(H+"head",new XElement(H+"title",title),new XElement(H+"style","body{font-family:sans-serif;line-height:1.8;margin:6%;color:#1f2937;}h1,h2,h3,h4{line-height:1.35;color:#111827;}blockquote{border-left:3px solid #1d4ed8;padding:8px 14px;background:#f4f6fa;margin:12px 0;}table{width:100%;border-collapse:collapse;margin:14px 0;}th,td{border-bottom:1px solid #e2e8f0;padding:8px;text-align:left;}th{background:#1e242d;color:#fff;}")),body)));
                        XNamespace epub="http://www.idpf.org/2007/ops";Write(zip,"OEBPS/nav.xhtml",Xml(new XElement(H+"html",new XAttribute(XNamespace.Xmlns+"epub",epub),new XElement(H+"head",new XElement(H+"title",title)),new XElement(H+"body",new XElement(H+"nav",new XAttribute(epub+"type","toc"),new XElement(H+"ol",new XElement(H+"li",new XElement(H+"a",new XAttribute("href","content.xhtml"),title))))))));
                    }else throw new InvalidDataException("请选择支持的导出格式。");
                }return output.ToArray();
            }
        }
    }
}
