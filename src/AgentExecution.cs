using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    // Opt-in execution environment: relative file tools and approved commands inside one user-chosen
    // workspace folder. Nothing runs unless the user enables it and a valid root directory is configured.
    internal static class AgentExecution {
        internal const string RootSetting = "agentWorkspaceRoot";
        internal const string EnabledSetting = "agentExecution";
        internal const int MaxReadBytes = 262144;
        internal const int MaxWriteChars = 200000;
        internal const int MaxOutputChars = 32000;
        internal const int MaxListEntries = 200;
        internal const int MaxTimeoutSeconds = 120;

        internal static bool Enabled(Library library) { return library.AgentExecutionEnabled; }
        internal static string Root(Library library) { return library.AgentExecutionRoot; }
        internal static bool Configured(Library library) { var root = Root(library); return Enabled(library) && root.Length > 0 && Directory.Exists(root); }
        internal static object Config(Library library) { var root = Root(library); return new { enabled = Enabled(library), root = root, exists = root.Length > 0 && Directory.Exists(root), configured = Configured(library) }; }
        internal static void Save(Library library, bool enabled, string root) {
            root = (root ?? "").Trim();
            if (root.Length > 0) {
                if (root.Length > 1000 || root.Any(Char.IsControl)) throw new InvalidDataException("工作目录路径无效。");
                if (!Path.IsPathRooted(root) || !Directory.Exists(root)) throw new InvalidDataException("工作目录必须是已存在的绝对路径。");
            }
            library.SaveAgentExecution(enabled, root);
        }

        internal static AgentToolSpec[] Tools() {
            return new[] {
                ReadTool("list_folder", "列出文件夹", "List one folder inside the configured execution workspace. Paths are relative to the workspace root; absolute paths and parent traversal are refused.", AgentRules.Props("path", AgentRules.Str("Relative folder path; empty means the root", 500)), (library, state, call, token) => Task.FromResult(ListFolder(library, call.args.ContainsKey("path") ? Convert.ToString(call.args["path"]) : ""))),
                ReadTool("read_text_file", "读取工作区文件", "Read a UTF-8 text file inside the configured execution workspace (relative path, up to 256 KB). Absolute paths and parent traversal are refused.", AgentRules.Props("path", AgentRules.Str("Relative file path", 500)), (library, state, call, token) => Task.FromResult(ReadFile(library, call.args.ContainsKey("path") ? Convert.ToString(call.args["path"]) : ""))),
                new AgentToolSpec {
                    Name = "write_text_file", Title = "写入工作区文件", Requires = Need.NormalLibrary, Write = true, CapabilityId = "agent.write_text_file",
                    Signature = "write_text_file{path,text,overwrite?}",
                    Description = "PROPOSE writing a UTF-8 text file inside the configured execution workspace. The user approves it. Parent folders are created inside the workspace; absolute paths and parent traversal are refused.",
                    Properties = AgentRules.Props("path", AgentRules.Str("Relative file path", 500), "text", AgentRules.Str("Full UTF-8 text to write", MaxWriteChars), "overwrite", new Dictionary<string,object> { { "type", "boolean" } }),
                    Required = new[] { "path", "text" },
                    Validate = (action, exists) => {
                        string path = action.args != null && action.args.ContainsKey("path") ? Convert.ToString(action.args["path"]) : "";
                        ValidateRelative(path, false);
                        string text = action.args != null && action.args.ContainsKey("text") ? Convert.ToString(action.args["text"]) : null;
                        if (text == null || text.Length > MaxWriteChars) throw new FormatException("text must be at most " + MaxWriteChars + " characters");
                    },
                    ExecuteWithContext = (context, state, call, token) => Task.FromResult(new AgentToolReceipt{result=WriteFile(context, call)})
                },
                new AgentToolSpec {
                    Name = "run_command", Title = "运行命令", Requires = Need.NormalLibrary, Write = true, CapabilityId = "agent.run_command",
                    Signature = "run_command{command,args[<=32],cwd?,timeoutSeconds?}",
                    Description = "PROPOSE running one executable inside the configured execution workspace, with the current user's permissions. The user approves the exact command. No shell is added; provide the program and arguments separately. Output and runtime are bounded.",
                    Properties = AgentRules.Props(
                        "command", AgentRules.Str("Executable name or absolute path", 260),
                        "args", AgentRules.Arr("Arguments", 32, AgentRules.Str("Argument", 1024)),
                        "cwd", AgentRules.Str("Relative working directory inside the workspace", 500),
                        "timeoutSeconds", new Dictionary<string,object> { { "type", "integer" }, { "minimum", 1 }, { "maximum", MaxTimeoutSeconds } }),
                    Required = new[] { "command" },
                    Validate = (action, exists) => {
                        string command = action.args != null && action.args.ContainsKey("command") ? Convert.ToString(action.args["command"]).Trim() : "";
                        if (command.Length == 0 || command.Length > 260 || command.Any(Char.IsControl)) throw new FormatException("command must be 1-260 characters");
                        var args = action.args.ContainsKey("args") ? AgentTaskState.Get<List<string>>(action.args, "args") : null;
                        if (args != null && (args.Count > 32 || args.Any(a => a == null || a.Length > 1024 || a.Any(Char.IsControl)))) throw new FormatException("args allow at most 32 short strings");
                        if (args != null && args.Sum(a => a.Length) > 4000) throw new FormatException("argument list is too long");
                        if (action.args.ContainsKey("cwd")) ValidateRelative(Convert.ToString(action.args["cwd"]), true);
                        if (action.args.ContainsKey("timeoutSeconds")) { int seconds; try { seconds = Convert.ToInt32(action.args["timeoutSeconds"]); } catch { throw new FormatException("timeoutSeconds must be an integer"); } if (seconds < 1 || seconds > MaxTimeoutSeconds) throw new FormatException("timeoutSeconds must be 1-" + MaxTimeoutSeconds); }
                    },
                    ExecuteWithContext = (context, state, call, token) => Task.FromResult(new AgentToolReceipt{result=RunCommand(context, call, token)})
                }
            };
        }

        static AgentToolSpec ReadTool(string name, string title, string description, Dictionary<string,object> props, Func<Library,AgentTaskState,AgentCall,CancellationToken,Task<Dictionary<string,object>>> read) {
            return new AgentToolSpec {
                Name = name, Title = title, Requires = Need.NormalLibrary, Write = false, CapabilityId = "agent." + name,
                Signature = name + "{path}", Description = description + " Runs automatically.",
                Properties = props, Required = new[] { "path" },
                Validate = (action, exists) => { ValidateRelative(action.args != null && action.args.ContainsKey("path") ? Convert.ToString(action.args["path"]) : "", true); },
                Read = read
            };
        }

        internal static void ValidateRelative(string path, bool allowEmpty) {
            path = (path ?? "").Trim();
            if (path.Length == 0) { if (allowEmpty) return; throw new FormatException("path is required"); }
            if (path.Length > 500 || path.Any(Char.IsControl) || Path.IsPathRooted(path)) throw new FormatException("path must be relative to the workspace root");
            var parts = path.Replace('\\', '/').Split('/');
            if (parts.Any(part => part == ".." || part == "." || part.Length == 0 || part.IndexOf(':') >= 0)) throw new FormatException("path must not contain parent traversal or drive letters");
        }

        internal static string Resolve(Library library, string relative, bool allowEmpty) {
            if (!Configured(library)) throw new InvalidDataException("执行环境未启用：请先在 Agent 的“能力与权限”中选择工作目录并开启执行。");
            string root = Path.GetFullPath(Root(library)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            ValidateRelative(relative, allowEmpty);
            string full = Path.GetFullPath(Path.Combine(root, (relative ?? "").Replace('/', Path.DirectorySeparatorChar)));
            if (!full.Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) && !full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("路径超出工作目录。");
            return full;
        }

        static Dictionary<string,object> ListFolder(Library library, string relative) {
            string full = Resolve(library, relative, true);
            if (!Directory.Exists(full)) throw new InvalidDataException("文件夹不存在。");
            var rows = new List<string>();
            foreach (var dir in Directory.GetDirectories(full).OrderBy(d => d, StringComparer.OrdinalIgnoreCase).Take(MaxListEntries)) rows.Add("[dir] " + Path.GetFileName(dir));
            foreach (var file in Directory.GetFiles(full).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Take(Math.Max(0, MaxListEntries - rows.Count))) { var info = new FileInfo(file); rows.Add("[file] " + info.Name + "  " + info.Length + " bytes"); }
            string shown = (relative ?? "").Trim(); if (shown.Length == 0) shown = ".";
            return new Dictionary<string,object> { { "text", "Workspace folder " + shown + " (" + rows.Count + " entries):\n" + String.Join("\n", rows) }, { "count", rows.Count } };
        }

        static Dictionary<string,object> ReadFile(Library library, string relative) {
            string full = Resolve(library, relative, false);
            if (!File.Exists(full)) throw new InvalidDataException("文件不存在。");
            var info = new FileInfo(full);
            if (info.Length > MaxReadBytes) throw new InvalidDataException("文件超过 256 KB，请改用 list_folder 或直接询问更小的文件。");
            byte[] bytes = File.ReadAllBytes(full);
            if (bytes.Take(Math.Min(bytes.Length, 4096)).Any(b => b == 0)) throw new InvalidDataException("这看起来是二进制文件，read_text_file 只读取 UTF-8 文本。");
            string text = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3) : Encoding.UTF8.GetString(bytes);
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (text.Length > MaxReadBytes) text = text.Substring(0, MaxReadBytes) + "\n[Truncated]";
            return new Dictionary<string,object> { { "text", "File " + relative + " (" + bytes.LongLength + " bytes):\n" + text }, { "path", relative }, { "bytes", bytes.LongLength } };
        }

        static Dictionary<string,object> WriteFile(ModuleContext context, AgentCall call) {
            context.Policy.Ensure(Need.NormalLibrary, "agent");
            var library = context.Library;
            string relative = Convert.ToString(call.args["path"]).Trim();
            string text = Convert.ToString(call.args["text"]);
            if (text.Length > MaxWriteChars) throw new InvalidDataException("text exceeds the write limit.");
            bool overwrite = call.args.ContainsKey("overwrite") && call.args["overwrite"] != null && Convert.ToBoolean(call.args["overwrite"]);
            string full = Resolve(library, relative, false);
            if (File.Exists(full) && !overwrite) throw new InvalidDataException("文件已存在；如需覆盖请显式设置 overwrite=true。");
            string parent = Path.GetDirectoryName(full);
            if (!String.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            bool existed = File.Exists(full);
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            File.WriteAllBytes(full, bytes);
            return new Dictionary<string,object> { { "text", (existed ? "Overwrote " : "Created ") + relative + " (" + bytes.LongLength + " bytes) inside the workspace." }, { "path", relative }, { "bytes", bytes.LongLength } };
        }

        static Dictionary<string,object> RunCommand(ModuleContext context, AgentCall call, CancellationToken token) {
            context.Policy.Ensure(Need.NormalLibrary, "agent");
            var library = context.Library;
            string command = Convert.ToString(call.args["command"]).Trim();
            var args = call.args.ContainsKey("args") && call.args["args"] != null ? AgentTaskState.Get<List<string>>(call.args, "args") : new List<string>();
            string cwdRelative = call.args.ContainsKey("cwd") && call.args["cwd"] != null ? Convert.ToString(call.args["cwd"]) : "";
            int timeout = call.args.ContainsKey("timeoutSeconds") && call.args["timeoutSeconds"] != null ? Convert.ToInt32(call.args["timeoutSeconds"]) : 30;
            if (timeout < 1 || timeout > MaxTimeoutSeconds) throw new InvalidDataException("命令超时设置无效。");
            string cwd = Resolve(library, cwdRelative, true);
            if (!Directory.Exists(cwd)) throw new InvalidDataException("工作目录不存在。");
            var start = new ProcessStartInfo { FileName = command, WorkingDirectory = cwd, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            if (args != null && args.Count > 0) start.Arguments = String.Join(" ", args.Select(QuoteArgument));
            using (var group = new DownloadProcessGroup())
            using (var process = new Process { StartInfo = start }) {
                try { process.Start(); } catch (Exception error) { throw new InvalidDataException("无法启动命令：" + error.Message); }
                group.Attach(process);
                var stdout = ReadLimitedAsync(process.StandardOutput, MaxOutputChars);
                var stderr = ReadLimitedAsync(process.StandardError, MaxOutputChars);
                var timer = Stopwatch.StartNew();
                while (!process.WaitForExit(100)) {
                    token.ThrowIfCancellationRequested();
                    if (timer.Elapsed.TotalSeconds >= timeout) { try { group.Stop(); } catch {} throw new InvalidDataException("命令在 " + timeout + " 秒内未完成，已终止。"); }
                }
                token.ThrowIfCancellationRequested();
                string output = stdout.GetAwaiter().GetResult(), errorText = stderr.GetAwaiter().GetResult();
                int exit = process.ExitCode;
                string text = "Exit code " + exit + "\nSTDOUT:\n" + output + (errorText.Length > 0 ? "\nSTDERR:\n" + errorText : "");
                if (text.Length > MaxOutputChars * 2) text = text.Substring(0, MaxOutputChars * 2) + "\n[Output truncated]";
                return new Dictionary<string,object> { { "text", text }, { "exitCode", exit }, { "stdout", output }, { "stderr", errorText } };
            }
        }

        static async Task<string> ReadLimitedAsync(StreamReader reader, int limit) {
            var builder = new StringBuilder(); var buffer = new char[4096]; int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0) { if (builder.Length < limit) builder.Append(buffer, 0, Math.Min(read, limit - builder.Length)); }
            return builder.ToString();
        }

        static string QuoteArgument(string value) {
            if (value == null) return "\"\"";
            if (value.Length > 0 && value.All(c => !Char.IsWhiteSpace(c) && c != '"')) return value;
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        internal static string Guidance {
            get { return "When the execution environment is enabled, list_folder/read_text_file read files inside the user's chosen workspace; write_text_file/run_command are approval-gated proposals with bounded output and runtime. Never assume files outside that workspace exist. "; }
        }
    }
}

namespace kok {
    public partial class Library {
        public bool AgentExecutionEnabled {
            get { lock (gate) { object value; if (Db.settings == null || !Db.settings.TryGetValue("agentExecution", out value) || value == null) return false; try { return Convert.ToBoolean(value); } catch { return false; } } }
        }
        public string AgentExecutionRoot {
            get { lock (gate) { object value; string root = Db.settings != null && Db.settings.TryGetValue("agentWorkspaceRoot", out value) ? Convert.ToString(value) : ""; return root == null ? "" : root.Trim(); } }
        }
        public void SaveAgentExecution(bool enabled, string root) {
            lock (gate) {
                if (Db.settings == null) Db.settings = new Dictionary<string, object>();
                Db.settings["agentExecution"] = enabled;
                Db.settings["agentWorkspaceRoot"] = root ?? "";
                Save(false);
            }
        }
    }
}
