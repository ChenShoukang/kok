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
    [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr h,uint command);
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
    static void Main(string[] args){try{
        try{SetProcessDpiAwarenessContext(new IntPtr(-4));}catch(EntryPointNotFoundException){}
        int pid=Int32.Parse(args[0]);var target=new IntPtr(Int64.Parse(args[1]));uint owner;GetWindowThreadProcessId(target,out owner);
        var process=Process.GetProcessById(pid);if(owner!=(uint)pid||!String.Equals(Path.GetFullPath(process.MainModule.FileName),Path.GetFullPath(args[2]),StringComparison.OrdinalIgnoreCase))throw new Exception("Refusing a window outside the isolated test process");
        string action=args[3];
        if(action=="click")SendMessage(target,0xF5,IntPtr.Zero,IntPtr.Zero);
        else if(action=="double")SendMessage(target,0x203,new IntPtr(1),new IntPtr((15<<16)|200));
        else if(action=="restore")SendMessage(target,0x112,new IntPtr(0xF120),IntPtr.Zero);
        else if(action=="activate"){SetWindowPos(target,IntPtr.Zero,0,0,0,0,3);SetForegroundWindow(target);}
        else if(action=="resize"){Rect r;GetWindowRect(target,out r);SetWindowPos(target,IntPtr.Zero,r.left,r.top,Int32.Parse(args[4]),Int32.Parse(args[5]),0x14);}
        else if(action=="gesture-start")SendMessage(target,0x231,IntPtr.Zero,IntPtr.Zero);
        else if(action=="gesture-end")SendMessage(target,0x232,IntPtr.Zero,IntPtr.Zero);
        else if(action=="gesture-cancel")SendMessage(target,0x1f,IntPtr.Zero,IntPtr.Zero);
        else if(action=="move-burst"||action=="resize-burst"||action=="resize-cancel"){
            Rect initial;GetWindowRect(target,out initial);SendMessage(target,0x231,IntPtr.Zero,IntPtr.Zero);
            try{for(int n=0;n<48;n++){double t=(1-Math.Cos(n*Math.PI/8))/2;int x=initial.left,y=initial.top,w=initial.right-initial.left,h=initial.bottom-initial.top;if(action=="move-burst"){x+=(int)(Math.Sin(n*.5)*100);y+=(int)(Math.Cos(n*.5)*50);}else{w=620+(int)(t*1000);h=480+(int)(t*480);}SetWindowPos(target,IntPtr.Zero,x,y,w,h,0x14);System.Threading.Thread.Sleep(16);}}
            finally{SendMessage(target,action=="resize-cancel"?0x1fu:0x232u,IntPtr.Zero,IntPtr.Zero);}
        }
        else if(action=="maximize-burst"){for(int n=0;n<8;n++){SendMessage(target,0x112,new IntPtr(n%2==0?0xF030:0xF120),IntPtr.Zero);System.Threading.Thread.Sleep(90);}}
        else if(action=="move"){Rect r;GetWindowRect(target,out r);SetWindowPos(target,IntPtr.Zero,Int32.Parse(args[4]),Int32.Parse(args[5]),r.right-r.left,r.bottom-r.top,0x14);}
        else if(action=="above"){var other=new IntPtr(Int64.Parse(args[4]));uint otherOwner;GetWindowThreadProcessId(other,out otherOwner);if(otherOwner!=(uint)pid)throw new Exception("Unowned comparison window");bool found=false;for(IntPtr w=GetWindow(target,2);w!=IntPtr.Zero;w=GetWindow(w,2))if(w==other){found=true;break;}Console.WriteLine(found);}
        else if(action=="hit"){Rect r;GetWindowRect(target,out r);int x=r.left+Int32.Parse(args[4]),y=r.top+Int32.Parse(args[5]);Console.WriteLine(SendMessage(target,0x84,IntPtr.Zero,new IntPtr((y<<16)|(x&65535))).ToInt64());}
        else throw new Exception("Unknown isolated test action");
    }catch(Exception error){Console.Error.WriteLine(error.Message);Environment.ExitCode=1;}}
}
