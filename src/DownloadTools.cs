using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace kok {
    public class DownloadComponent {public string version,url,sha256,executableSha256,filename,archiveRoot,probeSha256;public long size;}
    public class DownloadComponents {public DownloadComponent ffmpeg;}
    public class ComponentProgress {public string component,status="idle";public long received,total;}
    public sealed class DownloadTools {
        readonly string root;
        readonly DownloadComponents components;
        readonly object gate=new object();
        readonly Dictionary<string,string> verified=new Dictionary<string,string>();
        public DownloadTools(string dataDir,string appDir){root=Path.Combine(dataDir,"download-components");components=Library.Json.Deserialize<DownloadComponents>(File.ReadAllText(Path.Combine(appDir,"ui","download-components.json"),Encoding.UTF8));ValidateComponent(components.ffmpeg,"GyanD/codexffmpeg","ffmpeg.exe");foreach(string legacy in new[]{"yt-dlp.exe","deno.exe"}){string file=Path.Combine(root,legacy);try{if(File.Exists(file))File.Delete(file);}catch(IOException){}catch(UnauthorizedAccessException){}}}
        static void ValidateComponent(DownloadComponent c,string repository,string filename){
            Uri uri;if(c==null||c.filename!=filename||!Uri.TryCreate(c.url,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/"+repository+"/releases/download/",StringComparison.Ordinal)||!System.Text.RegularExpressions.Regex.IsMatch(c.sha256??"","^[a-f0-9]{64}$")||c.size<1||c.size>150*1024*1024)throw new InvalidDataException("下载组件清单无效。");
            if(!System.Text.RegularExpressions.Regex.IsMatch(c.executableSha256??"","^[a-f0-9]{64}$"))throw new InvalidDataException("下载组件清单无效。");
            if(filename=="ffmpeg.exe"&&(!System.Text.RegularExpressions.Regex.IsMatch(c.probeSha256??"","^[a-f0-9]{64}$")||!System.Text.RegularExpressions.Regex.IsMatch(c.archiveRoot??"","^ffmpeg-[0-9.]+-essentials_build$")))throw new InvalidDataException("下载组件清单无效。");
        }
        public static string HashFile(string file){using(var hash=SHA256.Create())using(var input=File.OpenRead(file))return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
        string Trusted(DownloadComponent component,bool probe=false){
            string file=Path.Combine(root,probe?"ffprobe.exe":component.filename);if(!File.Exists(file))return null;
            var info=new FileInfo(file);string stamp=info.Length+"/"+info.LastWriteTimeUtc.Ticks;
            lock(gate){string previous;if(verified.TryGetValue(file,out previous)&&previous==stamp)return file;}
            if(HashFile(file)!=(probe?component.probeSha256:component.executableSha256??component.sha256))return null;
            lock(gate)verified[file]=stamp;return file;
        }
        public string Ffmpeg {get{return Trusted(components.ffmpeg);}}
        public string Ffprobe {get{return Trusted(components.ffmpeg,true);}}
        public object Status(){return new{ffmpegInstalled=Ffmpeg!=null&&Ffprobe!=null,ffmpegVersion=components.ffmpeg.version,ffmpegBytes=components.ffmpeg.size};}
        static void Extract(ZipArchive archive,string name,string target,long limit){var entry=archive.GetEntry(name);if(entry==null||entry.Length>limit)throw new InvalidDataException("下载组件清单无效。");using(var input=entry.Open())using(var output=File.Create(target))input.CopyTo(output);}
        public async Task Install(Action<ComponentProgress> progress,CancellationToken token,string bundle="ffmpeg"){
            if(bundle!="ffmpeg")throw new InvalidDataException("请选择有效的下载组件。");
            Directory.CreateDirectory(root);
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token))using(var client=new HttpClient()){deadline.CancelAfter(TimeSpan.FromMinutes(8));token=deadline.Token;client.Timeout=TimeSpan.FromMinutes(8);client.DefaultRequestHeaders.UserAgent.ParseAdd("kok/0.6.2");
                foreach(var component in new[]{components.ffmpeg}){
                    token.ThrowIfCancellationRequested();if(Trusted(component)!=null&&Ffprobe!=null)continue;
                    progress(new ComponentProgress{component="FFmpeg / FFprobe",status="downloading",total=component.size});
                    string temporary=Path.Combine(root,Guid.NewGuid().ToString("N")+".download"),executable=Path.Combine(root,Guid.NewGuid().ToString("N")+".verified"),probe=Path.Combine(root,Guid.NewGuid().ToString("N")+".verified");
                    try{
                        using(var response=await client.GetAsync(component.url,HttpCompletionOption.ResponseHeadersRead,token)){
                            response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>150*1024*1024)throw new InvalidDataException("下载组件过大。");
                            using(token.Register(()=>response.Dispose()))using(var input=await response.Content.ReadAsStreamAsync())using(var output=File.Create(temporary)){var buffer=new byte[65536];long total=0,last=0;int read;while((read=await input.ReadAsync(buffer,0,buffer.Length,token))>0){total+=read;if(total>150*1024*1024)throw new InvalidDataException("下载组件过大。");await output.WriteAsync(buffer,0,read,token);if(total-last>256*1024){last=total;progress(new ComponentProgress{component="FFmpeg / FFprobe",status="downloading",received=total,total=component.size});}}}
                        }
                        token.ThrowIfCancellationRequested();if(HashFile(temporary)!=component.sha256)throw new InvalidDataException("下载组件校验失败，请重新安装。");
                        {
                            progress(new ComponentProgress{component="FFmpeg / FFprobe",status="verifying",received=component.size,total=component.size});
                            using(var archive=ZipFile.OpenRead(temporary)){
                                Extract(archive,component.archiveRoot+"/bin/ffmpeg.exe",executable,250*1024*1024);
                                Extract(archive,component.archiveRoot+"/bin/ffprobe.exe",probe,250*1024*1024);if(HashFile(probe)!=component.probeSha256)throw new InvalidDataException("下载组件校验失败，请重新安装。");Extract(archive,component.archiveRoot+"/LICENSE",Path.Combine(root,"FFmpeg-LICENSE.txt"),1024*1024);Extract(archive,component.archiveRoot+"/README.txt",Path.Combine(root,"FFmpeg-README.txt"),1024*1024);
                            }
                            if(HashFile(executable)!=component.executableSha256)throw new InvalidDataException("下载组件校验失败，请重新安装。");
                        }
                        token.ThrowIfCancellationRequested();string target=Path.Combine(root,component.filename);if(File.Exists(target))File.Replace(executable,target,null);else File.Move(executable,target);
                        {string probeTarget=Path.Combine(root,"ffprobe.exe");if(File.Exists(probeTarget))File.Replace(probe,probeTarget,null);else File.Move(probe,probeTarget);lock(gate)verified.Remove(probeTarget);}
                        lock(gate)verified.Remove(target);progress(new ComponentProgress{component=component.filename,status="installed",received=component.size,total=component.size});
                    }catch(Exception){token.ThrowIfCancellationRequested();throw;}finally{if(File.Exists(temporary))File.Delete(temporary);if(File.Exists(executable))File.Delete(executable);if(File.Exists(probe))File.Delete(probe);}
                }
            }
        }
        // CommandLineToArgvW-compatible quoting; no command shell is involved.
        public static string QuoteArgument(string value){
            var result=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){result.Append('\\',slashes*2+1).Append(c);slashes=0;continue;}result.Append('\\',slashes).Append(c);slashes=0;}return result.Append('\\',slashes*2).Append('"').ToString();
        }
        public static async Task<int> Run(string executable,IEnumerable<string> args,Action<string> stdout,Action<string> stderr,CancellationToken token,string workingDirectory=null){
            token.ThrowIfCancellationRequested();
            var start=new ProcessStartInfo(executable,String.Join(" ",args.Select(QuoteArgument))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=workingDirectory??Path.GetDirectoryName(executable)};
            using(var process=new Process{StartInfo=start})using(var children=new DownloadProcessGroup()){
                process.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null&&e.Data.Length<=8*1024*1024)stdout(e.Data);};
                process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)stderr(e.Data);};
                process.Start();children.Attach(process);
                using(token.Register(delegate{children.Stop();try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}})){
                    process.BeginOutputReadLine();process.BeginErrorReadLine();await Task.Run(()=>process.WaitForExit());token.ThrowIfCancellationRequested();return process.ExitCode;
                }
            }
        }
    }
    // Closing this handle terminates the media process and its children.
    sealed class DownloadProcessGroup : IDisposable {
        IntPtr handle;
        [StructLayout(LayoutKind.Sequential)] struct BasicLimits {public long processTime,jobTime;public uint flags;public UIntPtr minimum,maximum;public uint active;public UIntPtr affinity;public uint priority,scheduling;}
        [StructLayout(LayoutKind.Sequential)] struct IoCounters {public ulong readOps,writeOps,otherOps,readBytes,writeBytes,otherBytes;}
        [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits {public BasicLimits basic;public IoCounters io;public UIntPtr processMemory,jobMemory,peakProcess,peakJob;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attributes,string name);
        [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job,int kind,ref ExtendedLimits info,uint size);
        [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
        [DllImport("kernel32.dll")] static extern bool TerminateJobObject(IntPtr job,uint exitCode);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        public DownloadProcessGroup(){handle=CreateJobObject(IntPtr.Zero,null);var limits=new ExtendedLimits();limits.basic.flags=0x2000;if(handle!=IntPtr.Zero&&!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits)))){CloseHandle(handle);handle=IntPtr.Zero;}}
        public void Attach(Process process){if(handle!=IntPtr.Zero&&!AssignProcessToJobObject(handle,process.Handle)){CloseHandle(handle);handle=IntPtr.Zero;}}
        public void Stop(){if(handle!=IntPtr.Zero)TerminateJobObject(handle,1);}
        public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
    }
}
