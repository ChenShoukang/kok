using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace kok {
    // Native PDF page rasterization through the vendored, checksum-pinned PDFium DLL.
    // Only pages are rendered; the PDF file itself is never modified.
    internal static class PdfPage {
        internal const int MaxPages = 10000;
        internal const long MaxFileBytes = 100L * 1024 * 1024;
        internal const int MaxRenderPixels = 40000000;
        const int Bgra = 4;
        const int AnnotFlags = 0x01;
        static readonly object gate = new object();
        static bool initialized;

        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern void FPDF_InitLibrary();
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern void FPDF_DestroyLibrary();
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr FPDF_LoadMemDocument(IntPtr data, int size, [MarshalAs(UnmanagedType.LPStr)] string password);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern int FPDF_GetLastError();
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern int FPDF_GetPageCount(IntPtr document);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern double FPDF_GetPageWidthF(IntPtr page);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern double FPDF_GetPageHeightF(IntPtr page);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern int FPDFBitmap_GetStride(IntPtr bitmap);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern void FPDFBitmap_Destroy(IntPtr bitmap);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern void FPDF_ClosePage(IntPtr page);
        [DllImport("pdfium.dll", CallingConvention = CallingConvention.Cdecl)] static extern void FPDF_CloseDocument(IntPtr document);

        internal static bool Available {
            get {
                try { lock (gate) { Ensure(); return true; } }
                catch (DllNotFoundException) { return false; }
                catch (EntryPointNotFoundException) { return false; }
            }
        }
        internal static int PageCount(string path) { lock (gate) { Ensure(); return WithDocument(path, document => FPDF_GetPageCount(document)); } }

        internal static byte[] RenderJpeg(string path, int pageNumber, out int pageCount) {
            lock (gate) {
                Ensure();
                pageCount = 0;int count = 0;
                try {
                    byte[] rendered = WithDocument(path, document => {
                        count = FPDF_GetPageCount(document);
                        if (count < 1 || count > MaxPages) throw new InvalidDataException("The PDF page count is outside the supported range.");
                        if (pageNumber < 1 || pageNumber > count) throw new InvalidDataException("The PDF page number is outside the document.");
                        IntPtr page = FPDF_LoadPage(document, pageNumber - 1);
                        if (page == IntPtr.Zero) throw LastError("The PDF page could not be loaded.");
                        try {
                            double width = FPDF_GetPageWidthF(page), height = FPDF_GetPageHeightF(page);
                            if (!(width > 0) || !(height > 0)) throw new InvalidDataException("The PDF page has invalid dimensions.");
                            double scale = Math.Min(2d, Math.Min(AgentVision.MaxDimension / width, AgentVision.MaxDimension / height));
                            int pixelsWide = Math.Max(1, (int)Math.Round(width * scale)), pixelsHigh = Math.Max(1, (int)Math.Round(height * scale));
                            if ((long)pixelsWide * pixelsHigh > MaxRenderPixels || (long)pixelsWide * pixelsHigh > AgentVision.MaxPixels) throw new InvalidDataException("The PDF page renders to an image that is too large.");
                            byte[] pixels = new byte[pixelsWide * pixelsHigh * 4];
                            GCHandle pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                            IntPtr bitmap = IntPtr.Zero;
                            try {
                                bitmap = FPDFBitmap_CreateEx(pixelsWide, pixelsHigh, Bgra, pinned.AddrOfPinnedObject(), pixelsWide * 4);
                                if (bitmap == IntPtr.Zero) throw new InvalidDataException("PDF page rendering is unavailable.");
                                FPDFBitmap_FillRect(bitmap, 0, 0, pixelsWide, pixelsHigh, 0xFFFFFFFF);
                                FPDF_RenderPageBitmap(bitmap, page, 0, 0, pixelsWide, pixelsHigh, 0, AnnotFlags);
                                using (var source = new Bitmap(pixelsWide, pixelsHigh, PixelFormat.Format32bppRgb)) {
                                    var target = source.LockBits(new Rectangle(0, 0, pixelsWide, pixelsHigh), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                                    try { Marshal.Copy(pixels, 0, target.Scan0, pixels.Length); } finally { source.UnlockBits(target); }
                                    return AgentVision.EncodeImageJpeg(source);
                                }
                            } finally {
                                if (bitmap != IntPtr.Zero) FPDFBitmap_Destroy(bitmap);
                                pinned.Free();
                            }
                        } finally { FPDF_ClosePage(page); }
                    });
                    pageCount = count;
                    return rendered;
                } catch (DllNotFoundException) { throw new InvalidDataException("PDF page rendering is unavailable in this build."); }
                catch (EntryPointNotFoundException) { throw new InvalidDataException("PDF page rendering is unavailable in this build."); }
            }
        }

        static T WithDocument<T>(string path, Func<IntPtr, T> action) {
            var info = new FileInfo(path);
            if (!info.Exists) throw new InvalidDataException("The PDF file is missing.");
            if (info.Length < 8 || info.Length > MaxFileBytes) throw new InvalidDataException("The PDF file is outside the supported size range.");
            byte[] bytes = File.ReadAllBytes(path);
            GCHandle pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            IntPtr document = IntPtr.Zero;
            try {
                document = FPDF_LoadMemDocument(pinned.AddrOfPinnedObject(), bytes.Length, null);
                if (document == IntPtr.Zero) throw LastError("The PDF file could not be opened.");
                return action(document);
            } finally {
                if (document != IntPtr.Zero) FPDF_CloseDocument(document);
                pinned.Free();
            }
        }

        static InvalidDataException LastError(string fallback) {
            int code = FPDF_GetLastError();
            string reason = code == 2 ? "The file is not a PDF." : code == 3 ? "The PDF format is invalid." : code == 4 ? "The PDF is password protected." : code == 5 ? "The PDF uses unsupported security." : code == 6 ? "The PDF page is invalid." : "";
            return new InvalidDataException(reason.Length > 0 ? reason : fallback);
        }

        static void Ensure() {
            if (!initialized) { FPDF_InitLibrary(); initialized = true; }
        }
    }
}
