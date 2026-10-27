using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO.Compression;
using System.Collections.Generic;
using kok;

class DocumentTests {
    public static void Run(string root,Action<bool,string> check){
        var library=new Library(root);var md=library.CreateDocument("学习笔记","md","学习");check(md.ownedDocument&&md.kind=="document"&&md.collection=="学习"&&md.ext=="MD"&&File.Exists(md.path),"new Markdown creates a managed document in its collection");
        string text="# 标题\n\n**重点** 与 *想法*\n- 项目\n> 引用\n";var source=library.DocumentSource(md.id);var saved=library.SaveDocument(md.id,text,source.signature,null);check(File.ReadAllText(md.path,Encoding.UTF8)==text&&library.DocumentSource(md.id).text==text&&File.Exists(md.path+".bak"),"Markdown saves UTF-8 source exactly with an atomic previous-version backup");
        var duplicate=library.CreateDocument("学习笔记.md","md","学习");check(duplicate.path!=md.path&&duplicate.name=="学习笔记 (2)"&&File.ReadAllText(md.path)==text,"duplicate creation never overwrites the first document");
        int before=library.Db.items.Count;foreach(string name in new[]{"../escape","CON","NUL.txt","","bad:name",new string('x',101)}){bool rejected=false;try{library.CreateDocument(name,"md","");}catch(InvalidDataException){rejected=true;}check(rejected,"invalid document name is rejected: "+name.Substring(0,Math.Min(name.Length,20)));}check(library.Db.items.Count==before,"failed creation leaves no library entries");
        bool badFormat=false;try{library.CreateDocument("unsupported","html","");}catch(InvalidDataException){badFormat=true;}check(badFormat,"only supported creation formats are accepted");
        var docx=library.CreateDocument("中文 Word","docx","");library.SaveDocument(docx.id,text,library.DocumentSource(docx.id).signature,null);using(var zip=ZipFile.OpenRead(docx.path))using(var reader=new StreamReader(zip.GetEntry("word/document.xml").Open())){var xml=XDocument.Parse(reader.ReadToEnd());XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";check(zip.GetEntry("[Content_Types].xml")!=null&&xml.Descendants(w+"b").Any()&&xml.Descendants(w+"pStyle").Any(p=>(string)p.Attribute(w+"val")=="Heading1")&&xml.Root.Value.Contains("重点"),"DOCX is a real OpenXML package with Unicode headings and bold text");}
        var reload=new Library(root);check(reload.Find(docx.id).ownedDocument&&reload.DocumentSource(docx.id).text==text,"DOCX editing source and managed ownership survive reload");
        bool badRecipe=false;try{library.SaveDocument(md.id,"invalid replacement",saved.signature,new Dictionary<string,object>{{"fontSize",900}});}catch(InvalidDataException){badRecipe=true;}check(badRecipe&&File.ReadAllText(md.path)==text,"invalid save settings fail before changing the file");
        File.WriteAllText(md.path,"external edit",new UTF8Encoding(false));bool conflict=false;try{library.SaveDocument(md.id,"local edit",saved.signature,null);}catch(InvalidOperationException){conflict=true;}check(conflict&&File.ReadAllText(md.path)=="external edit","external modifications are not overwritten by stale saves");
        library.SaveDocumentSession(md.id,new Dictionary<string,object>{{"kind","text"},{"text","recover this"},{"signature",saved.signature},{"columns",1},{"fontSize",17}});var recovered=new Library(root).DocumentSource(md.id);check(recovered.recovery&&recovered.text=="recover this"&&File.ReadAllText(md.path)=="external edit","closing during a conflict preserves a recoverable local draft without replacing the file");
        var txt=library.CreateDocument("Plain","txt","");library.SaveDocument(txt.id,"plain\n文字",library.DocumentSource(txt.id).signature,null);check(File.ReadAllText(txt.path)=="plain\n文字","plain-text creation and direct save work offline");
        string imported=Path.Combine(root,"imported.md");File.WriteAllText(imported,"keep imported");library.Import(new[]{imported},null);var entry=library.Db.items.First(e=>e.path==imported);bool protectedFile=false;try{library.SaveDocument(entry.id,"overwrite","",null);}catch(InvalidDataException){protectedFile=true;}check(protectedFile&&File.ReadAllText(imported)=="keep imported","direct save cannot overwrite imported originals");
    }
}
