using System;
using System.IO;

namespace kok {
    public partial class Library {
        public string AppMode {get{lock(gate){object value;return Db.settings.TryGetValue("appMode",out value)?Convert.ToString(value):"";}}}
        public bool SmartNoticeSeen {get{lock(gate){object value;return Db.settings.TryGetValue("smartModeNoticeSeen",out value)&&value is bool&&(bool)value;}}}
        public bool AiEnabled {get{return !IsPrivate&&AppMode=="smart"&&SmartNoticeSeen;}}
        public object ModeState(){return new{mode=AppMode,smartNoticeSeen=SmartNoticeSeen};}
        public void SetAppMode(string mode,bool acknowledged){
            if(IsPrivate&&mode!="pure")throw new InvalidOperationException("私密文件夹不启用 AI，请先返回普通资料库。");
            if(mode!="pure"&&mode!="smart")throw new InvalidDataException("请选择纯净模式或智能模式。");
            lock(gate){if(mode=="smart"&&!SmartNoticeSeen&&!acknowledged)throw new InvalidDataException("请先阅读智能模式的大模型提示。");Db.settings["appMode"]=mode;if(mode=="smart"&&acknowledged)Db.settings["smartModeNoticeSeen"]=true;Save();}
        }
        public void RequireAiMode(){if(IsPrivate)throw new InvalidOperationException("私密文件夹不启用 AI，请先返回普通资料库。");if(!AiEnabled)throw new InvalidOperationException("纯净模式未启用 AI，请先在设置中切换至智能模式。");}
    }
}
