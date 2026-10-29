using System;
using System.IO;
using System.Linq;
namespace kok {
    public static class AgentWorkflows {
        public static readonly string[] Names={"organize","compare","reading","slides"};
        public static string Load(string appDir,string name){
            if(String.IsNullOrEmpty(name))return "";if(!Names.Contains(name))throw new InvalidDataException("任务工作流无效。");
            string dir=Path.Combine(appDir,"ui","skills",name),skill=File.ReadAllText(Path.Combine(dir,"SKILL.md")),resource=File.ReadAllText(Path.Combine(dir,"references","output.md"));
            if(skill.Length+resource.Length>7000)throw new InvalidDataException("任务工作流无效。");
            return "[Task workflow: "+name+"]\n"+skill+"\n[Output template and checks]\n"+resource;
        }
    }
}
