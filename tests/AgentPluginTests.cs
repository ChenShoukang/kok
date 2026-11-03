using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using kok;

class AgentPluginTests {
 static bool Rejects(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
 static Dictionary<string,object> Fields(string name,string version="",string description="",object skills=null,object prompts=null){
  var fields=new Dictionary<string,object>{{"name",name},{"version",version},{"description",description}};
  if(skills!=null)fields["skills"]=skills;
  if(prompts!=null)fields["prompts"]=prompts;
  return fields;
 }
 static string Manifest(string pluginsRoot,string folder,Dictionary<string,object> fields){
  string dir=Path.Combine(pluginsRoot,folder);Directory.CreateDirectory(dir);
  File.WriteAllText(Path.Combine(dir,"plugin.json"),Library.Json.Serialize(fields),new UTF8Encoding(false));
  return dir;
 }
 static string RawManifest(string pluginsRoot,string folder,string json){
  string dir=Path.Combine(pluginsRoot,folder);Directory.CreateDirectory(dir);
  File.WriteAllText(Path.Combine(dir,"plugin.json"),json,new UTF8Encoding(false));
  return dir;
 }
 static string WriteSkill(string skillsRoot,string name,string description,string body){
  string dir=Path.Combine(skillsRoot,name);Directory.CreateDirectory(dir);
  File.WriteAllText(Path.Combine(dir,"SKILL.md"),"---\nname: "+name+"\ndescription: "+description+"\n---\n"+body,new UTF8Encoding(false));
  return dir;
 }
 static void WritePrompt(string promptsRoot,string file,string content){
  Directory.CreateDirectory(promptsRoot);File.WriteAllText(Path.Combine(promptsRoot,file),content,new UTF8Encoding(false));
 }
 static Tuple<string,string,string> Roots(string baseDir){
  string app=Path.Combine(baseDir,"app"),user=Path.Combine(baseDir,"user"),library=Path.Combine(baseDir,"library");
  Directory.CreateDirectory(Path.Combine(app,"ui","plugins"));Directory.CreateDirectory(Path.Combine(user,".kok","plugins"));Directory.CreateDirectory(Path.Combine(library,".kok","plugins"));
  return Tuple.Create(app,user,library);
 }
 static string AppPlugins(string app){return Path.Combine(app,"ui","plugins");}
 static string UserPlugins(string user){return Path.Combine(user,".kok","plugins");}
 static string LibraryPlugins(string library){return Path.Combine(library,".kok","plugins");}
 public static void Run(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  AgentPlugins.UserRootOverride="";AgentPlugins.Reload();AgentSkills.Reload();AgentSkills.UserRootOverride="";
  // Manifest parsing, root precedence and first-wins names.
  var main=Roots(Path.Combine(root,"main"));
  string alphaDir=Manifest(AppPlugins(main.Item1),"alpha-pack",Fields("alpha-pack","1.0.0","Alpha pack.",new[]{"skills/"},new[]{"prompts/"}));
  WriteSkill(Path.Combine(alphaDir,"skills"),"alpha-skill","Alpha skill.","Alpha plugin body.");
  WriteSkill(Path.Combine(alphaDir,"skills"),"plugin-only-skill","Plugin only skill.","Plugin only body.");
  WritePrompt(Path.Combine(alphaDir,"prompts"),"write-notes.md","---\nname: write-notes\ndescription: Write notes for the selected material.\n---\n# Write notes\nWrite notes. $ARGUMENTS\n");
  WritePrompt(Path.Combine(alphaDir,"prompts"),"quick.md","# Quick title\nQuick body.");
  Manifest(UserPlugins(main.Item2),"alpha-pack",Fields("alpha-pack","9.9.9","User duplicate that must lose."));
  string betaDir=Manifest(UserPlugins(main.Item2),"beta-pack",Fields("beta-pack","2.0.0","Beta pack.",null,new[]{"prompts/"}));
  WritePrompt(Path.Combine(betaDir,"prompts"),"write-notes.md","# Duplicate prompt\nDuplicate body.");
  Manifest(LibraryPlugins(main.Item3),"gamma-pack",Fields("gamma-pack","","Library pack."));
  var plugins=AgentPlugins.Discover(main.Item1,main.Item2,main.Item3);
  var alpha=plugins.FirstOrDefault(plugin=>plugin.name=="alpha-pack");
  var beta=plugins.FirstOrDefault(plugin=>plugin.name=="beta-pack");
  check(plugins.Count==3&&alpha!=null&&alpha.version=="1.0.0"&&alpha.description=="Alpha pack."&&alpha.path==Path.GetFullPath(alphaDir)&&beta!=null&&beta.path.StartsWith(Path.GetFullPath(UserPlugins(main.Item2)),StringComparison.OrdinalIgnoreCase)&&plugins.Count(plugin=>plugin.name=="alpha-pack")==1,"plugin manifests discover from app, user and library roots with first-root-wins name collisions");
  var skillRoots=AgentPlugins.SkillRoots(plugins);
  var pluginSkills=AgentSkills.Discover(main.Item1,main.Item2,main.Item3,skillRoots);
  var appSkillsRoot=Path.Combine(main.Item1,"ui","skills");
  WriteSkill(appSkillsRoot,"alpha-skill","App shadow.","App body wins.");
  AgentSkills.Reload();
  var shadowed=AgentSkills.Discover(main.Item1,main.Item2,main.Item3,skillRoots);
  check(skillRoots.Count==1&&pluginSkills.Any(skill=>skill.name=="alpha-skill"&&skill.source=="plugin")&&pluginSkills.Any(skill=>skill.name=="plugin-only-skill")&&AgentSkills.IndexXml(pluginSkills).Contains("<name>alpha-skill</name>")&&AgentSkills.Find(shadowed,"alpha-skill").source=="app","plugin skill roots join AgentSkills discovery with app-first collision order");
  string loadWithRoots="",loadWithoutRoots="";
  try{loadWithRoots=AgentSkills.LoadBody(main.Item1,main.Item2,main.Item3,"plugin-only-skill",skillRoots);}catch(InvalidDataException){}
  try{AgentSkills.LoadBody(main.Item1,main.Item2,main.Item3,"plugin-only-skill");}catch(InvalidDataException){loadWithoutRoots="missing";}
  check(loadWithRoots=="Plugin only body."&&loadWithoutRoots=="missing","LoadBody resolves plugin skills only when the plugin skill roots are supplied");
  var prompts=AgentPlugins.Prompts(plugins);
  var writeNotes=prompts.FirstOrDefault(prompt=>prompt.name=="write-notes");
  var quick=prompts.FirstOrDefault(prompt=>prompt.name=="quick");
  check(writeNotes!=null&&writeNotes.plugin=="alpha-pack"&&writeNotes.description=="Write notes for the selected material."&&writeNotes.template.Contains("$ARGUMENTS")&&quick!=null&&quick.description=="Quick title"&&prompts.Count(prompt=>prompt.name=="write-notes")==1,"prompt templates parse frontmatter and fallback title while duplicate names keep the first root");
  check(AgentPlugins.ApplyArguments("Do $ARGUMENTS now.","the work")=="Do the work now."&&AgentPlugins.ApplyArguments("Do $arguments now.","x")=="Do x now."&&AgentPlugins.ApplyArguments("No placeholder.","args")=="No placeholder.\n\nargs"&&AgentPlugins.ApplyArguments("No placeholder.","")=="No placeholder.","the prompt argument helper replaces a case-insensitive placeholder or appends arguments after a blank line");
  // Invalid manifests and containment rules.
  var invalid=Roots(Path.Combine(root,"invalid"));
  Manifest(AppPlugins(invalid.Item1),"bad-name",Fields("Bad Name","1","x"));
  Manifest(AppPlugins(invalid.Item1),"no-name",new Dictionary<string,object>{{"description","missing name"}});
  RawManifest(AppPlugins(invalid.Item1),"bad-json","{not json");
  RawManifest(AppPlugins(invalid.Item1),"huge",new string('x',70*1024));
  string evilDir=Manifest(AppPlugins(invalid.Item1),"evil-pack",Fields("evil-pack","1","Escape attempts.",new[]{"../escape","C:\\Windows","skills/","./skills/"},new[]{"../prompts"}));
  WriteSkill(Path.Combine(evilDir,"skills"),"evil-skill","Evil skill.","body");
  var invalidPlugins=AgentPlugins.Discover(invalid.Item1,invalid.Item2,invalid.Item3);
  var evil=invalidPlugins.FirstOrDefault(plugin=>plugin.name=="evil-pack");
  check(invalidPlugins.Count==1&&evil!=null&&evil.skills.Count==1&&evil.skills[0]==Path.GetFullPath(Path.Combine(evilDir,"skills"))&&evil.prompts.Count==0,"invalid or oversized manifests are skipped and absolute or traversal resource paths are rejected without throwing");
  // Deterministic nested discovery: depth 1 and 2 are plugins, depth 3 is not.
  var nested=Roots(Path.Combine(root,"nested"));
  Manifest(AppPlugins(nested.Item1),"level-one",Fields("level-one"));
  Manifest(AppPlugins(nested.Item1),Path.Combine("level-one","level-two"),Fields("level-two"));
  Manifest(AppPlugins(nested.Item1),Path.Combine("level-one","level-two","level-three"),Fields("level-three"));
  var nestedPlugins=AgentPlugins.Discover(nested.Item1,nested.Item2,nested.Item3);
  check(nestedPlugins.Select(plugin=>plugin.name).OrderBy(name=>name,StringComparer.Ordinal).SequenceEqual(new[]{"level-one","level-two"}),"plugin discovery scans one and two subdirectory levels but not deeper");
  // Caps: 32 plugins, 32 prompts, 64 KB manifests, 8000 char templates.
  var caps=Roots(Path.Combine(root,"caps"));
  for(int n=0;n<40;n++)Manifest(AppPlugins(caps.Item1),"pack-"+n.ToString("D2"),Fields("pack-"+n.ToString("D2")));
  RawManifest(AppPlugins(caps.Item1),"oversized-manifest",new string('y',70*1024));
  var cappedPlugins=AgentPlugins.Discover(caps.Item1,caps.Item2,caps.Item3);
  var promptCaps=Roots(Path.Combine(root,"prompt-caps"));
  string promptPack=Manifest(AppPlugins(promptCaps.Item1),"prompt-pack",Fields("prompt-pack","1.0.0","Many prompts.",null,new[]{"prompts/"}));
  string promptDir=Path.Combine(promptPack,"prompts");
  for(int n=0;n<40;n++)WritePrompt(promptDir,"template-"+n.ToString("D2")+".md","# Template "+n+"\nBody "+n);
  WritePrompt(promptDir,"too-large.md","# Large\n"+new string('x',9000));
  var cappedPrompts=AgentPlugins.Prompts(AgentPlugins.Discover(promptCaps.Item1,promptCaps.Item2,promptCaps.Item3));
  check(cappedPlugins.Count==AgentPlugins.MaxPlugins&&cappedPrompts.Count==AgentPlugins.MaxPrompts&&cappedPrompts.All(prompt=>prompt.template.Length<=AgentPlugins.MaxTemplate)&&cappedPrompts.All(prompt=>prompt.name!="too-large"),"discovery caps plugin and prompt counts and drops oversized manifests and templates");
  // Unknown declarative fields are inert data and never executed.
  var inert=Roots(Path.Combine(root,"inert"));
  Manifest(AppPlugins(inert.Item1),"inert-pack",new Dictionary<string,object>{{"name","inert-pack"},{"version","1.0.0"},{"scripts",new[]{"evil.ps1"}},{"exec","rm -rf /"},{"main","evil.js"},{"entry","evil"},{ "extensions",new[]{".exe"} },{"hooks",new[]{"onStart"}}});
  var inertPlugins=AgentPlugins.Discover(inert.Item1,inert.Item2,inert.Item3);
  check(inertPlugins.Count==1&&inertPlugins[0].name=="inert-pack"&&inertPlugins[0].skills.Count==0&&inertPlugins[0].prompts.Count==0,"unknown script/exec/entry fields are ignored as inert data and never executed");
  // Reload picks up a new plugin and revision changes; missing roots never throw.
  int before=AgentPlugins.Discover(main.Item1,main.Item2,main.Item3).Count;
  string firstRevision=AgentPlugins.Revision;
  string deltaDir=Manifest(AppPlugins(main.Item1),"delta-pack",Fields("delta-pack","3.0.0","Newly added.",new[]{"skills/"}));
  WriteSkill(Path.Combine(deltaDir,"skills"),"delta-skill","Delta skill.","Delta body.");
  var detected=AgentPlugins.Discover(main.Item1,main.Item2,main.Item3);
  AgentPlugins.Reload();AgentSkills.Reload();
  var reloaded=AgentPlugins.Discover(main.Item1,main.Item2,main.Item3);
  bool revisionKnown=firstRevision!=null&&firstRevision.Length==64;
  check(revisionKnown&&detected.Count==before+1&&reloaded.Count==before+1&&AgentPlugins.Revision!=firstRevision&&AgentSkills.Discover(main.Item1,main.Item2,main.Item3,AgentPlugins.SkillRoots(reloaded)).Any(skill=>skill.name=="delta-skill"),"the plugin cache detects new manifests, Reload refreshes it and reloaded skill roots reach AgentSkills");
  var missing=AgentPlugins.Discover(Path.Combine(root,"no-app"),Path.Combine(root,"no-user"),Path.Combine(root,"no-library"));
  check(missing.Count==0&&AgentPlugins.SkillRoots(missing).Count==0&&AgentPlugins.Prompts(missing).Count==0&&AgentPlugins.SkillRoots(null).Count==0&&AgentPlugins.Prompts(null).Count==0,"missing plugin roots and null plugin lists are handled without throwing");
  // The test-only user root override replaces the profile directory.
  AgentPlugins.UserRootOverride=main.Item2;
  var overridden=AgentPlugins.Discover(main.Item1,Path.Combine(root,"no-such-profile"),main.Item3);
  AgentPlugins.UserRootOverride="";
  check(overridden.Any(plugin=>plugin.name=="beta-pack")&&overridden.Any(plugin=>plugin.name=="alpha-pack"),"the plugin test user-root override replaces the profile plugin root");
  AgentPlugins.Reload();AgentSkills.Reload();AgentPlugins.UserRootOverride="";AgentSkills.UserRootOverride="";
 }
}
