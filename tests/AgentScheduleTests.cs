using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using kok;

static class AgentScheduleTests {
 public static void Run(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  var lib=new Library(Path.Combine(root,"library"));lib.SetAppMode("smart",true);
  var schedule=new AgentSchedule{goal="每晚整理资料并生成摘要",cron="*/5 * * * *",enabled=true,budget=24000,requestLimit=10,scope=new AgentScope{mode="all"}};
  var saved=lib.SaveAgentSchedule(schedule);
  check(AgentJournal.ValidId(saved.id)&&lib.AgentScheduleList().Count==1,"saving a schedule assigns a stable native ID and persists it in library settings");
  var reopened=new Library(lib.DataDir);
  check(reopened.AgentScheduleList().Single().goal==schedule.goal,"schedules are encrypted-settings persisted across library reopen");
  var view=(Array)AgentSchedules.View(lib);check(view.Length==1,"schedule view exposes the stored schedule and its next run");
  bool badCron=false;try{lib.SaveAgentSchedule(new AgentSchedule{goal="bad",cron="not a cron"});}catch(InvalidDataException){badCron=true;}check(badCron,"invalid cron expressions are refused before saving");
  bool badGoal=false;try{lib.SaveAgentSchedule(new AgentSchedule{goal=" ",cron="0 8 * * *"});}catch(InvalidDataException){badGoal=true;}check(badGoal,"empty schedule goals are refused");
  var runner=lib.SaveAgentSchedule(new AgentSchedule{goal="每分钟运行",cron="* * * * *",enabled=true,budget=6000,requestLimit=4,scope=new AgentScope{mode="all"}});
  lib.MarkAgentScheduleRun(runner.id,DateTime.Now.AddMinutes(-3));
  var due=AgentSchedules.Due(lib,DateTime.Now);
  check(due!=null&&due.id==runner.id,"a cron schedule whose last run is in the past becomes due exactly once");
  lib.MarkAgentScheduleRun(runner.id,DateTime.Now);
  check(AgentSchedules.Due(lib,DateTime.Now)==null,"marking a schedule as run prevents an immediate duplicate trigger");
  check(lib.DeleteAgentSchedule(runner.id)&&lib.AgentScheduleList().All(row=>row.id!=runner.id),"deleting a schedule removes it durably");
  int savedCount=lib.AgentScheduleList().Count;
  for(int n=0;n<AgentSchedules.MaxSchedules-savedCount;n++)lib.SaveAgentSchedule(new AgentSchedule{goal="limit "+n,cron="0 8 * * *"});
  bool limit=false;try{lib.SaveAgentSchedule(new AgentSchedule{goal="too many",cron="0 8 * * *"});}catch(InvalidDataException){limit=true;}check(limit,"the number of saved schedules is bounded");
  check(AgentRules.MaxRounds==24,"long tasks allow up to 24 native model rounds");
  var schema=ActionSchemas.For("agentTaskStart");var properties=(Dictionary<string,object>)schema["properties"];
  var budget=(Dictionary<string,object>)properties["budget"];var budgetValues=((IEnumerable<int>)budget["enum"]).ToArray();
  var requestLimit=(Dictionary<string,object>)properties["requestLimit"];var contextTokens=(Dictionary<string,object>)properties["contextTokens"];
  check(budgetValues.Contains(48000)&&budgetValues.Contains(64000)&&Convert.ToInt32(requestLimit["maximum"])==24&&Convert.ToInt32(contextTokens["maximum"])==128000,"task limits expose longer budgets and larger context windows");
 }
}
