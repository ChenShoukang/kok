using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace kok {
    public partial class MainWindow {
        Panel windowChrome;
        ChromeButton minimizeWindow,maximizeWindow,closeWindow;
        ToolTip chromeHints;
        bool customChrome;
        Color chromeColor=Color.FromArgb(248,249,251);
        int ChromeHeight {get{return mediaFullScreen?0:(int)Math.Round(30*DeviceDpi/96.0);}}
        int ChromeEdge {get{return mediaFullScreen||WindowState==FormWindowState.Maximized?0:Math.Max(4,(int)Math.Round(5*DeviceDpi/96.0));}}
        [StructLayout(LayoutKind.Sequential)] struct ChromeRect {public int left,top,right,bottom;}
        [StructLayout(LayoutKind.Sequential)] struct ChromeMonitor {public int size;public ChromeRect monitor,work;public uint flags;}
        [StructLayout(LayoutKind.Sequential)] struct ChromeMargins {public int left,right,top,bottom;}
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref ChromeRect rect,uint flags);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref ChromeMonitor info);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr window,ref ChromeMargins margins);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        void InitializeWindowChrome(){
            customChrome=true;string theme=Convert.ToString(library.Db.settings.ContainsKey("theme")?library.Db.settings["theme"]:"light");bool dark=theme=="dark";
            if(theme=="system")try{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))dark=key!=null&&Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0;}catch{}
            Color initial=dark?Color.FromArgb(33,33,33):Color.White;object enabled,color;if(library.Db.settings.TryGetValue("customColorsEnabled",out enabled)&&enabled is bool&&(bool)enabled&&library.Db.settings.TryGetValue(dark?"customDarkBg":"customLightBg",out color)){Color parsed;if(TryChromeColor(Convert.ToString(color),out parsed))initial=parsed;}
            windowChrome=new Panel{BackColor=initial,TabStop=false};chromeHints=new ToolTip();
            minimizeWindow=new ChromeButton("minimize");maximizeWindow=new ChromeButton("maximize");closeWindow=new ChromeButton("close");
            minimizeWindow.Click+=delegate{WindowState=FormWindowState.Minimized;};maximizeWindow.Click+=delegate{ToggleChromeMaximize();};closeWindow.Click+=delegate{Close();};
            windowChrome.Controls.AddRange(new Control[]{minimizeWindow,maximizeWindow,closeWindow});windowChrome.MouseDown+=DragChrome;
            web.Dock=DockStyle.None;Controls.Add(windowChrome);windowChrome.BringToFront();
            ApplyWindowAppearance(initial,false);Resize+=delegate{LayoutWindowChrome();};DpiChanged+=delegate{LayoutWindowChrome();};
            FormClosed+=delegate{chromeHints.Dispose();};LayoutWindowChrome();if(IsHandleCreated)SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x0027);
        }
        static bool TryChromeColor(string value,out Color color){color=Color.White;if(!Regex.IsMatch(value??"","^#[0-9a-fA-F]{6}$"))return false;color=ColorTranslator.FromHtml(value);return true;}
        void ApplyWindowAppearance(Color color,bool reduceMotion){
            chromeColor=color;BackColor=color;if(windowChrome==null)return;windowChrome.BackColor=color;
            Color foreground=color.R*.2126+color.G*.7152+color.B*.0722>145?Color.FromArgb(35,35,35):Color.FromArgb(235,235,235);
            foreach(var button in new[]{minimizeWindow,maximizeWindow,closeWindow}){button.BackColor=color;button.ForeColor=foreground;button.ReducedMotion=reduceMotion;button.Invalidate();}
            minimizeWindow.AccessibleName=L("最小化");maximizeWindow.AccessibleName=L(WindowState==FormWindowState.Maximized?"还原窗口":"最大化");closeWindow.AccessibleName=L("关闭窗口");
            foreach(var button in new[]{minimizeWindow,maximizeWindow,closeWindow})chromeHints.SetToolTip(button,button.AccessibleName);
            if(web.CoreWebView2!=null)web.DefaultBackgroundColor=color;
            if(IsHandleCreated)try{int dark=foreground.R>128?1:0;DwmSetWindowAttribute(Handle,20,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        }
        void ToggleChromeMaximize(){if(mediaFullScreen)return;WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
        void DragChrome(object sender,MouseEventArgs e){
            if(e.Button==MouseButtons.Left){if(e.Clicks==2){ToggleChromeMaximize();return;}ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(WindowState==FormWindowState.Normal&&e.Y<ChromeEdge?12:2),IntPtr.Zero);}
            else if(e.Button==MouseButtons.Right)SendMessage(Handle,0x112,new IntPtr(0xF100),new IntPtr(32));
        }
        void LayoutWindowChrome(){
            if(windowChrome==null||IsDisposed)return;windowChrome.Visible=!mediaFullScreen;int height=ChromeHeight,edge=ChromeEdge,width=(int)Math.Round(46*DeviceDpi/96.0);
            windowChrome.SetBounds(0,0,ClientSize.Width,height);closeWindow.SetBounds(Math.Max(0,ClientSize.Width-width),0,width,height);maximizeWindow.SetBounds(Math.Max(0,ClientSize.Width-width*2),0,width,height);minimizeWindow.SetBounds(Math.Max(0,ClientSize.Width-width*3),0,width,height);
            maximizeWindow.Restored=WindowState==FormWindowState.Maximized;maximizeWindow.Invalidate();maximizeWindow.AccessibleName=L(maximizeWindow.Restored?"还原窗口":"最大化");chromeHints.SetToolTip(maximizeWindow,maximizeWindow.AccessibleName);
            var content=new Rectangle(edge,height,Math.Max(0,ClientSize.Width-edge*2),Math.Max(0,ClientSize.Height-height-edge));web.Bounds=content;
            if(engineErrorPanel!=null){engineErrorPanel.Dock=DockStyle.None;engineErrorPanel.Bounds=content;}windowChrome.BringToFront();
        }
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);if(customChrome&&!mediaFullScreen)try{var margins=new ChromeMargins{left=1,right=1,top=1,bottom=1};DwmExtendFrameIntoClientArea(Handle,ref margins);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){} }
        protected override void WndProc(ref Message message){
            if(customChrome&&!mediaFullScreen){
                if(message.Msg==0x83&&message.WParam!=IntPtr.Zero){
                    if(IsZoomed(Handle)){var rect=(ChromeRect)Marshal.PtrToStructure(message.LParam,typeof(ChromeRect));var info=new ChromeMonitor{size=Marshal.SizeOf(typeof(ChromeMonitor))};if(GetMonitorInfo(MonitorFromRect(ref rect,2),ref info))Marshal.StructureToPtr(info.work,message.LParam,false);}
                    message.Result=IntPtr.Zero;return;
                }
                if(message.Msg==0x84&&WindowState==FormWindowState.Normal){long packed=message.LParam.ToInt64();var point=PointToClient(new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff)));int edge=ChromeEdge;bool left=point.X<edge,right=point.X>=ClientSize.Width-edge,top=point.Y<edge,bottom=point.Y>=ClientSize.Height-edge;int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:0;if(hit!=0){message.Result=new IntPtr(hit);return;}}
            }
            base.WndProc(ref message);
        }
        sealed class ChromeButton : Button {
            readonly string kind;readonly System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer{Interval=16};float hover,target;
            public bool Restored,ReducedMotion;
            public ChromeButton(string kind){this.kind=kind;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;TabStop=false;AccessibleRole=AccessibleRole.PushButton;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);animation.Tick+=delegate{hover+=(target-hover)*.32f;if(Math.Abs(target-hover)<.015f){hover=target;animation.Stop();}Invalidate();};}
            protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);MoveHover(1);}
            protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);MoveHover(0);}
            void MoveHover(float value){target=value;if(ReducedMotion){hover=value;Invalidate();}else animation.Start();}
            protected override void OnPaint(PaintEventArgs e){
                var graphics=e.Graphics;graphics.Clear(BackColor);Color tint=kind=="close"?Color.FromArgb(220,52,62):ForeColor;using(var brush=new SolidBrush(Color.FromArgb((int)(hover*(kind=="close"?225:24)),tint)))graphics.FillRectangle(brush,ClientRectangle);
                float scale=DeviceDpi/96f,cx=Width/2f,cy=Height/2f,r=4.5f*scale;Color ink=kind=="close"&&hover>.5?Color.White:ForeColor;graphics.SmoothingMode=SmoothingMode.AntiAlias;
                using(var pen=new Pen(ink,Math.Max(1,scale))){if(kind=="minimize")graphics.DrawLine(pen,cx-r,cy+3*scale,cx+r,cy+3*scale);else if(kind=="close"){graphics.DrawLine(pen,cx-r,cy-r,cx+r,cy+r);graphics.DrawLine(pen,cx+r,cy-r,cx-r,cy+r);}else if(Restored){graphics.DrawLines(pen,new[]{new PointF(cx-r+2*scale,cy-r),new PointF(cx+r,cy-r),new PointF(cx+r,cy+r-2*scale)});graphics.DrawRectangle(pen,cx-r,cy-r+2*scale,r*2-2*scale,r*2-2*scale);}else graphics.DrawRectangle(pen,cx-r,cy-r,r*2,r*2);}
            }
            protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
        }
    }
}
