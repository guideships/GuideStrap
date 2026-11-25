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
    // Self-update from GitHub releases (only when AppInfo.UpdateRepo is set)
    static class Updater
    {
        public static bool Configured => AppInfo.UpdateRepo.Length > 0;

        public static async Task<(string Version, string Url)?> CheckAsync()
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{AppInfo.UpdateRepo}/releases/latest");
                req.Headers.UserAgent.ParseAdd("GuideStrap");
                using var resp = await RobloxApi.Http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                string tag = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
                if (!System.Version.TryParse(tag, out var latest) || !System.Version.TryParse(AppInfo.Version, out var mine) || latest <= mine)
                    return null;
                foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        return (tag, asset.GetProperty("browser_download_url").GetString() ?? "");
                }
            }
            catch { }
            return null;
        }

        public static async Task InstallAsync(string url)
        {
            string exe = Environment.ProcessPath!;
            string fresh = exe + ".new", old = exe + ".old";
            byte[] data = await RobloxApi.Http.GetByteArrayAsync(url);
            // a real Windows program starts with "MZ"; anything else is a broken or cut-off download
            if (data.Length < 1_000_000 || data[0] != (byte)'M' || data[1] != (byte)'Z')
                throw new Exception(L.T("The update didn't download properly. Try again."));
            await File.WriteAllBytesAsync(fresh, data);
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);      // Windows lets a running .exe be renamed
            try { File.Move(fresh, exe); }
            catch
            {
                File.Move(old, exe);  // put the working copy back so GuideStrap still starts
                throw;
            }
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            Application.Exit();
        }

        public static void CleanupOld()
        {
            try
            {
                string old = (Environment.ProcessPath ?? "") + ".old";
                if (File.Exists(old)) File.Delete(old);
            }
            catch { }
        }
    }
}
