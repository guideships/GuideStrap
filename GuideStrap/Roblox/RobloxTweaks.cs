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
    // Windows-side and Roblox-settings tweaks used at launch
    static class RobloxTweaks
    {
        static bool RobloxRunning()
        {
            var procs = Process.GetProcessesByName("RobloxPlayerBeta");
            foreach (var p in procs) p.Dispose();
            return procs.Length > 0;
        }

        // Roblox's own max frame rate setting (the same one as in its menu). Only written while Roblox is
        // closed, because Roblox saves this file when it exits and would overwrite the change.
        public static void SetFpsCap(int fps)
        {
            try
            {
                if (RobloxRunning()) return;
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");
                if (!Directory.Exists(dir)) return;
                string? file = Directory.GetFiles(dir, "GlobalBasicSettings_*.xml")
                    .Where(f => !Path.GetFileName(f).Contains("Studio", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .FirstOrDefault();
                if (file == null) return;
                string xml = File.ReadAllText(file);
                var re = new Regex(@"(<int name=""FramerateCap"">)-?\d+(</int>)");
                if (!re.IsMatch(xml)) return;
                string updated = re.Replace(xml, m => m.Groups[1].Value + fps + m.Groups[2].Value, 1);
                if (updated != xml) File.WriteAllText(file, updated);
            }
            catch { }
        }

        // Gives Roblox more CPU time. Checks for a little while because Roblox can take a moment to start.
        public static void BoostPriority(ProcessPriorityClass level)
        {
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 15; i++)
                {
                    await Task.Delay(2000);
                    foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
                    {
                        using (p)
                        {
                            try { if (!p.HasExited && p.PriorityClass != level) p.PriorityClass = level; }
                            catch { }
                        }
                    }
                }
            });
        }
    }
}
