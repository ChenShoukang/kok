using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    // Approval-gated batch indexing for local PDF pages (PDFium text layer) and video frames
    // (FFmpeg extraction + Windows OCR when available). Indexing is local-only, never calls a model,
    // is bounded by page/frame counts and invalidates through the existing content stamp.
    internal static class AgentMediaIndex {
        internal const int MaxPdfPages = 25;
        internal const int MaxVideoFrames = 12;
        internal const int MaxVideoStepSeconds = 600;
        internal static Func<byte[],int,CancellationToken,Task<string>> OcrOverride;

        internal static AgentToolSpec[] Tools() {
            return new[] {
                new AgentToolSpec {
                    Name = "index_pdf_pages", Title = "批量索引 PDF 页", Requires = Need.NormalLibrary, Write = true, CapabilityId = "agent.index_pdf_pages",
                    Signature = "index_pdf_pages{id,start?,count?}",
                    Description = "PROPOSE indexing the text layer of up to 25 pages from one PDF in scope into the local content index. The user approves it. Uses the local PDFium text layer, never calls a model and never modifies the PDF. Already indexed pages are merged.",
                    Properties = AgentRules.Props(
                        "id", AgentRules.Id,
                        "start", new Dictionary<string,object> { { "type", "integer" }, { "minimum", 1 }, { "maximum", PdfPage.MaxPages } },
                        "count", new Dictionary<string,object> { { "type", "integer" }, { "minimum", 1 }, { "maximum", MaxPdfPages } }),
                    Required = new[] { "id" },
                    Validate = (action, exists) => {
                        string id = Arg(action, "id");
                        if (id.Length != 32 || !AgentJournal.ValidId(id) || !exists(id)) throw new FormatException("id must be one PDF in the allowed scope");
                        int start = IntArg(action, "start", 1), count = IntArg(action, "count", 10);
                        if (start < 1 || start > PdfPage.MaxPages || count < 1 || count > MaxPdfPages) throw new FormatException("start/count are outside the PDF indexing bounds");
                    },
                    ExecuteWithContext = (context, state, call, token) => IndexPdf(context, state, call, token)
                },
                new AgentToolSpec {
                    Name = "index_video_frames", Title = "批量索引视频帧", Requires = Need.NormalLibrary, Write = true, CapabilityId = "agent.index_video_frames",
                    Signature = "index_video_frames{id,start?,step?,count?}",
                    Description = "PROPOSE extracting up to 12 frames from one video in scope with the optional FFmpeg component and indexing their Windows OCR text into the local content index. The user approves it. Never calls a model and never modifies the video. Missing OCR/FFmpeg is reported explicitly.",
                    Properties = AgentRules.Props(
                        "id", AgentRules.Id,
                        "start", new Dictionary<string,object> { { "type", "integer" }, { "minimum", 0 }, { "maximum", 86400 } },
                        "step", new Dictionary<string,object> { { "type", "integer" }, { "minimum", 1 }, { "maximum", MaxVideoStepSeconds } },
                        "count", new Dictionary<string,object> { { "type", "integer" }, { "minimum", 1 }, { "maximum", MaxVideoFrames } }),
                    Required = new[] { "id" },
                    Validate = (action, exists) => {
                        string id = Arg(action, "id");
                        if (id.Length != 32 || !AgentJournal.ValidId(id) || !exists(id)) throw new FormatException("id must be one video in the allowed scope");
                        int start = IntArg(action, "start", 0), step = IntArg(action, "step", 10), count = IntArg(action, "count", 6);
                        if (start < 0 || start > 86400 || step < 1 || step > MaxVideoStepSeconds || count < 1 || count > MaxVideoFrames) throw new FormatException("start/step/count are outside the video frame indexing bounds");
                    },
                    ExecuteWithContext = (context, state, call, token) => IndexVideo(context, state, call, token)
                }
            };
        }

        static string Arg(AgentAction action, string name) { object value; return action.args != null && action.args.TryGetValue(name, out value) && value != null ? Convert.ToString(value).Trim() : ""; }
        static int IntArg(AgentAction action, string name, int fallback) { object value; if (action.args == null || !action.args.TryGetValue(name, out value) || value == null) return fallback; try { return Convert.ToInt32(value); } catch { throw new FormatException(name + " must be an integer"); } }
        static int CallInt(AgentCall call, string name, int fallback) { object value; if (call.args == null || !call.args.TryGetValue(name, out value) || value == null) return fallback; return Convert.ToInt32(value); }
        static string CallText(AgentCall call, string name) { object value; return call.args != null && call.args.TryGetValue(name, out value) && value != null ? Convert.ToString(value).Trim() : ""; }

        static Entry Scoped(ModuleContext context, AgentTaskState state, string id) {
            context.Policy.Ensure(Need.NormalLibrary, "agent");
            var entry = context.Library.Find(id);
            if (entry == null || state == null || state.options == null || !state.options.allowed.Contains(id)) throw new InvalidDataException("That source is not in the allowed scope.");
            return entry;
        }

        static Task<AgentToolReceipt> IndexPdf(ModuleContext context, AgentTaskState state, AgentCall call, CancellationToken token) {
            return Task.Run(() => {
                token.ThrowIfCancellationRequested();
                var library = context.Library;
                string id = CallText(call, "id");
                var entry = Scoped(context, state, id);
                if (entry.ext != "PDF") throw new InvalidDataException("index_pdf_pages only accepts PDF sources.");
                if (!PdfPage.Available) throw new InvalidDataException("PDF page text extraction is unavailable in this build.");
                int start = CallInt(call, "start", 1), count = CallInt(call, "count", 10);
                if (start < 1 || count < 1 || count > MaxPdfPages) throw new InvalidDataException("PDF indexing bounds are invalid.");
                string stamp = library.ContentStamp(id);
                if (stamp.Length == 0) throw new InvalidDataException("The PDF source is missing.");
                var added = new List<ContentPage>();
                int totalPages = 0;
                for (int page = start; page < start + count; page++) {
                    token.ThrowIfCancellationRequested();
                    string text = PdfPage.PageText(entry.path, page, out totalPages);
                    if (page > totalPages) break;
                    if (!String.IsNullOrWhiteSpace(text)) added.Add(new ContentPage { page = page, text = text, method = "text" });
                }
                if (added.Count == 0) return new AgentToolReceipt { result = new Dictionary<string,object> { { "text", "No text layer was found on the requested PDF pages; nothing was indexed." }, { "indexed", 0 }, { "totalPages", totalPages } } };
                var merged = Merge(library, id, added, page => page.page);
                var status = library.IndexContentSource(id, merged, stamp);
                return new AgentToolReceipt { result = new Dictionary<string,object> { { "text", "Indexed " + added.Count + " PDF page(s) locally after approval; no model was called and the PDF was not modified." }, { "indexed", added.Count }, { "totalPages", totalPages }, { "engine", "pdfium-text" }, { "chunks", status.chunks } } };
            });
        }

        static async Task<AgentToolReceipt> IndexVideo(ModuleContext context, AgentTaskState state, AgentCall call, CancellationToken token) {
            var library = context.Library;
            string id = CallText(call, "id");
            var entry = Scoped(context, state, id);
            if (!VideoFrame.IsVideo(entry)) throw new InvalidDataException("index_video_frames only accepts video sources.");
            int start = CallInt(call, "start", 0), step = CallInt(call, "step", 10), count = CallInt(call, "count", 6);
            if (start < 0 || step < 1 || step > MaxVideoStepSeconds || count < 1 || count > MaxVideoFrames) throw new InvalidDataException("Video frame indexing bounds are invalid.");
            string ffmpeg = VideoFrame.ResolveFfmpeg(library);
            if (String.IsNullOrEmpty(ffmpeg)) throw new InvalidDataException("Video frame indexing needs the optional FFmpeg media component; install it first.");
            string stamp = library.ContentStamp(id);
            if (stamp.Length == 0) throw new InvalidDataException("The video source is missing.");
            var added = new List<ContentPage>();
            int frameIndex = 0;
            for (int index = 0; index < count; index++) {
                token.ThrowIfCancellationRequested();
                int time = start + index * step;
                if (time > 86400) break;
                byte[] frame = VideoFrame.Extract(entry.path, time, ffmpeg, token);
                string text;
                if (OcrOverride != null) text = await OcrOverride(frame, frameIndex, token);
                else {
                    var ocr = await LocalOcr.Read(frame, context.AppDirectory, Path.Combine(library.DataDir, "ocr-work"), token);
                    text = ocr != null && ocr.ContainsKey("text") ? Convert.ToString(ocr["text"]) : "";
                }
                frameIndex++;
                if (!String.IsNullOrWhiteSpace(text)) added.Add(new ContentPage { time = time, text = text.Trim(), method = "ocr" });
            }
            if (added.Count == 0) return new AgentToolReceipt { result = new Dictionary<string,object> { { "text", "OCR found no text in the requested video frames; nothing was indexed." }, { "indexed", 0 }, { "frames", frameIndex } } };
            var merged = Merge(library, id, added, page => page.time);
            var status = library.IndexContentSource(id, merged, stamp);
            return new AgentToolReceipt { result = new Dictionary<string,object> { { "text", "Indexed OCR text from " + added.Count + " video frame(s) locally after approval; no model was called and the video was not modified." }, { "indexed", added.Count }, { "frames", frameIndex }, { "engine", "windows-ocr" }, { "chunks", status.chunks } } };
        }

        // Merge newly indexed pages/frames with existing page or frame chunks, keyed by page number or timestamp.
        static List<ContentPage> Merge(Library library, string id, List<ContentPage> added, Func<ContentPage,double> key) {
            var rows = new Dictionary<double, ContentPage>();
            foreach (var chunk in library.ReadContentChunks(new[] { id })) {
                if (chunk.kind != "page" && chunk.kind != "ocr_page" && chunk.kind != "ocr_frame") continue;
                double at = chunk.kind == "ocr_frame" ? (chunk.anchor == null ? 0 : chunk.anchor.time) : (chunk.anchor == null ? 0 : chunk.anchor.page);
                ContentPage page;
                if (!rows.TryGetValue(at, out page)) { page = new ContentPage { page = chunk.anchor == null ? 0 : chunk.anchor.page, time = chunk.anchor == null ? 0 : chunk.anchor.time, method = chunk.kind == "ocr_frame" ? "ocr" : "text" }; rows[at] = page; }
                page.text = (page.text ?? "") + "\n" + chunk.text;
            }
            foreach (var page in added) rows[key(page)] = page;
            return rows.Values.OrderBy(page => key(page)).Take(2000).ToList();
        }
    }
}
