using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace kok {
    // Declarative extension packages (v1): a plugin directory with plugin.json may ship Agent-Skills
    // SKILL.md trees and prompt templates. Nothing inside a plugin is executed; unknown fields (scripts,
    // exec, main, entry, extensions, hooks, ...) are ignored as inert data.
    public sealed class KokPlugin { public string name,version="",description="",path; public List<string> skills=new List<string>(),prompts=new List<string>(); }
    public sealed class KokPrompt { public string name,description,template,path,plugin; }
    public static class AgentPlugins {
        public const int MaxPlugins=32,MaxManifestBytes=64*1024,MaxPrompts=32,MaxTemplate=8000,MaxName=64,MaxVersion=32,MaxDescription=200,MaxPaths=16;
        static readonly object gate=new object();
        static List<KokPlugin> cache;static string cacheKey,revision="";
        // Tests only: replaces the profile directory passed to Discover so temp plugins are deterministic.
        internal static string UserRootOverride="";
        public static string Revision {get{lock(gate)return revision;}}
        public static void Reload(){lock(gate){cache=null;cacheKey=null;revision="";}}
        public static List<KokPlugin> Discover(string appDir,string userDir,string libraryDir){
            string user=String.IsNullOrWhiteSpace(UserRootOverride)?userDir:UserRootOverride;
            var roots=new List<KeyValuePair<string,string>>();
            if(!String.IsNullOrWhiteSpace(appDir))roots.Add(new KeyValuePair<string,string>("app",Path.Combine(appDir,"ui","plugins")));
            if(!String.IsNullOrWhiteSpace(user))roots.Add(new KeyValuePair<string,string>("user",Path.Combine(user,".kok","plugins")));
            if(!String.IsNullOrWhiteSpace(libraryDir))roots.Add(new KeyValuePair<string,string>("library",Path.Combine(libraryDir,".kok","plugins")));
            string key=String.Join("|",roots.Select(root=>root.Key+"="+root.Value));
            var found=new List<string>();var snapshot=new List<ManifestFile>();
            foreach(var root in roots)ScanRoot(root.Value,found,snapshot);
            string next=RevisionOf(roots,snapshot);
            lock(gate){
                if(cache!=null&&cacheKey==key&&revision==next)return new List<KokPlugin>(cache);
                var parsed=new List<KokPlugin>();
                foreach(var manifest in found){
                    if(parsed.Count>=MaxPlugins)break;
                    var plugin=Parse(manifest);
                    if(plugin==null||parsed.Any(existing=>existing.name==plugin.name))continue;
                    parsed.Add(plugin);
                }
                cache=parsed;cacheKey=key;revision=next;
                return new List<KokPlugin>(parsed);
            }
        }
        public static List<string> SkillRoots(List<KokPlugin> plugins){
            var roots=new List<string>();if(plugins==null)return roots;
            foreach(var plugin in plugins){
                if(plugin==null||plugin.skills==null)continue;
                foreach(var dir in plugin.skills){
                    if(String.IsNullOrWhiteSpace(dir)||!Directory.Exists(dir))continue;
                    if(roots.Any(existing=>String.Equals(existing,dir,StringComparison.OrdinalIgnoreCase)))continue;
                    roots.Add(dir);
                }
            }
            return roots;
        }
        public static List<KokPrompt> Prompts(List<KokPlugin> plugins){
            var prompts=new List<KokPrompt>();if(plugins==null)return prompts;
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var plugin in plugins){
                if(plugin==null||plugin.prompts==null)continue;
                foreach(var dir in plugin.prompts){
                    string[] files;try{if(!Directory.Exists(dir))continue;files=Directory.GetFiles(dir,"*.md");}catch{continue;}
                    Array.Sort(files,StringComparer.OrdinalIgnoreCase);
                    foreach(var file in files){
                        if(prompts.Count>=MaxPrompts)return prompts;
                        var prompt=ParsePrompt(plugin,file);
                        if(prompt==null||!names.Add(prompt.name))continue;
                        prompts.Add(prompt);
                    }
                }
            }
            return prompts;
        }
        // UI helper: replaces the case-insensitive $ARGUMENTS placeholder, or appends the arguments after a
        // blank line when the template has no placeholder. Never sends anything; purely textual.
        public static string ApplyArguments(string template,string arguments){
            template=template??"";arguments=arguments??"";
            if(template.IndexOf("$ARGUMENTS",StringComparison.OrdinalIgnoreCase)>=0)return Regex.Replace(template,@"\$ARGUMENTS",_=>arguments,RegexOptions.IgnoreCase);
            return arguments.Length==0?template:template+"\n\n"+arguments;
        }

        sealed class ManifestFile {public string path;public long length,modified;}
        static void ScanRoot(string root,List<string> found,List<ManifestFile> snapshot){
            if(String.IsNullOrWhiteSpace(root)||!Directory.Exists(root))return;
            string[] first;try{first=Directory.GetDirectories(root);}catch{return;}
            Array.Sort(first,StringComparer.OrdinalIgnoreCase);
            foreach(var dir in first){
                if(IsSkipped(Path.GetFileName(dir)))continue;
                AddManifest(dir,found,snapshot);
                string[] second;try{second=Directory.GetDirectories(dir);}catch{continue;}
                Array.Sort(second,StringComparer.OrdinalIgnoreCase);
                foreach(var nested in second){if(IsSkipped(Path.GetFileName(nested)))continue;AddManifest(nested,found,snapshot);}
            }
        }
        static bool IsSkipped(string name){return name.StartsWith(".",StringComparison.Ordinal)||String.Equals(name,"node_modules",StringComparison.OrdinalIgnoreCase);}
        static void AddManifest(string dir,List<string> found,List<ManifestFile> snapshot){
            string file=Path.Combine(dir,"plugin.json");
            try{if(!File.Exists(file))return;var info=new FileInfo(file);snapshot.Add(new ManifestFile{path=file,length=info.Length,modified=info.LastWriteTimeUtc.Ticks});found.Add(file);}catch{}
        }
        static string RevisionOf(List<KeyValuePair<string,string>> roots,List<ManifestFile> files){
            var text=new StringBuilder();
            foreach(var root in roots)text.Append(root.Key).Append('=').Append(root.Value).Append('\n');
            foreach(var file in files.OrderBy(item=>item.path,StringComparer.OrdinalIgnoreCase))text.Append(file.path).Append('|').Append(file.length).Append('|').Append(file.modified).Append('\n');
            using(var sha=SHA256.Create())return String.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(value=>value.ToString("x2")));
        }
        static KokPlugin Parse(string manifest){
            try{
                var info=new FileInfo(manifest);if(info.Length>MaxManifestBytes)return null;
                string text=File.ReadAllText(manifest,Encoding.UTF8);
                var value=Library.Json.DeserializeObject(text) as Dictionary<string,object>;
                if(value==null)return null;
                string dir=Path.GetFullPath(Path.GetDirectoryName(manifest));
                string name=Value(value,"name");
                if(String.IsNullOrEmpty(name)||name.Length>MaxName||!Regex.IsMatch(name,"^[a-z0-9]+(-[a-z0-9]+)*$"))return null;
                string version=Value(value,"version")??"";if(version.Length>MaxVersion)return null;
                string description=Value(value,"description")??"";if(description.Length>MaxDescription)return null;
                return new KokPlugin{name=name,version=version,description=description,path=dir,skills=Paths(value,"skills",dir),prompts=Paths(value,"prompts",dir)};
            }catch{return null;}
        }
        static string Value(Dictionary<string,object> value,string key){object raw;return value!=null&&value.TryGetValue(key,out raw)&&raw!=null?Convert.ToString(raw):null;}
        static List<string> Paths(Dictionary<string,object> value,string key,string pluginDir){
            var result=new List<string>();
            object raw;if(value==null||!value.TryGetValue(key,out raw)||raw==null)return result;
            var items=raw as object[];if(items==null)return result;
            foreach(var item in items){
                if(result.Count>=MaxPaths)break;
                string relative=Convert.ToString(item);if(String.IsNullOrWhiteSpace(relative))continue;
                string full;try{full=Path.GetFullPath(Path.Combine(pluginDir,relative.Trim())).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}catch{continue;}
                if(!Contains(pluginDir,full))continue;// reject absolute paths, ".." traversal and other escapes
                if(result.Any(existing=>String.Equals(existing,full,StringComparison.OrdinalIgnoreCase)))continue;
                result.Add(full);
            }
            return result;
        }
        static bool Contains(string parent,string child){
            string root=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string target=Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar);
            return target.StartsWith(root,StringComparison.OrdinalIgnoreCase)||String.Equals(root.TrimEnd(Path.DirectorySeparatorChar),target,StringComparison.OrdinalIgnoreCase);
        }
        static KokPrompt ParsePrompt(KokPlugin plugin,string file){
            try{
                var info=new FileInfo(file);if(info.Length>MaxTemplate*4)return null;
                string text=File.ReadAllText(file,Encoding.UTF8);
                if(text.Length>0&&text[0]=='\uFEFF')text=text.Substring(1);
                text=text.Replace("\r\n","\n").Replace('\r','\n');
                string name=Path.GetFileNameWithoutExtension(file),description="",body=text;
                if(text.StartsWith("---\n",StringComparison.Ordinal)){
                    int end=text.IndexOf("\n---",4,StringComparison.Ordinal);
                    if(end>0){
                        string front=text.Substring(4,end-4);body=text.Substring(end+4);
                        if(body.StartsWith("\n",StringComparison.Ordinal))body=body.Substring(1);
                        foreach(var raw in front.Split('\n')){
                            string line=raw.Trim();if(line.Length==0||line.StartsWith("#",StringComparison.Ordinal))continue;
                            int colon=line.IndexOf(':');if(colon<=0)continue;
                            string key=line.Substring(0,colon).Trim(),setting=line.Substring(colon+1).Trim();
                            if(setting.Length>=2&&(setting[0]=='"'||setting[0]=='\'')&&setting[setting.Length-1]==setting[0])setting=setting.Substring(1,setting.Length-2);
                            if(key=="name")name=setting;else if(key=="description")description=setting;
                        }
                    }
                }
                if(name.Length==0||name.Length>MaxName||!Regex.IsMatch(name,"^[a-z0-9-]{1,64}$"))return null;
                if(description.Length>MaxDescription)return null;
                body=body.Trim();
                if(description.Length==0){
                    string title=body.Split('\n').FirstOrDefault(line=>line.Trim().Length>0)??"";
                    if(title.StartsWith("# ",StringComparison.Ordinal))description=title.Substring(2).Trim();
                }
                if(body.Length>MaxTemplate)return null;
                return new KokPrompt{name=name,description=description,template=body,path=Path.GetFullPath(file),plugin=plugin.name};
            }catch{return null;}
        }
    }
}
