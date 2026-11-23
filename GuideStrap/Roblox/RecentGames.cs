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
    // Recently played games (saved list + cached icons)
    class RecentGame
    {
        public long PlaceId { get; set; }
        public string Name { get; set; } = "";
        public string Creator { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public DateTime LastPlayed { get; set; }
    }

    static class RecentGames
    {
        static string FilePath => Path.Combine(Paths.Base, "recent.json");
        public static int Version { get; private set; }

        public static List<RecentGame> Load()
        {
            try { return JsonSerializer.Deserialize<List<RecentGame>>(File.ReadAllText(FilePath)) ?? new(); }
            catch { return new(); }
        }

        public static void Add(RecentGame game)
        {
            var list = Load();
            list.RemoveAll(g => g.PlaceId == game.PlaceId);
            list.Insert(0, game);
            if (list.Count > 12) list.RemoveRange(12, list.Count - 12);
            try
            {
                Directory.CreateDirectory(Paths.Base);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
            }
            catch { }
            Version++;
        }

        public static async Task<Image?> GetIconAsync(RecentGame game)
        {
            string path = Path.Combine(Paths.Icons, game.PlaceId + ".png");
            try
            {
                if (!File.Exists(path))
                {
                    if (game.IconUrl.Length == 0) return null;
                    Directory.CreateDirectory(Paths.Icons);
                    await File.WriteAllBytesAsync(path, await RobloxApi.Http.GetByteArrayAsync(game.IconUrl));
                }
                using var fs = File.OpenRead(path);
                using var img = Image.FromStream(fs);
                return new Bitmap(img, 128, 128);
            }
            catch { return null; }
        }
    }
}
