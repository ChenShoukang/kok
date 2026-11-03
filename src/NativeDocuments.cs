using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace kok {
 internal static class NativeDocuments {
  internal static Entry Entry(ModuleContext ctx,Dictionary<string,object> a){var item=ctx.Library.Find(ModuleContext.Text(a,"id"));if(item==null||!File.Exists(item.path))throw new FileNotFoundException("找不到原文件，文件可能已移动。请从资料库移除后重新导入。");return item;}
  internal static async Task<object> Handle(ModuleContext ctx,string action,Dictionary<string,object> a){
   var lib=ctx.Library;if(action=="read"){var entry=Entry(ctx,a);ctx.Policy.Ensure(Need.None,"reading");return await Task.Run(()=>{var book=Library.ReadBook(entry);lib.IndexItem(entry,String.Join("\n\n",book.chapters.ConvertAll(ch=>ch.text)));return (object)book;});}
   if(action=="documentVersions"||action=="documentVersion"||action=="documentVersionRestore"){
    if(action=="documentVersionRestore"){using(ctx.BeginWrite()){var restored=await Task.Run(()=>DocumentVersionActions.Handle(ctx,action,a));ctx.Notify("libraryChanged",null);return restored;}}
    return await Task.Run(()=>DocumentVersionActions.Handle(ctx,action,a));
   }
   if(action=="documentSource")return await Task.Run(()=>lib.DocumentSource(ModuleContext.Text(a,"id")));
   using(ctx.BeginWrite()){object result;if(action=="createDocument"){string operation=OperationOrigin.Id??ctx.Jobs.OperationKey()??Guid.NewGuid().ToString("N");var existing=lib.Db.items.Find(e=>e.agentOperationId==operation);if(existing!=null)return existing;if(lib.OperationStatus(operation)!="")throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");result=await Task.Run(()=>lib.CreateDocument(ModuleContext.Text(a,"name"),ModuleContext.Text(a,"format"),ModuleContext.Text(a,"collection"),ModuleContext.Text(a,"text"),operation,OperationOrigin.Source??"user"));}else result=await Task.Run(()=>lib.SaveDocument(ModuleContext.Text(a,"id"),ModuleContext.Text(a,"text"),ModuleContext.Text(a,"signature"),AgentTaskState.Get<Dictionary<string,object>>(a,"recipe")));ctx.Notify("libraryChanged",null);return result;}
  }
  internal static async Task<object> Presentation(ModuleContext ctx,string action,Dictionary<string,object> a){
   var lib=ctx.Library;if(action=="readPresentation"){var e=Entry(ctx,a);if(e.ext!="PPTX")throw new InvalidDataException("请使用 PPTX 格式；旧版 PPT 请先转换。");var deck=await Task.Run(()=>PresentationFiles.Read(e.path));ctx.Policy.Ensure(Need.None,"presentations");deck.name=e.name;return deck;}
   if(action=="previewPresentation"){string tool=ModuleContext.Text(a,"tool");if(tool!="create_presentation"&&tool!="edit_presentation")throw new InvalidDataException("Agent 修改无效。");var args=AgentTaskState.Get<Dictionary<string,object>>(a,"args");AgentRules.ValidateAction(new AgentAction{tool=tool,args=args},id=>lib.Find(id)!=null);return await Task.Run(()=>{byte[] preview=tool=="create_presentation"?AgentSlides.Build(args):lib.PresentationEditPreview(args);using(var memory=new MemoryStream(preview))return (object)PresentationFiles.Read(memory);});}
   byte[] bytes=PresentationFiles.Decode(ModuleContext.Text(a,"base64"));using(ctx.BeginWrite()){
    if(action=="newPresentation"){var item=await Task.Run(()=>lib.CreatePresentation(ModuleContext.Text(a,"name"),ModuleContext.Text(a,"collection"),bytes,Guid.NewGuid().ToString("N"),"user"));ctx.Notify("libraryChanged",null);return item;}
    if(action=="savePresentation"){var saved=await Task.Run(()=>lib.SavePresentation(ModuleContext.Text(a,"id"),bytes,ModuleContext.Text(a,"signature")));ctx.Notify("libraryChanged",null);return saved;}
    var original=Entry(ctx,a);string output=ctx.ChooseDestination(original,".pptx","PowerPoint|*.pptx");if(output==null)return new{cancelled=true};string temp=output+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllBytes(temp,bytes);if(File.Exists(output))File.Replace(temp,output,null);else File.Move(temp,output);}finally{if(File.Exists(temp))File.Delete(temp);}lib.Import(new[]{output},null);ctx.Notify("libraryChanged",null);return new{saved=true,name=Path.GetFileName(output)};
   }
  }
 }
}
