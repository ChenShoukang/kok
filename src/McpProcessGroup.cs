using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace kok {
 // A lifetime boundary, not a sandbox: trusted servers still run with the user's permissions.
 // KILL_ON_JOB_CLOSE also terminates child workers when a connection is cancelled or kok exits.
 internal sealed class McpProcessGroup:IDisposable {
  IntPtr job;
  [StructLayout(LayoutKind.Sequential)] struct BasicLimits {public long processTime,jobTime;public uint flags;public UIntPtr minWorkingSet,maxWorkingSet;public uint activeProcesses;public UIntPtr affinity;public uint priority,scheduling;}
  [StructLayout(LayoutKind.Sequential)] struct IoCounters {public ulong readOps,writeOps,otherOps,readBytes,writeBytes,otherBytes;}
  [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits {public BasicLimits basic;public IoCounters io;public UIntPtr processMemory,jobMemory,peakProcessMemory,peakJobMemory;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr attributes,string name);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int infoClass,ref ExtendedLimits limits,uint length);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
  internal McpProcessGroup(Process process){job=CreateJobObject(IntPtr.Zero,null);var limits=new ExtendedLimits{basic=new BasicLimits{flags=0x2000}};if(job==IntPtr.Zero||!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits)))||!AssignProcessToJobObject(job,process.Handle)){Dispose();throw new AgentFailure("mcp_start","无法管理 MCP 子进程生命周期，已取消启动。");}}
  public void Dispose(){var handle=System.Threading.Interlocked.Exchange(ref job,IntPtr.Zero);if(handle!=IntPtr.Zero)CloseHandle(handle);}
 }
}
