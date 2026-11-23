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
    class ServerInfo
    {
        public long PlaceId { get; set; }
        public string JobId { get; set; } = "";
        public DateTime Time { get; set; }
    }

    // The last server you were in (read from Roblox's log), for Rejoin and the crash helper
    static class LastServer
    {
        static string FilePath => Path.Combine(Paths.Base, "lastserver.json");

        public static void Save(long placeId, string jobId)
        {
            try
            {
                Directory.CreateDirectory(Paths.Base);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(new ServerInfo { PlaceId = placeId, JobId = jobId, Time = DateTime.Now }));
            }
            catch { }
        }

        public static ServerInfo? Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var s = JsonSerializer.Deserialize<ServerInfo>(File.ReadAllText(FilePath));
                return s != null && s.PlaceId > 0 && Guid.TryParse(s.JobId, out _) ? s : null;
            }
            catch { return null; }
        }
    }
}
