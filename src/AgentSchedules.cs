using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NCrontab;

namespace kok {
    // Native cron-style schedules for the Agent. A schedule only starts a normal task; every task
    // still follows the approval policy, scope and budget it was saved with.
    public class AgentSchedule {
        public string id, goal, cron, created = "", lastRun = "";
        public bool enabled;
        public AgentScope scope = new AgentScope();
        public int budget = 14000, requestLimit = 8;
    }
    internal static class AgentSchedules {
        internal const int MaxSchedules = 20;
        internal static readonly int[] Budgets = { 6000, 14000, 24000, 48000, 64000 };

        internal static CrontabSchedule Parse(string cron) {
            if (String.IsNullOrWhiteSpace(cron) || cron.Length > 100 || cron.Any(Char.IsControl)) throw new InvalidDataException("计划表达式无效。");
            try { return CrontabSchedule.Parse(cron.Trim()); } catch (Exception error) { throw new InvalidDataException("计划表达式无效：" + error.Message); }
        }

        internal static DateTime? NextRun(AgentSchedule schedule, DateTime now) {
            if (schedule == null || !schedule.enabled) return null;
            try {
                var cron = Parse(schedule.cron);
                DateTime after = now;
                DateTime parsed;
                if (!String.IsNullOrWhiteSpace(schedule.lastRun) && DateTime.TryParse(schedule.lastRun, out parsed) && parsed > after) after = parsed;
                else if (String.IsNullOrWhiteSpace(schedule.lastRun) && !String.IsNullOrWhiteSpace(schedule.created) && DateTime.TryParse(schedule.created, out parsed)) { /* first run counts from creation */ }
                return cron.GetNextOccurrence(after);
            } catch { return null; }
        }

        internal static AgentSchedule Due(Library library, DateTime now) {
            foreach (var schedule in List(library)) {
                if (!schedule.enabled) continue;
                DateTime after = now;
                DateTime parsed;
                if (!String.IsNullOrWhiteSpace(schedule.lastRun) && DateTime.TryParse(schedule.lastRun, out parsed)) after = parsed; else if (!String.IsNullOrWhiteSpace(schedule.created) && DateTime.TryParse(schedule.created, out parsed)) after = parsed;
                try { var next = Parse(schedule.cron).GetNextOccurrence(after); if (next <= now) return schedule; } catch {}
            }
            return null;
        }

        internal static List<AgentSchedule> List(Library library) {
            return library.AgentScheduleList();
        }

        internal static object View(Library library) {
            var now = DateTime.Now;
            return List(library).OrderBy(s => s.created, StringComparer.Ordinal).Select(s => new { schedule = s, nextRun = NextRun(s, now)?.ToString("o") ?? "" }).ToArray();
        }
    }

    public partial class Library {
        public List<AgentSchedule> AgentScheduleList() {
            lock (gate) {
                var result = new List<AgentSchedule>();
                object value;
                if (Db.settings == null || !Db.settings.TryGetValue("agentSchedules", out value) || value == null) return result;
                try { var rows = Json.Deserialize<List<AgentSchedule>>(Convert.ToString(value)); if (rows != null) result.AddRange(rows.Where(row => row != null && AgentJournal.ValidId(row.id))); } catch {}
                return result;
            }
        }
        public AgentSchedule SaveAgentSchedule(AgentSchedule schedule) {
            if (schedule == null) throw new InvalidDataException("计划任务无效。");
            lock (gate) {
                string goal = (schedule.goal ?? "").Trim();
                if (goal.Length < 1 || goal.Length > 2000 || goal.Any(c => Char.IsControl(c) && c != '\n' && c != '\t')) throw new InvalidDataException("计划目标需要 1-2000 个字符。");
                AgentSchedules.Parse(schedule.cron);
                if (!AgentSchedules.Budgets.Contains(schedule.budget)) throw new InvalidDataException("计划任务的上下文档位无效。");
                if (schedule.requestLimit < 1 || schedule.requestLimit > AgentRules.MaxRounds) throw new InvalidDataException("计划任务的请求上限无效。");
                if (schedule.scope == null) schedule.scope = new AgentScope();
                if (!new[] { "all", "selected", "collection" }.Contains(schedule.scope.mode)) throw new InvalidDataException("计划任务的资料范围无效。");
                if (schedule.scope.ids != null && (schedule.scope.ids.Count > 24 || schedule.scope.ids.Any(id => !AgentJournal.ValidId(id)))) throw new InvalidDataException("计划任务的资料范围无效。");
                if (schedule.scope.mode == "collection" && String.IsNullOrWhiteSpace(schedule.scope.collection)) throw new InvalidDataException("请选择计划任务的资料集合。");
                var rows = AgentScheduleList();
                if (String.IsNullOrWhiteSpace(schedule.id)) schedule.id = Guid.NewGuid().ToString("N");
                else if (!AgentJournal.ValidId(schedule.id)) throw new InvalidDataException("计划任务 ID 无效。");
                if (String.IsNullOrWhiteSpace(schedule.created)) schedule.created = DateTime.Now.ToString("o");
                if (rows.All(row => row.id != schedule.id) && rows.Count >= AgentSchedules.MaxSchedules) throw new InvalidDataException("最多保存 " + AgentSchedules.MaxSchedules + " 个计划任务。");
                schedule.goal = goal; schedule.cron = schedule.cron.Trim();
                int index = rows.FindIndex(row => row.id == schedule.id);
                if (index >= 0) { schedule.created = rows[index].created; schedule.lastRun = rows[index].lastRun; rows[index] = schedule; } else rows.Add(schedule);
                if (Db.settings == null) Db.settings = new Dictionary<string, object>();
                Db.settings["agentSchedules"] = Json.Serialize(rows);
                Save(false);
                return schedule;
            }
        }
        public void MarkAgentScheduleRun(string id, DateTime when) {
            lock (gate) {
                var rows = AgentScheduleList();
                var row = rows.FirstOrDefault(x => x.id == id);
                if (row == null) return;
                row.lastRun = when.ToString("o");
                if (Db.settings == null) Db.settings = new Dictionary<string, object>();
                Db.settings["agentSchedules"] = Json.Serialize(rows);
                Save(false);
            }
        }
        public bool DeleteAgentSchedule(string id) {
            lock (gate) {
                var rows = AgentScheduleList();
                if (rows.RemoveAll(row => row.id == id) == 0) return false;
                if (Db.settings == null) Db.settings = new Dictionary<string, object>();
                Db.settings["agentSchedules"] = Json.Serialize(rows);
                Save(false);
                return true;
            }
        }
    }
}
