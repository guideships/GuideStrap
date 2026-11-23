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
    static class ModAssets
    {
        public static string? CursorDir(AppSettings s) => s.CursorPreset switch
        {
            "guidestrap" or "dot" or "cross" or "circle" or "classic" => EnsurePreset(s.CursorPreset),
            "custom" => File.Exists(Path.Combine(Paths.Managed, "cursor-custom", "ArrowCursor.png")) ? Path.Combine(Paths.Managed, "cursor-custom") : null,
            _ => null,
        };

        static string? EnsurePreset(string name)
        {
            string dir = Path.Combine(Paths.Managed, "cursor-" + name);
            string file = Path.Combine(dir, "ArrowCursor.png");
            try
            {
                if (!File.Exists(file))
                {
                    Directory.CreateDirectory(dir);
                    using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        if (name is "dot" or "cross" or "circle")
                        {
                            DrawMark(g, name);
                        }
                        else if (name == "classic")
                        {
                            var pts = new[] { new PointF(0, 0), new PointF(0, 19), new PointF(5, 14.5f), new PointF(8.5f, 22),
                                              new PointF(12, 20.5f), new PointF(8.5f, 13), new PointF(14.5f, 13) };
                            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(pts[i].X + 32, pts[i].Y + 32);
                            using var fill = new SolidBrush(Color.White);
                            g.FillPolygon(fill, pts);
                            using var pen = new Pen(Color.Black, 1.6f) { LineJoin = LineJoin.Round };
                            g.DrawPolygon(pen, pts);
                        }
                        else
                        {
                            // Roblox centers cursors, so the arrow tip sits in the middle of the image
                            var pts = new[] { new PointF(0, 0), new PointF(0, 19), new PointF(5, 14.5f), new PointF(8.5f, 22),
                                              new PointF(12, 20.5f), new PointF(8.5f, 13), new PointF(14.5f, 13) };
                            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(pts[i].X + 32, pts[i].Y + 32);
                            using var fill = new SolidBrush(Color.FromArgb(124, 72, 232));
                            g.FillPolygon(fill, pts);
                            using var pen = new Pen(Color.White, 1.6f) { LineJoin = LineJoin.Round };
                            g.DrawPolygon(pen, pts);
                        }
                    }
                    bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                    File.Copy(file, Path.Combine(dir, "ArrowFarCursor.png"), true);
                }
                return dir;
            }
            catch { return null; }
        }

        // Small marks drawn white with a dark outline, centered in a 64x64 image (Roblox centers these)
        static void DrawMark(Graphics g, string name)
        {
            const float c = 32f;
            using var outline = new Pen(Color.FromArgb(220, 0, 0, 0), 3.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var white = new Pen(Color.White, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            switch (name)
            {
                case "dot":
                    using (var o = new SolidBrush(Color.FromArgb(220, 0, 0, 0))) g.FillEllipse(o, c - 5f, c - 5f, 10, 10);
                    using (var f = new SolidBrush(Color.White)) g.FillEllipse(f, c - 3.5f, c - 3.5f, 7, 7);
                    break;
                case "cross":
                    foreach (var pen in new[] { outline, white })
                    {
                        g.DrawLine(pen, c - 9, c, c - 3, c);
                        g.DrawLine(pen, c + 3, c, c + 9, c);
                        g.DrawLine(pen, c, c - 9, c, c - 3);
                        g.DrawLine(pen, c, c + 3, c, c + 9);
                    }
                    break;
                case "circle":
                    g.DrawEllipse(outline, c - 7, c - 7, 14, 14);
                    g.DrawEllipse(white, c - 7, c - 7, 14, 14);
                    break;
            }
        }

        // Shift lock crosshair: content\textures\MouseLockedCursor.png
        public static string? CrosshairFile(AppSettings s)
        {
            if (s.CrosshairPreset == "custom")
            {
                string custom = Path.Combine(Paths.Managed, "crosshair-custom", "MouseLockedCursor.png");
                return File.Exists(custom) ? custom : null;
            }
            if (s.CrosshairPreset is not ("dot" or "cross" or "circle")) return null;
            string dir = Path.Combine(Paths.Managed, "crosshair-" + s.CrosshairPreset);
            string file = Path.Combine(dir, "MouseLockedCursor.png");
            try
            {
                if (!File.Exists(file))
                {
                    Directory.CreateDirectory(dir);
                    using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        DrawMark(g, s.CrosshairPreset);
                    }
                    bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                }
                return file;
            }
            catch { return null; }
        }

        public static bool ImportCustomCrosshair(string source)
        {
            try
            {
                string dir = Path.Combine(Paths.Managed, "crosshair-custom");
                Directory.CreateDirectory(dir);
                using var src = Image.FromFile(source);
                using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Min(1f, Math.Min(64f / src.Width, 64f / src.Height));
                    float w = src.Width * scale, h = src.Height * scale;
                    g.DrawImage(src, (64 - w) / 2, (64 - h) / 2, w, h);
                }
                bmp.Save(Path.Combine(dir, "MouseLockedCursor.png"), System.Drawing.Imaging.ImageFormat.Png);
                return true;
            }
            catch { return false; }
        }

        // Character sounds: muted = a tiny silent mp3, custom = the user's own file
        public static readonly string SoundsDir = Path.Combine(Paths.Managed, "sounds");

        const string SilentMp3 = "//sQxAADwAABpAAAACAAADSAAAAETEFNRTMuMTAwVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+xLEKYPAAAGkAAAAIAAANIAAAARVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+xDEU4PAAAGkAAAAIAAANIAAAARVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVf/7EsR9A8AAAaQAAAAgAAA0gAAABFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVf/7EMSnA8AAAaQAAAAgAAA0gAAABFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV";

        public static string? SoundFile(string mode, string kind)
        {
            try
            {
                if (mode == "mute")
                {
                    string silent = Path.Combine(SoundsDir, "silent.mp3");
                    if (!File.Exists(silent))
                    {
                        Directory.CreateDirectory(SoundsDir);
                        File.WriteAllBytes(silent, Convert.FromBase64String(SilentMp3));
                    }
                    return silent;
                }
                if (mode == "custom")
                {
                    string file = Path.Combine(SoundsDir, kind + ".mp3");
                    return File.Exists(file) ? file : null;
                }
            }
            catch { }
            return null;
        }

        // Sound packs: one folder per pack in GuideStrap\SoundPacks, files go into content\sounds
        public static List<string> SoundPacks()
        {
            try
            {
                if (!Directory.Exists(Paths.SoundPacks)) return new();
                return Directory.GetDirectories(Paths.SoundPacks).Select(d => Path.GetFileName(d))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch { return new(); }
        }

        public static string? SoundPackDir(AppSettings s)
        {
            if (string.IsNullOrEmpty(s.SoundPack) || s.SoundPack.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
            string dir = Path.Combine(Paths.SoundPacks, s.SoundPack);
            return Directory.Exists(dir) ? dir : null;
        }

        public static bool ImportCustomCursor(string source)
        {
            try
            {
                string dir = Path.Combine(Paths.Managed, "cursor-custom");
                Directory.CreateDirectory(dir);
                using var src = Image.FromFile(source);
                using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Min(1f, Math.Min(64f / src.Width, 64f / src.Height));
                    float w = src.Width * scale, h = src.Height * scale;
                    g.DrawImage(src, (64 - w) / 2, (64 - h) / 2, w, h);
                }
                string file = Path.Combine(dir, "ArrowCursor.png");
                bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                File.Copy(file, Path.Combine(dir, "ArrowFarCursor.png"), true);
                return true;
            }
            catch { return false; }
        }
    }
}
