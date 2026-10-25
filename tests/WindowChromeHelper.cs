using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
class WindowChromeHelper {
    [StructLayout(LayoutKind.Sequential)]struct Rect{public int left,top,right,bottom;}
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out Rect r);
    [DllImport("user32.dll")]static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
    static void Main(string[] args){try{
        try{SetProcessDpiAwarenessContext(new IntPtr(-4));}catch(EntryPointNotFoundException){}
        int pid=Int32.Parse(args[0]);var target=new IntPtr(Int64.Parse(args[1]));uint owner;GetWindowThreadProcessId(target,out owner);
        var process=Process.GetProcessById(pid);if(owner!=(uint)pid||!String.Equals(Path.GetFullPath(process.MainModule.FileName),Path.GetFullPath(args[2]),StringComparison.OrdinalIgnoreCase))throw new Exception("Refusing a window outside the isolated test process");
        string action=args[3];
        if(action=="click")SendMessage(target,0xF5,IntPtr.Zero,IntPtr.Zero);
        else if(action=="double")SendMessage(target,0x203,new IntPtr(1),new IntPtr((15<<16)|200));
        else if(action=="restore")SendMessage(target,0x112,new IntPtr(0xF120),IntPtr.Zero);
        else if(action=="resize"){Rect r;GetWindowRect(target,out r);SetWindowPos(target,IntPtr.Zero,r.left,r.top,Int32.Parse(args[4]),Int32.Parse(args[5]),0x14);}
        else if(action=="hit"){Rect r;GetWindowRect(target,out r);int x=r.left+Int32.Parse(args[4]),y=r.top+Int32.Parse(args[5]);Console.WriteLine(SendMessage(target,0x84,IntPtr.Zero,new IntPtr((y<<16)|(x&65535))).ToInt64());}
        else throw new Exception("Unknown isolated test action");
    }catch(Exception error){Console.Error.WriteLine(error.Message);Environment.ExitCode=1;}}
}
