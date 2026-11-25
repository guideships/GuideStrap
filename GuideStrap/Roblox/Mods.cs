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
    // MODS: write FastFlags + custom font into the Roblox install
    static class Mods
    {
        // Copies mod files into the GuideStrap copy of Roblox. Originals are backed up first, and every
        // launch starts by putting the originals back, so turning a mod off really removes it.
        static void ApplyOverlay(string versionDir, Dictionary<string, string> files)
        {
            string backupDir = Path.Combine(versionDir, "_guidestrap_originals");
            string manifest = Path.Combine(backupDir, "applied.txt");
            if (File.Exists(manifest))
            {
                foreach (var rel in File.ReadAllLines(manifest))
                {
                    if (rel.Length == 0) continue;
                    string target = Path.Combine(versionDir, rel), backup = Path.Combine(backupDir, rel);
                    try
                    {
                        if (File.Exists(backup)) File.Copy(backup, target, true);
                        else if (File.Exists(target)) File.Delete(target);
                    }
                    catch { }
                }
            }

            var applied = new List<string>();
            foreach (var (rel, source) in files)
            {
                if (!File.Exists(source) || rel.StartsWith("_guidestrap", StringComparison.OrdinalIgnoreCase)) continue;
                string target = Path.GetFullPath(Path.Combine(versionDir, rel));
                if (!target.StartsWith(Path.GetFullPath(versionDir), StringComparison.OrdinalIgnoreCase)) continue;
                string backup = Path.Combine(backupDir, rel);
                try
                {
                    if (File.Exists(target) && !File.Exists(backup))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                        File.Copy(target, backup);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target, true);
                    applied.Add(rel);
                }
                catch { }
            }
            Directory.CreateDirectory(backupDir);
            File.WriteAllLines(manifest, applied);
        }

        public static void Apply(string versionDir, AppSettings settings)
        {
            string csDir = Path.Combine(versionDir, "ClientSettings");
            Directory.CreateDirectory(csDir);
            var flags = settings.Flags.Where(kv => FF.IsAllowed(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            File.WriteAllText(Path.Combine(csDir, "ClientAppSettings.json"),
                JsonSerializer.Serialize(flags, new JsonSerializerOptions { WriteIndented = true }));

            // file mods: your mods folder + death sound + cursor + sky
            var overlay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(Paths.Mods))
                foreach (var f in Directory.GetFiles(Paths.Mods, "*", SearchOption.AllDirectories))
                    overlay[Path.GetRelativePath(Paths.Mods, f)] = f;
            string? pack = ModAssets.SoundPackDir(settings);
            if (pack != null)
                foreach (var f in Directory.GetFiles(pack, "*", SearchOption.AllDirectories))
                    overlay[Path.Combine("content", "sounds", Path.GetRelativePath(pack, f))] = f;
            if (settings.DeathSoundFile != null && File.Exists(settings.DeathSoundFile))
                overlay[@"content\sounds\ouch.ogg"] = settings.DeathSoundFile;
            string? steps = ModAssets.SoundFile(settings.FootstepSound, "footsteps");
            if (steps != null) overlay[@"content\sounds\action_footsteps_plastic.mp3"] = steps;
            string? jump = ModAssets.SoundFile(settings.JumpSound, "jump");
            if (jump != null)
            {
                overlay[@"content\sounds\action_jump.mp3"] = jump;
                if (settings.JumpSound == "mute") overlay[@"content\sounds\action_jump_land.mp3"] = jump;
            }
            string? crosshair = ModAssets.CrosshairFile(settings);
            if (crosshair != null) overlay[@"content\textures\MouseLockedCursor.png"] = crosshair;
            string? cursorDir = ModAssets.CursorDir(settings);
            if (cursorDir != null)
                foreach (var name in new[] { "ArrowCursor.png", "ArrowFarCursor.png" })
                    overlay[$@"content\textures\Cursors\KeyboardMouse\{name}"] = Path.Combine(cursorDir, name);
            if (settings.CustomSky && Directory.Exists(Paths.Sky))
                foreach (var f in Directory.GetFiles(Paths.Sky, "*.tex"))
                    overlay[$@"PlatformContent\pc\textures\sky\{Path.GetFileName(f)}"] = f;
            bool skipMods = File.Exists(Paths.SkipModsMarker);   // set by the crash helper, for one launch
            if (skipMods) overlay.Clear();
            ApplyOverlay(versionDir, overlay);
            if (skipMods) try { File.Delete(Paths.SkipModsMarker); } catch { }

            string fontsDir = Path.Combine(versionDir, "content", "fonts");
            string familiesDir = Path.Combine(fontsDir, "families");
            if (!Directory.Exists(familiesDir)) return;

            string backupDir = Path.Combine(fontsDir, "families_original");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
                foreach (var f in Directory.GetFiles(familiesDir, "*.json"))
                    File.Copy(f, Path.Combine(backupDir, Path.GetFileName(f)));
            }
            else
            {
                foreach (var f in Directory.GetFiles(backupDir, "*.json"))
                    File.Copy(f, Path.Combine(familiesDir, Path.GetFileName(f)), true);
            }
            // a second Roblox window (multiple accounts) can still be using the old font file, so never fail the launch here
            foreach (var old in Directory.GetFiles(fontsDir, "CustomFont.*"))
                try { File.Delete(old); } catch { }

            if (skipMods || settings.FontFile == null || !File.Exists(settings.FontFile)) return;

            string destName = "CustomFont" + Path.GetExtension(settings.FontFile).ToLowerInvariant();
            string destPath = Path.Combine(fontsDir, destName);
            try { File.Copy(settings.FontFile, destPath, true); }
            catch when (File.Exists(destPath)) { }   // in use by the other Roblox window: it's already there

            foreach (var file in Directory.GetFiles(familiesDir, "*.json"))
            {
                if (Path.GetFileName(file).Contains("emoji", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var root = JsonNode.Parse(File.ReadAllText(file));
                    if (root?["faces"] is not JsonArray faces) continue;
                    foreach (var face in faces)
                        if (face is JsonObject o) o["assetId"] = "rbxasset://fonts/" + destName;
                    File.WriteAllText(file, root.ToJsonString());
                }
                catch { }   // one odd font file shouldn't stop Roblox from starting
            }
        }
    }
}
