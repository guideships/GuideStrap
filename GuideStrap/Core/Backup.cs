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
    class BackupInfo
    {
        public string Base { get; set; } = "";
        public string Version { get; set; } = "";
    }

    // One file with your settings, mods, favorites and playtime. Saved logins are left out:
    // they're encrypted for this Windows user and wouldn't work anywhere else.
    static class Backup
    {
        static readonly string[] Folders = { "Mods", "ManagedMods", "SoundPacks" };
        static readonly string[] FilePatterns = { "settings.json", "favorites.json", "recent.json", "playtime.json", "CustomFont.*", "launcher-background.*" };

        static bool Matches(string name, string pattern) => pattern.EndsWith(".*")
            ? name.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);

        public static void Export(string target)
        {
            string tmp = target + ".tmp";
            try
            {
                if (File.Exists(tmp)) File.Delete(tmp);
                using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
                {
                    if (Directory.Exists(Paths.Base))
                        foreach (var f in Directory.GetFiles(Paths.Base))
                            if (FilePatterns.Any(p => Matches(Path.GetFileName(f), p)))
                                zip.CreateEntryFromFile(f, Path.GetFileName(f), CompressionLevel.Optimal);
                    foreach (var folder in Folders)
                    {
                        string dir = Path.Combine(Paths.Base, folder);
                        if (!Directory.Exists(dir)) continue;
                        foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                            zip.CreateEntryFromFile(f, folder + "/" + Path.GetRelativePath(dir, f).Replace('\\', '/'), CompressionLevel.Optimal);
                    }
                    var info = zip.CreateEntry("backup.json");
                    using var w = new StreamWriter(info.Open());
                    w.Write(JsonSerializer.Serialize(new BackupInfo { Base = Paths.Base, Version = AppInfo.Version }));
                }
                File.Move(tmp, target, true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        // Puts a backup back. Your saved accounts and this PC's own settings (like Roblox links) stay as they are.
        public static void Import(string source)
        {
            using var zip = ZipFile.OpenRead(source);
            var infoEntry = zip.GetEntry("backup.json") ?? throw new Exception(L.T("That file isn't a GuideStrap backup."));
            BackupInfo? info;
            using (var r = new StreamReader(infoEntry.Open()))
            {
                try { info = JsonSerializer.Deserialize<BackupInfo>(r.ReadToEnd()); }
                catch { info = null; }
            }
            if (info == null) throw new Exception(L.T("That file isn't a GuideStrap backup."));

            var current = AppSettings.Load();
            Directory.CreateDirectory(Paths.Base);
            string baseFull = Path.GetFullPath(Paths.Base).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            // folders in the backup replace the ones here
            foreach (var folder in Folders)
            {
                string dir = Path.Combine(Paths.Base, folder);
                if (Directory.Exists(dir) && zip.Entries.Any(e => e.FullName.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)))
                    try { Directory.Delete(dir, true); } catch { }
            }
            foreach (var entry in zip.Entries)
            {
                string name = entry.FullName.Replace('\\', '/');
                if (name == "backup.json" || name.EndsWith("/")) continue;
                bool allowed = Folders.Any(f => name.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase))
                               || (!name.Contains('/') && FilePatterns.Any(p => Matches(name, p)));
                if (!allowed) continue;
                string dest = Path.GetFullPath(Path.Combine(Paths.Base, name));
                if (!dest.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase)) continue;   // never outside the GuideStrap folder
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, true);
            }

            // the backup may come from another PC or Windows user: point its file paths at this GuideStrap folder
            var s = AppSettings.Load();
            string oldBase = info.Base ?? "";
            string? Fix(string? p) => p != null && oldBase.Length > 0 && p.StartsWith(oldBase, StringComparison.OrdinalIgnoreCase)
                ? Paths.Base + p[oldBase.Length..] : p;
            s.FontFile = Fix(s.FontFile);
            s.DeathSoundFile = Fix(s.DeathSoundFile);
            s.LauncherBackground = Fix(s.LauncherBackground);
            s.Flags = (s.Flags ?? new()).Where(kv => FF.IsAllowed(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            s.ActiveAccountId = current.ActiveAccountId;
            s.PreviousLinkHandlers = current.PreviousLinkHandlers;
            s.LastSeenVersion = current.LastSeenVersion;
            s.LastUpdateCheck = current.LastUpdateCheck;
            s.Save();
        }
    }
}
