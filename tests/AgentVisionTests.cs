using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
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
  string pdfPath=Path.Combine(root,"report.pdf");File.WriteAllBytes(pdfPath,PdfFixture(1));
  var pdfEntry=new Entry{id=Guid.NewGuid().ToString("N").Substring(0,32),name="illustrated report",path=pdfPath,kind="document",ext="PDF",added=DateTime.UtcNow.ToString("o"),size=new FileInfo(pdfPath).Length};lib.Db.items.Add(pdfEntry);lib.Save();
  var pdfState=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="Read a PDF page",allowed=new List<string>{pdfEntry.id},vision=true}};
  var pdfCall=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_image",args=new Dictionary<string,object>{{"id",pdfEntry.id},{"page",1}}};
  bool pageRejected=false;try{AgentRules.ValidateAction(new AgentAction{tool="read_image",args=new Dictionary<string,object>{{"id",pdfEntry.id},{"page",0}}},id=>id==pdfEntry.id);}catch(FormatException){pageRejected=true;}check(pageRejected,"read_image rejects non-positive PDF page numbers before rendering");
  bool mixedRejected=false;try{AgentRules.ValidateAction(new AgentAction{tool="read_image",args=new Dictionary<string,object>{{"id",pdfEntry.id},{"page",1},{"time",3}}},id=>id==pdfEntry.id);}catch(FormatException){mixedRejected=true;}check(mixedRejected,"read_image rejects asking for a PDF page and a video frame at the same time");
  var pdfResult=spec.Read(lib,pdfState,pdfCall,CancellationToken.None).GetAwaiter().GetResult();
  check(Convert.ToString(pdfResult["imageKind"])=="pdf"&&Convert.ToInt32(pdfResult["imagePage"])==1&&Convert.ToString(pdfResult["imageFrame"]).Length==32,"read_image rasterizes one bounded PDF page with the vendored PDFium renderer");
  byte[] sealedFrame=File.ReadAllBytes(Path.Combine(lib.DataDir,"vision-frames",Convert.ToString(pdfResult["imageFrame"])+".sealed"));
  check(sealedFrame.Length>2&&sealedFrame[0]!=0xFF&&sealedFrame[1]!=0xD8,"rendered PDF frames are stored as DPAPI-encrypted cache files, not loose JPEGs");
  var pdfParts=AgentVision.ImageParts(lib,new[]{new AgentCall{id=pdfCall.id,tool="read_image",status="done",result=pdfResult}});
  check(pdfParts.Count==2&&Library.Json.Serialize(pdfParts).Contains("data:image/jpeg;base64,")&&!Library.Json.Serialize(pdfResult).Contains("base64"),"PDF frames become image_url content parts without copying frame bytes into the task journal");
  File.SetLastWriteTimeUtc(pdfPath,DateTime.UtcNow.AddMinutes(6));
  var stalePdf=AgentVision.ImageParts(lib,new[]{new AgentCall{id=pdfCall.id,tool="read_image",status="done",result=pdfResult}});
  check(stalePdf.Count==0,"a changed PDF no longer attaches its cached page frame");
  bool pageOnImage=false;try{spec.Read(lib,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_image",args=new Dictionary<string,object>{{"id",entry.id},{"page",1}}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){pageOnImage=true;}check(pageOnImage,"PDF page numbers cannot be applied to ordinary images");
  bool outOfRange=false;try{spec.Read(lib,pdfState,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_image",args=new Dictionary<string,object>{{"id",pdfEntry.id},{"page",5}}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){outOfRange=true;}check(outOfRange,"PDF page numbers outside the document are refused");
  string videoPath=Path.Combine(root,"clip.mp4");File.WriteAllBytes(videoPath,new byte[]{0,1,2,3,4,5,6,7});
  var videoEntry=new Entry{id=Guid.NewGuid().ToString("N").Substring(0,32),name="clip",path=videoPath,kind="video",ext=".mp4",added=DateTime.UtcNow.ToString("o"),size=new FileInfo(videoPath).Length};lib.Db.items.Add(videoEntry);lib.Save();
  var videoState=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="Read a video frame",allowed=new List<string>{videoEntry.id},vision=true}};
  try{
   VideoFrame.ExtractOverride=(path,seconds,token)=>{using(var bitmap=new Bitmap(320,180))using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);using(var font=new Font("Arial",20))graphics.DrawString("KOK FRAME "+seconds,font,Brushes.Black,10,70);return AgentVision.EncodeImageJpeg(bitmap);}};
   var videoResult=spec.Read(lib,videoState,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_image",args=new Dictionary<string,object>{{"id",videoEntry.id},{"time",7}}},CancellationToken.None).GetAwaiter().GetResult();
   check(Convert.ToString(videoResult["imageKind"])=="video"&&Convert.ToInt32(videoResult["imageTime"])==7&&Convert.ToString(videoResult["imageFrame"]).Length==32,"read_image extracts one video frame at the requested timestamp and caches it like a PDF page");
   var videoParts=AgentVision.ImageParts(lib,new[]{new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_image",status="done",result=videoResult}});
   check(videoParts.Count==2&&Library.Json.Serialize(videoParts).Contains("data:image/jpeg;base64,"),"video frames become image_url content parts for the next model request");
  }finally{VideoFrame.ExtractOverride=null;}
  VideoFrame.FfmpegOverride=()=>null;
  try{bool missingFfmpeg=false;try{spec.Read(lib,videoState,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_image",args=new Dictionary<string,object>{{"id",videoEntry.id},{"time",7}}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException error){missingFfmpeg=error.Message.Contains("FFmpeg");}check(missingFfmpeg,"video frames report the optional FFmpeg requirement when the media component is not installed");}
  finally{VideoFrame.FfmpegOverride=null;}
 }
 static byte[] PdfFixture(int pages){
  var builder=new StringBuilder();var offsets=new List<int>();
  builder.Append("%PDF-1.4\n");
  void Object(int number,string body){offsets.Add(builder.Length);builder.Append(number+" 0 obj\n"+body+"\nendobj\n");}
  Object(1,"<< /Type /Catalog /Pages 2 0 R >>");
  Object(2,"<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
  Object(3,"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>");
  string stream="BT /F1 24 Tf 30 100 Td (KOK PDF VISION) Tj ET";
  Object(4,"<< /Length "+stream.Length+" >>\nstream\n"+stream+"\nendstream");
  Object(5,"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
  int xref=builder.Length;builder.Append("xref\n0 6\n0000000000 65535 f \n");
  foreach(int offset in offsets)builder.Append(offset.ToString("D10")+" 00000 n \n");
  builder.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF");
  return Encoding.ASCII.GetBytes(builder.ToString());
 }
 static void WriteImage(string path,int width,int height){
  using(var bitmap=new Bitmap(width,height))using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);using(var font=new Font("Arial",Math.Max(8,width/20)))graphics.DrawString("KOK VISION",font,Brushes.Black,10,10);bitmap.Save(path,ImageFormat.Png);}
 }
}
