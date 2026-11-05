using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace kok {
    internal sealed class ProjectDelivery {
        public string sourceId, exportId, created, sha256;
    }

    public partial class Library {
        const int DeliveryLimit=500;
        static readonly byte[] DeliveryPurpose=Encoding.UTF8.GetBytes("kok.project-deliveries.v1");
        string DeliveryFile => Path.Combine(DataDir,"project-deliveries.sealed");

        List<ProjectDelivery> LoadDeliveries(){
            if(!File.Exists(DeliveryFile))return new List<ProjectDelivery>();
            var bytes=ProtectedData.Unprotect(File.ReadAllBytes(DeliveryFile),DeliveryPurpose,DataProtectionScope.CurrentUser);
            return Json.Deserialize<List<ProjectDelivery>>(Encoding.UTF8.GetString(bytes))??new List<ProjectDelivery>();
        }

        void SaveDeliveries(List<ProjectDelivery> rows){
            var bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Serialize(rows)),DeliveryPurpose,DataProtectionScope.CurrentUser);
            string temp=DeliveryFile+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllBytes(temp,bytes);
                if(File.Exists(DeliveryFile))File.Replace(temp,DeliveryFile,null);
                else File.Move(temp,DeliveryFile);
            } finally { if(File.Exists(temp))File.Delete(temp); }
        }

        // Called only by the native export handlers after a copy has been written and imported.
        internal bool TryRecordProjectDelivery(Entry source,Entry exported,byte[] bytes){
            if(IsPrivate||!source.ownedDocument)return true;
            lock(gate){
                if(!Db.workspace.spaces.Any(p=>p.kind=="project"&&p.artifacts!=null&&p.artifacts.Contains(source.id)))return true;
                if(exported==null||exported.id==source.id)return false;
                try {
                    var rows=LoadDeliveries();
                    string fingerprint;
                    using(var sha=SHA256.Create())fingerprint=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                    rows.Insert(0,new ProjectDelivery{sourceId=source.id,exportId=exported.id,created=DateTime.UtcNow.ToString("o"),sha256=fingerprint});
                    SaveDeliveries(rows.Take(DeliveryLimit).ToList());
                    return true;
                } catch(Exception error){
                    RuntimeLog.Write("project-delivery-save",error);
                    return false;
                }
            }
        }

        internal object ReadProjectDeliveries(string projectId){
            EnsureNormalContent();
            lock(gate){
                var project=FindSpaceRecord(projectId);
                if(project==null||project.kind!="project")return new object[0];
                var sources=new HashSet<string>(project.artifacts??new List<string>());
                var rows=new List<object>();
                foreach(var record in LoadDeliveries()){
                    if(record==null||!sources.Contains(record.sourceId))continue;
                    var source=Find(record.sourceId);
                    var exported=Find(record.exportId);
                    if(source==null||exported==null)continue;
                    rows.Add(new{record.sourceId,record.exportId,record.created,record.sha256,source=source.name,name=Path.GetFileName(exported.path),format=exported.ext,path=exported.path,available=File.Exists(exported.path)});
                    if(rows.Count>=40)break;
                }
                return rows;
            }
        }

        void ForgetProjectDeliveries(HashSet<string> ids){
            if(!File.Exists(DeliveryFile))return;
            var rows=LoadDeliveries();
            if(rows.RemoveAll(row=>row!=null&&(ids.Contains(row.sourceId)||ids.Contains(row.exportId)))>0)SaveDeliveries(rows);
        }
    }
}
