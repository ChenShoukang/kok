using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace kok {
    public class DocumentContent { public string text,signature; public bool recovery; }
    public class DocumentSaved { public string signature,modified; public long size; public bool saved=true; }
    public partial class Library {
        const int DocumentTextLimit=380000;
        string DocumentDirectory {get{return Path.Combine(DataDir,"documents");}}
        static string DraftValue(Dictionary<string,object> draft,string key){object value;return draft!=null&&draft.TryGetValue(key,out value)?Convert.ToString(value):"";}
        static string DocumentHash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return "sha256:"+BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        void CheckDocumentDirectory(){Directory.CreateDirectory(DocumentDirectory);if((File.GetAttributes(DocumentDirectory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("文档目录不能是链接目录。");}
        Entry ManagedDocument(string id){
            if(IsPrivate)throw new InvalidOperationException("请先返回普通资料库新建或保存文档。");
            var entry=Find(id);string root=Path.GetFullPath(DocumentDirectory)+Path.DirectorySeparatorChar;
            if(entry==null||!entry.ownedDocument||!new[]{"MD","DOCX","TXT"}.Contains(entry.ext)||!Path.GetFullPath(entry.path).StartsWith(root,StringComparison.OrdinalIgnoreCase)||!String.Equals(Path.GetDirectoryName(Path.GetFullPath(entry.path)),root.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("只能直接保存 kok 新建的文档。");
            CheckDocumentDirectory();if(File.Exists(entry.path)&&(File.GetAttributes(entry.path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("文档文件不能是链接文件。");return entry;
        }
        public Entry CreateDocument(string name,string format,string collection,string initialText="",string operationId="",string operationSource=""){
            if(IsPrivate)throw new InvalidOperationException("请先返回普通资料库新建或保存文档。");
            if(!new[]{"md","docx","txt"}.Contains(format))throw new InvalidDataException("请选择 Markdown、Word 或纯文本格式。");
            if(initialText==null||initialText.Length>DocumentTextLimit)throw new InvalidDataException("文档超过轻量编辑的大小限制。");
            name=(name??"").Trim();if(name.EndsWith("."+format,StringComparison.OrdinalIgnoreCase))name=name.Substring(0,name.Length-format.Length-1);
            if(name.Length==0||name.Length>100||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith(".")||name.EndsWith(" ")||Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase))throw new InvalidDataException("请输入有效的文档名称，最多 100 个字符。");
            collection=collection??"";if(collection.Length>100||collection.Any(Char.IsControl))throw new InvalidDataException("分类名称无效。");
            lock(gate){
                CheckDocumentDirectory();string file=null;byte[] bytes=format=="docx"?TextDocumentExport.Export(initialText,name,"docx"):new UTF8Encoding(false).GetBytes(initialText);
                for(int n=1;n<=10000;n++){string candidate=Path.Combine(DocumentDirectory,name+(n==1?"":" ("+n+")")+"."+format);try{using(var stream=new FileStream(candidate,FileMode.CreateNew,FileAccess.Write,FileShare.None))stream.Write(bytes,0,bytes.Length);file=candidate;break;}catch(IOException){if(!File.Exists(candidate))throw;}}
                if(file==null)throw new IOException("同名文档过多，请更换名称。");
                var info=new FileInfo(file);var entry=new Entry{id=Guid.NewGuid().ToString("N"),path=file,name=Path.GetFileNameWithoutExtension(file),kind="document",ext=format.ToUpperInvariant(),added=DateTime.UtcNow.ToString("o"),modified=info.LastWriteTimeUtc.ToString("o"),opened="",size=info.Length,collection=collection,ownedDocument=true,agentOperationId=operationId};
                entry.editDraft=new Dictionary<string,object>{{"kind","text"},{"signature",DocumentHash(file)},{"text",initialText},{"format",format},{"columns",1},{"fontSize",17}};
                bool addedCollection=collection!=""&&!Db.collections.Contains(collection);Db.items.Add(entry);if(addedCollection)Db.collections.Add(collection);
                Operation createdOp=null;try{if(operationSource!="")createdOp=TrackCreatedOperation(entry,operationId,operationSource,"documents");Save();}catch{if(createdOp!=null)Db.operations.Remove(createdOp);Db.items.Remove(entry);if(addedCollection)Db.collections.Remove(collection);File.Delete(file);throw;}return entry;
            }
        }
        public DocumentContent DocumentSource(string id){
            lock(gate){var entry=ManagedDocument(id);bool recovery=DraftValue(entry.editDraft,"recovery")=="True";if(File.Exists(entry.path)&&new FileInfo(entry.path).Length>8*1024*1024)throw new InvalidDataException("文档超过轻量编辑的大小限制。");string signature=File.Exists(entry.path)?DocumentHash(entry.path):"";
                if(recovery)return new DocumentContent{text=DraftValue(entry.editDraft,"text"),signature=DraftValue(entry.editDraft,"signature"),recovery=true};
                if(signature=="")throw new FileNotFoundException("文档文件已移动或删除。");
                if(new FileInfo(entry.path).Length>8*1024*1024)throw new InvalidDataException("文档超过轻量编辑的大小限制。");
                string text=entry.ext=="DOCX"?(DraftValue(entry.editDraft,"signature")==signature?DraftValue(entry.editDraft,"text"):String.Join("\n\n",ReadBook(entry).chapters.Select(c=>c.text))):File.ReadAllText(entry.path,Encoding.UTF8);
                if(text.Length>DocumentTextLimit)throw new InvalidDataException("文档超过轻量编辑的大小限制。");return new DocumentContent{text=text,signature=signature};
            }
        }
        public DocumentSaved SaveDocument(string id,string text,string expectedSignature,Dictionary<string,object> recipe){
            if(text==null||text.Length>DocumentTextLimit)throw new InvalidDataException("文档超过轻量编辑的大小限制。");
            lock(gate){var entry=ManagedDocument(id);if(!File.Exists(entry.path))throw new FileNotFoundException("文档文件已移动或删除。");if(new FileInfo(entry.path).Length>8*1024*1024)throw new InvalidDataException("文档超过轻量编辑的大小限制。");string current=DocumentHash(entry.path);
                if(current!=expectedSignature){var source=DocumentSource(id);if(source.recovery||source.text!=text)throw new InvalidOperationException("文件已在其他位置修改，请另存副本以保留修改。");return new DocumentSaved{signature=current,modified=entry.modified,size=entry.size};}
                int columns=recipe==null?1:(int)CreativeEdits.Number(recipe,"columns",1,1,2);double fontSize=recipe==null?17:CreativeEdits.Number(recipe,"fontSize",17,13,24);byte[] bytes=entry.ext=="DOCX"?TextDocumentExport.Export(text,entry.name,"docx",columns):new UTF8Encoding(false).GetBytes(text);
                return CommitDocument(entry,text,bytes,columns,fontSize);
            }
        }
        public void SaveDocumentSession(string id,Dictionary<string,object> draft){
            try{SaveDocument(id,DraftValue(draft,"text"),DraftValue(draft,"signature"),draft);}catch{lock(gate){var entry=ManagedDocument(id);if(DraftValue(draft,"text").Length>DocumentTextLimit)throw;draft["recovery"]=true;entry.editDraft=draft;Save();}}
        }
    }
    public partial class MainWindow {
        static readonly string[] DocumentActions={"createDocument","documentSource","saveDocument"};
        async Task<object> HandleDocumentAction(string action,Dictionary<string,object> args){
            var target=library;if(action=="documentSource")return await Task.Run(()=>target.DocumentSource(StringArg(args,"id")));
            backgroundLibraryWrites++;try{object result;if(action=="createDocument")result=await Task.Run(()=>target.CreateDocument(StringArg(args,"name"),StringArg(args,"format"),StringArg(args,"collection"),StringArg(args,"text")));else result=await Task.Run(()=>target.SaveDocument(StringArg(args,"id"),StringArg(args,"text"),StringArg(args,"signature"),Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(args["recipe"]))));Notify("libraryChanged",null);return result;}finally{backgroundLibraryWrites--;}
        }
    }
}
