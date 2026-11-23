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
    // Gives memory back to Windows when GuideStrap is only sitting in the tray
    static class MemoryTrim
    {
        [DllImport("psapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EmptyWorkingSet(IntPtr process);

        public static void Now()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using var me = Process.GetCurrentProcess();
                EmptyWorkingSet(me.Handle);
            }
            catch { }
        }
    }

    // Which app opens roblox:// links (Play buttons on the website). Turning it off restores normal Roblox.
    static class LinkHandler
    {
        static readonly string[] Protocols = { "roblox-player", "roblox" };

        static string ReadCommand(string protocol)
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{protocol}\shell\open\command");
            return key?.GetValue("") as string ?? "";
        }

        static void WriteCommand(string protocol, string command)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}");
            key.SetValue("", "URL: Roblox Protocol");
            key.SetValue("URL Protocol", "");
            using var cmd = key.CreateSubKey(@"shell\open\command");
            cmd.SetValue("", command);
        }

        static string Exe => Environment.ProcessPath ?? "GuideStrap.exe";

        public static bool IsOurs() => Protocols.All(p => ReadCommand(p).Contains(Exe, StringComparison.OrdinalIgnoreCase));

        public static void Register(AppSettings settings, string? exePath = null)
        {
            string exe = exePath ?? Exe;
            foreach (var p in Protocols)
            {
                string old = ReadCommand(p);
                if (old.Length > 0 && !old.Contains("GuideStrap", StringComparison.OrdinalIgnoreCase))
                    settings.PreviousLinkHandlers[p] = old;
                WriteCommand(p, $"\"{exe}\" \"%1\"");
            }
        }

        public static bool PointsTo(string exePath) => Protocols.Any(p => ReadCommand(p).Contains(exePath, StringComparison.OrdinalIgnoreCase));

        public static void Restore(AppSettings settings)
        {
            string? official = FindRobloxLauncher();
            foreach (var p in Protocols)
            {
                string? old = settings.PreviousLinkHandlers.GetValueOrDefault(p);
                if (old != null && !old.Contains("GuideStrap", StringComparison.OrdinalIgnoreCase)) WriteCommand(p, old);
                else if (official != null) WriteCommand(p, $"\"{official}\" %1");
                else Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{p}", false);
            }
        }

        static string? FindRobloxLauncher()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
            if (!Directory.Exists(dir)) return null;
            return new DirectoryInfo(dir).GetDirectories()
                .OrderByDescending(d => d.LastWriteTime)
                .Select(d => Path.Combine(d.FullName, "RobloxPlayerBeta.exe"))
                .FirstOrDefault(File.Exists);
        }
    }

    // Lets more than one Roblox window run by claiming Roblox's "only one copy" lock first
    static class MultiInstance
    {
        static readonly List<IDisposable> held = new();

        public static void Hold()
        {
            if (held.Count > 0) return;
            foreach (var name in new[] { "ROBLOX_singletonMutex", "ROBLOX_singletonEvent" })
            {
                try { held.Add(new Mutex(true, name)); } catch { }
            }
        }
    }

    static class Cleaner
    {
        public static long ClearCache()
        {
            long freed = 0;
            void Delete(string dir, Func<FileInfo, bool> keepIf)
            {
                if (!Directory.Exists(dir)) return;
                foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (keepIf(f)) continue;
                    try { long len = f.Length; f.Delete(); freed += len; } catch { }
                }
            }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Delete(Path.Combine(Path.GetTempPath(), "Roblox"), _ => false);
            Delete(Path.Combine(local, "Roblox", "logs"), f => f.LastWriteTime > DateTime.Now.AddDays(-3));
            Delete(Paths.Avatars, _ => false);
            Delete(Paths.Icons, _ => false);
            return freed;
        }
    }
}
