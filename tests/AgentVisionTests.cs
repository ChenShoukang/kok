using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Threading;
using kok;

static class AgentVisionTests {
 public static void Run(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  string small=Path.Combine(root,"shot.png");WriteImage(small,320,180);
  string big=Path.Combine(root,"big.png");WriteImage(big,3000,2000);
  byte[] encoded=AgentVision.EncodeJpeg(big);
  using(var image=Image.FromStream(new MemoryStream(encoded)))check(image.Width<=AgentVision.MaxDimension&&image.Height<=AgentVision.MaxDimension&&encoded.Length<=AgentVision.MaxEncodedBytes,"vision downscales large images into a bounded JPEG before attaching them");
  var lib=new Library(Path.Combine(root,"library"));lib.SetAppMode("smart",true);
  var entry=new Entry{id=Guid.NewGuid().ToString("N").Substring(0,32),name="screenshot",path=small,kind="photo",ext=".png",added=DateTime.UtcNow.ToString("o"),size=new FileInfo(small).Length};lib.Db.items.Add(entry);lib.Save();
  var state=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="Read a screenshot",allowed=new List<string>{entry.id},vision=true}};
  var call=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_image",args=new Dictionary<string,object>{{"id",entry.id}}};
  var spec=AgentVision.Tools().Single();
  check(AgentRules.ReadTools.Contains("read_image")&&!AgentRules.WriteTools.Contains("read_image"),"read_image is registered as an automatically running read tool");
  bool rejected=false;try{AgentRules.ValidateAction(new AgentAction{tool="read_image",args=new Dictionary<string,object>{{"id",new string('f',32)}}},id=>false);}catch(FormatException){rejected=true;}check(rejected,"read_image rejects out-of-scope ids before any file read");
  var result=spec.Read(lib,state,call,CancellationToken.None).GetAwaiter().GetResult();
  check(Convert.ToString(result["imageId"])==entry.id&&result.ContainsKey("imageStamp"),"an enabled vision model records a scoped image receipt without copying image bytes into the journal");
  var parts=AgentVision.ImageParts(lib,new[]{new AgentCall{id=call.id,tool="read_image",status="done",result=result}});
  check(parts.Count==2&&Library.Json.Serialize(parts).Contains("data:image/jpeg;base64,"),"successful image reads become image_url content parts for the next model request");
  File.SetLastWriteTimeUtc(small,DateTime.UtcNow.AddMinutes(5));
  var staleParts=AgentVision.ImageParts(lib,new[]{new AgentCall{id=call.id,tool="read_image",status="done",result=result}});
  check(staleParts.Count==0,"a changed image file no longer attaches stale visual evidence");
  File.SetLastWriteTimeUtc(small,DateTime.UtcNow);
  var noVision=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="OCR fallback",allowed=new List<string>{entry.id},vision=false}};
  var fallback=spec.Read(lib,noVision,call,CancellationToken.None).GetAwaiter().GetResult();
  check(!fallback.ContainsKey("imageId")&&!String.IsNullOrWhiteSpace(Convert.ToString(fallback["text"])),"with vision disabled read_image returns OCR text or an explicit unavailable message instead of image bytes");
  var other=new Library(Path.Combine(root,"other-library"));other.SetAppMode("smart",true);
  bool missing=false;try{spec.Read(other,noVision,call,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){missing=true;}catch(FormatException){missing=true;}
  check(missing,"images outside the current library or scope are refused");
 }
 static void WriteImage(string path,int width,int height){
  using(var bitmap=new Bitmap(width,height))using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);using(var font=new Font("Arial",Math.Max(8,width/20)))graphics.DrawString("KOK VISION",font,Brushes.Black,10,10);bitmap.Save(path,ImageFormat.Png);}
 }
}
