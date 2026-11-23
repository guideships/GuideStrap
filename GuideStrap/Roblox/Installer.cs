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
    // INSTALLER: downloads Roblox straight from Roblox's servers
    static class Installer
    {
        static readonly Dictionary<string, string> PackageMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["RobloxApp.zip"] = "",
            ["redist.zip"] = "",
            ["WebView2.zip"] = "",
            ["shaders.zip"] = "shaders/",
            ["ssl.zip"] = "ssl/",
            ["WebView2RuntimeInstaller.zip"] = "WebView2RuntimeInstaller/",
            ["content-avatar.zip"] = "content/avatar/",
            ["content-configs.zip"] = "content/configs/",
            ["content-fonts.zip"] = "content/fonts/",
            ["content-sky.zip"] = "content/sky/",
            ["content-sounds.zip"] = "content/sounds/",
            ["content-textures2.zip"] = "content/textures/",
            ["content-models.zip"] = "content/models/",
            ["content-platform-fonts.zip"] = "PlatformContent/pc/fonts/",
            ["content-platform-dictionaries.zip"] = "PlatformContent/pc/shared_compression_dictionaries/",
            ["content-terrain.zip"] = "PlatformContent/pc/terrain/",
            ["content-textures3.zip"] = "PlatformContent/pc/textures/",
            ["extracontent-luapackages.zip"] = "ExtraContent/LuaPackages/",
            ["extracontent-translations.zip"] = "ExtraContent/translations/",
            ["extracontent-models.zip"] = "ExtraContent/models/",
            ["extracontent-textures.zip"] = "ExtraContent/textures/",
            ["extracontent-places.zip"] = "ExtraContent/places/",
        };

        public static async Task InstallAsync(string versionGuid, string versionDir, Action<string, float> report, CancellationToken ct = default)
        {
            string tempDir = versionDir + "_installing";
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            string cdn = $"https://setup.rbxcdn.com/{versionGuid}-";
            report("Getting the latest Roblox...", -1f);
            string manifest = await RobloxApi.Http.GetStringAsync(cdn + "rbxPkgManifest.txt", ct);
            string[] lines = manifest.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            // "v0", then groups of 4 lines: file name, MD5, download size, unpacked size
            var packages = new List<(string Name, string Md5, long Size)>();
            for (int i = 1; i + 2 < lines.Length; i += 4)
            {
                if (!lines[i].EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                packages.Add((lines[i], lines[i + 1].ToLowerInvariant(), long.Parse(lines[i + 2])));
            }

            long total = Math.Max(1, packages.Sum(p => p.Size));
            long done = 0;
            int lastPct = -1;
            Directory.CreateDirectory(Paths.Downloads);

            foreach (var pkg in packages)
            {
                // packages that didn't change since the last Roblox update are reused instead of downloaded again
                string cached = Path.Combine(Paths.Downloads, pkg.Md5 + ".zip");
                if (File.Exists(cached))
                {
                    byte[] old = await File.ReadAllBytesAsync(cached, ct);
                    if (Convert.ToHexString(MD5.HashData(old)).ToLowerInvariant() == pkg.Md5)
                    {
                        done += pkg.Size;
                        report($"Installing Roblox... {Math.Min((int)(done * 100 / total), 100)}%", (float)done / total);
                        string dest = PackageMap.TryGetValue(pkg.Name, out var oldSub) ? Path.Combine(tempDir, oldSub) : tempDir;
                        await Task.Run(() => ExtractZip(old, dest), ct);
                        continue;
                    }
                    try { File.Delete(cached); } catch { }
                }

                using var resp = await RobloxApi.Http.GetAsync(cdn + pkg.Name, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var ms = new MemoryStream();
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    ms.Write(buffer, 0, read);
                    done += read;
                    int pct = (int)(done * 100 / total);
                    if (pct != lastPct)
                    {
                        lastPct = pct;
                        report($"Installing Roblox... {Math.Min(pct, 100)}%", (float)done / total);
                    }
                }

                byte[] data = ms.ToArray();
                if (Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant() != pkg.Md5)
                    throw new Exception($"{pkg.Name} downloaded corrupted. Try again.");
                try { await File.WriteAllBytesAsync(cached, data, ct); } catch { }

                string target = PackageMap.TryGetValue(pkg.Name, out var sub) ? Path.Combine(tempDir, sub) : tempDir;
                await Task.Run(() => ExtractZip(data, target), ct);
            }

            File.WriteAllText(Path.Combine(tempDir, "AppSettings.xml"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Settings>\r\n" +
                "\t<ContentFolder>content</ContentFolder>\r\n" +
                "\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n</Settings>\r\n");

            if (Directory.Exists(versionDir)) Directory.Delete(versionDir, true);
            Directory.Move(tempDir, versionDir);

            // keep only the packages this version uses
            var keep = packages.Select(p => p.Md5 + ".zip").ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.GetFiles(Paths.Downloads))
                if (!keep.Contains(Path.GetFileName(f))) try { File.Delete(f); } catch { }
        }

        static void ExtractZip(byte[] data, string targetDir)
        {
            string root = Path.GetFullPath(targetDir);
            using var zip = new ZipArchive(new MemoryStream(data));
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
                string dest = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('\\', '/')));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, true);
            }
        }

        public static void CleanupOldVersions(string keep)
        {
            if (!Directory.Exists(Paths.Versions)) return;
            foreach (var dir in Directory.GetDirectories(Paths.Versions))
            {
                if (Path.GetFileName(dir) == keep) continue;
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
