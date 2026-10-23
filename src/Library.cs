using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace kok {
    public class Entry {
        public string id, path, name, kind, ext, added, opened;
        public string collection = "", album = "", artist = "本地音乐";
        public long size;
        public bool favorite;
        public double progress, position;
        public int chapter;
        public string notes = "";
        public bool hasCover;
        public string coverVersion = "";
        public string aiCategory = "", aiSummary = "", aiFingerprint = "";
        public List<string> tags = new List<string>();
        public string modified="";
        public int width,height;
        public List<ChatTurn> aiChat=new List<ChatTurn>();
        public List<Annotation> annotations = new List<Annotation>();
        public Dictionary<string,string> chapterTexts = new Dictionary<string,string>();
    }
    public class Annotation { public string id,style,text,note; public int chapter,start,end,page; public double x,y; public List<MarkRect> rects = new List<MarkRect>(); }
    public class MarkRect { public double x,y,w,h; }
    public class FontAsset { public string id,name,path; }
    public class Database {
        public int version = 1;
        public List<Entry> items = new List<Entry>();
        public Dictionary<string, object> settings = new Dictionary<string, object>();
        public List<string> collections = new List<string>();
        public List<FontAsset> fonts = new List<FontAsset>();
        public List<WatchFolder> watchFolders = new List<WatchFolder>();
        public List<string> ignoredAutoPaths = new List<string>();
    }
    public class Chapter { public string title, text; public bool placeholder,generatedTitle; }
    public class Book { public string title; public List<Chapter> chapters = new List<Chapter>(); }
    public class ImportResult { public int added, skipped; public List<string> errors = new List<string>(); }

    public partial class Library {
        public readonly string DataDir;
        readonly VaultSession vault;
        public bool IsPrivate {get{return vault!=null;}}
        public Database Db;
        private readonly object gate = new object();
        public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 100 };
        private static readonly Dictionary<string,string> Formats = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        static Library() {
            foreach(var group in Compatibility.Groups) AddFormats(group.kind,group.extensions);
        }
        static void AddFormats(string kind, string extensions) { foreach (string x in extensions.Split(' ')) Formats["." + x] = kind; }
        public static string Classify(string path) { string kind; return Formats.TryGetValue(Path.GetExtension(path), out kind) ? kind : null; }
        public Library(string dataDir,VaultSession privateVault=null) {
            vault=privateVault;
            DataDir = dataDir;
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(Path.Combine(DataDir, "thumbs"));
            string file = Path.Combine(DataDir, vault==null?"library.json":"library.vault");
            if (File.Exists(file)) {
                try { Db = Json.Deserialize<Database>(vault==null?File.ReadAllText(file, Encoding.UTF8):Encoding.UTF8.GetString(vault.Unseal(File.ReadAllBytes(file),"library"))); if (Db == null || Db.items == null || Db.settings == null) throw new InvalidDataException(); }
                catch (Exception ex) { throw new InvalidDataException(vault==null?"资料库无法读取。原文件已保留，请检查 data/library.json 或 library.json.bak 备份。":"私密资料库校验失败，原数据已保留。", ex); }
            } else {if(vault!=null&&File.Exists(file+".bak"))throw new InvalidDataException("私密资料库索引缺失，请保留备份并恢复后再试。");Db = new Database();}
            if(vault!=null)Db.settings["appMode"]="pure";
            foreach(var item in Db.items.Where(x=>x.kind=="video"))item.hasCover=File.Exists(ThumbnailPath(item));
        }
        public string Snapshot() { lock(gate) return Json.Serialize(Db); }
        public Entry Find(string id) { lock(gate) return Db.items.FirstOrDefault(x => x.id == id); }
        public void Save() {
            lock(gate) {
                string file = Path.Combine(DataDir, vault==null?"library.json":"library.vault"), temp = file + ".tmp";
                if(vault==null)File.WriteAllText(temp, Json.Serialize(Db), new UTF8Encoding(false));else File.WriteAllBytes(temp,vault.Seal(Encoding.UTF8.GetBytes(Json.Serialize(Db)),"library"));
                if (File.Exists(file)) File.Replace(temp, file, file + ".bak"); else File.Move(temp, file);
            }
        }
        public ImportResult Import(IEnumerable<string> paths, Action<int> progress) {
            var result = new ImportResult();
            HashSet<string> known;
            lock(gate) known = new HashSet<string>(Db.items.Select(x => x.path), StringComparer.OrdinalIgnoreCase);
            int seen = 0;
            foreach (string input in paths) {
                try {
                    string path = Path.GetFullPath(input);
                    string kind = Classify(path);
                    if (kind == null || !File.Exists(path) || !known.Add(path)) { result.skipped++; continue; }
                    var info = new FileInfo(path);
                    string folder = new DirectoryInfo(Path.GetDirectoryName(path)).Name;
                    var entry = new Entry { id = Guid.NewGuid().ToString("N"), path = path, name = Path.GetFileNameWithoutExtension(path), kind = kind, ext = info.Extension.TrimStart('.').ToUpperInvariant(), size = info.Length, added = DateTime.UtcNow.ToString("o"), modified=info.LastWriteTimeUtc.ToString("o"), opened = "", collection = folder=="samples" ? "kok 示例" : folder, album = folder=="samples" ? "kok · 原创音景" : folder };
                    lock(gate) { Db.items.Add(entry); Db.ignoredAutoPaths.RemoveAll(x=>String.Equals(x,path,StringComparison.OrdinalIgnoreCase)); if(!Db.collections.Contains(entry.collection)) Db.collections.Add(entry.collection); }
                    result.added++;
                    if (kind == "photo") MakeThumbnail(entry);
                } catch (Exception ex) { if (result.errors.Count < 20) result.errors.Add(Path.GetFileName(input) + ": " + ex.Message); }
                if (++seen % 25 == 0 && progress != null) progress(seen);
            }
            Save(); return result;
        }
        public ImportResult ImportDropped(IEnumerable<string> paths, Action<int> progress) {
            var errors = new List<string>();
            var result = Import(ExpandDropped(paths, errors), progress);
            result.errors.AddRange(errors.Take(Math.Max(0,20-result.errors.Count)));
            return result;
        }
        static IEnumerable<string> ExpandDropped(IEnumerable<string> paths, List<string> errors) {
            foreach(string path in paths) {
                if(Directory.Exists(path)) { foreach(string file in Walk(path,errors)) yield return file; }
                else yield return path;
            }
        }
        public static IEnumerable<string> Walk(string folder, List<string> errors) {
            var stack = new Stack<string>(); stack.Push(folder);
            while (stack.Count > 0) {
                string dir = stack.Pop(); string[] files, dirs;
                try { files = Directory.GetFiles(dir); dirs = Directory.GetDirectories(dir); }
                catch (Exception ex) { if(errors.Count < 20) errors.Add(dir + ": " + ex.Message); continue; }
                foreach (string file in files) yield return file;
                foreach (string child in dirs) {
                    try { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) stack.Push(child); }
                    catch (Exception ex) { if(errors.Count < 20) errors.Add(child + ": " + ex.Message); }
                }
            }
        }
        public void Update(string id, Dictionary<string,object> patch) {
            lock(gate) {
                Entry e = Find(id); if(e == null) throw new FileNotFoundException("资料已移除。");
                if(patch.ContainsKey("favorite")) e.favorite = Convert.ToBoolean(patch["favorite"]);
                if(patch.ContainsKey("progress")) e.progress = Clamp(Convert.ToDouble(patch["progress"]),0,1);
                if(patch.ContainsKey("position")) e.position = Clamp(Convert.ToDouble(patch["position"]),0,1e10);
                if(patch.ContainsKey("chapter")) e.chapter = Math.Max(0, Convert.ToInt32(patch["chapter"]));
                if(patch.ContainsKey("notes")) { string text = Convert.ToString(patch["notes"]); if(text.Length > 200000) throw new InvalidDataException("笔记过长。"); e.notes = text; }
                if(patch.ContainsKey("collection")) { e.collection = Limit(Convert.ToString(patch["collection"]),100); if(e.collection!=""&&!Db.collections.Contains(e.collection))Db.collections.Add(e.collection); }
                if(patch.ContainsKey("album")) e.album=Limit(Convert.ToString(patch["album"]),200);
                if(patch.ContainsKey("artist")) e.artist=Limit(Convert.ToString(patch["artist"]),200);
                if(patch.ContainsKey("width")&&patch.ContainsKey("height")){int width=Convert.ToInt32(patch["width"]),height=Convert.ToInt32(patch["height"]);if(width<1||height<1||width>100000||height>100000)throw new InvalidDataException("媒体尺寸无效。");e.width=width;e.height=height;}
                if(patch.ContainsKey("annotations")) {
                    var annotations=Json.Deserialize<List<Annotation>>(Json.Serialize(patch["annotations"]));
                    if(annotations==null||annotations.Count>10000)throw new InvalidDataException("标记数量超过上限。");
                    foreach(var mark in annotations) { if(!new[]{"underline","highlight","insert"}.Contains(mark.style)||mark.id==null||mark.id.Length>64||mark.start<0||mark.end<mark.start)throw new InvalidDataException("标记格式无效。");mark.note=Limit(mark.note??"",20000);mark.text=Limit(mark.text??"",100000); }
                    e.annotations=annotations;
                }
                if(patch.ContainsKey("chapterTexts")) { var texts=Json.Deserialize<Dictionary<string,string>>(Json.Serialize(patch["chapterTexts"])); if(texts==null||texts.Count>2000||texts.Values.Sum(x=>(long)x.Length)>24*1024*1024)throw new InvalidDataException("编辑正文过大。");e.chapterTexts=texts; }
                if(patch.ContainsKey("opened")) e.opened = DateTime.UtcNow.ToString("o");
                Save();
            }
        }
        static string Limit(string value,int max) { if(value.Length>max)throw new InvalidDataException("内容过长。");return value; }
        public void AddCollection(string name) { name=Limit(name.Trim(),100);if(name=="")throw new InvalidDataException("请输入分类名称。");lock(gate){if(!Db.collections.Contains(name))Db.collections.Add(name);Save();} }
        public void DeleteCollection(string name) { lock(gate){Db.collections.Remove(name);foreach(var item in Db.items)if(item.collection==name)item.collection="";Save();} }
        static double Clamp(double n,double a,double b) { return double.IsNaN(n) || double.IsInfinity(n) ? a : Math.Max(a,Math.Min(b,n)); }
        public void Remove(string id) { lock(gate) { var item=Find(id); if(item!=null&&!Db.ignoredAutoPaths.Contains(item.path))Db.ignoredAutoPaths.Add(item.path); Db.items.RemoveAll(x=>x.id==id); Save(); } }
        public void Settings(Dictionary<string,object> value) { lock(gate) { foreach(var pair in value) if(new[]{"theme","readerSize","readerTheme","volume","view","sort","loop","shuffle","fontFamily","videoRate","smartSpeed","uiLanguage","readerToc","videoVolume","readerFollowTheme","videoShortcutsEnabled","videoPlayKey","videoSeekKeys","videoSeekSeconds","videoVolumeKeys","videoDoubleClick","organizeMode"}.Contains(pair.Key)) Db.settings[pair.Key] = pair.Value; Save(); } }
        public FontAsset ImportFont(string source) {
            var info=new FileInfo(source);if(info.Length>20*1024*1024)throw new InvalidDataException("字体文件不可超过 20 MB。");
            using(var stream=File.OpenRead(source)){byte[] head=new byte[4];if(stream.Read(head,0,4)!=4||!((head[0]==0&&head[1]==1&&head[2]==0&&head[3]==0)||Encoding.ASCII.GetString(head)=="OTTO"))throw new InvalidDataException("请选择有效的 TTF 或 OTF 字体。");}
            var font=new FontAsset{id=Guid.NewGuid().ToString("N"),name=Path.GetFileNameWithoutExtension(source)};
            Directory.CreateDirectory(Path.Combine(DataDir,"fonts"));font.path=Path.Combine(DataDir,"fonts",font.id+Path.GetExtension(source).ToLowerInvariant());File.Copy(source,font.path);
            lock(gate){Db.fonts.Add(font);Save();}return font;
        }
        public void SaveEditedPhoto(string original,string target,byte[] bytes) {
            if(Path.GetFullPath(target).Equals(Path.GetFullPath(original),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("请选择新的文件名，以保留原片。");
            using(var input=new MemoryStream(bytes))using(var image=Image.FromStream(input)) {
                if(image.Width>8192||image.Height>8192)throw new InvalidDataException("输出图片尺寸过大。");
                image.Save(target,Path.GetExtension(target).ToLowerInvariant()==".jpg"?System.Drawing.Imaging.ImageFormat.Jpeg:System.Drawing.Imaging.ImageFormat.Png);
            }
            Import(new[]{target},null);
        }
        public string ThumbnailPath(Entry e) { return Path.Combine(DataDir,"thumbs",e.id + (vault==null?".jpg":".sealed")); }
        public Stream OpenThumbnail(Entry e){return vault==null?(Stream)File.OpenRead(ThumbnailPath(e)):new MemoryStream(vault.Unseal(File.ReadAllBytes(ThumbnailPath(e)),"thumb/"+e.id),false);}
        void WriteThumbnail(string file,Entry item,byte[] image){File.WriteAllBytes(file,vault==null?image:vault.Seal(image,"thumb/"+item.id));}
        public object SaveVideoCover(string id,string encoded) {
            var item=Find(id);if(item==null||item.kind!="video")throw new InvalidDataException("请选择资料库中的视频。");
            const string prefix="data:image/jpeg;base64,";
            if(encoded==null||!encoded.StartsWith(prefix,StringComparison.Ordinal)||encoded.Length>1024*1024)throw new InvalidDataException("视频封面数据无效。");
            byte[] bytes=Convert.FromBase64String(encoded.Substring(prefix.Length));
            string target=ThumbnailPath(item),temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream)) {
                    if(image.Width<1||image.Height<1||image.Width>640||image.Height>1280)throw new InvalidDataException("视频封面尺寸无效。");
                    using(var bitmap=new Bitmap(image.Width,image.Height))using(var graphics=Graphics.FromImage(bitmap)) {
                        graphics.Clear(Color.Black);graphics.DrawImage(image,0,0,image.Width,image.Height);using(var output=new MemoryStream()){bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Jpeg);WriteThumbnail(temp,item,output.ToArray());}
                    }
                }
                lock(gate) {
                    if(!Db.items.Contains(item))return new { saved=false };
                    if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
                    item.hasCover=true;item.coverVersion=DateTime.UtcNow.Ticks.ToString();Save();
                    return new { saved=true,hasCover=true,coverVersion=item.coverVersion };
                }
            } finally { if(File.Exists(temp))File.Delete(temp); }
        }
        void MakeThumbnail(Entry e) {
            try {
                using(var source = Image.FromFile(e.path)) {
                    RotateOrientation(source);
                    double scale = Math.Min(1, 600.0 / Math.Max(source.Width,source.Height));
                    using(var image = new Bitmap(Math.Max(1,(int)(source.Width*scale)),Math.Max(1,(int)(source.Height*scale))))
                    using(var g = Graphics.FromImage(image)) {
                        g.Clear(Color.FromArgb(240,240,235)); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(source,0,0,image.Width,image.Height);
                        using(var output=new MemoryStream()){image.Save(output,System.Drawing.Imaging.ImageFormat.Jpeg);WriteThumbnail(ThumbnailPath(e),e,output.ToArray());}
                    }
                }
            } catch { /* Unsupported images remain accessible through their system application. */ }
        }
        static void RotateOrientation(Image image) {
            try {
                if(!image.PropertyIdList.Contains(0x112)) return;
                int orientation = image.GetPropertyItem(0x112).Value[0];
                RotateFlipType[] transforms = { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                if(orientation > 0 && orientation < transforms.Length) image.RotateFlip(transforms[orientation]);
            } catch { }
        }
        public static string ReadText(string path) {
            if(new FileInfo(path).Length > 24*1024*1024) throw new InvalidDataException("文本超过 24 MB，请使用系统应用打开。");
            byte[] bytes = File.ReadAllBytes(path);
            if(bytes.Length >= 2 && bytes[0]==255 && bytes[1]==254) return Encoding.Unicode.GetString(bytes,2,bytes.Length-2);
            if(bytes.Length >= 2 && bytes[0]==254 && bytes[1]==255) return Encoding.BigEndianUnicode.GetString(bytes,2,bytes.Length-2);
            try { return new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF'); }
            catch(DecoderFallbackException) { return Encoding.GetEncoding(54936).GetString(bytes); }
        }
        static XDocument ReadXml(ZipArchiveEntry e) {
            if(e == null) throw new InvalidDataException("文档结构不完整。");
            if(e.Length > 24*1024*1024) throw new InvalidDataException("文档章节过大。");
            using(var stream=e.Open()) using(var reader=System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 24*1024*1024 })) return XDocument.Load(reader);
        }
        public static Book ReadBook(Entry e) {
            var book = new Book { title = e.name };
            string ext=Path.GetExtension(e.path).ToLowerInvariant();
            if(ext==".epub") {
                using(var zip=ZipFile.OpenRead(e.path)) {
                    XNamespace ns="urn:oasis:names:tc:opendocument:xmlns:container";
                    var container=ReadXml(zip.GetEntry("META-INF/container.xml"));
                    string opf=(string)container.Descendants(ns+"rootfile").First().Attribute("full-path");
                    var package=ReadXml(zip.GetEntry(opf)); XNamespace p="http://www.idpf.org/2007/opf";
                    XNamespace dc="http://purl.org/dc/elements/1.1/";
                    book.title=(string)package.Descendants(dc+"title").FirstOrDefault() ?? e.name;
                    string baseDir=opf.Contains("/") ? opf.Substring(0,opf.LastIndexOf('/')+1) : "";
                    var manifest=package.Descendants(p+"manifest").Elements(p+"item").Where(x=>x.Attribute("id")!=null).ToDictionary(x=>(string)x.Attribute("id"));
                    long total=0;
                    foreach(var reference in package.Descendants(p+"spine").Elements(p+"itemref")) {
                        XElement item; if(!manifest.TryGetValue((string)reference.Attribute("idref"),out item)) continue;
                        string href=(string)item.Attribute("href"); if(String.IsNullOrEmpty(href)) continue;
                        var uri=new Uri(new Uri("https://book.local/"+baseDir),href);
                        if(uri.Host!="book.local") continue;
                        string name=Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
                        var entry=zip.GetEntry(name); if(entry==null) continue;
                        total+=entry.Length; if(total > 48*1024*1024 || book.chapters.Count > 2000) throw new InvalidDataException("电子书内容过大。");
                        var doc=ReadXml(entry);
                        var body=doc.Descendants().FirstOrDefault(x=>x.Name.LocalName=="body") ?? doc.Root;
                        foreach(var unsafeNode in body.Descendants().Where(x=>new[]{"script","style","iframe","object","head"}.Contains(x.Name.LocalName)).ToList()) unsafeNode.Remove();
                        string title=body.Descendants().Where(x=>new[]{"h1","h2","h3"}.Contains(x.Name.LocalName)).Select(x=>x.Value.Trim()).FirstOrDefault();
                        var blocks=body.Descendants().Where(x=>new[]{"p","h1","h2","h3","h4","li","blockquote","pre"}.Contains(x.Name.LocalName) && !x.Ancestors().Any(a=>a!=body && new[]{"p","li","blockquote","pre"}.Contains(a.Name.LocalName))).Select(x=>x.Value.Trim()).Where(x=>x.Length>0).ToList();
                        if(blocks.Count>1&&blocks[0]==title)blocks.RemoveAt(0);
                        string text=blocks.Count>0 ? String.Join("\n\n",blocks) : body.Value.Trim();
                        if(text.Length>0) book.chapters.Add(new Chapter { generatedTitle=String.IsNullOrWhiteSpace(title),title=String.IsNullOrWhiteSpace(title) ? "第 " + (book.chapters.Count+1) + " 节" : title, text=text });
                    }
                }
            } else if(ext==".docx") {
                using(var zip=ZipFile.OpenRead(e.path)) {
                    XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                    var doc=ReadXml(zip.GetEntry("word/document.xml"));
                    book.chapters.Add(new Chapter { title=e.name, text=String.Join("\n\n",doc.Descendants(w+"p").Select(x=>String.Concat(x.Descendants(w+"t").Select(t=>t.Value)))) });
                }
            } else if(new[]{".txt",".md",".markdown",".csv",".json",".log",".html",".htm"}.Contains(ext)) {
                string text=ReadText(e.path);
                const int chunk=16000;
                while(text.Length>0) {
                    int end=Math.Min(chunk,text.Length);
                    if(end<text.Length) { int newline=text.IndexOf('\n',end); if(newline>=0 && newline<end+4000) end=newline+1; }
                    book.chapters.Add(new Chapter { generatedTitle=book.chapters.Count>0,title=book.chapters.Count==0 ? e.name : "第 "+(book.chapters.Count+1)+" 节", text=text.Substring(0,end) });
                    text=text.Substring(end);
                }
            } else throw new InvalidDataException("此格式请使用系统应用打开。内置阅读支持 EPUB、TXT、DOCX、Markdown 和常见文本；PDF 使用内置预览。");
            if(book.chapters.Count==0) book.chapters.Add(new Chapter { title=e.name,placeholder=true,text="这个文件没有可读取的文字内容。扫描版文档需要先进行 OCR；加密电子书需要授权阅读器。" });
            if(e.chapterTexts!=null)foreach(var edit in e.chapterTexts){int index;if(Int32.TryParse(edit.Key,out index)&&index>=0&&index<book.chapters.Count)book.chapters[index].text=edit.Value;}
            return book;
        }
    }
}
