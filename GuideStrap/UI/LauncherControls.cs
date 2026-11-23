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
    // The logo, doing a smooth full turn with a short pause in between.
    // Drawn once into a bitmap, then only that bitmap is rotated each frame (cheap and smooth).
    // The logo is drawn smaller than the box so its corners never get cut off while turning.
    public class SpinningLogo : Control
    {
        bool spinning;
        double time;
        float angle;
        Bitmap? cache;

        public SpinningLogo() => SetStyle(Theme.PaintStyles, true);

        public bool Spinning
        {
            get => spinning;
            set
            {
                if (spinning == value) return;
                spinning = value;
                if (value)
                {
                    time = 0;
                    Anim.Loop((this, "spin"), dt =>
                    {
                        time = (time + dt) % 1700;
                        double p = Math.Min(1, time / 1250);
                        double eased = p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;   // ease in-out
                        angle = (float)(360 * eased);
                        Invalidate();
                        Update();
                    });
                }
                else
                {
                    Anim.StopLoop((this, "spin"));
                    Anim.Run((this, "settle"), angle % 360, angle > 180 ? 360 : 0, 300, v => { angle = v; Invalidate(); });
                }
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            cache?.Dispose();
            cache = null;
        }

        protected override void Dispose(bool disposing)
        {
            Anim.StopLoop((this, "spin"));
            cache?.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            if (cache == null)
            {
                cache = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var cg = Graphics.FromImage(cache);
                cg.Clear(Color.Transparent);
                // tile diagonal must stay inside the box at every angle: 0.66 * 1.414 < 1
                Theme.DrawLogo(cg, new RectangleF(0, 0, Width, Height), 0f, 0.66f);
            }
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(Width / 2f, Height / 2f);
            g.RotateTransform(angle);
            g.DrawImage(cache, -Width / 2f, -Height / 2f, Width, Height);
        }
    }

    // Progress bar: a segment slides across while waiting, then it fills while installing
    public class LoadingBar : Control
    {
        bool indeterminate;
        float shown;
        double phase;

        public LoadingBar() => SetStyle(Theme.PaintStyles, true);

        public float Value
        {
            set
            {
                float target = Math.Clamp(value, 0f, 1f);
                Anim.Run((this, "fill"), shown, target, 250, v => { shown = v; Invalidate(); });
            }
        }

        public bool Indeterminate
        {
            get => indeterminate;
            set
            {
                if (indeterminate == value) return;
                indeterminate = value;
                if (value)
                {
                    phase = 0;
                    Anim.Loop((this, "marquee"), dt => { phase = (phase + dt / 1400.0) % 1.0; Invalidate(); });
                }
                else
                {
                    Anim.StopLoop((this, "marquee"));
                    Invalidate();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            Anim.StopLoop((this, "marquee"));
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float h = Height - 1, rad = h / 2;
            var track = new RectangleF(0, 0, Width - 1, h);
            using (var tp = Theme.Round(track, rad))
            using (var tb = new SolidBrush(Theme.ButtonBg))
                g.FillPath(tb, tp);

            using var fb = new SolidBrush(Theme.Accent);
            if (indeterminate)
            {
                float segW = Width * 0.3f;
                double p = phase < 0.5 ? 2 * phase * phase : 1 - Math.Pow(-2 * phase + 2, 2) / 2;   // ease in-out
                float x = (float)(-segW + (Width + segW) * p);
                using var clip = Theme.Round(track, rad);
                g.SetClip(clip);
                using var seg = Theme.Round(new RectangleF(x, 0, segW, h), rad);
                g.FillPath(fb, seg);
                g.ResetClip();
            }
            else if (shown > 0.003f)
            {
                using var fill = Theme.Round(new RectangleF(0, 0, Math.Max(h, (Width - 1) * shown), h), rad);
                g.FillPath(fb, fill);
            }
        }
    }

    // Big menu button with icon, text and a chevron that nudges on hover
    public class LauncherButton : Control
    {
        float hoverT;
        bool down;
        public string Icon { get; set; } = "";

        public LauncherButton()
        {
            SetStyle(Theme.PaintStyles, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10f);
            ForeColor = Theme.Text;
        }

        void AnimateHover(float to, int ms) => Anim.Run((this, "hover"), hoverT, to, ms, v => { hoverT = v; Invalidate(); });
        protected override void OnMouseEnter(EventArgs e) { AnimateHover(1f, 120); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { down = false; AnimateHover(0f, 220); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = Theme.Mix(Theme.Card, Theme.ButtonBg, hoverT);
            if (down) fill = Theme.Mix(fill, Color.Black, 0.2f);
            using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(6));
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            using (var p = new Pen(Theme.Mix(Theme.Border, Theme.Track, hoverT * 0.6f))) g.DrawPath(p, path);

            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var iconFont = Theme.F(Theme.IconFont, 12f);
            var chevFont = Theme.F(Theme.IconFont, 8f);
            TextRenderer.DrawText(g, Icon, iconFont, new Rectangle(LogicalToDeviceUnits(16), 0, LogicalToDeviceUnits(28), Height), Theme.Text, flags);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(LogicalToDeviceUnits(52), 0, Width - LogicalToDeviceUnits(96), Height), ForeColor, flags);
            int cx = Width - LogicalToDeviceUnits(30) + (int)(LogicalToDeviceUnits(3) * hoverT);
            TextRenderer.DrawText(g, "", chevFont, new Rectangle(cx, 0, LogicalToDeviceUnits(20), Height),
                Theme.Mix(Theme.SubText, Theme.Text, hoverT), flags);
        }
    }

    // Small accent-colored link with an icon
    public class LinkText : Control
    {
        bool hover;
        public string Icon { get; set; } = "";

        public LinkText()
        {
            SetStyle(Theme.PaintStyles, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            var iconFont = Theme.F(Theme.IconFont, 9.5f);
            var a = Icon.Length > 0 ? TextRenderer.MeasureText(Icon, iconFont) : Size.Empty;
            var b = TextRenderer.MeasureText(Text, Font);
            Size = new Size(a.Width + b.Width + LogicalToDeviceUnits(2), Math.Max(a.Height, b.Height) + LogicalToDeviceUnits(4));
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            Color c = hover ? Theme.Text : Theme.AccentLight;
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine;
            var iconFont = Theme.F(Theme.IconFont, 9.5f);
            int x = 0;
            if (Icon.Length > 0)
            {
                var a = TextRenderer.MeasureText(g, Icon, iconFont);
                TextRenderer.DrawText(g, Icon, iconFont, new Rectangle(0, 0, a.Width, Height), c, flags);
                x = a.Width;
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x, Height), c, flags);
        }
    }
}
