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
    // SHARED LAUNCH LOGIC (used by the launcher and the settings window)
    static class RobloxLauncher
    {
        // report(text, progress): progress from 0 to 1 fills the bar, a negative value means "busy, unknown length".
        // Returns the Roblox process, or null if the game page was opened in the browser instead.
        public static async Task<Process?> LaunchAsync(AppSettings settings, List<Account> accounts, string? launchUri, string placeText,
            Action<string, float> report, CancellationToken ct, string? jobId = null, ServerPick pick = ServerPick.None)
        {
            report("Connecting to Roblox...", -1f);
            string versionGuid = await RobloxApi.GetLatestVersionAsync(ct);
            string versionDir = Path.Combine(Paths.Versions, versionGuid);
            string exe = Path.Combine(versionDir, "RobloxPlayerBeta.exe");

            if (!File.Exists(exe))
            {
                try { await Installer.InstallAsync(versionGuid, versionDir, report, ct); }
                catch (OperationCanceledException)
                {
                    try { Directory.Delete(versionDir + "_installing", true); } catch { }
                    throw;
                }
            }
            ct.ThrowIfCancellationRequested();
            Installer.CleanupOldVersions(versionGuid);

            long? placeId = launchUri != null ? ParsePlaceIdFromLink(launchUri) : ParsePlaceId(placeText);
            if (launchUri == null && placeText.Trim().Length > 0 && placeId == null)
                throw new Exception("That doesn't look like a game link or place ID.");

            // a setup linked to this game is used for this launch only
            var effective = settings;
            if (placeId != null && settings.GameSetups.TryGetValue(placeId.Value.ToString(), out var setupName)
                && settings.Setups.TryGetValue(setupName, out var setup))
            {
                effective = settings.Clone();
                setup.ApplyTo(effective);
                report($"Applying your {setupName} setup...", -1f);
            }
            else report("Applying your settings...", -1f);
            Mods.Apply(versionDir, effective);

            var acc = accounts.FirstOrDefault(a => a.UserId == settings.ActiveAccountId);
            Presence.SetAccount(acc?.DisplayName);
            string signingIn = settings.StreamerMode || acc == null ? "Signing in..." : $"Signing in as {acc.DisplayName}...";

            if (launchUri == null && pick != ServerPick.None)
            {
                if (placeId == null) throw new Exception("Paste a game link first, or join a game once.");
                report(pick == ServerPick.Small ? "Finding a small server..." : "Finding another server...", -1f);
                jobId = await RobloxApi.FindServerAsync(placeId.Value, pick == ServerPick.Small, LastServer.Load()?.JobId, ct)
                        ?? throw new Exception("Couldn't find an open server for that game. Try again in a moment.");
            }
            if (jobId != null && !Guid.TryParse(jobId, out _)) jobId = null;

            string arg;
            if (launchUri != null)   // opened from the website's Play button
            {
                arg = launchUri;
                if (acc != null && settings.UseAccountForWebsite && launchUri.Contains("gameinfo:"))
                {
                    report(signingIn, -1f);
                    string ticket = await RobloxApi.GetAuthTicketAsync(acc.Cookie);
                    arg = Regex.Replace(launchUri, @"gameinfo:[^+]*", _ => "gameinfo:" + ticket);
                }
            }
            else if (acc == null)
            {
                if (placeId != null && jobId != null)
                    arg = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={jobId}";
                else if (placeId != null)
                {
                    Process.Start(new ProcessStartInfo($"https://www.roblox.com/games/{placeId}") { UseShellExecute = true });
                    return null;
                }
                else arg = "--app";
            }
            else
            {
                report(signingIn, -1f);
                string ticket = await RobloxApi.GetAuthTicketAsync(acc.Cookie);
                arg = RobloxApi.BuildLaunchUri(ticket, placeId, jobId);
            }
            ct.ThrowIfCancellationRequested();

            report("Starting Roblox...", -1f);
            if (effective.FpsCap > 0) RobloxTweaks.SetFpsCap(effective.FpsCap);
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = versionDir, UseShellExecute = false };
            psi.ArgumentList.Add(arg);
            var proc = Process.Start(psi);
            if (effective.Priority > 0)
                RobloxTweaks.BoostPriority(effective.Priority == 2 ? ProcessPriorityClass.High : ProcessPriorityClass.AboveNormal);
            return proc;
        }

        // Place ID inside a roblox-player: or roblox:// link (the website's Play button)
        public static long? ParsePlaceIdFromLink(string link)
        {
            try
            {
                var m = Regex.Match(Uri.UnescapeDataString(link), @"placeId=(\d+)", RegexOptions.IgnoreCase);
                return m.Success && long.TryParse(m.Groups[1].Value, out var id) ? id : null;
            }
            catch { return null; }
        }

        public static long? ParsePlaceId(string text)
        {
            text = text.Trim();
            if (text.Length == 0) return null;
            var m = Regex.Match(text, @"games/(\d+)");
            if (m.Success) return long.Parse(m.Groups[1].Value);
            m = Regex.Match(text, @"[?&]placeId=(\d+)", RegexOptions.IgnoreCase);
            if (m.Success) return long.Parse(m.Groups[1].Value);
            return long.TryParse(text, out var id) ? id : null;
        }
    }

    // Cursor files: built-in presets are drawn in code, custom images are resized to 64x64
    enum ServerPick { None, Hop, Small }
}
