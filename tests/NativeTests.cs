using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using kok;

class Tests {
    static int count;
    static void Check(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); Console.WriteLine("PASS: "+name); count++; }
    static void Entry(ZipArchive zip,string path,string content) { using(var writer=new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false))) writer.Write(content); }
    static int Main() {
        string temp=Path.Combine(Path.GetTempPath(),"kok-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try {
            string text=Path.Combine(temp,"中文书名.txt"); File.WriteAllText(text,"第一章\n\n你好，世界。",new UTF8Encoding(true));
            var lib=new Library(Path.Combine(temp,"data"));
            var r=lib.Import(new[]{text,text,Path.Combine(temp,"missing.txt")},null);
            Check(r.added==1&&r.skipped==2,"import deduplicates and ignores missing files");
            var i=lib.Db.items[0]; Check(i.kind=="book"&&i.name=="中文书名","Unicode filenames and classification");
            Check(Library.ReadBook(i).chapters[0].text.Contains("你好"),"UTF-8 BOM text reading");
            File.WriteAllText(text,"你好，中文编码。",Encoding.GetEncoding(54936)); Check(Library.ReadText(text)=="你好，中文编码。","GB18030 decoding");
            File.WriteAllText(text,"Unicode 中文",Encoding.Unicode); Check(Library.ReadText(text)=="Unicode 中文","UTF-16 decoding");
            lib.Update(i.id,new Dictionary<string,object>{{"notes","笔记 <script>"},{"favorite",true},{"progress",2.0},{"position",15.5},{"chapter",2}});
            var reopened=new Library(lib.DataDir); var saved=reopened.Find(i.id);
            Check(saved.favorite&&saved.notes=="笔记 <script>"&&saved.progress==1&&saved.position==15.5&&saved.chapter==2,"atomic persistence and bounded progress");
            Check(File.Exists(Path.Combine(lib.DataDir,"library.json.bak")),"previous database kept as backup");
            lib.Remove(i.id); Check(File.Exists(text)&&lib.Db.items.Count==0,"removing library entry preserves original file");
            string docx=Path.Combine(temp,"test.docx");
            using(var zip=ZipFile.Open(docx,ZipArchiveMode.Create)) Entry(zip,"word/document.xml","<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>Hello 文档</w:t></w:r></w:p></w:body></w:document>");
            Check(Library.ReadBook(new kok.Entry{path=docx,name="test"}).chapters[0].text=="Hello 文档","DOCX text extraction");
            string epub=Path.Combine(temp,"test.epub");
            using(var zip=ZipFile.Open(epub,ZipArchiveMode.Create)) {
                Entry(zip,"META-INF/container.xml","<container xmlns='urn:oasis:names:tc:opendocument:xmlns:container'><rootfiles><rootfile full-path='OEBPS/content.opf'/></rootfiles></container>");
                Entry(zip,"OEBPS/content.opf","<package xmlns='http://www.idpf.org/2007/opf'><metadata><dc:title xmlns:dc='http://purl.org/dc/elements/1.1/'>小书</dc:title></metadata><manifest><item id='c2' href='second.xhtml'/><item id='c1' href='first.xhtml'/></manifest><spine><itemref idref='c1'/><itemref idref='c2'/></spine></package>");
                Entry(zip,"OEBPS/first.xhtml","<html xmlns='http://www.w3.org/1999/xhtml'><body><h1>第一章</h1><p>你好</p><script>BAD_SCRIPT</script><p><img src='https://evil.invalid/pixel'/>文字</p></body></html>");
                Entry(zip,"OEBPS/second.xhtml","<html xmlns='http://www.w3.org/1999/xhtml'><body><h2>第二章</h2><p>正文</p></body></html>");
            }
            var book=Library.ReadBook(new kok.Entry{path=epub,name="test"});
            Check(book.title=="小书"&&book.chapters.Count==2&&book.chapters[0].title=="第一章","EPUB follows spine reading order");
            Check(!book.chapters[0].text.Contains("BAD_SCRIPT")&&!book.chapters[0].text.Contains("evil.invalid"),"EPUB is text-only and excludes scripts and remote resources");
            long s,e;
            Check(MainWindow.TryRange("bytes=10-19",100,out s,out e)&&s==10&&e==19,"closed byte range");
            Check(MainWindow.TryRange("bytes=90-",100,out s,out e)&&s==90&&e==99,"open byte range");
            Check(MainWindow.TryRange("bytes=-20",100,out s,out e)&&s==80&&e==99,"suffix byte range");
            Check(MainWindow.TryRange("bytes=90-999",100,out s,out e)&&e==99,"range clips to file size");
            Check(!MainWindow.TryRange("bytes=100-",100,out s,out e)&&!MainWindow.TryRange("bytes=20-10",100,out s,out e)&&!MainWindow.TryRange("bytes=0-1,4-5",100,out s,out e)&&!MainWindow.TryRange("bytes=0-",0,out s,out e),"invalid ranges rejected");
            using(var stream=new SliceStream(new MemoryStream(new byte[]{0,1,2,3,4,5}),2,3)) { var bytes=new byte[10]; Check(stream.Read(bytes,0,10)==3&&bytes.Take(3).SequenceEqual(new byte[]{2,3,4})&&stream.Read(bytes,0,1)==0,"stream view does not read past range"); stream.Seek(-1,SeekOrigin.End); Check(stream.ReadByte()==4,"range stream supports seek"); }
            lib.Import(new[]{docx},null);var editable=lib.Db.items.First(x=>x.path==docx);
            var marks=new[]{new Annotation{id="mark1",style="highlight",text="Hello",note="我的想法",chapter=0,start=0,end=5,page=0}};
            lib.Update(editable.id,new Dictionary<string,object>{{"annotations",marks},{"chapterTexts",new Dictionary<string,string>{{"0","Hello 文档：新增文字"}}},{"collection","工作资料"}});
            var edited=new Library(lib.DataDir).Find(editable.id);
            Check(edited.annotations.Count==1&&edited.annotations[0].note=="我的想法"&&Library.ReadBook(edited).chapters[0].text.Contains("新增文字"),"annotations and inserted document text survive restart");
            Check(lib.Db.collections.Contains("工作资料")&&edited.collection=="工作资料","custom categorization persists");
            lib.DeleteCollection("工作资料");Check(editable.collection==""&&File.Exists(docx),"removing category preserves documents");
            string sub=Path.Combine(temp,"movie.zh.srt");File.WriteAllText(sub,"1\n00:00:01,200 --> 00:00:03,500\n你好，字幕\n",Encoding.UTF8);
            var subs=Subtitles.Read(sub);Check(subs.cues.Count==1&&Math.Abs(subs.cues[0].start-1.2)<.001&&subs.cues[0].text=="你好，字幕","SRT timestamps and Unicode captions");
            Check(Subtitles.Discover(Path.Combine(temp,"movie.mp4")).Count==1,"automatic matching of language-suffixed subtitles");
            string ass=Path.Combine(temp,"test.ass");File.WriteAllText(ass,"[Events]\nDialogue: 0,0:00:01.00,0:00:03.00,Default,,0,0,0,,{\\b1}Hello\\Nworld, again",Encoding.UTF8);
            Check(Subtitles.Read(ass).cues[0].text=="Hello\nworld, again","ASS text compatibility preserves lines and commas");
            string micro=Path.Combine(temp,"test.sub");File.WriteAllText(micro,"{1}{1}25.0\n{25}{75}Hello|world",Encoding.UTF8);
            Check(Subtitles.Read(micro).cues[0].start==1&&Subtitles.Read(micro).cues[0].end==3,"MicroDVD frame-rate subtitles");
            string ttml=Path.Combine(temp,"test.ttml");File.WriteAllText(ttml,"<tt xmlns='http://www.w3.org/ns/ttml'><body><p begin='1.2s' end='3.4s'>文字字幕</p></body></tt>",Encoding.UTF8);
            Check(Subtitles.Read(ttml).cues[0].text=="文字字幕","TTML text subtitles");
            byte[] imageBytes;using(var image=new System.Drawing.Bitmap(16,12))using(var stream=new MemoryStream()){using(var g=System.Drawing.Graphics.FromImage(image))g.Clear(System.Drawing.Color.CornflowerBlue);image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);imageBytes=stream.ToArray();}
            string originalPhoto=Path.Combine(temp,"original.png"),editedPhoto=Path.Combine(temp,"edited.jpg");File.WriteAllBytes(originalPhoto,imageBytes);lib.SaveEditedPhoto(originalPhoto,editedPhoto,imageBytes);
            Check(File.ReadAllBytes(originalPhoto).SequenceEqual(imageBytes)&&File.Exists(editedPhoto)&&lib.Db.items.Any(x=>x.path==editedPhoto),"photo copy saved and indexed without changing original");
            bool protectedPhoto=false;try{lib.SaveEditedPhoto(originalPhoto,originalPhoto,imageBytes);}catch(InvalidDataException){protectedPhoto=true;}Check(protectedPhoto,"photo editor refuses to overwrite original");
            string arial=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");
            if(File.Exists(arial)){var font=lib.ImportFont(arial);Check(File.Exists(font.path)&&new Library(lib.DataDir).Db.fonts.Any(f=>f.id==font.id),"TTF font copied and persisted");}
            TranslationTests.Run(Path.Combine(temp,"ai"),Check);
            File.WriteAllText(Path.Combine(lib.DataDir,"library.json"),"bad json"); bool rejected=false;try{new Library(lib.DataDir);}catch(InvalidDataException){rejected=true;}
            Check(rejected&&File.ReadAllText(Path.Combine(lib.DataDir,"library.json"))=="bad json","corrupt database preserved and reported");
            Console.WriteLine(count+" tests passed.");return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
        finally { Directory.Delete(temp,true); }
    }
}
