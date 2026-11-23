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
    // Install / reinstall / uninstall.
    // The installed copy (Start menu, desktop shortcut, Roblox links) never shows setup.
    // Running any other copy (like the downloaded .exe) shows setup, or Reinstall/Repair if already installed.
    static class Setup
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GuideStrap";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool StartsWithWindows
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue("GuideStrap") != null;
                }
                catch { return false; }
            }
        }

        // Starts the installed GuideStrap in the tray when Windows starts
        public static void SetStartup(bool on)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (on) key.SetValue("GuideStrap", $"\"{InstallExe}\" --tray");
                else key.DeleteValue("GuideStrap", false);
            }
            catch { }
        }
        public static string InstallExe => Path.Combine(Paths.Base, "GuideStrap.exe");
        static string StartMenuLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "GuideStrap.lnk");
        static string DesktopLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GuideStrap.lnk");

        static bool RunningInstalled =>
            string.Equals(Path.GetFullPath(Environment.ProcessPath ?? ""), Path.GetFullPath(InstallExe), StringComparison.OrdinalIgnoreCase);

        public static string? InstalledVersion()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
                return key?.GetValue("DisplayVersion") as string;
            }
            catch { return null; }
        }

        public static bool IsInstalled => File.Exists(InstallExe) && InstalledVersion() != null;
        public static bool HasDesktopShortcut => File.Exists(DesktopLink);

        public static bool NeedsSetup()
        {
#if DEBUG
            return false;   // never while developing in Visual Studio
#else
            return !RunningInstalled;
#endif
        }

        // A Roblox link opened a copy that isn't the installed one: pass the link to the installed copy.
        public static bool HandOffIfInstalled(string[] args)
        {
#if DEBUG
            return false;
#else
            bool hasLink = args.Any(a => a.StartsWith("roblox", StringComparison.OrdinalIgnoreCase));
            if (!hasLink || RunningInstalled || !IsInstalled) return false;
            var psi = new ProcessStartInfo(InstallExe) { UseShellExecute = false, WorkingDirectory = Paths.Base };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            return true;
#endif
        }

        public static bool OtherCopiesRunning()
        {
            int me = Environment.ProcessId;
            return Process.GetProcessesByName("GuideStrap").Any(p => { using (p) return p.Id != me; });
        }

        public static void CloseOtherCopies()
        {
            int me = Environment.ProcessId;
            foreach (var p in Process.GetProcessesByName("GuideStrap"))
            {
                using (p)
                {
                    if (p.Id == me) continue;
                    try { p.Kill(); p.WaitForExit(4000); } catch { }
                }
            }
        }

        public static void Install(string language, bool desktopShortcut, bool openLinks, bool repairRoblox, bool startWithWindows)
        {
            Directory.CreateDirectory(Paths.Base);
            if (!RunningInstalled)
            {
                // the old copy can stay locked for a moment after closing, so retry briefly
                for (int attempt = 0; ; attempt++)
                {
                    try { File.Copy(Environment.ProcessPath!, InstallExe, true); break; }
                    catch (IOException) when (attempt < 10) { Thread.Sleep(300); }
                }
            }

            CreateShortcut(StartMenuLink);
            SetStartup(startWithWindows);
            if (desktopShortcut) CreateShortcut(DesktopLink);
            else try { if (File.Exists(DesktopLink)) File.Delete(DesktopLink); } catch { }
            WriteEntry();

            var settings = AppSettings.Load();
            settings.Language = language;
            if (openLinks) LinkHandler.Register(settings, InstallExe);
            else if (LinkHandler.PointsTo(InstallExe)) LinkHandler.Restore(settings);
            settings.Save();

            if (repairRoblox)
                try { if (Directory.Exists(Paths.Versions)) Directory.Delete(Paths.Versions, true); } catch { }
        }

        public static void StartInstalled() =>
            Process.Start(new ProcessStartInfo(InstallExe) { UseShellExecute = true, WorkingDirectory = Paths.Base });

        // keeps the version shown in Windows' app list right after self-updates
        public static void RefreshEntry()
        {
            try
            {
                if (!RunningInstalled || InstalledVersion() == AppInfo.Version) return;
                WriteEntry();
                // after an update, remake the shortcuts and tell Windows to reload icons,
                // otherwise the desktop and Start menu keep showing the old icon
                if (File.Exists(DesktopLink)) CreateShortcut(DesktopLink);
                if (File.Exists(StartMenuLink)) CreateShortcut(StartMenuLink);
                // a GuideStrap pinned to the taskbar is a shortcut too
                string pinned = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar", "GuideStrap.lnk");
                if (File.Exists(pinned)) CreateShortcut(pinned);
                NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);   // SHCNE_ASSOCCHANGED
            }
            catch { }
        }

        static void WriteEntry()
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
            key.SetValue("DisplayName", "GuideStrap");
            key.SetValue("DisplayVersion", AppInfo.Version);
            key.SetValue("Publisher", "Guide");
            key.SetValue("DisplayIcon", InstallExe);
            key.SetValue("InstallLocation", Paths.Base);
            key.SetValue("UninstallString", $"\"{InstallExe}\" --uninstall");
            key.SetValue("URLInfoAbout", "https://github.com/guideships/GuideStrap");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            try { key.SetValue("EstimatedSize", (int)(new FileInfo(InstallExe).Length / 1024), RegistryValueKind.DWord); } catch { }
        }

        static void CreateShortcut(string path)
        {
            try
            {
                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return;
                dynamic shell = Activator.CreateInstance(type)!;
                dynamic link = shell.CreateShortcut(path);
                link.TargetPath = InstallExe;
                link.WorkingDirectory = Paths.Base;
                link.IconLocation = InstallExe + ",0";
                link.Description = "GuideStrap";
                link.Save();
                Marshal.FinalReleaseComObject(link);
                Marshal.FinalReleaseComObject(shell);
            }
            catch { }
        }

        // Returns true if GuideStrap was removed (the caller should then exit)
        public static bool Uninstall(IWin32Window? owner = null)
        {
            var settings = AppSettings.Load();
            L.Lang = settings.Language.Length > 0 ? settings.Language : L.SystemLanguage();
            Theme.Apply(settings.ThemeName, settings.Accent);

            if (OtherCopiesRunning())
            {
                ThemedDialog.Prompt(owner, "Close GuideStrap first", "GuideStrap is still running (check the tray near the clock). Close it, then try again.", "OK");
                return false;
            }
            bool keep;
            using (var ask = new ThemedDialog("Uninstall GuideStrap?",
                       "This removes GuideStrap and its copy of Roblox. Normal Roblox stays installed.",
                       new[] { "Uninstall", "Cancel" },
                       "Keep my settings, accounts and mods in case I reinstall"))
            {
                if (owner == null) ask.StartPosition = FormStartPosition.CenterScreen;
                ask.ShowDialog(owner);
                if (ask.Choice != 0) return false;
                keep = ask.Checked;
            }

            SetStartup(false);
            try { if (LinkHandler.PointsTo(InstallExe)) LinkHandler.Restore(settings); } catch { }
            foreach (var link in new[] { StartMenuLink, DesktopLink })
                try { if (File.Exists(link)) File.Delete(link); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }

            ThemedDialog.Prompt(owner, "GuideStrap", keep
                ? "GuideStrap was uninstalled. Your settings, accounts and mods are kept for next time."
                : "GuideStrap was uninstalled.", "OK");
            // deleted a moment after we exit (this .exe can't delete itself while running).
            // Keeping settings: only the app, its copy of Roblox and downloaded caches go.
            string q(string path) => "\"" + path + "\"";
            string remove = keep
                ? $"del /f /q {q(InstallExe)} & del /f /q {q(InstallExe + ".old")} & rmdir /s /q {q(Paths.Versions)} & rmdir /s /q {q(Paths.Downloads)}" +
                  $" & rmdir /s /q {q(Paths.Avatars)} & rmdir /s /q {q(Paths.Icons)}"
                : $"rmdir /s /q {q(Paths.Base)}";
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & {remove}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            });
            return true;
        }
    }
}
