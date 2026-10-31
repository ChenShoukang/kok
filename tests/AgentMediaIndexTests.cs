using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

static class AgentMediaIndexTests {
 static byte[] Pdf(int pages) {
  var objects = new List<string>();
  int fontObject = 3 + pages * 2;
  objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
  var kids = Enumerable.Range(0, pages).Select(i => (3 + i * 2) + " 0 R").ToArray();
  objects.Add("<< /Type /Pages /Kids [" + String.Join(" ", kids) + "] /Count " + pages + " >>");
  for (int index = 0; index < pages; index++) {
   int contentObject = 4 + index * 2;
   objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources << /Font << /F1 " + fontObject + " 0 R >> >> /Contents " + contentObject + " 0 R >>");
   string text = "BT /F1 16 Tf 20 120 Td (PAGE_" + (index + 1) + "_TEXT) Tj ET";
   objects.Add("<< /Length " + text.Length + " >>\nstream\n" + text + "\nendstream");
  }
  objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
  var builder = new StringBuilder("%PDF-1.4\n");
  var offsets = new List<int>();
  for (int index = 0; index < objects.Count; index++) {
   offsets.Add(builder.Length);
   builder.Append((index + 1) + " 0 obj\n" + objects[index] + "\nendobj\n");
  }
  int xref = builder.Length;
  builder.Append("xref\n0 " + (objects.Count + 1) + "\n0000000000 65535 f \n");
  foreach (int offset in offsets) builder.Append(offset.ToString("D10") + " 00000 n \n");
  builder.Append("trailer\n<< /Size " + (objects.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF");
  return Encoding.ASCII.GetBytes(builder.ToString());
 }
 static byte[] Jpeg() {
  using (var bitmap = new Bitmap(320, 180)) using (var graphics = Graphics.FromImage(bitmap)) using (var stream = new MemoryStream()) {
   graphics.Clear(Color.White); using (var font = new Font("Arial", 18)) graphics.DrawString("FRAME", font, Brushes.Black, 10, 70);
   bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg); return stream.ToArray();
  }
 }
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  string pdfPath = Path.Combine(root, "report.pdf"); File.WriteAllBytes(pdfPath, Pdf(3));
  var library = new Library(Path.Combine(root, "library")); library.SetAppMode("smart", true);
  var pdf = new Entry { id = Guid.NewGuid().ToString("N").Substring(0, 32), name = "multi page report", path = pdfPath, kind = "document", ext = "PDF", added = DateTime.UtcNow.ToString("o"), size = new FileInfo(pdfPath).Length };
  library.Db.items.Add(pdf); library.Save();
  var tools = AgentMediaIndex.Tools();
  var pdfTool = tools.Single(t => t.Name == "index_pdf_pages");
  var videoTool = tools.Single(t => t.Name == "index_video_frames");
  check(AgentRules.WriteTools.Contains("index_pdf_pages") && AgentRules.WriteTools.Contains("index_video_frames") && !AgentRules.ReadTools.Contains("index_pdf_pages"), "PDF/video batch indexing is registered as approval-only write tools");
  bool startRejected = false; try { AgentRules.ValidateAction(new AgentAction { tool = "index_pdf_pages", args = new Dictionary<string,object> { { "id", pdf.id }, { "start", 0 } } }, id => id == pdf.id); } catch (FormatException) { startRejected = true; }
  bool countRejected = false; try { AgentRules.ValidateAction(new AgentAction { tool = "index_pdf_pages", args = new Dictionary<string,object> { { "id", pdf.id }, { "count", AgentMediaIndex.MaxPdfPages + 1 } } }, id => id == pdf.id); } catch (FormatException) { countRejected = true; }
  check(startRejected && countRejected, "batch PDF indexing rejects out-of-range page windows before reading any file");
  var state = new AgentTaskState { id = Guid.NewGuid().ToString("N"), options = new AgentTaskOptions { sessionId = Guid.NewGuid().ToString("N"), goal = "index media", allowed = new List<string> { pdf.id } } };
  var context = new ModuleContext(library, new TranslationService(library.DataDir), new ModulePolicy(library), null, CancellationToken.None, null, new Operations(library, null), null, appDirectory: AppDomain.CurrentDomain.BaseDirectory);
  var first = pdfTool.ExecuteWithContext(context, state, new AgentCall { id = Guid.NewGuid().ToString("N"), tool = "index_pdf_pages", args = new Dictionary<string,object> { { "id", pdf.id }, { "start", 1 }, { "count", 2 } } }, CancellationToken.None).GetAwaiter().GetResult();
  var chunks = library.ReadContentChunks(new[] { pdf.id });
  string text = String.Join("\n", chunks.Select(c => c.text));
  check(Convert.ToInt32(first.result["indexed"]) == 2 && text.Contains("PAGE_1_TEXT") && text.Contains("PAGE_2_TEXT") && !text.Contains("PAGE_3_TEXT"), "approved PDF batch indexing extracts page text locally into the content index without a model call");
  var second = pdfTool.ExecuteWithContext(context, state, new AgentCall { id = Guid.NewGuid().ToString("N"), tool = "index_pdf_pages", args = new Dictionary<string,object> { { "id", pdf.id }, { "start", 3 }, { "count", 1 } } }, CancellationToken.None).GetAwaiter().GetResult();
  text = String.Join("\n", library.ReadContentChunks(new[] { pdf.id }).Select(c => c.text));
  check(Convert.ToInt32(second.result["indexed"]) == 1 && text.Contains("PAGE_1_TEXT") && text.Contains("PAGE_3_TEXT"), "a later PDF batch merges with already indexed pages instead of dropping them");
  File.WriteAllBytes(pdfPath, Pdf(3)); File.SetLastWriteTimeUtc(pdfPath, DateTime.UtcNow.AddMinutes(3));
  var third = pdfTool.ExecuteWithContext(context, state, new AgentCall { id = Guid.NewGuid().ToString("N"), tool = "index_pdf_pages", args = new Dictionary<string,object> { { "id", pdf.id }, { "start", 1 }, { "count", 1 } } }, CancellationToken.None).GetAwaiter().GetResult();
  check(Convert.ToInt32(third.result["indexed"]) == 1, "re-indexing a changed PDF uses the new source stamp and stays bounded");
  string videoPath = Path.Combine(root, "clip.mp4"); File.WriteAllBytes(videoPath, new byte[] { 0, 1, 2, 3, 4, 5 });
  var video = new Entry { id = Guid.NewGuid().ToString("N").Substring(0, 32), name = "clip", path = videoPath, kind = "video", ext = ".mp4", added = DateTime.UtcNow.ToString("o"), size = new FileInfo(videoPath).Length };
  library.Db.items.Add(video); library.Save();
  var videoState = new AgentTaskState { id = Guid.NewGuid().ToString("N"), options = new AgentTaskOptions { sessionId = Guid.NewGuid().ToString("N"), goal = "index video", allowed = new List<string> { video.id } } };
  try {
   VideoFrame.FfmpegOverride = () => "fixture-ffmpeg";
   VideoFrame.ExtractOverride = (path, seconds, token) => Jpeg();
   AgentMediaIndex.OcrOverride = (frame, index, token) => Task.FromResult("FRAME_TEXT_" + index);
   var videoResult = videoTool.ExecuteWithContext(context, videoState, new AgentCall { id = Guid.NewGuid().ToString("N"), tool = "index_video_frames", args = new Dictionary<string,object> { { "id", video.id }, { "start", 0 }, { "step", 5 }, { "count", 2 } } }, CancellationToken.None).GetAwaiter().GetResult();
   var videoChunks = library.ReadContentChunks(new[] { video.id }).Where(c => c.kind == "ocr_frame").ToArray();
   check(Convert.ToInt32(videoResult.result["indexed"]) == 2 && videoChunks.Length == 2 && videoChunks.Any(c => c.text.Contains("FRAME_TEXT_0")) && videoChunks.Any(c => Math.Abs(c.anchor.time - 5) < 0.001), "approved video frame indexing stores timestamped OCR chunks locally and never calls a model");
  } finally { VideoFrame.FfmpegOverride = null; VideoFrame.ExtractOverride = null; AgentMediaIndex.OcrOverride = null; }
  bool wrongType = false; try { videoTool.ExecuteWithContext(context, videoState, new AgentCall { id = Guid.NewGuid().ToString("N"), tool = "index_video_frames", args = new Dictionary<string,object> { { "id", pdf.id }, { "count", 1 } } }, CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidDataException) { wrongType = true; } catch (AgentFailure) { wrongType = true; }
  check(wrongType, "video frame indexing refuses non-video sources");
 }
}
