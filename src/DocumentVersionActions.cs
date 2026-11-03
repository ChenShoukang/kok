using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace kok {
    // Host actions for the native version history (P2-A): list versions, read one version (or the
    // synthesized current sentinel for diffing) and restore. Real work stays in Library.VersionHistory.
    internal static class DocumentVersionActions {
        internal const string CurrentSentinel="current";
        internal static object Handle(ModuleContext ctx,string action,Dictionary<string,object> args){
            var library=ctx.Library;string id=ModuleContext.Text(args,"id");
            if(action=="documentVersions")return new{versions=library.ListVersions(id)};
            string operationId=ModuleContext.Text(args,"operationId");
            if(action=="documentVersionLabel")return library.LabelVersion(id,operationId,ModuleContext.Text(args,"label"));
            if(action=="documentVersionUnlabel")return library.UnlabelVersion(id,operationId);
            if(action=="documentVersion"){
                if(operationId==CurrentSentinel)return CurrentVersion(library,id);
                return library.ReadVersion(id,operationId);
            }
            if(action=="documentVersionRestore"){
                if(operationId==CurrentSentinel)throw new InvalidDataException("版本记录无效。");
                var restored=library.RestoreVersion(id,operationId);
                var current=CurrentVersion(library,id);
                restored["currentText"]=current["currentText"];restored["textAvailable"]=current["textAvailable"];
                return restored;
            }
            throw new InvalidDataException("未知模块动作。");
        }
        // Synthesized "current" version so the UI can diff any saved version against the live document.
        static Dictionary<string,object> CurrentVersion(Library library,string id){
            library.ListVersions(id);// validates the item is an owned, traversal-safe document
            var item=library.Find(id);
            long size=item.size;string hash="";
            try{
                if(File.Exists(item.path)){byte[] bytes=File.ReadAllBytes(item.path);size=bytes.LongLength;hash=BytesHash(bytes);}
            }catch{hash="";}
            string currentText=null;bool textAvailable=false;
            try{currentText=library.DocumentSource(id).text;textAvailable=true;}catch{currentText=null;}
            return new Dictionary<string,object>{
                {"itemId",id},{"operationId",CurrentSentinel},{"current",true},{"created",item.modified??""},{"title","当前版本"},{"source",""},
                {"beforeHash",hash},{"afterHash",hash},{"beforeText",null},{"afterText",currentText},
                {"currentText",currentText},{"textAvailable",textAvailable},{"size",size}};
        }
        static string BytesHash(byte[] bytes){using(var sha=SHA256.Create())return "sha256:"+BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
