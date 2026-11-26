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

    // Lets more than one Roblox window run. Roblox keeps one copy by creating ROBLOX_singletonMutex and
    // ROBLOX_singletonEvent; a new Roblox finds them and closes the old window. GuideStrap claims both names first
    // (as mutexes, so Roblox can't create its event under that name), which means Roblox never gets the lock.
    // If an open Roblox already has it, GuideStrap closes just those two handles in it (like Process Explorer's
    // "Close Handle") and then takes the lock.
    static class MultiInstance
    {
        static volatile bool owned;

        public static bool Owned => owned;

        // Claims the lock on its own thread (a mutex belongs to the thread that took it, so that thread stays alive
        // until GuideStrap exits). True when GuideStrap has it, or another GuideStrap does and no Roblox is open.
        public static bool Hold()
        {
            if (owned) return true;
            bool ok = false;
            using var ready = new ManualResetEventSlim(false);
            var t = new Thread(() =>
            {
                var taken = new List<Mutex>();
                try
                {
                    foreach (var name in new[] { "ROBLOX_singletonMutex", "ROBLOX_singletonEvent" })
                    {
                        // throws if Roblox already made this name as its event: then Roblox has the lock
                        var m = new Mutex(true, name, out bool created);
                        if (!created)
                        {
                            bool got;
                            try { got = m.WaitOne(0); } catch (AbandonedMutexException) { got = true; }   // its last owner closed
                            if (!got) { m.Dispose(); throw new InvalidOperationException("taken"); }
                        }
                        taken.Add(m);
                    }
                    ok = true;
                }
                catch
                {
                    foreach (var m in taken) { try { m.ReleaseMutex(); } catch { } m.Dispose(); }
                }
                ready.Set();
                if (ok) Thread.Sleep(Timeout.Infinite);
            }) { IsBackground = true, Name = "RobloxSingletonHolder" };
            t.Start();
            ready.Wait(3000);
            owned = ok;
            // someone else holds it: fine if that's another GuideStrap (no Roblox open), not if it's Roblox
            return ok || !AnyRoblox();
        }

        // Before a launch: claim the lock, closing Roblox's own background copy (the one Roblox starts with Windows,
        // no game window) if that's what holds it. False when an open Roblox game window still has it.
        public static bool Ensure()
        {
            if (Hold()) return true;
            if (CloseBackgroundRoblox())
            {
                Thread.Sleep(600);
                if (Hold()) return true;
            }
            // an open Roblox game has the lock: close its lock handles, then take the lock ourselves
            if (RobloxLockCloser.CloseAll() > 0)
            {
                Thread.Sleep(200);
                return Hold();
            }
            return false;
        }

        public static bool GameOpen() => Roblox(p => p.MainWindowHandle != IntPtr.Zero);
        static bool AnyRoblox() => Roblox(_ => true);

        static bool Roblox(Func<Process, bool> test)
        {
            bool any = false;
            foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                try { if (!any && !p.HasExited && test(p)) any = true; } catch { }
                p.Dispose();
            }
            return any;
        }

        // Roblox's background copy has no window; closing it is harmless (Windows starts it again next time)
        static bool CloseBackgroundRoblox()
        {
            bool closed = false;
            foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                try
                {
                    if (!p.HasExited && p.MainWindowHandle == IntPtr.Zero && (DateTime.Now - p.StartTime).TotalSeconds > 20)
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                        closed = true;
                    }
                }
                catch { }
                p.Dispose();
            }
            return closed;
        }

        // Closes every Roblox window (the user said yes), then claims the lock
        public static bool CloseAllAndHold()
        {
            foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                try { if (!p.HasExited) { p.Kill(); p.WaitForExit(5000); } } catch { }
                p.Dispose();
            }
            Thread.Sleep(600);
            return Hold();
        }
    }

    // Closes ROBLOX_singletonEvent / ROBLOX_singletonMutex inside open Roblox windows, the same thing
    // Process Explorer's "Close Handle" does. Only those two named handles, only in RobloxPlayerBeta.
    static class RobloxLockCloser
    {
        [DllImport("ntdll.dll")] static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int length, out int returnLength);
        [DllImport("ntdll.dll")] static extern int NtQueryObject(IntPtr handle, int infoClass, IntPtr info, int length, out int returnLength);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr sourceHandle, IntPtr targetProcess, out IntPtr targetHandle,
            uint access, bool inherit, uint options);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

        const int SystemExtendedHandleInformation = 64;
        const int ObjectNameInformation = 1;
        const uint PROCESS_DUP_HANDLE = 0x40;
        const uint DUPLICATE_CLOSE_SOURCE = 1, DUPLICATE_SAME_ACCESS = 2;
        const int STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);
        const int EntrySize = 40;   // SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX on 64-bit Windows

        // returns how many handles were closed
        public static int CloseAll()
        {
            if (IntPtr.Size != 8) return 0;
            var pids = new HashSet<long>();
            foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                try { if (!p.HasExited) pids.Add(p.Id); } catch { }
                p.Dispose();
            }
            if (pids.Count == 0) return 0;

            // an unnamed event and mutex of our own show which type numbers mean "Event" and "Mutant" on this PC,
            // so only those handles in Roblox get looked at
            using var ev = new EventWaitHandle(false, EventResetMode.ManualReset);
            using var mx = new Mutex(false);
            long me = Environment.ProcessId;
            long evHandle = ev.SafeWaitHandle.DangerousGetHandle().ToInt64();
            long mxHandle = mx.SafeWaitHandle.DangerousGetHandle().ToInt64();

            IntPtr buf = IntPtr.Zero;
            var procs = new Dictionary<long, IntPtr>();
            try
            {
                int size = 1 << 20;
                while (true)
                {
                    buf = Marshal.AllocHGlobal(size);
                    int status = NtQuerySystemInformation(SystemExtendedHandleInformation, buf, size, out int need);
                    if (status == STATUS_INFO_LENGTH_MISMATCH)
                    {
                        Marshal.FreeHGlobal(buf); buf = IntPtr.Zero;
                        size = Math.Max(size * 2, need + (1 << 16));
                        if (size > (512 << 20)) return 0;
                        continue;
                    }
                    if (status != 0) return 0;
                    break;
                }
                long count = Marshal.ReadIntPtr(buf).ToInt64();
                IntPtr first = IntPtr.Add(buf, IntPtr.Size * 2);

                int evType = -1, mxType = -1;
                for (long i = 0; i < count; i++)
                {
                    IntPtr e = IntPtr.Add(first, checked((int)(i * EntrySize)));
                    if (Marshal.ReadInt64(e, 8) != me) continue;
                    long hv = Marshal.ReadInt64(e, 16);
                    int type = Marshal.ReadInt16(e, 30) & 0xFFFF;
                    if (hv == evHandle) evType = type;
                    else if (hv == mxHandle) mxType = type;
                }
                if (evType < 0 && mxType < 0) return 0;

                int closed = 0;
                for (long i = 0; i < count; i++)
                {
                    IntPtr e = IntPtr.Add(first, checked((int)(i * EntrySize)));
                    long pid = Marshal.ReadInt64(e, 8);
                    if (!pids.Contains(pid)) continue;
                    int type = Marshal.ReadInt16(e, 30) & 0xFFFF;
                    if (type != evType && type != mxType) continue;
                    IntPtr hv = new IntPtr(Marshal.ReadInt64(e, 16));

                    if (!procs.TryGetValue(pid, out var hp))
                    {
                        hp = OpenProcess(PROCESS_DUP_HANDLE, false, (int)pid);
                        procs[pid] = hp;
                    }
                    if (hp == IntPtr.Zero) continue;

                    if (!DuplicateHandle(hp, hv, GetCurrentProcess(), out var copy, 0, false, DUPLICATE_SAME_ACCESS)) continue;
                    string? name = ObjectName(copy);
                    CloseHandle(copy);
                    if (name == null) continue;
                    if (name.EndsWith("\\ROBLOX_singletonEvent", StringComparison.Ordinal) ||
                        name.EndsWith("\\ROBLOX_singletonMutex", StringComparison.Ordinal))
                    {
                        // closes the handle inside Roblox (no copy is made)
                        if (DuplicateHandle(hp, hv, IntPtr.Zero, out _, 0, false, DUPLICATE_CLOSE_SOURCE)) closed++;
                    }
                }
                return closed;
            }
            catch { return 0; }
            finally
            {
                foreach (var h in procs.Values) if (h != IntPtr.Zero) CloseHandle(h);
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
            }
        }

        // the object's full name, like \Sessions\1\BaseNamedObjects\ROBLOX_singletonEvent (safe for events and mutexes)
        static string? ObjectName(IntPtr handle)
        {
            const int len = 2048;
            IntPtr b = Marshal.AllocHGlobal(len);
            try
            {
                if (NtQueryObject(handle, ObjectNameInformation, b, len, out _) != 0) return null;
                int bytes = Marshal.ReadInt16(b) & 0xFFFF;
                IntPtr text = Marshal.ReadIntPtr(b, IntPtr.Size);
                return bytes == 0 || text == IntPtr.Zero ? null : Marshal.PtrToStringUni(text, bytes / 2);
            }
            finally { Marshal.FreeHGlobal(b); }
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
