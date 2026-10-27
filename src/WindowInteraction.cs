using System;
using System.Drawing;
using System.Windows.Forms;

namespace kok {
    // A move changes screen position; only a size change invalidates document layout.
    sealed class WindowInteraction {
        readonly Form window;
        readonly Action<bool,bool> notify;
        Size startSize;
        internal bool Active {get;private set;}
        internal bool Resizing {get;private set;}
        internal WindowInteraction(Form window,Action<bool,bool> notify){this.window=window;this.notify=notify;}
        internal void Process(int message){
            if(message==0x231){if(Active)return;Active=true;Resizing=false;startSize=window.ClientSize;notify(true,false);}
            else if(message==0x214)MarkResize();
            else if(message==0x232||message==0x1f||message==0x215)End();
        }
        internal void SizeChanged(){if(window.WindowState==FormWindowState.Minimized){End();return;}if(Active&&window.ClientSize!=startSize)MarkResize();}
        void MarkResize(){if(!Active||Resizing)return;Resizing=true;notify(true,true);}
        internal void End(){if(!Active)return;bool resized=Resizing;Active=false;Resizing=false;notify(false,resized);}
    }
}
