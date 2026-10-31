using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace kok {
 // .NET Framework's redirected stdin writes the console encoding's preamble on Start.
 // Own the byte pipes so a UTF-8 console can never prepend a BOM to JSON-RPC.
 // Inherit only the three stdio handles; attach the suspended child to its lifetime job.
 internal sealed class McpPipeProcess:IDisposable {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Startup {
   public int size;public string reserved,desktop,title;public uint x,y,width,height,charsX,charsY,fill,flags;public ushort show,reservedSize;public IntPtr reservedBytes,input,output,error;
  }
  [StructLayout(LayoutKind.Sequential)] struct StartupEx {public Startup startup;public IntPtr attributes;}
  [StructLayout(LayoutKind.Sequential)] struct ProcessInfo {public IntPtr process,thread;public uint id,threadId;}
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref IntPtr size);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returned);
  [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateProcess(string application,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string directory,ref StartupEx startup,out ProcessInfo result);
  [DllImport("kernel32.dll",SetLastError=true)] static extern uint ResumeThread(IntPtr thread);
  [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr process,uint code);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
  readonly AnonymousPipeServerStream inputPipe=new AnonymousPipeServerStream(PipeDirection.Out,HandleInheritability.Inheritable),outputPipe=new AnonymousPipeServerStream(PipeDirection.In,HandleInheritability.Inheritable),errorPipe=new AnonymousPipeServerStream(PipeDirection.In,HandleInheritability.Inheritable);
  McpProcessGroup group;int disposed;
  internal Process Process;internal StreamWriter Input;internal StreamReader Output,Error;
  static Exception Failed(){return new AgentFailure("mcp_start","MCP 标准输入输出进程启动失败，Windows 错误码 "+Marshal.GetLastWin32Error()+"。");}
  internal McpPipeProcess(ProcessStartInfo start){
   IntPtr attributes=IntPtr.Zero,handles=IntPtr.Zero,environment=IntPtr.Zero,size=IntPtr.Zero;bool initialized=false,ready=false;var created=new ProcessInfo();
   try{
    InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);if(size==IntPtr.Zero)throw Failed();attributes=Marshal.AllocHGlobal(size);if(!InitializeProcThreadAttributeList(attributes,1,0,ref size))throw Failed();initialized=true;
    handles=Marshal.AllocHGlobal(IntPtr.Size*3);Marshal.WriteIntPtr(handles,0,inputPipe.ClientSafePipeHandle.DangerousGetHandle());Marshal.WriteIntPtr(handles,IntPtr.Size,outputPipe.ClientSafePipeHandle.DangerousGetHandle());Marshal.WriteIntPtr(handles,IntPtr.Size*2,errorPipe.ClientSafePipeHandle.DangerousGetHandle());
    if(!UpdateProcThreadAttribute(attributes,0,new IntPtr(0x20002),handles,new IntPtr(IntPtr.Size*3),IntPtr.Zero,IntPtr.Zero))throw Failed();
    string block=String.Join("\0",start.EnvironmentVariables.Keys.Cast<string>().OrderBy(k=>k,StringComparer.OrdinalIgnoreCase).Select(k=>k+"="+start.EnvironmentVariables[k]))+"\0\0";environment=Marshal.StringToHGlobalUni(block);
    var startup=new StartupEx{startup=new Startup{size=Marshal.SizeOf(typeof(StartupEx)),flags=0x101,show=0,input=inputPipe.ClientSafePipeHandle.DangerousGetHandle(),output=outputPipe.ClientSafePipeHandle.DangerousGetHandle(),error=errorPipe.ClientSafePipeHandle.DangerousGetHandle()},attributes=attributes};
    var command=new StringBuilder(McpStdioClient.Quote(start.FileName)+" "+start.Arguments);if(!CreateProcess(start.FileName,command,IntPtr.Zero,IntPtr.Zero,true,0x08000000|0x00080000|0x00000400|0x00000004,environment,start.WorkingDirectory,ref startup,out created))throw Failed();
    Process=Process.GetProcessById((int)created.id);group=new McpProcessGroup(Process);
    inputPipe.DisposeLocalCopyOfClientHandle();outputPipe.DisposeLocalCopyOfClientHandle();errorPipe.DisposeLocalCopyOfClientHandle();
    Input=new StreamWriter(inputPipe,new UTF8Encoding(false,true)){AutoFlush=true};Output=new StreamReader(outputPipe,new UTF8Encoding(false,true),false);Error=new StreamReader(errorPipe,new UTF8Encoding(false,true),false);
    if(ResumeThread(created.thread)==UInt32.MaxValue)throw Failed();ready=true;
   }finally{
    if(!ready){if(created.process!=IntPtr.Zero)TerminateProcess(created.process,1);Dispose();}
    if(created.thread!=IntPtr.Zero)CloseHandle(created.thread);if(created.process!=IntPtr.Zero)CloseHandle(created.process);
    if(initialized)DeleteProcThreadAttributeList(attributes);if(attributes!=IntPtr.Zero)Marshal.FreeHGlobal(attributes);if(handles!=IntPtr.Zero)Marshal.FreeHGlobal(handles);if(environment!=IntPtr.Zero)Marshal.FreeHGlobal(environment);
   }
  }
  public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;try{Input?.Dispose();}catch{}try{if(Process!=null&&!Process.HasExited&&!Process.WaitForExit(150))Process.Kill();}catch{}group?.Dispose();try{Output?.Dispose();Error?.Dispose();}catch{}inputPipe.Dispose();outputPipe.Dispose();errorPipe.Dispose();Process?.Dispose();}
 }
}
