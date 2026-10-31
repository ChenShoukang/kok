using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using kok;

static class OsSandboxTests {
 sealed class Result { public int exit; public string stdout = "", stderr = ""; }
 static Result RunHelper(string spec) {
  var start = OsSandbox.HelperStart(spec);
  using (var process = new Process { StartInfo = start }) {
   process.Start();
   var stdout = process.StandardOutput.ReadToEndAsync();
   var stderr = process.StandardError.ReadToEndAsync();
   if (!process.WaitForExit(30000)) { try { process.Kill(); } catch { } throw new Exception("sandbox helper did not exit"); }
   return new Result { exit = process.ExitCode, stdout = stdout.GetAwaiter().GetResult(), stderr = stderr.GetAwaiter().GetResult() };
  }
 }
 public static void Run(string root, Action<bool,string> check) {
  Directory.CreateDirectory(root);
  bool supported = OsSandbox.Supported;
  check(supported, "AppContainer sandbox is available for user-approved commands (" + OsSandbox.Status + ")");
  if (!supported) return;
  string workspace = Path.Combine(root, "workspace"); Directory.CreateDirectory(workspace);
  OsSandbox.GrantWorkspace(workspace); OsSandbox.GrantWorkspace(workspace);
  string shell = Environment.GetEnvironmentVariable("ComSpec");
  string spec = OsSandbox.WriteSpec(shell, new[] { "/c", "echo INSIDE>inside.txt" }, workspace);
  var inside = RunHelper(spec);
  string insideFile = Path.Combine(workspace, "inside.txt");
  check(inside.exit == 0 && File.Exists(insideFile) && File.ReadAllText(insideFile).Contains("INSIDE"), "the AppContainer sandbox lets an approved command write inside the permitted workspace");
  string outside = Path.Combine(Path.GetDirectoryName(root), "sandbox-outside-" + Guid.NewGuid().ToString("N") + ".txt");
  spec = OsSandbox.WriteSpec(shell, new[] { "/c", "echo OUTSIDE>\"" + outside + "\"" }, workspace);
  var denied = RunHelper(spec);
  check(!File.Exists(outside) && (denied.exit != 0 || denied.stderr.Contains("denied") || denied.stderr.Contains("拒绝")), "the AppContainer sandbox denies writes outside the workspace even when the command targets an absolute path");
  spec = OsSandbox.WriteSpec(Path.Combine(root, "missing.exe"), new string[0], workspace);
  var invalid = RunHelper(spec);
  check(invalid.exit != 0 && invalid.stderr.Contains("sandbox"), "the sandbox helper rejects invalid executables without running anything");
  var library = new Library(Path.Combine(root, "library")); library.SetAppMode("smart", true);
  library.SaveAgentExecution(true, workspace, true);
  check(library.AgentExecutionEnabled && library.AgentSandboxEnabled && library.AgentExecutionRoot == workspace, "the execution sandbox setting is persisted with the workspace configuration");
  var config = Library.Json.Serialize(AgentExecution.Config(library));
  check(config.Contains("\"sandbox\":true") && config.Contains("AppContainer"), "execution configuration reports sandbox availability and the effective mode");
  library.SaveAgentExecution(true, workspace, false);
  check(!library.AgentSandboxEnabled, "turning the sandbox off is recorded explicitly instead of silently falling back");
 }
}
