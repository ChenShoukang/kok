using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    // One video frame at a requested timestamp, extracted by the optional, checksum-pinned FFmpeg
    // component. FFmpeg is not shipped in the ZIP; when it is unavailable the tool reports a clear
    // install path instead of guessing. The frame is decoded and re-encoded before use.
    internal static class VideoFrame {
        internal const int MaxOutputBytes = 24 * 1024 * 1024;
        internal const int MaxSeconds = 86400;
        internal static readonly string[] VideoExtensions = { ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi", ".wmv", ".ts", ".mpg", ".mpeg", ".flv" };
        internal static Func<string> FfmpegOverride;
        internal static Func<string,double,CancellationToken,byte[]> ExtractOverride;

        internal static bool IsVideo(Entry entry) {
            if (entry == null) return false;
            if (entry.kind == "video") return true;
            string ext = (entry.ext ?? "").Trim().ToLowerInvariant();
            if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;
            return VideoExtensions.Contains(ext);
        }

        internal static string ResolveFfmpeg(Library library) {
            if (FfmpegOverride != null) return FfmpegOverride();
            try {
                var tools = new DownloadTools(library.DataDir, AppDomain.CurrentDomain.BaseDirectory);
                return tools.Ffmpeg;
            } catch { return null; }
        }

        internal static byte[] Extract(string path, double seconds, string ffmpeg, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if (ExtractOverride != null) return ExtractOverride(path, seconds, token);
            if (String.IsNullOrEmpty(ffmpeg) || !File.Exists(ffmpeg)) throw new InvalidDataException("Video frames need the optional FFmpeg media component; install it first.");
            if (seconds < 0 || seconds > MaxSeconds) throw new InvalidDataException("The frame time is outside the supported range.");
            var info = new FileInfo(path);
            if (!info.Exists) throw new InvalidDataException("The video file is missing.");
            if (info.Length < 1 || info.Length > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("The video file is outside the supported size range.");
            var start = new ProcessStartInfo {
                FileName = ffmpeg,
                Arguments = "-nostdin -v error -ss " + seconds.ToString("0.###", CultureInfo.InvariantCulture) + " -i " + Quote(path) + " -frames:v 1 -f image2pipe -vcodec mjpeg -",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(path))
            };
            foreach (var key in start.EnvironmentVariables.Keys.Cast<string>().Where(k => k.StartsWith("KOK_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.EnvironmentVariables.Remove(key);
            using (var group = new DownloadProcessGroup())
            using (var process = new Process { StartInfo = start }) {
                try { process.Start(); } catch (Exception error) { throw new InvalidDataException("The video frame extractor could not start: " + error.Message); }
                group.Attach(process);
                var stdout = ReadBinaryAsync(process.StandardOutput.BaseStream, MaxOutputBytes);
                var stderr = ReadTextAsync(process.StandardError, 8192);
                var timer = Stopwatch.StartNew();
                while (!process.WaitForExit(100)) {
                    token.ThrowIfCancellationRequested();
                    if (timer.Elapsed.TotalSeconds >= 30) { try { group.Stop(); } catch { } throw new InvalidDataException("The video frame extractor exceeded 30 seconds and was stopped."); }
                }
                token.ThrowIfCancellationRequested();
                byte[] frame = stdout.GetAwaiter().GetResult();
                string errors = stderr.GetAwaiter().GetResult();
                if (process.ExitCode != 0) throw new InvalidDataException("The video frame extractor failed" + (errors.Length > 0 ? (": " + errors.Substring(0, Math.Min(300, errors.Length))) : "."));
                if (frame.Length < 4 || frame[0] != 0xFF || frame[1] != 0xD8 || frame[frame.Length - 2] != 0xFF || frame[frame.Length - 1] != 0xD9) throw new InvalidDataException("The video frame extractor did not return a valid JPEG.");
                using (var image = Image.FromStream(new MemoryStream(frame))) {
                    if (image.Width < 1 || image.Height < 1 || (long)image.Width * image.Height > AgentVision.MaxPixels) throw new InvalidDataException("The extracted video frame has invalid dimensions.");
                    return AgentVision.EncodeImageJpeg(image);
                }
            }
        }

        static async Task<byte[]> ReadBinaryAsync(Stream source, int limit) {
            using (var memory = new MemoryStream()) {
                var buffer = new byte[16384]; int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                    if (memory.Length + read > limit) throw new InvalidDataException("The extracted frame is larger than 24 MB.");
                    memory.Write(buffer, 0, read);
                }
                return memory.ToArray();
            }
        }
        static async Task<string> ReadTextAsync(StreamReader reader, int limit) {
            var builder = new StringBuilder(); var buffer = new char[2048]; int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0) { if (builder.Length < limit) builder.Append(buffer, 0, Math.Min(read, limit - builder.Length)); }
            return builder.ToString();
        }
        static string Quote(string value) { return "\"" + (value ?? "").Replace("\"", "\\\"") + "\""; }
    }
}
