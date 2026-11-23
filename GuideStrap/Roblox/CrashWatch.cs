using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using DiscordRPC;

namespace GuideStrap
{
    class CrashInfo
    {
        public uint Code { get; init; }
        public bool WhileStarting { get; init; }
        public string? Clue { get; init; }   // "gpu" or "memory", read from Roblox's log
    }

    // Notices when Roblox closes with an error code instead of closing normally.
    // Keeps a lightweight handle to each Roblox process so the exit code can be read after it closes.
    static class CrashWatch
    {
        const uint QueryLimited = 0x1000, StillActive = 259;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetExitCodeProcess(IntPtr process, out uint code);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr handle);

        static readonly Dictionary<int, (IntPtr Handle, DateTime Seen)> tracked = new();
        static readonly List<(uint Code, bool Early)> exits = new();
        static readonly Queue<string> lines = new();

        // Normal closes, leaving a game and Task Manager use small codes. Real crashes use Windows error codes.
        // 0xFFFFFFFF (-1) is what tools use when they force-close a program, so it doesn't count.
        public static bool IsCrash(uint code) => code >= 0x80000000 && code != 0xFFFFFFFF;

        public static void Begin()
        {
            CloseAll();
            exits.Clear();
            lines.Clear();
        }

        public static void Line(string line)
        {
            lines.Enqueue(line);
            while (lines.Count > 80) lines.Dequeue();
        }

        // Returns true while at least one Roblox is running (one process scan does both jobs)
        public static bool Poll()
        {
            bool running = false;
            try
            {
                foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
                {
                    running = true;
                    using (p)
                    {
                        if (tracked.ContainsKey(p.Id)) continue;
                        IntPtr h = OpenProcess(QueryLimited, false, p.Id);
                        if (h != IntPtr.Zero) tracked[p.Id] = (h, DateTime.Now);
                    }
                }
                foreach (var kv in tracked.ToList())
                {
                    var (handle, seen) = kv.Value;
                    bool ok = GetExitCodeProcess(handle, out uint code);
                    if (ok && code == StillActive) continue;
                    if (ok) exits.Add((code, DateTime.Now - seen < TimeSpan.FromSeconds(25)));
                    CloseHandle(handle);
                    tracked.Remove(kv.Key);
                }
            }
            catch { return true; }   // unsure: keep watching rather than stop early
            return running;
        }

        public static CrashInfo? End(bool report)
        {
            if (report) Poll();
            CloseAll();
            if (!report) return null;
            var crashes = exits.Where(e => IsCrash(e.Code)).ToList();
            if (crashes.Count == 0) return null;
            var last = crashes[^1];
            return new CrashInfo { Code = last.Code, WhileStarting = last.Early, Clue = Clue() };
        }

        // For a crash right after launch, before the watcher started
        public static CrashInfo? FromExitCode(uint code) =>
            IsCrash(code) ? new CrashInfo { Code = code, WhileStarting = true } : null;

        static void CloseAll()
        {
            foreach (var t in tracked.Values) CloseHandle(t.Handle);
            tracked.Clear();
        }

        static string? Clue()
        {
            string text = string.Join("\n", lines).ToLowerInvariant();
            if (text.Contains("device_removed") || text.Contains("device removed") || text.Contains("device lost") || text.Contains("device_lost"))
                return "gpu";
            if (text.Contains("out of memory") || text.Contains("outofmemory"))
                return "memory";
            return null;
        }
    }
}
