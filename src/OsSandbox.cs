using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace kok {
    // Windows AppContainer sandbox for user-approved commands and local MCP servers. The child token
    // has no capabilities (no network), no broad filesystem access and no user elevation; the host
    // grants the AppContainer package SID read/execute on the chosen executable and modify rights on
    // the workspace only. The actual process creation happens in a trusted helper mode of kok.exe so
    // the caller can keep ordinary redirected pipes, job-object timeouts and bounded output.
    internal static class OsSandbox {
        internal const string ProfileName = "kok.agent.sandbox.v1";
        const int ErrorAccessDenied = 5;
        static readonly object gate = new object();
        static bool probed, supported;
        static string failure = "";
        static IntPtr packageSid = IntPtr.Zero;
        static string packageSidString = "";
        static readonly HashSet<string> grantedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> grantedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> grantedTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal const int MaxTreeFiles = 4096;

        internal static bool Supported { get { lock (gate) { Probe(); return supported; } } }
        internal static string Status { get { lock (gate) { Probe(); return supported ? "AppContainer ready (" + packageSidString + ")" : "AppContainer unavailable: " + failure; } } }

        static void Probe() {
            if (probed) return;
            probed = true;
            try {
                int hr = CreateAppContainerProfile(ProfileName, "kok Agent sandbox", "Isolated processes for user-approved Agent commands", IntPtr.Zero, 0, out packageSid);
                if (hr != 0) hr = DeriveAppContainerSidFromAppContainerName(ProfileName, out packageSid);
                if (hr != 0 || packageSid == IntPtr.Zero) throw new InvalidOperationException("AppContainer profile unavailable (0x" + hr.ToString("X8") + ")");
                IntPtr text;
                if (!ConvertSidToStringSid(packageSid, out text)) throw new InvalidOperationException("AppContainer SID unavailable");
                packageSidString = Marshal.PtrToStringUni(text);
                LocalFree(text);
                if (String.IsNullOrEmpty(packageSidString)) throw new InvalidOperationException("AppContainer SID is empty");
                supported = true;
            } catch (Exception error) {
                supported = false;
                failure = error.Message;
            }
        }

        internal static void GrantWorkspace(string directory) {
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) throw new InvalidDataException("Sandbox workspace does not exist.");
            string full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            lock (gate) {
                Probe();
                if (!supported) throw new InvalidDataException(Status);
                if (grantedDirectories.Contains(full)) return;
                var security = Directory.GetAccessControl(full);
                var rule = new FileSystemAccessRule(new SecurityIdentifier(packageSidString), FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
                bool exists = security.GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Any(r => r.IdentityReference.Value == packageSidString && (r.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify && r.AccessControlType == AccessControlType.Allow);
                if (!exists) { security.AddAccessRule(rule); Directory.SetAccessControl(full, security); }
                grantedDirectories.Add(full);
            }
        }

        // Grants an explicitly chosen portable-runtime directory (and its existing contents) read/execute
        // access to the AppContainer package. Only user-writable, ACL-modifiable directories can work;
        // failures are reported instead of silently launching an unsandboxed runtime.
        internal static void GrantReadTree(string directory) {
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) throw new InvalidDataException("便携运行时目录不存在。");
            string full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            if (full.Length <= 3 || full.EndsWith(":" + Path.DirectorySeparatorChar)) throw new InvalidDataException("不能把整个磁盘根目录授权给沙箱。");
            lock (gate) {
                Probe();
                if (!supported) throw new InvalidDataException(Status);
                if (grantedTrees.Contains(full)) return;
                try { AddTreeRule(full, true); } catch (UnauthorizedAccessException) { throw new InvalidDataException("无法授权该运行时目录；请把便携运行时放在用户可写目录，或关闭该服务的沙箱。"); }
                int files = 0;
                var pending = new Queue<string>(); pending.Enqueue(full);
                while (pending.Count > 0) {
                    string current = pending.Dequeue();
                    try { AddTreeRule(current, true); } catch (UnauthorizedAccessException) { throw new InvalidDataException("运行时目录中的文件夹无法授权：" + current); }
                    foreach (string entry in Directory.EnumerateFileSystemEntries(current)) {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                        if ((attributes & FileAttributes.Directory) != 0) { pending.Enqueue(entry); continue; }
                        files++;
                        if (files > MaxTreeFiles) throw new InvalidDataException("运行时目录包含过多文件，无法安全授权；请缩小目录范围。");
                        try { AddTreeRule(entry, false); } catch (UnauthorizedAccessException) { throw new InvalidDataException("运行时文件无法授权：" + entry); }
                    }
                }
                grantedTrees.Add(full);
            }
        }

        static void AddTreeRule(string path, bool directory) {
            var security = directory ? (FileSystemSecurity)Directory.GetAccessControl(path) : File.GetAccessControl(path);
            var identity = new SecurityIdentifier(packageSidString);
            bool exists = security.GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Any(r => r.IdentityReference.Value == packageSidString && (r.FileSystemRights & FileSystemRights.ReadAndExecute) == FileSystemRights.ReadAndExecute && r.AccessControlType == AccessControlType.Allow);
            if (exists) return;
            var rule = directory
                ? new FileSystemAccessRule(identity, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow)
                : new FileSystemAccessRule(identity, FileSystemRights.ReadAndExecute, AccessControlType.Allow);
            security.AddAccessRule(rule);
            if (directory) Directory.SetAccessControl(path, (DirectorySecurity)security); else File.SetAccessControl(path, (FileSecurity)security);
        }

        internal static void GrantFile(string file) {
            if (String.IsNullOrWhiteSpace(file) || !File.Exists(file)) return;
            string full = Path.GetFullPath(file);
            lock (gate) {
                Probe();
                if (!supported) throw new InvalidDataException(Status);
                if (grantedFiles.Contains(full)) return;
                var security = File.GetAccessControl(full);
                var rule = new FileSystemAccessRule(new SecurityIdentifier(packageSidString), FileSystemRights.ReadAndExecute, AccessControlType.Allow);
                bool exists = security.GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Any(r => r.IdentityReference.Value == packageSidString && (r.FileSystemRights & FileSystemRights.ReadAndExecute) == FileSystemRights.ReadAndExecute && r.AccessControlType == AccessControlType.Allow);
                if (!exists) { security.AddAccessRule(rule); File.SetAccessControl(full, security); }
                grantedFiles.Add(full);
            }
        }

        internal static string WriteSpec(string executable, IEnumerable<string> args, string workingDirectory) {
            var spec = new Dictionary<string, object> { { "exe", executable ?? "" }, { "args", (args ?? Enumerable.Empty<string>()).Take(64).ToList() }, { "cwd", workingDirectory ?? "" } };
            string path = Path.Combine(Path.GetTempPath(), "kok-sandbox-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, Library.Json.Serialize(spec), new UTF8Encoding(false));
            return path;
        }

        internal static ProcessStartInfo HelperStart(string specFile) {
            string helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kok.exe");
            if (!File.Exists(helper)) throw new InvalidDataException("The sandbox helper executable is missing.");
            var start = new ProcessStartInfo { FileName = helper, Arguments = "--sandbox-exec " + Quote(specFile), WorkingDirectory = Path.GetDirectoryName(specFile), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var key in start.EnvironmentVariables.Keys.Cast<string>().ToArray()) {
                string clean = key.ToUpperInvariant();
                if (clean == "HTTP_PROXY" || clean == "HTTPS_PROXY" || clean == "ALL_PROXY" || clean == "NO_PROXY" || clean.StartsWith("KOK_")) start.EnvironmentVariables.Remove(key);
            }
            return start;
        }

        internal static int RunHelper(string specFile) {
            try {
                var info = new FileInfo(specFile);
                if (!info.Exists || info.Length > 128 * 1024) { Console.Error.WriteLine("sandbox: invalid request file"); return 130; }
                var spec = Library.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(specFile, Encoding.UTF8));
                if (spec == null) { Console.Error.WriteLine("sandbox: empty request"); return 130; }
                string exe = spec.ContainsKey("exe") ? Convert.ToString(spec["exe"]) : "";
                string cwd = spec.ContainsKey("cwd") ? Convert.ToString(spec["cwd"]) : "";
                var args = spec.ContainsKey("args") ? Library.Json.Deserialize<List<string>>(Library.Json.Serialize(spec["args"])) : new List<string>();
                if (String.IsNullOrWhiteSpace(exe) || exe.Length > 1024 || exe.Any(Char.IsControl) || !File.Exists(exe)) { Console.Error.WriteLine("sandbox: executable not found or invalid"); return 131; }
                if (cwd.Length > 0 && (!Path.IsPathRooted(cwd) || !Directory.Exists(cwd))) { Console.Error.WriteLine("sandbox: working directory not found"); return 132; }
                if (args == null || args.Count > 64 || args.Any(a => a == null || a.Length > 4000 || a.Any(Char.IsControl))) { Console.Error.WriteLine("sandbox: invalid arguments"); return 133; }
                lock (gate) { Probe(); }
                if (!supported) { Console.Error.WriteLine("sandbox: " + failure); return 134; }
                return Launch(exe, args, cwd);
            } catch (Exception error) {
                try { Console.Error.WriteLine("sandbox: " + error.Message); } catch { }
                return 135;
            } finally {
                try { File.Delete(specFile); } catch { }
            }
        }

        static int Launch(string executable, List<string> args, string cwd) {
            string commandLine = Quote(executable) + (args.Count > 0 ? " " + String.Join(" ", args.Select(Quote)) : "");
            IntPtr listSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref listSize);
            if (listSize == IntPtr.Zero) { Console.Error.WriteLine("sandbox: attribute list failed"); return 136; }
            IntPtr list = Marshal.AllocHGlobal(listSize);
            if (!InitializeProcThreadAttributeList(list, 1, 0, ref listSize)) { Marshal.FreeHGlobal(list); Console.Error.WriteLine("sandbox: attribute list init failed"); return 136; }
            var security = new SecurityCapabilities { AppContainerSid = packageSid, Capabilities = IntPtr.Zero, CapabilityCount = 0, Reserved = 0 };
            var startup = new StartupInfoEx();
            startup.StartupInfo.cb = (uint)Marshal.SizeOf(typeof(StartupInfoEx));
            startup.StartupInfo.dwFlags = 0x00000100; // STARTF_USESTDHANDLES
            startup.StartupInfo.hStdInput = GetStdHandle(-10);
            startup.StartupInfo.hStdOutput = GetStdHandle(-11);
            startup.StartupInfo.hStdError = GetStdHandle(-12);
            foreach (IntPtr handle in new[] { startup.StartupInfo.hStdInput, startup.StartupInfo.hStdOutput, startup.StartupInfo.hStdError }) {
                if (handle != IntPtr.Zero && handle != new IntPtr(-1)) SetHandleInformation(handle, 1, 1);
            }
            startup.lpAttributeList = list;
            bool updated = UpdateProcThreadAttribute(list, 0, (IntPtr)0x00020009, ref security, (IntPtr)Marshal.SizeOf(typeof(SecurityCapabilities)), IntPtr.Zero, IntPtr.Zero);
            if (!updated) { DeleteProcThreadAttributeList(list); Marshal.FreeHGlobal(list); Console.Error.WriteLine("sandbox: security capability attribute failed"); return 136; }
            ProcessInformation process;
            bool ok = CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, true, 0x00080000 | 0x00000004, IntPtr.Zero, cwd.Length == 0 ? null : cwd, ref startup, out process);
            int error = Marshal.GetLastWin32Error();
            DeleteProcThreadAttributeList(list);
            Marshal.FreeHGlobal(list);
            if (!ok) {
                Console.Error.WriteLine(error == ErrorAccessDenied ? "sandbox: access denied — the executable or working directory is not readable by the AppContainer sandbox." : "sandbox: process creation failed (" + error + ")");
                return 137;
            }
            try {
                ResumeThread(process.hThread);
                WaitForSingleObject(process.hProcess, 0xFFFFFFFF);
                uint exitCode;
                GetExitCodeProcess(process.hProcess, out exitCode);
                return unchecked((int)exitCode);
            } finally {
                CloseHandle(process.hThread);
                CloseHandle(process.hProcess);
            }
        }

        static string Quote(string value) {
            if (String.IsNullOrEmpty(value)) return "\"\"";
            if (value.All(c => !Char.IsWhiteSpace(c) && c != '"')) return value;
            var builder = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { builder.Append('\\', slashes * 2 + 1); builder.Append(c); }
                else { builder.Append('\\', slashes); builder.Append(c); }
                slashes = 0;
            }
            builder.Append('\\', slashes * 2);
            builder.Append('"');
            return builder.ToString();
        }

        [StructLayout(LayoutKind.Sequential)] struct SecurityCapabilities { public IntPtr AppContainerSid; public IntPtr Capabilities; public uint CapabilityCount; public uint Reserved; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct StartupInfo {
            public uint cb; public string lpReserved, lpDesktop, lpTitle;
            public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public ushort wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr lpAttributeList; }
        [StructLayout(LayoutKind.Sequential)] struct ProcessInformation { public IntPtr hProcess, hThread; public uint dwProcessId, dwThreadId; }

        [DllImport("userenv.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern int CreateAppContainerProfile(string name, string displayName, string description, IntPtr capabilities, uint capabilityCount, out IntPtr sid);
        [DllImport("userenv.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern int DeriveAppContainerSidFromAppContainerName(string name, out IntPtr sid);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool ConvertSidToStringSid(IntPtr sid, out IntPtr text);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr value);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool InitializeProcThreadAttributeList(IntPtr list, uint count, uint flags, ref IntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, ref SecurityCapabilities value, IntPtr size, IntPtr previous, IntPtr returnSize);
        [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateProcess(string application, string commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string currentDirectory, ref StartupInfoEx startup, out ProcessInformation process);
        [DllImport("kernel32.dll", SetLastError = true)] static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int number);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    }
}
