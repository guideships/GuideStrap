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
    // THEME
    public static class Theme
    {
        public static Color Bg = Color.FromArgb(14, 11, 22);
        public static Color Bar = Color.FromArgb(18, 14, 29);
        public static Color Card = Color.FromArgb(24, 19, 38);
        public static Color Input = Color.FromArgb(16, 12, 26);
        public static Color ButtonBg = Color.FromArgb(35, 28, 55);
        public static Color ButtonHover = Color.FromArgb(47, 38, 73);
        public static Color Border = Color.FromArgb(39, 31, 61);
        public static Color Track = Color.FromArgb(62, 50, 94);
        public static Color Accent = Color.FromArgb(124, 72, 232);
        public static Color AccentHover = Color.FromArgb(143, 96, 242);
        public static Color AccentLight = Color.FromArgb(185, 156, 255);

        public static readonly (string Name, Color Color)[] Accents =
        {
            ("Grey", Color.FromArgb(100, 100, 100)),
            ("Purple", Color.FromArgb(124, 72, 232)),
            ("Blue", Color.FromArgb(59, 120, 236)),
            ("Rose", Color.FromArgb(220, 70, 128)),
            ("Emerald", Color.FromArgb(22, 163, 116)),
            ("Amber", Color.FromArgb(222, 142, 28)),
        };

        // the logo is always the black version, in every theme
        public static readonly Color LogoDark = Color.FromArgb(24, 24, 24);
        public static readonly Color LogoLight = Color.FromArgb(50, 50, 50);

        public static readonly string[] Themes = { "Purple", "Black", "Glass" };

        // Glass theme: frosted cards, light edges and slow moving lights behind the sidebar
        public static bool IsGlass { get; private set; }
        // Glass only: windows slightly see-through. Off by default, because Windows draws
        // softer (less sharp) text in see-through windows.
        public static bool SeeThrough { get; private set; }
        public static float WindowOpacity => IsGlass && SeeThrough ? 0.95f : 1f;   // was 0.86: more solid so the background doesn't distract

        // Call once at startup, before any window is made.
        // Call at startup, before any window is made. Calling it again switches theme (open windows must be rebuilt).
        public static void Apply(string theme, string accent, bool seeThrough = false)
        {
            IsGlass = false;
            SeeThrough = seeThrough;
            // start from the Purple colors every time, so switching themes never leaves old colors behind
            Bg = Color.FromArgb(14, 11, 22);
            Bar = Color.FromArgb(18, 14, 29);
            Card = Color.FromArgb(24, 19, 38);
            Input = Color.FromArgb(16, 12, 26);
            ButtonBg = Color.FromArgb(35, 28, 55);
            ButtonHover = Color.FromArgb(47, 38, 73);
            Border = Color.FromArgb(39, 31, 61);
            Track = Color.FromArgb(62, 50, 94);
            NavHover = Color.FromArgb(23, 18, 37);
            NavSelected = Color.FromArgb(33, 26, 53);
            Text = Color.FromArgb(241, 237, 250);
            SubText = Color.FromArgb(155, 145, 180);
            Magenta = Color.FromArgb(168, 56, 214);
            if (theme == "Black")
            {
                // plain dark greys, no tint. Text kept bright for contrast.
                Bg = Color.FromArgb(10, 10, 10);
                Bar = Color.FromArgb(15, 15, 15);
                Card = Color.FromArgb(21, 21, 21);
                Input = Color.FromArgb(13, 13, 13);
                ButtonBg = Color.FromArgb(34, 34, 34);
                ButtonHover = Color.FromArgb(46, 46, 46);
                Border = Color.FromArgb(40, 40, 40);
                Track = Color.FromArgb(52, 52, 52);
                NavHover = Color.FromArgb(22, 22, 22);       // was 19: hard to see against the 10 sidebar
                NavSelected = Color.FromArgb(33, 33, 33);
                Text = Color.FromArgb(240, 240, 240);
                SubText = Color.FromArgb(165, 165, 165);
                Magenta = Color.FromArgb(120, 120, 120);
                SetAccent(accent);   // Black uses the accent color you pick (Grey by default)
            }
            else if (theme == "Glass")
            {
                // dark grey, no color, and the windows are slightly see-through
                IsGlass = true;
                // darker cards so text stays easy to read when the window is see-through
                Bg = Color.FromArgb(15, 15, 17);
                Bar = Color.FromArgb(18, 18, 20);
                Card = Color.FromArgb(22, 22, 25);
                Input = Color.FromArgb(15, 15, 17);
                ButtonBg = Color.FromArgb(36, 36, 40);
                ButtonHover = Color.FromArgb(48, 48, 53);
                Border = Color.FromArgb(44, 44, 49);
                Track = Color.FromArgb(60, 60, 65);
                NavHover = Color.FromArgb(24, 24, 27);
                NavSelected = Color.FromArgb(34, 34, 38);
                Text = Color.FromArgb(240, 240, 242);
                SubText = Color.FromArgb(168, 168, 172);
                Accent = Color.FromArgb(104, 104, 110);       // white text on it stays readable
                AccentHover = Color.FromArgb(122, 122, 128);
                AccentLight = Color.FromArgb(222, 222, 226);
                Magenta = Color.FromArgb(124, 124, 130);
                accentName = "Grey";
                UpdatePrimary();
            }
            else SetAccent(accent);
        }

        // ---- main action colors (Play, Launch Roblox, dialog OK buttons, switches that are on) ----
        // With a color accent: the accent with white text. With the grey accent (and Glass) the plain grey
        // looked turned off, so it's a brighter grey (still white text, not a white button).
        public static Color PrimaryBg = Color.FromArgb(124, 72, 232);
        public static Color PrimaryHover = Color.FromArgb(143, 96, 242);
        public static Color PrimaryText = Color.White;
        static string accentName = "Purple";

        static void UpdatePrimary()
        {
            if (accentName == "Grey")
            {
                PrimaryBg = Mix(Accent, Color.White, 0.08f);      // 100 grey -> 112, white text stays easy to read
                PrimaryHover = Mix(Accent, Color.White, 0.17f);
                PrimaryText = Color.White;
            }
            else
            {
                PrimaryBg = Accent;
                PrimaryHover = AccentHover;
                PrimaryText = Color.White;
            }
        }

        // ---- keyboard focus (shown only after Tab or arrow keys, like Windows does) ----
        // A ring just inside the control. Dark on light buttons, light everywhere else.
        public static void DrawFocusRing(Graphics g, RectangleF r, float radius, float width, Color behind)
        {
            bool light = behind.GetBrightness() > 0.6f;
            Color c = light ? Bg : Mix(AccentLight, Text, 0.35f);
            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Round(r, radius))
            using (var p = new Pen(c, width))
                g.DrawPath(p, path);
            g.SmoothingMode = mode;
        }

        public static void SetAccent(string name)
        {
            var a = Accents.FirstOrDefault(x => x.Name == name);
            if (a.Name == null) a = Accents[0];
            Accent = a.Color;
            AccentHover = Mix(a.Color, Color.White, 0.14f);
            // grey gets a brighter highlight so selected icons still stand out
            AccentLight = Mix(a.Color, Color.White, a.Name == "Grey" ? 0.72f : 0.42f);
            accentName = a.Name;
            UpdatePrimary();
        }
        public static Color Magenta = Color.FromArgb(168, 56, 214);
        public static readonly Color Gold = Color.FromArgb(242, 194, 88);
        public static Color Text = Color.FromArgb(241, 237, 250);
        public static Color SubText = Color.FromArgb(155, 145, 180);
        public static Color NavHover = Color.FromArgb(23, 18, 37);
        public static Color NavSelected = Color.FromArgb(33, 26, 53);
        public static readonly Color Danger = Color.FromArgb(214, 62, 86);
        public static readonly Color Success = Color.FromArgb(92, 214, 140);

        public static readonly string IconFont = DetectIconFont();

        static string DetectIconFont()
        {
            try
            {
                using var fonts = new InstalledFontCollection();
                return fonts.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            }
            catch { return "Segoe MDL2 Assets"; }
        }

        static Icon? appIcon;
        public static Icon AppIcon => appIcon ??= MakeIcon();

        static Icon MakeIcon()
        {
            using var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                DrawLogo(g, new RectangleF(0, 0, 64, 64));
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        // GuideStrap logo: tilted dark tile with white outline strokes and a compass in the middle
        public static void DrawLogo(Graphics g, RectangleF r, float spin = 0f, float scale = 0.80f)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float side = Math.Min(r.Width, r.Height) * scale;
            var state = g.Save();
            g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);
            g.RotateTransform(-18f + spin);

            var tile = new RectangleF(-side / 2, -side / 2, side, side);
            float rad = side * 0.10f;
            using (var path = Round(tile, rad))
            {
                using (var b = new SolidBrush(LogoDark)) g.FillPath(b, path);
                g.SetClip(path, CombineMode.Intersect);
                using (var lb = new SolidBrush(LogoLight))
                    g.FillPolygon(lb, new[] { new PointF(tile.Left, tile.Top), new PointF(tile.Right, tile.Top), new PointF(tile.Left, tile.Bottom) });
                g.ResetClip();
            }

            float sw = Math.Max(1f, side * 0.045f);
            using (var outer = Round(RectangleF.Inflate(tile, -sw / 2, -sw / 2), rad))
            using (var p = new Pen(Color.FromArgb(245, 245, 250), sw))
                g.DrawPath(p, outer);
            if (side >= 24)
            {
                float ins = side * 0.13f;
                using var inner = Round(RectangleF.Inflate(tile, -ins, -ins), rad * 0.5f);
                using var p2 = new Pen(Color.FromArgb(200, 245, 245, 250), Math.Max(1f, sw * 0.5f));
                g.DrawPath(p2, inner);
            }

            // compass (emoji style): gold rim, cream face, red north needle
            float cr = side * 0.25f, face = cr * 0.8f;
            using (var rim = new SolidBrush(Color.FromArgb(232, 164, 52))) g.FillEllipse(rim, -cr, -cr, cr * 2, cr * 2);
            using (var fb = new SolidBrush(Color.FromArgb(250, 244, 230))) g.FillEllipse(fb, -face, -face, face * 2, face * 2);
            if (side >= 40)
            {
                using var tick = new Pen(Color.FromArgb(150, 120, 90), Math.Max(1f, side * 0.014f));
                g.DrawLine(tick, 0, -face * 0.92f, 0, -face * 0.74f);
                g.DrawLine(tick, 0, face * 0.92f, 0, face * 0.74f);
                g.DrawLine(tick, -face * 0.92f, 0, -face * 0.74f, 0);
                g.DrawLine(tick, face * 0.92f, 0, face * 0.74f, 0);
            }
            g.RotateTransform(45f);
            float len = face * 0.82f, w = face * 0.22f;
            using (var red = new SolidBrush(Color.FromArgb(222, 52, 52)))
                g.FillPolygon(red, new[] { new PointF(0, -len), new PointF(w, 0), new PointF(-w, 0) });
            using (var gray = new SolidBrush(Color.FromArgb(190, 196, 206)))
                g.FillPolygon(gray, new[] { new PointF(0, len), new PointF(w, 0), new PointF(-w, 0) });
            float pin = face * 0.12f;
            using (var pb = new SolidBrush(Color.FromArgb(70, 60, 60))) g.FillEllipse(pb, -pin, -pin, pin * 2, pin * 2);
            g.Restore(state);
        }

        // Shared font cache: fewer GDI objects, no per-frame allocations while animating
        static readonly Dictionary<(string, float, FontStyle), Font> fontCache = new();
        public static Font F(string name, float size, FontStyle style = FontStyle.Regular)
        {
            var key = (name, size, style);
            if (!fontCache.TryGetValue(key, out var f)) fontCache[key] = f = new Font(UiFont(name, size), size, style);
            return f;
        }

        // Windows 11 has "Segoe UI Variable", a newer version of Segoe UI that's tuned to look sharp at
        // small sizes. Use it when both its normal and semibold styles are installed, otherwise keep Segoe UI.
        static bool? hasVariable;
        public static string UiFont(string name, float size)
        {
            if (name != "Segoe UI" && name != "Segoe UI Semibold") return name;
            hasVariable ??= DetectVariable();
            if (hasVariable != true) return name;
            bool big = size >= 13f;   // titles use the "Display" cut, everything else the "Text" cut
            if (name == "Segoe UI") return big ? "Segoe UI Variable Display" : "Segoe UI Variable Text";
            return big ? "Segoe UI Variable Display Semibold" : "Segoe UI Variable Text Semibold";
        }

        static bool DetectVariable()
        {
            try
            {
                using var fonts = new InstalledFontCollection();
                var names = new HashSet<string>(fonts.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                return names.Contains("Segoe UI Variable Text") && names.Contains("Segoe UI Variable Text Semibold")
                    && names.Contains("Segoe UI Variable Display") && names.Contains("Segoe UI Variable Display Semibold");
            }
            catch { return false; }
        }

        // The user's own picture, cropped to fill w x h and darkened so text stays readable
        public static Bitmap? LoadBackdrop(string? path, int w, int h, int shade = 170)
        {
            if (path == null || w <= 0 || h <= 0 || !File.Exists(path)) return null;
            try
            {
                using var fs = File.OpenRead(path);
                using var src = Image.FromStream(fs);
                var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);   // fastest format to draw
                using var g = Graphics.FromImage(bmp);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float scale = Math.Max((float)w / src.Width, (float)h / src.Height);
                float dw = src.Width * scale, dh = src.Height * scale;
                g.DrawImage(src, (w - dw) / 2, (h - dh) / 2, dw, dh);
                using var dark = new SolidBrush(Color.FromArgb(shade, Bg));
                g.FillRectangle(dark, 0, 0, w, h);
                return bmp;
            }
            catch { return null; }
        }

        // Give transparent labels a solid background (much cheaper to paint)
        // Clears a custom control to its parent's color, or to the matching piece of the parent's background picture
        public static void ClearToParent(Control c, Graphics g, Color fallback)
        {
            // see-through parents (pages over your background picture) are skipped:
            // go up to the first one that has a real color or the picture, and line the picture up with it
            var parent = c.Parent;
            int ox = c.Left, oy = c.Top;
            while (parent != null && parent.BackColor.A < 255 && parent.BackgroundImage == null)
            {
                ox += parent.Left;
                oy += parent.Top;
                parent = parent.Parent;
            }
            g.Clear(parent != null && parent.BackColor.A == 255 ? parent.BackColor : fallback);
            if (parent?.BackgroundImage is not Image img || parent.BackgroundImageLayout != ImageLayout.None) return;

            // Copy only the piece of the picture behind this control (not the whole picture, clipped).
            // This runs for every switch, label and button on screen, so it has to be cheap.
            var src = Rectangle.Intersect(new Rectangle(ox, oy, c.Width, c.Height), new Rectangle(0, 0, img.Width, img.Height));
            if (src.Width <= 0 || src.Height <= 0) return;
            var state = g.Save();
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(img, new Rectangle(src.X - ox, src.Y - oy, src.Width, src.Height), src, GraphicsUnit.Pixel);
            g.Restore(state);
        }

        // the color behind a control, skipping see-through parents
        public static Color SolidBack(Control c, Color fallback)
        {
            for (var p = c.Parent; p != null; p = p.Parent)
                if (p.BackColor.A == 255) return p.BackColor;
            return fallback;
        }

        // Dark scrollbars (Windows 10 1809+ and Windows 11) instead of the bright default ones
        public static T DarkScroll<T>(T c) where T : Control
        {
            void Set() { try { NativeMethods.SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }
            if (c.IsHandleCreated) Set();
            c.HandleCreated += (_, _) => Set();
            return c;
        }

        public static void Solidify(Control root)
        {
            foreach (Control c in root.Controls)
            {
                // labels over a background picture stay see-through
                if (c is Label l && l.BackColor == Color.Transparent && root.BackColor.A == 255 && root.BackgroundImage == null) l.BackColor = root.BackColor;
                if (c.HasChildren) Solidify(c);
            }
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public const ControlStyles PaintStyles = ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                                                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor;
    }
}
