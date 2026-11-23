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
    // Quick check before launching: are Roblox's servers answering, and is the internet working at all?
    static class RobloxStatus
    {
        public enum Result { Up, Down, Offline }

        static readonly HttpClient probe = new() { Timeout = TimeSpan.FromSeconds(5) };

        static async Task<bool> Ok(string url)
        {
            try
            {
                using var r = await probe.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                return (int)r.StatusCode < 500;
            }
            catch { return false; }
        }

        public static async Task<Result> CheckAsync()
        {
            var roblox = await Task.WhenAll(
                Ok("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer"),
                Ok("https://users.roblox.com/v1/users/1"),
                Ok("https://www.roblox.com/"));
            if (roblox.Count(x => !x) < 2) return Result.Up;
            var internet = await Task.WhenAll(Ok("https://www.google.com/generate_204"), Ok("https://www.cloudflare.com/cdn-cgi/trace"));
            return internet.Any(x => x) ? Result.Down : Result.Offline;
        }
    }
}
