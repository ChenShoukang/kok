using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using kok;
class PresentationRulesTests {
 static void Put(ZipArchive zip,string name,string text){using(var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false)))writer.Write(text);}
 public static byte[] Fixture(bool external=false){using(var memory=new MemoryStream()){using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)){
  Put(zip,"[Content_Types].xml","<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'/>");
  Put(zip,"ppt/presentation.xml","<p:presentation xmlns:p='http://schemas.openxmlformats.org/presentationml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><p:sldIdLst><p:sldId id='256' r:id='rId1'/></p:sldIdLst><p:sldSz cx='12192000' cy='6858000'/></p:presentation>");
  Put(zip,"ppt/_rels/presentation.xml.rels","<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Target='slides/slide1.xml'/></Relationships>");
  Put(zip,"ppt/slides/slide1.xml","<p:sld xmlns:p='http://schemas.openxmlformats.org/presentationml/2006/main' xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><p:cSld><p:spTree><p:sp><p:nvSpPr><p:nvPr><p:ph type='title'/></p:nvPr></p:nvSpPr><p:spPr><a:xfrm><a:off x='914400' y='457200'/><a:ext cx='9144000' cy='914400'/></a:xfrm></p:spPr><p:txBody><a:p><a:r><a:rPr sz='3000' b='1'/><a:t>中文标题</a:t></a:r></a:p></p:txBody></p:sp>"+(external?"<p:pic><p:blipFill><a:blip r:embed='image1'/></p:blipFill></p:pic>":"")+"</p:spTree></p:cSld></p:sld>");
  if(external)Put(zip,"ppt/slides/_rels/slide1.xml.rels","<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='image1' Target='https://example.invalid/image.png' TargetMode='External'/></Relationships>");
 }return memory.ToArray();}}
 static bool Blocked(Action action){try{action();return false;}catch(InvalidDataException){return true;}catch(InvalidOperationException){return true;}catch(FormatException){return true;}}
 static byte[] Part(string file,string part){using(var zip=ZipFile.OpenRead(file))using(var stream=zip.GetEntry(part).Open())using(var memory=new MemoryStream()){stream.CopyTo(memory);return memory.ToArray();}}
 static void AgentEditing(string root,Action<bool,string> check){
  var library=new Library(Path.Combine(root,"agent-edit"));library.SetAppMode("smart",true);var token=System.Threading.CancellationToken.None;
  var compositions=new[]{"cover","content","comparison","two_columns","process","cards","metrics","section","quote"};
  foreach(string theme in new[]{"editorial","paper","midnight","violet"}){
   var designRows=compositions.Select(layout=>new Dictionary<string,object>{{"title","让内容清楚可见"},{"layout",layout},{"bullets",layout=="metrics"?new[]{"68% | 示例完成率","24h | 示例反馈周期"}:new[]{"用真实资料形成观点","保留留白与内容层级"}},{"notes","示例数字只用于设计验收。"}}).ToArray();
   var designArgs=new Dictionary<string,object>{{"title","设计验收"},{"theme",theme},{"slides",designRows}};AgentRules.ValidateAction(new AgentAction{tool="create_presentation",args=designArgs},id=>false);
   using(var input=new MemoryStream(AgentSlides.Build(designArgs))){var design=PresentationFiles.Read(input);check(design.slides.Count==9&&design.unsupported==0&&design.slides.All(slide=>slide.shapes.All(shape=>shape.x>=0&&shape.y>=0&&shape.x+shape.w<=design.width+.01&&shape.y+shape.h<=design.height+.01)),"nine editable compositions keep every native shape inside the slide: "+theme);check(design.slides[0].shapes.Any(shape=>shape.fill!="")&&design.slides[0].shapes.First(shape=>shape.text=="让内容清楚可见").fontFamily==(theme=="editorial"?"SimSun":"Microsoft YaHei")&&design.slides[6].shapes.Any(shape=>shape.text=="68%"),"native theme retains distinct typography, flat visual fields and source-supplied metric values: "+theme);}
  }
  var denseRows=new[]{new{title="过长正文",layout="process",bullets=new[]{new string('字',200),new string('字',200),new string('字',200),new string('字',200)}}};
  var dense=new Dictionary<string,object>{{"title","密集页面"},{"slides",denseRows}};
  check(Blocked(()=>AgentRules.ValidateAction(new AgentAction{tool="create_presentation",args=dense},id=>false)),"over-dense slide copy is rejected for model repair instead of silently cropping text");
  var rows=new[]{new{title="项目回顾",layout="cover",bullets=new[]{"保留可编辑的文字与版式"},notes="开场说明与来源"},new{title="当前进展",layout="two_columns",bullets=new[]{"左侧事实","右侧结论"},notes="原来的备注"},new{title="下一步",layout="content",bullets=new[]{"保留这一页"},notes=""}};
  foreach(string theme in new[]{"paper","midnight","violet"}){var args=new Dictionary<string,object>{{"title","设计演示"},{"theme",theme},{"slides",rows}};AgentRules.ValidateAction(new AgentAction{tool="create_presentation",args=args},id=>false);using(var input=new MemoryStream(AgentSlides.Build(args))){var deck=PresentationFiles.Read(input);check(deck.slides.Count==3&&deck.slides[0].notes=="开场说明与来源"&&deck.slides[1].shapes.Count(s=>s.text=="左侧事实"||s.text=="右侧结论")==2,"native PPTX contains editable cover, two columns and speaker notes: "+theme);check(deck.slides[0].background==(theme=="paper"?"FFFFFF":theme=="midnight"?"111218":"F7F5FF"),"PPT theme roundtrips through the built-in reader: "+theme);}}
  var entry=library.CreatePresentation("逐页编辑","",AgentSlides.Build(new Dictionary<string,object>{{"title","逐页编辑"},{"slides",rows}}));
  using(var zip=ZipFile.Open(entry.path,ZipArchiveMode.Update)){Put(zip,"ppt/media/keep.bin","untouched media");Put(zip,"ppt/charts/keep.xml","<chart>保留图表数据</chart>");}
  byte[] original=File.ReadAllBytes(entry.path),unchanged=Part(entry.path,"ppt/slides/slide3.xml");var before=PresentationFiles.Read(entry.path);string shape=before.slides[1].shapes.First(s=>s.text=="左侧事实").id;
  var task=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{allowed=new List<string>{entry.id}}};
  var edits=new List<Dictionary<string,object>>{new Dictionary<string,object>{{"action","text"},{"page",2},{"shape_id",shape},{"old_text","左侧事实"},{"new_text","已经核实的事实"}},new Dictionary<string,object>{{"action","notes"},{"page",2},{"new_text","修改后的演讲备注"}}};
  var call=library.PrepareAgentCall(task,new AgentAction{tool="edit_presentation",args=new Dictionary<string,object>{{"id",entry.id},{"edits",edits}}},"turn",null);call.sequence=1;task.calls.Add(call);check(File.ReadAllBytes(entry.path).SequenceEqual(original),"PPT edits prepare a preview without writing before approval");
  var receipt=library.WriteAgentTool(task,call,token);call.status="done";call.result=receipt.result;call.stamp=receipt.stamp;var after=PresentationFiles.Read(entry.path);
  check(after.slides[1].shapes.Any(s=>s.text=="已经核实的事实")&&after.slides[1].notes=="修改后的演讲备注"&&after.slides[1].shapes.Any(s=>s.text=="右侧结论"),"approved PPT edits target exact shapes and notes while retaining other text");
  check(Part(entry.path,"ppt/slides/slide3.xml").SequenceEqual(unchanged)&&System.Text.Encoding.UTF8.GetString(Part(entry.path,"ppt/media/keep.bin"))=="untouched media"&&System.Text.Encoding.UTF8.GetString(Part(entry.path,"ppt/charts/keep.xml")).Contains("保留图表"),"PPT edits preserve untouched slide, media and chart package parts byte for byte");
  check(Convert.ToBoolean(receipt.result["readBackVerified"])&&Convert.ToString(receipt.result["stamp"])==library.AgentCreatedStamp(entry.id),"PPT edit receipt is tied to the verified saved package");
  byte[] applied=File.ReadAllBytes(entry.path);library.WriteAgentTool(task,call,token);check(File.ReadAllBytes(entry.path).SequenceEqual(applied)&&library.OperationList().Count(op=>op.id==call.id)==1,"PPT write receipts prevent duplicate execution");
  var reopened=new Library(library.DataDir);reopened.OperationUndo(call.id);check(File.ReadAllBytes(entry.path).SequenceEqual(original)&&reopened.Find(entry.id)!=null,"PPT undo survives restart and restores exact bytes without removing the work");
  var inserted=new Dictionary<string,object>{{"title","新增章节"},{"layout","section"},{"bullets",new[]{"新的内容"}},{"notes","新增备注"}};
  var replacement=new Dictionary<string,object>{{"title","重新设计"},{"layout","quote"},{"bullets",new[]{"值得保留的一句话"}}};
  var structureEdits=new[]{new Dictionary<string,object>{{"action","insert_slide"},{"page",1},{"slide",inserted}},new Dictionary<string,object>{{"action","replace_slide"},{"page",3},{"slide",replacement}},new Dictionary<string,object>{{"action","move_slide"},{"page",4},{"to",2}},new Dictionary<string,object>{{"action","delete_slide"},{"page",4}}};
  var structure=new Dictionary<string,object>{{"id",entry.id},{"theme","violet"},{"edits",structureEdits}};
  var structural=reopened.PrepareAgentCall(task,new AgentAction{tool="edit_presentation",args=structure},"turn",null);reopened.WriteAgentTool(task,structural,token);var revised=PresentationFiles.Read(entry.path);
  check(revised.slides.Count==3&&revised.slides[1].title=="下一步"&&revised.slides[2].title=="新增章节"&&revised.slides[2].notes=="新增备注","PPT insert, redesign, move and delete use sequential page positions");
  reopened.OperationUndo(structural.id);check(File.ReadAllBytes(entry.path).SequenceEqual(original),"structural PPT edits undo as one approved operation");
  var stale=reopened.PrepareAgentCall(task,new AgentAction{tool="edit_presentation",args=new Dictionary<string,object>{{"id",entry.id},{"edits",edits}}},"turn",null);reopened.Find(entry.id).editDraft=new Dictionary<string,object>{{"kind","presentation"},{"signature","manual draft"}};reopened.Save();
  check(Blocked(()=>reopened.WriteAgentTool(task,stale,token))&&File.ReadAllBytes(entry.path).SequenceEqual(original),"an intervening manual slide draft invalidates pending Agent approval");reopened.Find(entry.id).editDraft=new Dictionary<string,object>();reopened.Save();
  string imported=Path.Combine(root,"imported-original.pptx");File.WriteAllBytes(imported,original);reopened.Import(new[]{imported},null);var source=reopened.Db.items.First(e=>e.path==imported);task.options.allowed.Add(source.id);
  check(Blocked(()=>reopened.PrepareAgentCall(task,new AgentAction{tool="edit_presentation",args=new Dictionary<string,object>{{"id",source.id},{"edits",edits}}},"turn",null)),"Agent cannot overwrite an imported original PPTX");
  var copy=reopened.PrepareAgentCall(task,new AgentAction{tool="copy_presentation",args=new Dictionary<string,object>{{"id",source.id},{"name","原件的编辑副本"}}},"turn",null);var copied=reopened.WriteAgentTool(task,copy,token);var managed=reopened.Find(Convert.ToString(copied.result["id"]));check(managed.ownedDocument&&File.ReadAllBytes(managed.path).SequenceEqual(original)&&File.ReadAllBytes(imported).SequenceEqual(original),"approved PPT copy retains the full original package and creates a separate managed work");
  var read=new AgentCall{tool="read_presentation",args=new Dictionary<string,object>{{"id",entry.id},{"start_page",2},{"max_pages",1}}};var result=reopened.ReadAgentTool(task,read,token).GetAwaiter().GetResult();var window=Library.Json.Deserialize<Dictionary<string,object>>(Convert.ToString(result["text"]));check(Convert.ToInt32(window["next_page"])==3&&Library.Json.Serialize(window["slides"]).Contains(shape),"Agent PPT reads return bounded exact pages and native shape IDs");
  check(Blocked(()=>AgentRules.ValidateAction(new AgentAction{tool="edit_presentation",args=new Dictionary<string,object>{{"id",entry.id},{"edits",new[]{new Dictionary<string,object>{{"action","delete_slide"},{"page",0}}}}}},id=>true)),"invalid presentation page numbers fail validation before proposals");
 }
 public static void Run(string root,Action<bool,string> check){Directory.CreateDirectory(root);AgentEditing(root,check);var bytes=Fixture();PresentationDeck deck;using(var data=new MemoryStream(bytes))deck=PresentationFiles.Read(data);
  check(deck.slides.Count==1&&deck.slides[0].title=="中文标题"&&deck.slides[0].shapes[0].bold&&deck.slides[0].shapes[0].fontSize==30,"PPTX preview extracts Unicode text, geometry and basic formatting");
  check(Math.Abs(deck.width-13.333333)<.001&&deck.slides[0].shapes[0].x==1,"PPTX geometry retains its source aspect ratio and positions");
  using(var data=new MemoryStream(Fixture(true)))check(PresentationFiles.Read(data).unsupported==1,"PPTX external image relationships are skipped without a network request");
  bool invalid=false;try{PresentationFiles.Decode("not a package");}catch{invalid=true;}check(invalid,"invalid PPTX exports are rejected before any destination file is opened");
  var library=new Library(Path.Combine(root,"data"));Entry first=library.CreatePresentation("Presentation","Slides",bytes),second=library.CreatePresentation("Presentation","Slides",bytes);
  check(first.ext=="PPTX"&&second.name=="Presentation (2)"&&File.ReadAllBytes(first.path).SequenceEqual(bytes),"new PPTX files are classified and duplicate names never replace originals");
  invalid=false;try{library.CreatePresentation("../escape","",bytes);}catch(InvalidDataException){invalid=true;}check(invalid,"presentation creation rejects paths outside the managed document directory");
  check(library.BuildKnowledgeIndex().nodes.Any(n=>n.id==first.id),"PPTX readable text participates in the local knowledge index");
  check(VisualEditRules.Parse("{\"text\":\"Changed selection\"}")=="Changed selection","visual edits accept a bounded text replacement without tool commands");
  invalid=false;try{VisualEditRules.Parse("{\"text\":\"\"}");}catch(InvalidDataException){invalid=true;}check(invalid,"empty visual edits cannot silently delete a selection");
  var blocks=new List<VisualEditBlock>{new VisualEditBlock{id="title",text="Title"},new VisualEditBlock{id="body",text="Body"}};
  check(VisualEditRules.ParseBlocks("{\"edits\":[{\"id\":\"body\",\"text\":\"New body\"},{\"id\":\"title\",\"text\":\"New title\"}]}",blocks).Count==2,"whole-page edits retain exact object IDs before application");
  foreach(string answer in new[]{"{\"edits\":[{\"id\":\"title\",\"text\":\"A\"},{\"id\":\"title\",\"text\":\"B\"}]}","{\"edits\":[{\"id\":\"title\",\"text\":\"A\"},{\"id\":\"outside\",\"text\":\"B\"}]}"}){invalid=false;try{VisualEditRules.ParseBlocks(answer,blocks);}catch(InvalidDataException){invalid=true;}check(invalid,"duplicate and out-of-page AI object IDs are rejected atomically");}
 }
}
