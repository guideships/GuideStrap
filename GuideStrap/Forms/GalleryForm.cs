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
    class GalleryItem
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string? Preview { get; set; }
    }

    class GalleryIndex
    {
        public List<GalleryItem> Items { get; set; } = new();
    }

    // MOD GALLERY: a list kept in GuideStrap's GitHub repo, installed with one click
    class GalleryForm : ThemedForm
    {
        const long MaxBytes = 60_000_000;
        readonly Action<Action<AppSettings>> apply;
        readonly Label status;
        readonly Panel list;
        readonly List<Image> images = new();

        public GalleryForm(Action<Action<AppSettings>> apply)
        {
            this.apply = apply;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CloseOnEscape = true;
            Text = L.T("Mod gallery");

            int w = D(580), pad = D(20);
            var bar = BuildTitleBar("Mod gallery", false);
            int y = D(36) + D(8);
            Controls.Add(At(Lbl("Mod gallery", 13f, semibold: true), pad, y));
            y += D(30);
            status = Lbl("Loading the gallery...", 9f, Theme.SubText);
            status.Location = new Point(pad, y);
            Controls.Add(status);
            y += D(28);
            list = Theme.DarkScroll(new Panel { Location = new Point(pad, y), Size = new Size(w - pad * 2, D(390)), AutoScroll = true, BackColor = Theme.Bg });
            Controls.Add(list);
            y += list.Height + D(10);
            var note = Lbl("Only cosmetic files: images, skies and sounds. Everything installs into GuideStrap's copy of Roblox.", 8.5f, Theme.SubText);
            note.AutoSize = false;
            note.Size = new Size(w - pad * 2, D(34));
            note.Location = new Point(pad, y);
            Controls.Add(note);
            y += D(34);
            Controls.Add(bar);
            ClientSize = new Size(w, y + pad);
            Shown += async (_, _) => await LoadAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var i in images) i.Dispose();
            base.Dispose(disposing);
        }

        static string TypeName(string type) => type switch
        {
            "sky" => "Sky",
            "cursor" => "Cursor",
            "crosshair" => "Crosshair",
            _ => "Sound pack",
        };

        async Task LoadAsync()
        {
            try
            {
                string json = await RobloxApi.Http.GetStringAsync(AppInfo.GalleryUrl);
                var index = JsonSerializer.Deserialize<GalleryIndex>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var items = (index?.Items ?? new())
                    .Where(i => i.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    .Where(i => i.Type.ToLowerInvariant() is "sky" or "cursor" or "crosshair" or "soundpack")
                    .ToList();
                if (IsDisposed) return;
                status.Text = items.Count == 0 ? L.T("The gallery is empty right now.") : L.F("{0} mods", items.Count);
                int cardW = list.Width - SystemInformation.VerticalScrollBarWidth - D(4);
                int ly = 0;
                foreach (var item in items)
                {
                    var card = ItemCard(item, cardW);
                    card.Location = new Point(0, ly);
                    list.Controls.Add(card);
                    ly += card.Height + D(8);
                }
            }
            catch
            {
                if (!IsDisposed) status.Text = L.T("Couldn't load the gallery. Check your internet and try again.");
            }
        }

        Card ItemCard(GalleryItem item, int width)
        {
            string type = item.Type.ToLowerInvariant();
            var card = new Card { Size = new Size(width, D(80)) };
            var pic = new PictureBox
            {
                Size = new Size(D(52), D(52)), Location = new Point(D(14), D(14)),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Input,
            };
            var name = At(Lbl(item.Name, 10f, semibold: true), D(80), D(10));
            name.UseMnemonic = false;
            var sub = At(Lbl(L.T(TypeName(type)) + (item.Author.Length > 0 ? "  \u00B7  " + L.F("by {0}", item.Author) : ""), 8.5f, Theme.AccentLight), D(80), D(33));
            var desc = Lbl(item.Description, 8.5f, Theme.SubText);
            desc.AutoSize = false;
            desc.AutoEllipsis = true;
            desc.Size = new Size(width - D(80) - D(140), D(18));
            desc.Location = new Point(D(80), D(53));
            var install = Primary("Install", "\uE896", D(110), D(30));
            install.Location = new Point(width - D(126), D(25));
            install.Click += async (_, _) =>
            {
                install.Enabled = false;
                install.Text = L.T("Installing...");
                try
                {
                    await InstallAsync(item, type);
                    install.Text = L.T("Installed");
                }
                catch (Exception ex)
                {
                    install.Text = L.T("Install");
                    install.Enabled = true;
                    Ask("Couldn't install that", ex.Message, "OK");
                }
            };
            card.Controls.AddRange(new Control[] { pic, name, sub, desc, install });

            string? previewUrl = item.Preview ?? (type is "cursor" or "crosshair" ? item.Url : null);
            if (previewUrl != null && previewUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                _ = LoadPreviewAsync(pic, previewUrl);
            return card;
        }

        async Task LoadPreviewAsync(PictureBox pic, string url)
        {
            try
            {
                byte[] data = await RobloxApi.Http.GetByteArrayAsync(url);
                if (data.Length > 4_000_000) return;
                using var ms = new MemoryStream(data);
                using var img = Image.FromStream(ms);
                var copy = new Bitmap(img);
                if (pic.IsDisposed) { copy.Dispose(); return; }
                images.Add(copy);
                pic.Image = copy;
            }
            catch { }
        }

        static async Task<byte[]> DownloadAsync(string url)
        {
            using var resp = await RobloxApi.Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength > MaxBytes) throw new Exception(L.T("That mod is too big."));
            byte[] data = await resp.Content.ReadAsByteArrayAsync();
            if (data.Length > MaxBytes) throw new Exception(L.T("That mod is too big."));
            return data;
        }

        // Files in a zip with the given extensions. A single top folder is removed, so "Pack/ouch.ogg" becomes "ouch.ogg".
        static List<(string Path, byte[] Data)> ReadZip(byte[] zipData, params string[] extensions)
        {
            var files = new List<(string Path, byte[] Data)>();
            using (var zip = new ZipArchive(new MemoryStream(zipData)))
            {
                foreach (var entry in zip.Entries)
                {
                    string rel = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (rel.Length == 0 || rel.EndsWith('/') || rel.Contains("..")) continue;
                    if (!extensions.Any(e => rel.EndsWith(e, StringComparison.OrdinalIgnoreCase))) continue;
                    if (entry.Length > 30_000_000) continue;
                    using var es = entry.Open();
                    using var ms = new MemoryStream();
                    es.CopyTo(ms);
                    files.Add((rel, ms.ToArray()));
                }
            }
            if (files.Count > 0 && files.All(f => f.Path.Contains('/')))
            {
                string top = files[0].Path[..files[0].Path.IndexOf('/')];
                if (files.All(f => f.Path.StartsWith(top + "/", StringComparison.OrdinalIgnoreCase)))
                    files = files.Select(f => (f.Path[(top.Length + 1)..], f.Data)).ToList();
            }
            return files;
        }

        static string SafeName(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            string clean = new string(name.Where(c => !bad.Contains(c)).ToArray()).Trim().TrimEnd('.');
            if (clean.Length > 40) clean = clean[..40];
            return clean.Length == 0 ? "Pack" : clean;
        }

        async Task InstallAsync(GalleryItem item, string type)
        {
            byte[] data = await DownloadAsync(item.Url);
            switch (type)
            {
                case "cursor":
                case "crosshair":
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "guidestrap-" + Guid.NewGuid().ToString("N") + ".png");
                    await File.WriteAllBytesAsync(tmp, data);
                    try
                    {
                        bool ok = type == "cursor" ? ModAssets.ImportCustomCursor(tmp) : ModAssets.ImportCustomCrosshair(tmp);
                        if (!ok) throw new Exception(L.T("That image couldn't be read."));
                    }
                    finally { try { File.Delete(tmp); } catch { } }
                    if (type == "cursor") apply(s => s.CursorPreset = "custom");
                    else apply(s => s.CrosshairPreset = "custom");
                    break;
                }
                case "sky":
                {
                    var tex = ReadZip(data, ".tex");
                    if (tex.Count == 0) throw new Exception(L.T("That sky pack has no .tex files."));
                    if (Directory.Exists(Paths.Sky)) Directory.Delete(Paths.Sky, true);
                    Directory.CreateDirectory(Paths.Sky);
                    foreach (var (path, bytes) in tex)
                        await File.WriteAllBytesAsync(Path.Combine(Paths.Sky, Path.GetFileName(path)), bytes);
                    apply(s => s.CustomSky = true);
                    break;
                }
                default:
                {
                    var sounds = ReadZip(data, ".ogg", ".mp3");
                    if (sounds.Count == 0) throw new Exception(L.T("That sound pack has no .ogg or .mp3 files."));
                    string name = SafeName(item.Name);
                    string dir = Path.Combine(Paths.SoundPacks, name);
                    string root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    foreach (var (path, bytes) in sounds)
                    {
                        string dest = Path.GetFullPath(Path.Combine(dir, path));
                        if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        await File.WriteAllBytesAsync(dest, bytes);
                    }
                    apply(s => s.SoundPack = name);
                    break;
                }
            }
        }
    }
}
