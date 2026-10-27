using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

class SessionWindowHelper {
    delegate bool WindowCallback(IntPtr handle,IntPtr arg);
    [DllImport("user32.dll")]static extern bool EnumWindows(WindowCallback callback,IntPtr arg);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr handle,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr handle,StringBuilder name,int length);
    [DllImport("user32.dll")]static extern IntPtr GetDlgItem(IntPtr handle,int id);
    [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr handle,int index);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr handle,uint message,IntPtr w,IntPtr l);
    static int Main(string[] args){try{
        int pid=Int32.Parse(args[0]);var process=Process.GetProcessById(pid);var window=new IntPtr(Int64.Parse(args[1]));uint owner;GetWindowThreadProcessId(window,out owner);
        if(owner!=(uint)pid||!String.Equals(Path.GetFullPath(process.MainModule.FileName),Path.GetFullPath(args[2]),StringComparison.OrdinalIgnoreCase))throw new Exception("Refusing a window outside the isolated test process");
        string action=args[3];
        if(action=="request-close"){PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero);return 0;}
        if(action=="decline-failed-close"){
            var timer=Stopwatch.StartNew();IntPtr dialog=IntPtr.Zero;
            while(timer.ElapsedMilliseconds<6500&&dialog==IntPtr.Zero){EnumWindows((handle,arg)=>{uint candidate;GetWindowThreadProcessId(handle,out candidate);if(candidate!=(uint)pid)return true;var name=new StringBuilder(128);GetClassName(handle,name,name.Capacity);if(name.ToString()=="#32770"&&GetDlgItem(handle,7)!=IntPtr.Zero){dialog=handle;return false;}return true;},IntPtr.Zero);if(dialog==IntPtr.Zero)System.Threading.Thread.Sleep(25);}
            if(dialog==IntPtr.Zero)throw new Exception("No save-failure confirmation appeared within the timeout");
            bool defaultNo=(GetWindowLong(GetDlgItem(dialog,7),-16)&15)==1;
            SendMessage(dialog,0x111,new IntPtr(7),IntPtr.Zero);if(!defaultNo)throw new Exception("Close confirmation did not default to keeping the window open");
            Console.WriteLine("Default No verified; window retained.");return 0;
        }
        if(action=="close-and-wait"){PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero);if(!process.WaitForExit(5000))throw new Exception("A healthy window failed to close after retry");Console.WriteLine("Healthy close completed.");return 0;}
        throw new Exception("Unknown isolated test action");
    }catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}}
}
