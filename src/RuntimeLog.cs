using System;
using System.IO;
using System.Text;

namespace kok {
    // Local, bounded diagnostics. Never records file contents, API requests or credentials.
    public static class RuntimeLog {
        static readonly object gate=new object();
        public static string FilePath { get; private set; }
        public static void Configure(string dataDir) { FilePath=Path.Combine(dataDir,"logs","kok.log"); }
        public static void Write(string name,string details) {
            try { lock(gate) {
                if(FilePath==null)return;
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                if(File.Exists(FilePath)&&new FileInfo(FilePath).Length>1024*1024) {
                    string previous=FilePath+".previous";if(File.Exists(previous))File.Delete(previous);File.Move(FilePath,previous);
                }
                string value=(details??"").Replace('\r',' ').Replace('\n',' ');if(value.Length>6000)value=value.Substring(0,6000);
                File.AppendAllText(FilePath,DateTime.UtcNow.ToString("o")+" "+name+" "+value+Environment.NewLine,new UTF8Encoding(false));
            }} catch { /* A read-only disk must not turn a playback error into an application crash. */ }
        }
        public static void Write(string name,Exception error) {
            Write(name,error.GetType().FullName+" HRESULT="+error.HResult.ToString("X8")+" "+error.StackTrace);
        }
    }
}
