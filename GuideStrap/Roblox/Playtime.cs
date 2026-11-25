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
    class PlaySession
    {
        public long PlaceId { get; set; }
        public string Name { get; set; } = "";
        public DateTime Start { get; set; }   // your PC's time
        public DateTime End { get; set; }
    }

    class PlaytimeData
    {
        public List<PlaySession> Sessions { get; set; } = new();          // the last 60 days
        public Dictionary<string, double> Totals { get; set; } = new();   // game name -> seconds, all time
    }

    // How long you play each game. Read from Roblox's log while GuideStrap stays in the tray.
    static class Playtime
    {
        static string FilePath => Path.Combine(Paths.Base, "playtime.json");
        static readonly object gate = new();
        public static int Version { get; private set; }

        public static PlaytimeData Load()
        {
            lock (gate)
            {
                try
                {
                    if (!File.Exists(FilePath)) return new();
                    var d = JsonSerializer.Deserialize<PlaytimeData>(File.ReadAllText(FilePath)) ?? new();
                    d.Sessions ??= new();
                    d.Totals ??= new();
                    d.Sessions.RemoveAll(s => s == null || s.End < s.Start);
                    return d;
                }
                catch { return new(); }
            }
        }

        public static void Record(long placeId, string? name, DateTime startUtc, DateTime endUtc)
        {
            var start = startUtc.ToLocalTime();
            var end = endUtc.ToLocalTime();
            double seconds = (end - start).TotalSeconds;
            if (placeId == 0 || seconds < 15 || seconds > 172800) return;
            if (string.IsNullOrWhiteSpace(name))
                name = RecentGames.Load().FirstOrDefault(g => g.PlaceId == placeId)?.Name;
            if (string.IsNullOrWhiteSpace(name)) name = "Game " + placeId;
            lock (gate)
            {
                var d = Load();
                d.Sessions.Add(new PlaySession { PlaceId = placeId, Name = name, Start = start, End = end });
                var cutoff = DateTime.Now.AddDays(-60);
                d.Sessions.RemoveAll(s => s.End < cutoff);
                if (d.Sessions.Count > 3000) d.Sessions.RemoveRange(0, d.Sessions.Count - 3000);
                d.Totals[name] = d.Totals.GetValueOrDefault(name) + seconds;
                try
                {
                    Directory.CreateDirectory(Paths.Base);
                    File.WriteAllText(FilePath, JsonSerializer.Serialize(d));
                }
                catch { }
                Version++;
            }
        }

        // Seconds played on each of the last 7 days (oldest first, today last)
        public static double[] LastWeekByDay(PlaytimeData d)
        {
            var result = new double[7];
            var today = DateTime.Today;
            for (int i = 0; i < 7; i++)
            {
                var from = today.AddDays(i - 6);
                var to = from.AddDays(1);
                foreach (var s in d.Sessions) result[i] += Overlap(s, from, to);
            }
            return result;
        }

        public static List<(string Name, double Seconds)> TopThisWeek(PlaytimeData d, int count)
        {
            var from = DateTime.Today.AddDays(-6);
            var to = DateTime.Today.AddDays(1);
            return d.Sessions
                .GroupBy(s => s.Name)
                .Select(g => (Name: g.Key, Seconds: g.Sum(s => Overlap(s, from, to))))
                .Where(x => x.Seconds >= 60)
                .OrderByDescending(x => x.Seconds)
                .Take(count)
                .ToList();
        }

        static double Overlap(PlaySession s, DateTime from, DateTime to)
        {
            var a = s.Start > from ? s.Start : from;
            var b = s.End < to ? s.End : to;
            return b > a ? (b - a).TotalSeconds : 0;
        }

        // The game you were in at that moment (used to sort screenshots)
        public static string? GameAt(PlaytimeData d, DateTime local)
        {
            foreach (var s in d.Sessions)
                if (local >= s.Start.AddMinutes(-1) && local <= s.End.AddMinutes(1)) return s.Name;
            return null;
        }

        public static string Format(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{(int)t.TotalMinutes} min";
        }
    }

    // Moves Roblox screenshots into a folder for each game: Pictures\Roblox\<game name>
    static class ScreenshotSorter
    {
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Roblox");

        public static int Sort()
        {
            if (!Directory.Exists(Folder)) return 0;
            var data = Playtime.Load();
            if (data.Sessions.Count == 0) return 0;
            int moved = 0;
            foreach (var file in Directory.GetFiles(Folder, "*.png", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    string? game = Playtime.GameAt(data, File.GetLastWriteTime(file));
                    if (game == null) continue;
                    string dir = Path.Combine(Folder, SafeName(game));
                    Directory.CreateDirectory(dir);
                    string dest = Path.Combine(dir, Path.GetFileName(file));
                    if (File.Exists(dest)) continue;
                    File.Move(file, dest);
                    moved++;
                }
                catch { }
            }
            return moved;
        }

        public static string SafeName(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            string s = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.').Trim();
            if (s.Length > 60) s = s[..60].Trim();
            return s.Length == 0 ? "Game" : s;
        }
    }
}
