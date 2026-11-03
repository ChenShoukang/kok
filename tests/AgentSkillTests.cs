using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using kok;

class AgentSkillTests {
 static bool Rejects(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
 static string Skill(string root,string folder,string name,string description,string body,string extra=""){
  string dir=Path.Combine(root,folder);Directory.CreateDirectory(dir);
  File.WriteAllText(Path.Combine(dir,"SKILL.md"),"---\nname: "+name+"\ndescription: "+description+"\n"+extra+"---\n"+body,new UTF8Encoding(false));
  return dir;
 }
 static string Root(string baseDir,string kind){string dir=Path.Combine(baseDir,kind);Directory.CreateDirectory(dir);return dir;}
 public static void Run(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);AgentSkills.UserRootOverride="";AgentSkills.Reload();
  string app=Path.Combine(root,"app"),user=Path.Combine(root,"user"),library=Path.Combine(root,"library");
  string appRoot=Root(app,Path.Combine("ui","skills")),userRoot=Root(user,Path.Combine(".agents","skills")),libraryRoot=Root(library,Path.Combine(".agents","skills"));
  Skill(appRoot,"alpha","alpha","First skill alpha with <xml> & \"quotes\".","Alpha body text.");
  Skill(appRoot,"beta","beta","Second skill beta.","Beta body text.","disable-model-invocation: true\n");
  Skill(userRoot,"gamma","gamma","User skill gamma.","Gamma body.");
  Skill(userRoot,"alpha","alpha","Duplicate alpha from the user root.","Ignored duplicate body.");
  Skill(libraryRoot,"delta","delta","Library skill delta.","Delta body.");
  var skills=AgentSkills.Discover(app,user,library);
  var alpha=AgentSkills.Find(skills,"alpha");
  check(skills.Count==4&&alpha!=null&&alpha.source=="app"&&AgentSkills.Find(skills,"gamma").source=="user"&&AgentSkills.Find(skills,"delta").source=="library"&&skills.Count(skill=>skill.name=="alpha")==1,"skills discovery reads app, user and library roots with first-wins collision order");
  check(alpha.description=="First skill alpha with <xml> & \"quotes\"."&&alpha.body=="Alpha body text."&&alpha.path==Path.Combine(appRoot,"alpha","SKILL.md")&&!alpha.disabled,"a valid SKILL.md parses name, description, body and absolute path");
  var beta=AgentSkills.Find(skills,"beta");
  string index=AgentSkills.IndexXml(skills);
  check(beta.disabled&&index.Contains("<name>gamma</name>")&&!index.Contains("<name>beta</name>")&&index.StartsWith("<available_skills>")&&index.EndsWith("</available_skills>"),"disable-model-invocation hides a skill from the prompt index while discovery keeps its disabled flag");
  check(index.Contains("&lt;xml&gt; &amp; &quot;quotes&quot;")&&!index.Contains("<xml>"),"the prompt index XML-escapes names, descriptions and locations");
  Skill(appRoot,"bad-upper","BadName","Invalid uppercase name.","x");
  Skill(appRoot,"no-desc","no-desc","","x");
  Directory.CreateDirectory(Path.Combine(appRoot,"plain"));File.WriteAllText(Path.Combine(appRoot,"plain","SKILL.md"),"just text without frontmatter");
  AgentSkills.Reload();var filtered=AgentSkills.Discover(app,user,library);
  check(filtered.Count==4&&filtered.All(skill=>skill.name!="BadName"&&skill.name!="no-desc"),"files with missing or invalid frontmatter are skipped without throwing");
  Skill(userRoot,Path.Combine("deep","a","b","c"),"deepone","Deep but allowed skill.","Deep body.");
  Skill(userRoot,Path.Combine("too","deep2","d","e","f"),"toodeep","Beyond the depth limit.","x");
  Skill(userRoot,Path.Combine(".hidden"),"hidden-skill","Hidden dot directory.","x");
  Skill(userRoot,Path.Combine("node_modules"),"module-skill","Dependency directory.","x");
  Skill(userRoot,"nest","nest","Nested skill root.","x");
  Skill(userRoot,Path.Combine("nest","inner"),"nested-inner","Nested inside a skill.","x");
  AgentSkills.Reload();var depth=AgentSkills.Discover(app,user,library);
  check(depth.Any(skill=>skill.name=="deepone")&&depth.All(skill=>skill.name!="toodeep"&&skill.name!="hidden-skill"&&skill.name!="module-skill"&&skill.name!="nested-inner")&&depth.Any(skill=>skill.name=="nest"),"discovery walks four directory levels and skips dot, node_modules and nested-skill directories");
  string capApp=Path.Combine(root,"cap-app"),capUser=Path.Combine(root,"cap-user"),capLibrary=Path.Combine(root,"cap-library");
  string capRoot=Root(capApp,Path.Combine("ui","skills"));Root(capUser,Path.Combine(".agents","skills"));Root(capLibrary,Path.Combine(".agents","skills"));
  for(int n=0;n<105;n++)Skill(capRoot,"skill-"+n.ToString("D3"),"skill-"+n.ToString("D3"),"Bulk skill "+n+".","Body "+n);
  AgentSkills.Reload();var capped=AgentSkills.Discover(capApp,capUser,capLibrary);
  string bodyApp=Path.Combine(root,"body-app");string bodyRoot=Root(bodyApp,Path.Combine("ui","skills"));
  Skill(bodyRoot,"long-body","long-body","Body is capped.",new string('b',25000));
  AgentSkills.Reload();var bodyCapped=AgentSkills.Discover(bodyApp,Path.Combine(root,"body-user"),Path.Combine(root,"body-library"));
  check(capped.Count==AgentSkills.MaxSkills&&AgentSkills.Find(bodyCapped,"long-body").body.Length==AgentSkills.MaxBody,"discovery caps the skill count and truncates oversized bodies");
  AgentSkills.Reload();int before=AgentSkills.Discover(app,user,library).Count;
  Skill(libraryRoot,"epsilon","epsilon","Newly added library skill.","Epsilon body.");
  var refreshed=AgentSkills.Discover(app,user,library);
  AgentSkills.Reload();var reloaded=AgentSkills.Discover(app,user,library);
  check(refreshed.Count==before+1&&AgentSkills.Find(refreshed,"epsilon")!=null&&reloaded.Count==before+1,"the discovery cache refreshes on file changes and Reload clears it");
  check(AgentSkills.LoadBody(app,user,library,"gamma")=="Gamma body."&&AgentSkills.LoadBody(app,user,library,"alpha")=="Alpha body text.","LoadBody returns the stored markdown body for a discovered skill");
  string missing="";try{AgentSkills.LoadBody(app,user,library,"does-not-exist");}catch(InvalidDataException error){missing=error.Message;}
  check(missing.Contains("找不到技能")&&Rejects(()=>AgentSkills.LoadBody(app,user,library,"../escape")),"LoadBody fails closed with the localized message for unknown skill names");
  var tool=AgentSkills.Tools();
  bool badArguments=false;try{tool.Validate(new AgentAction{tool="load_skill",args=new Dictionary<string,object>()},name=>false);}catch(FormatException){badArguments=true;}
  bool badFormat=false;try{tool.Validate(new AgentAction{tool="load_skill",args=new Dictionary<string,object>{{"name","Bad Name!"}}},name=>false);}catch(FormatException){badFormat=true;}
  tool.Validate(new AgentAction{tool="load_skill",args=new Dictionary<string,object>{{"name","alpha"}}},name=>false);
  check(tool.Name=="load_skill"&&!tool.Write&&tool.Requires==Need.None&&tool.Required.SequenceEqual(new[]{"name"})&&badArguments&&badFormat,"the load_skill tool spec is a read-only Need.None tool that validates the skill-name format");
  check(AgentSkills.IndexXml(new List<AgentSkill>())==""&&AgentSkills.IndexXml(new List<AgentSkill>{new AgentSkill{name="x",description="d",disabled=true}})=="","an empty or fully disabled skill list produces no prompt index block");
  AgentSkills.UserRootOverride=user;var overridden=AgentSkills.Discover(app,Path.Combine(root,"no-such-profile"),library);AgentSkills.UserRootOverride="";
  check(AgentSkills.Find(overridden,"gamma")!=null&&AgentSkills.Find(overridden,"delta")!=null,"the test user-root override replaces the profile root without changing other roots");
  check(AgentSkills.Discover(Path.Combine(root,"no-app"),Path.Combine(root,"no-user"),Path.Combine(root,"no-library")).Count==0&&AgentSkills.Find(null,"alpha")==null&&AgentSkills.Find(skills,"")==null,"missing roots and null skill lists are handled without throwing");
  AgentSkills.Reload();AgentSkills.UserRootOverride="";
 }
}
