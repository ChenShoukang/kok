using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
namespace kok {
 internal sealed class FileVersion {public string itemId,before,afterHash;}
 internal sealed class FileRestore:IDisposable {
  readonly Entry item;readonly byte[] after;readonly long size;readonly string modified;bool committed;
  public FileRestore(Entry item,byte[] before){this.item=item;after=File.ReadAllBytes(item.path);size=item.size;modified=item.modified;Library.ReplaceOwnedBytes(item.path,before);item.size=before.LongLength;item.modified=File.GetLastWriteTimeUtc(item.path).ToString("o");}
  public void Commit(){committed=true;}
  public void Dispose(){if(committed)return;Library.ReplaceOwnedBytes(item.path,after);item.size=size;item.modified=modified;}
 }
 public partial class Library {
  static string BytesHash(byte[] bytes){using(var hash=SHA256.Create())return "sha256:"+BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
  internal static void ReplaceOwnedBytes(string path,byte[] bytes){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);File.Replace(temp,path,path+".bak");}finally{if(File.Exists(temp))File.Delete(temp);}}
  string VersionPath(string id){if(!AgentJournal.ValidId(id))throw new InvalidDataException("操作记录无效。");return Path.Combine(DataDir,"versions",id+".sealed");}
  void SaveFileVersion(string id,FileVersion version){string folder=Path.Combine(DataDir,"versions");Directory.CreateDirectory(folder);string path=VersionPath(id);if(File.Exists(path))throw new InvalidOperationException("操作记录无效。");byte[] plain=Encoding.UTF8.GetBytes(Json.Serialize(version));byte[] sealedBytes=vault==null?ProtectedData.Protect(plain,Encoding.UTF8.GetBytes("kok.file-history.v1"),DataProtectionScope.CurrentUser):vault.Seal(plain,"file-history");File.WriteAllBytes(path,sealedBytes);}
  FileRestore RestoreFile(Operation op){if(String.IsNullOrEmpty(op.fileVersion))return null;byte[] encrypted=File.ReadAllBytes(VersionPath(op.fileVersion));byte[] bytes=vault==null?ProtectedData.Unprotect(encrypted,Encoding.UTF8.GetBytes("kok.file-history.v1"),DataProtectionScope.CurrentUser):vault.Unseal(encrypted,"file-history");var version=Json.Deserialize<FileVersion>(Encoding.UTF8.GetString(bytes));var item=ManagedDocument(version.itemId);if(DocumentHash(item.path)!=version.afterHash)throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");return new FileRestore(item,Convert.FromBase64String(version.before));}
  DocumentSaved CommitDocument(Entry entry,string text,byte[] bytes,int columns,double fontSize){
   var before=Json.Deserialize<Entry>(Json.Serialize(entry));byte[] oldBytes=File.ReadAllBytes(entry.path);string nextHash=BytesHash(bytes);var op=new Operation{id=Guid.NewGuid().ToString("N"),module="documents",source=OperationOrigin.Source??"user",title="保存文档",created=DateTime.UtcNow.ToString("o"),state="applied"};
   var draft=new Dictionary<string,object>{{"kind","text"},{"signature",nextHash},{"text",text},{"format",entry.ext.ToLowerInvariant()},{"columns",columns},{"fontSize",fontSize}};
   op.patches.Add(new AgentChange{id=entry.id,before=new Dictionary<string,object>{{"editDraft",Json.Deserialize<Dictionary<string,object>>(Json.Serialize(entry.editDraft))}},after=new Dictionary<string,object>{{"editDraft",draft}}});CheckOpCapacity(op);
   bool changed=BytesHash(oldBytes)!=nextHash;if(changed){op.fileVersion=op.id;SaveFileVersion(op.id,new FileVersion{itemId=entry.id,before=Convert.ToBase64String(oldBytes),afterHash=nextHash});}
   try{if(changed)ReplaceOwnedBytes(entry.path,bytes);entry.editDraft=draft;entry.size=bytes.LongLength;entry.modified=File.GetLastWriteTimeUtc(entry.path).ToString("o");OperationRows.Add(op);Save(false,new[]{entry.id},new[]{op.id});return new DocumentSaved{signature=nextHash,modified=entry.modified,size=entry.size};}
   catch{OperationRows.Remove(op);if(changed)ReplaceOwnedBytes(entry.path,oldBytes);foreach(var field in typeof(Entry).GetFields())field.SetValue(entry,field.GetValue(before));throw;}
  }
 }
}
