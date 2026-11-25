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
    // CUSTOM CONTROLS
    public class Card : Panel
    {
        public Color BorderColor { get; set; } = Theme.Border;
        public int Radius { get; set; } = 5;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control is Label l && l.BackColor == Color.Transparent) l.BackColor = BackColor;
            if (e.Control != null)
            {
                L.Apply(e.Control);
                WatchHover(e.Control);
            }
        }

        // The card's edge lights up while the mouse is over it (or over anything inside it)
        float hoverT;
        bool hovering;

        void WatchHover(Control c)
        {
            c.MouseEnter += (_, _) => UpdateHover();
            c.MouseLeave += (_, _) => UpdateHover();
            c.ControlAdded += (_, a) => { if (a.Control != null) WatchHover(a.Control); };
            foreach (Control child in c.Controls) WatchHover(child);
        }

        void UpdateHover()
        {
            if (IsDisposed || !IsHandleCreated) return;
            bool inside = ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (inside == hovering) return;
            hovering = inside;
            Anim.Run((this, "glow"), hoverT, inside ? 1f : 0f, inside ? 160 : 260, v => { hoverT = v; Invalidate(); });
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); UpdateHover(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); UpdateHover(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float rad = LogicalToDeviceUnits(Theme.IsGlass ? Radius + 3 : Radius);
            using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), rad);
            using var b = new SolidBrush(BackColor);
            g.FillPath(b, path);

            // a custom border color (like a focused text box) always wins over the glass edge
            if (!Theme.IsGlass || BorderColor != Theme.Border)
            {
                // other themes: the edge brightens a little on hover (a custom border color, like a focused text box, stays as is)
                var edgeColor = BorderColor == Theme.Border ? Theme.Mix(BorderColor, Theme.AccentLight, 0.30f * hoverT) : BorderColor;
                using var p = new Pen(edgeColor);
                g.DrawPath(p, path);
                return;
            }

            // Glass: soft light along the top edge fading down, like light hitting frosted glass
            int top = (int)(60 + 60 * hoverT), bottom = (int)(14 + 26 * hoverT);
            Color edgeTop = Theme.Mix(Theme.Border, Color.White, 0.30f + 0.25f * hoverT);
            Color edgeBottom = Theme.Mix(Theme.Border, Theme.Accent, 0.55f * hoverT);
            using (var lg = new LinearGradientBrush(new RectangleF(0, -1, Width, Height + 2), Color.FromArgb(top + 120, edgeTop), Color.FromArgb(bottom + 200, edgeBottom), 90f))
            using (var edge = new Pen(lg, 1f))
                g.DrawPath(edge, path);
            if (Height > rad * 2 + 4)
            {
                using var sheen = new Pen(Color.FromArgb((int)(22 + 20 * hoverT), 255, 255, 255));
                g.DrawLine(sheen, rad, 1.5f, Width - rad - 1, 1.5f);
            }
        }
    }

    // Card with a clickable header that smoothly opens/closes
    public class Expander : Card
    {
        readonly int headerH, contentH;
        bool expanded;
        float chevron;
        public Panel Content { get; }

        public Expander(string titleText, string descText, int width, int headerHeight, int contentHeight)
        {
            headerH = headerHeight;
            contentH = contentHeight;
            Size = new Size(width, headerH);
            Cursor = Cursors.Hand;

            var title = new Label
            {
                Text = titleText, AutoSize = true, Font = Theme.F("Segoe UI", 9.75f),
                ForeColor = Theme.Text, BackColor = Color.Transparent, Location = new Point(LogicalToDeviceUnits(16), LogicalToDeviceUnits(10)),
            };
            var desc = new Label
            {
                Text = descText, AutoSize = true, Font = Theme.F("Segoe UI", 8.5f),
                ForeColor = Theme.SubText, BackColor = Color.Transparent, Location = new Point(LogicalToDeviceUnits(16), LogicalToDeviceUnits(31)),
            };
            Content = new Panel
            {
                BackColor = Theme.Card, Cursor = Cursors.Default,
                Location = new Point(1, headerH), Size = new Size(width - 2, contentH),
            };
            Controls.Add(title);
            Controls.Add(desc);
            Controls.Add(Content);

            Click += (_, _) => Toggle();
            title.Click += (_, _) => Toggle();
            desc.Click += (_, _) => Toggle();
            title.Cursor = desc.Cursor = Cursors.Hand;
        }

        public void Toggle()
        {
            expanded = !expanded;
            int target = expanded ? headerH + contentH + LogicalToDeviceUnits(6) : headerH;
            Anim.Run((this, "height"), Height, target, 300, v => Height = (int)Math.Round(v));
            Anim.Run((this, "chevron"), chevron, expanded ? 1f : 0f, 300, v => { chevron = v; Invalidate(); });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = Width - LogicalToDeviceUnits(30), cy = headerH / 2f, s = LogicalToDeviceUnits(5);
            var pts = new[] { new PointF(cx - s, cy - s / 2), new PointF(cx, cy + s / 2), new PointF(cx + s, cy - s / 2) };
            using (var m = new Matrix())
            {
                m.RotateAt(180f * chevron, new PointF(cx, cy));
                m.TransformPoints(pts);
            }
            using var pen = new Pen(Theme.Text, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, pts);
            if (Height > headerH + 2)
            {
                using var line = new Pen(Theme.Border);
                g.DrawLine(line, 1, headerH - 1, Width - 2, headerH - 1);
            }
        }
    }

    // Draws the old and new page as bitmaps sliding past each other (like scrolling between tabs)
    public class PageTransition : Control
    {
        Bitmap? from, to;
        float t;
        int dir = 1;

        public PageTransition()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        }

        public static Bitmap Snapshot(Control c)
        {
            var bmp = new Bitmap(Math.Max(1, c.Width), Math.Max(1, c.Height), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            c.DrawToBitmap(bmp, new Rectangle(Point.Empty, c.Size));
            return bmp;
        }

        public void Cover(Bitmap snapshot)
        {
            to?.Dispose(); to = null;
            from?.Dispose(); from = snapshot;
            t = 0;
            Visible = true;
            BringToFront();
            Refresh();
        }

        public void Slide(Bitmap next, int direction)
        {
            to?.Dispose();
            to = next;
            dir = direction;
            Anim.Run((this, "slide"), 0f, 1f, 340, v => { t = v; Invalidate(); Update(); }, () =>
            {
                Visible = false;
                from?.Dispose(); from = null;
                to?.Dispose(); to = null;
            });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            Theme.ClearToParent(this, g, Theme.Bg);
            int off = (int)Math.Round(Height * t) * dir;
            if (from != null) g.DrawImageUnscaled(from, 0, -off);
            if (to != null) g.DrawImageUnscaled(to, 0, dir * Height - off);
        }
    }

    // Dock=Top panel that slides open/closed and holds a card
    public class CollapsePanel : Panel
    {
        readonly int openH;
        public Card Inner { get; }
        public bool IsOpen { get; private set; }

        public CollapsePanel(int width, int cardHeight, int gap)
        {
            openH = cardHeight + gap;
            Dock = DockStyle.Top;
            Height = 0;
            BackColor = Theme.Bg;
            Inner = new Card { Location = new Point(0, 0), Size = new Size(width, cardHeight) };
            Controls.Add(Inner);
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            Anim.Run((this, "height"), Height, IsOpen ? openH : 0, 260, v => Height = (int)Math.Round(v));
        }

        public void Collapse() { if (IsOpen) Toggle(); }
    }

    public class RoundButton : Control
    {
        float hoverT;
        bool down;
        public Color BaseColor { get; set; } = Theme.Accent;
        public Color HoverColor { get; set; } = Theme.AccentHover;
        public Color GradientTo { get; set; } = Color.Empty;
        public Color BorderColor { get; set; } = Color.Empty;
        public int Radius { get; set; } = 4;
        public string Icon { get; set; } = "";

        public RoundButton()
        {
            SetStyle(Theme.PaintStyles, true);
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            Font = Theme.F("Segoe UI Semibold", 9f);
        }

        void AnimateHover(float to, int ms) => Anim.Run((this, "hover"), hoverT, to, ms, v => { hoverT = v; Invalidate(); });

        protected override void OnMouseEnter(EventArgs e) { AnimateHover(1f, 120); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { down = false; AnimateHover(0f, 200); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        // Windows also sends a click for the right mouse button: only the left one presses a button
        protected override void OnClick(EventArgs e) { if (e is MouseEventArgs m && m.Button != MouseButtons.Left) return; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color bg = Theme.SolidBack(this, Theme.Bg);
            Theme.ClearToParent(this, g, bg);   // same as a plain clear, unless there's a background picture behind
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color b0 = BaseColor.A == 0 ? bg : BaseColor;
            Color h0 = HoverColor.A == 0 ? bg : HoverColor;
            Color fill = Theme.Mix(b0, h0, hoverT);
            if (down) fill = Theme.Mix(fill, Color.Black, 0.15f);
            if (!Enabled) fill = Theme.Mix(fill, bg, 0.5f);

            using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(Radius));
            if (BaseColor.A == 0 && GradientTo.IsEmpty)
            {
                // see-through button: only the hover color fades in, so whatever is behind it shows
                float a = down ? Math.Max(hoverT, 0.85f) : hoverT;
                if (HoverColor.A > 0 && a > 0.01f)
                {
                    Color h = down ? Theme.Mix(HoverColor, Color.Black, 0.15f) : HoverColor;
                    using var hb = new SolidBrush(Color.FromArgb((int)(255 * Math.Clamp(a, 0f, 1f)), h));
                    g.FillPath(hb, path);
                }
            }
            else if (!GradientTo.IsEmpty)
            {
                Color g1 = Theme.Mix(BaseColor, Color.White, hoverT * 0.12f);
                Color g2 = Theme.Mix(GradientTo, Color.White, hoverT * 0.12f);
                if (down) { g1 = Theme.Mix(g1, Color.Black, 0.15f); g2 = Theme.Mix(g2, Color.Black, 0.15f); }
                if (!Enabled) { g1 = Theme.Mix(g1, bg, 0.5f); g2 = Theme.Mix(g2, bg, 0.5f); }
                using var lg = new LinearGradientBrush(new RectangleF(-1, 0, Width + 2, Height), g1, g2, 0f);
                g.FillPath(lg, path);
            }
            else
            {
                using var b = new SolidBrush(fill);
                g.FillPath(b, path);
            }
            if (!BorderColor.IsEmpty) { using var p = new Pen(BorderColor); g.DrawPath(p, path); }

            Color fc = Enabled ? ForeColor : Theme.Mix(ForeColor, bg, 0.5f);
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var iconFont = Theme.F(Theme.IconFont, Font.Size);
            bool showIcon = Icon.Length > 0;
            Size iconSize = showIcon ? TextRenderer.MeasureText(g, Icon, iconFont, Size.Empty, flags) : Size.Empty;
            Size textSize = Text.Length > 0 ? TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags) : Size.Empty;
            int gap = showIcon && Text.Length > 0 ? LogicalToDeviceUnits(8) : 0;
            int avail = Width - LogicalToDeviceUnits(12);
            // long text (some languages) on a narrow button: drop the icon first, then shorten the text with "..."
            if (showIcon && Text.Length > 0 && iconSize.Width + gap + textSize.Width > avail)
            {
                showIcon = false;
                iconSize = Size.Empty;
                gap = 0;
            }
            int x = (Width - iconSize.Width - gap - textSize.Width) / 2;
            if (showIcon)
            {
                TextRenderer.DrawText(g, Icon, iconFont, new Rectangle(x, 0, iconSize.Width + 2, Height), fc, flags);
                x += iconSize.Width + gap;
            }
            if (Text.Length > 0)
            {
                if (textSize.Width > avail)
                    TextRenderer.DrawText(g, Text, Font, new Rectangle(LogicalToDeviceUnits(6), 0, Math.Max(1, avail), Height), fc,
                        flags | TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter);
                else
                    TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, textSize.Width + 4, Height), fc, flags);
            }
        }
    }

    // Sidebar navigation with a sliding selection indicator and search filtering
    public class NavList : Control
    {
        record Item(string Key, string Icon, string Text, string Keywords, bool IsHeader);
        readonly List<Item> items = new();
        List<int> visible = new();
        float[] slotY = Array.Empty<float>();   // y of each visible row
        float[] hoverT = Array.Empty<float>();
        int hover = -1;
        string selectedKey = "";
        float indicatorY = -1;
        public int ItemHeight { get; set; } = 40;
        public int Gap { get; set; } = 4;
        public int HeaderHeight { get; set; } = 26;
        public event Action<string>? Navigate;

        public NavList()
        {
            SetStyle(Theme.PaintStyles, true);
            Font = Theme.F("Segoe UI", 9.5f);
        }

        public void Add(string key, string icon, string text, string keywords) => Push(new Item(key, icon, L.T(text), keywords, false));

        // a small title above a group of tabs
        public void AddHeader(string text) => Push(new Item("", "", L.T(text), "", true));

        void Push(Item it)
        {
            items.Add(it);
            visible = Enumerable.Range(0, items.Count).ToList();
            hoverT = new float[items.Count];
            Relayout();
        }

        void Relayout()
        {
            slotY = new float[visible.Count];
            float y = 0;
            for (int pos = 0; pos < visible.Count; pos++)
            {
                slotY[pos] = y;
                y += items[visible[pos]].IsHeader ? HeaderHeight : ItemHeight + Gap;
            }
            ContentHeight = (int)Math.Ceiling(y);
        }

        public int ContentHeight { get; private set; }

        public string? FirstVisibleKey => visible.Select(i => items[i]).FirstOrDefault(i => !i.IsHeader)?.Key;
        public int IndexOf(string key) => items.FindIndex(i => !i.IsHeader && i.Key == key);
        public string? TextOf(string key) => items.Find(i => !i.IsHeader && i.Key == key)?.Text;

        public void SetFilter(string text)
        {
            text = text.Trim();
            var keep = new List<int>();
            int header = -1;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.IsHeader) { header = i; continue; }
                bool match = text.Length == 0
                             || it.Text.Contains(text, StringComparison.OrdinalIgnoreCase)
                             || it.Keywords.Contains(text, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
                // a header only shows when at least one tab under it shows
                if (header >= 0 && !keep.Contains(header)) keep.Add(header);
                keep.Add(i);
            }
            visible = keep;
            Relayout();
            hover = -1;
            indicatorY = SlotY(selectedKey);
            Invalidate();
        }

        float SlotY(string key)
        {
            int idx = IndexOf(key);
            int pos = visible.IndexOf(idx);
            return pos < 0 || idx < 0 ? -1 : slotY[pos];
        }

        public void Select(string key, bool animate)
        {
            selectedKey = key;
            float target = SlotY(key);
            if (!animate || indicatorY < 0 || target < 0) { indicatorY = target; Invalidate(); return; }
            Anim.Run((this, "select"), indicatorY, target, 280, v => { indicatorY = v; Invalidate(); });
        }

        int HitTest(int y)
        {
            for (int pos = 0; pos < visible.Count; pos++)
            {
                var it = items[visible[pos]];
                if (it.IsHeader) continue;
                if (y >= slotY[pos] && y < slotY[pos] + ItemHeight) return visible[pos];
            }
            return -1;
        }

        void SetHover(int idx)
        {
            if (idx == hover) return;
            int old = hover;
            hover = idx;
            if (old >= 0) Anim.Run((this, "hover", old), hoverT[old], 0f, 200, v => { hoverT[old] = v; Invalidate(); });
            if (idx >= 0) Anim.Run((this, "hover", idx), hoverT[idx], 1f, 120, v => { hoverT[idx] = v; Invalidate(); });
            Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseMove(MouseEventArgs e) { SetHover(HitTest(e.Y)); base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { SetHover(-1); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            int idx = HitTest(e.Y);
            if (idx >= 0) Navigate?.Invoke(items[idx].Key);
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float r = LogicalToDeviceUnits(Theme.IsGlass ? 7 : 4);

            if (indicatorY >= 0)
            {
                var rect = new RectangleF(0.5f, indicatorY + 0.5f, Width - 1.5f, ItemHeight - 1f);
                using var sel = Theme.Round(rect, r);
                if (Theme.IsGlass)
                {
                    // frosted pill: accent glow on the left fading out, thin light edge
                    using var fb = new SolidBrush(Color.FromArgb(34, 255, 255, 255));
                    g.FillPath(fb, sel);
                    using var glow = new LinearGradientBrush(new RectangleF(rect.X - 1, rect.Y, rect.Width + 2, rect.Height),
                        Color.FromArgb(90, Theme.Accent), Color.FromArgb(0, Theme.Accent), 0f);
                    g.FillPath(glow, sel);
                    using var edge = new Pen(Color.FromArgb(70, 255, 255, 255));
                    g.DrawPath(edge, sel);
                }
                else
                {
                    using var sb = new SolidBrush(Theme.NavSelected);
                    g.FillPath(sb, sel);
                }
            }

            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            const TextFormatFlags headFlags = TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var iconFont = Theme.F(Theme.IconFont, 11f);
            var headFont = Theme.F("Segoe UI Semibold", 7.5f);
            for (int pos = 0; pos < visible.Count; pos++)
            {
                int i = visible[pos];
                var it = items[i];
                int y = (int)slotY[pos];
                if (it.IsHeader)
                {
                    TextRenderer.DrawText(g, it.Text.ToUpperInvariant(), headFont,
                        new Rectangle(LogicalToDeviceUnits(12), y, Width - LogicalToDeviceUnits(16), HeaderHeight - LogicalToDeviceUnits(6)),
                        Theme.SubText, headFlags);
                    continue;
                }
                bool isSel = it.Key == selectedKey;
                if (!isSel && hoverT[i] > 0.01f)
                {
                    float t = Math.Clamp(hoverT[i], 0f, 1f);
                    using var hp = Theme.Round(new RectangleF(0.5f, y + 0.5f, Width - 1.5f, ItemHeight - 1f), r);
                    using var hb = new SolidBrush(Theme.IsGlass ? Color.FromArgb((int)(20 * t), 255, 255, 255)
                                                                : Color.FromArgb((int)(255 * t), Theme.NavHover));
                    g.FillPath(hb, hp);
                }
                TextRenderer.DrawText(g, it.Icon, iconFont, new Rectangle(LogicalToDeviceUnits(12), y, LogicalToDeviceUnits(24), ItemHeight),
                    isSel ? Theme.AccentLight : Theme.Text, flags);
                TextRenderer.DrawText(g, it.Text, Font, new Rectangle(LogicalToDeviceUnits(42), y, Width - LogicalToDeviceUnits(46), ItemHeight),
                    Theme.Text, flags);
            }

            if (indicatorY >= 0)
            {
                float ph = ItemHeight * 0.42f;
                using var pill = Theme.Round(new RectangleF(0, indicatorY + (ItemHeight - ph) / 2, LogicalToDeviceUnits(3), ph), 1.5f);
                using var pb = new SolidBrush(Theme.Accent);
                g.FillPath(pb, pill);
            }
        }
    }

    public class ToggleSwitch : Control
    {
        bool isChecked;
        float knob;   // 0 = off, 1 = on (animated)
        public event EventHandler? CheckedChanged;

        public bool Checked
        {
            get => isChecked;
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Anim.Run((this, "knob"), knob, value ? 1f : 0f, 180, v => { knob = v; Invalidate(); });
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ToggleSwitch()
        {
            SetStyle(Theme.PaintStyles, true);
            Cursor = Cursors.Hand;
            Size = new Size(44, 22);
        }

        protected override void OnClick(EventArgs e)
        {
            if (e is MouseEventArgs m && m.Button != MouseButtons.Left) return;   // right-click doesn't flip it
            Checked = !Checked;
            base.OnClick(e);
        }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color bg = Theme.SolidBack(this, Theme.Card);
            Theme.ClearToParent(this, g, bg);
            if (!Enabled)
            {
                // turned off: just a faint outline
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var dr = new RectangleF(1, 1, Width - 3, Height - 3);
                using var dpath = Theme.Round(dr, dr.Height / 2);
                using (var dp = new Pen(Theme.Mix(bg, Theme.SubText, 0.35f), 1.2f)) g.DrawPath(dp, dpath);
                float ds = dr.Height * 0.5f, dpad = (dr.Height - ds) / 2;
                using var dk = new SolidBrush(Theme.Mix(bg, Theme.SubText, 0.35f));
                g.FillEllipse(dk, dr.X + dpad + 1, dr.Y + dpad, ds, ds);
                return;
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using var path = Theme.Round(r, r.Height / 2);
            using (var b = new SolidBrush(Theme.Mix(bg, Theme.Accent, knob))) g.FillPath(b, path);
            using (var p = new Pen(Theme.Mix(Theme.SubText, Theme.Accent, knob), 1.2f)) g.DrawPath(p, path);

            float size = r.Height * (0.5f + 0.12f * knob);
            float pad = (r.Height - size) / 2;
            float x0 = r.X + pad + 1, x1 = r.Right - pad - size - 1;
            float x = x0 + (x1 - x0) * knob;
            using var kb = new SolidBrush(Theme.Mix(Theme.SubText, Theme.Bg, knob));
            g.FillEllipse(kb, x, r.Y + pad, size, size);
        }
    }

    public class ModernSlider : Control
    {
        int min, max = 10, val;
        float shown;
        bool dragging;
        public event EventHandler? ValueChanged;
        public event EventHandler? Released;
        public bool ShowTicks { get; set; }

        public int Minimum { get => min; set { min = value; Invalidate(); } }
        public int Maximum { get => max; set { max = value; Invalidate(); } }
        public int Value
        {
            get => val;
            set
            {
                int v = Math.Clamp(value, min, max);
                if (v == val) return;
                val = v;
                if (dragging) Anim.Run((this, "value"), shown, v, 90, x => { shown = x; Invalidate(); });
                else Anim.Run((this, "value"), shown, v, 260, x => { shown = x; Invalidate(); });
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ModernSlider()
        {
            SetStyle(Theme.PaintStyles, true);
            Cursor = Cursors.Hand;
            Height = 32;
        }

        float ThumbR => Math.Min(Height * 0.3f, 10f * DeviceDpi / 96f);
        float TrackLeft => ThumbR + 2;
        float TrackRight => Width - ThumbR - 2;
        float XFor(float v) => max == min ? TrackLeft : TrackLeft + (TrackRight - TrackLeft) * (v - min) / (max - min);

        void SetFromX(int x)
        {
            float t = (x - TrackLeft) / Math.Max(1f, TrackRight - TrackLeft);
            Value = min + (int)Math.Round(Math.Clamp(t, 0f, 1f) * (max - min));
        }

        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; SetFromX(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging) { dragging = false; Released?.Invoke(this, EventArgs.Empty); }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color bg = Theme.SolidBack(this, Theme.Card);
            Theme.ClearToParent(this, g, bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cy = Height / 2f;
            float th = Math.Max(4f, Height * 0.14f);
            float w = Math.Max(1f, TrackRight - TrackLeft);

            using (var track = Theme.Round(new RectangleF(TrackLeft, cy - th / 2, w, th), th / 2))
            using (var tb = new SolidBrush(Theme.Track))
                g.FillPath(tb, track);

            float x = XFor(shown);
            if (x - TrackLeft > 1)
            {
                using var fill = Theme.Round(new RectangleF(TrackLeft, cy - th / 2, x - TrackLeft, th), th / 2);
                using var fb = new SolidBrush(Theme.Accent);
                g.FillPath(fb, fill);
            }

            if (ShowTicks)
                for (int v = min; v <= max; v++)
                {
                    float tx = XFor(v);
                    using var tick = new SolidBrush(v <= shown + 0.01f ? Color.White : Theme.SubText);
                    g.FillEllipse(tick, tx - 2, cy - 2, 4, 4);
                }

            float r = ThumbR;
            using (var ring = new SolidBrush(Theme.ButtonBg)) g.FillEllipse(ring, x - r, cy - r, r * 2, r * 2);
            using (var border = new Pen(Theme.Border)) g.DrawEllipse(border, x - r, cy - r, r * 2, r * 2);
            float inner = r * (dragging ? 0.62f : 0.5f);
            using (var dot = new SolidBrush(Theme.AccentLight)) g.FillEllipse(dot, x - inner, cy - inner, inner * 2, inner * 2);
        }
    }

    public class ProgressLine : Control
    {
        float target, shown;
        public bool ShowTrack { get; set; }
        public float Value
        {
            get => target;
            set
            {
                target = Math.Clamp(value, 0f, 1f);
                Anim.Run((this, "progress"), shown, target, 300, v => { shown = v; Invalidate(); });
            }
        }

        public ProgressLine() => SetStyle(Theme.PaintStyles, true);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bar);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float rad = ShowTrack ? Height / 2f : 0;
            if (ShowTrack)
            {
                using var tp = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), rad);
                using var tb = new SolidBrush(Theme.ButtonBg);
                g.FillPath(tb, tp);
            }
            if (shown <= 0.002f) return;
            float w = Math.Max(ShowTrack ? Height : 1, (Width - 1) * shown);
            using var fb = new SolidBrush(Theme.Accent);
            using var fp = Theme.Round(new RectangleF(0, 0, w, Height - 1), rad);
            g.FillPath(fb, fp);
        }
    }

    public class DarkCombo : ComboBox
    {
        public DarkCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            Font = Theme.F("Segoe UI", 9.25f);
            Cursor = Cursors.Hand;
        }

        // Windows draws a light border and arrow button on flat combo boxes; paint over them in the theme's colors
        protected override void WndProc(ref Message m)
        {
            // Mouse wheel over a closed dropdown scrolls the page instead of quietly changing the setting
            if (m.Msg == 0x020A && !DroppedDown)   // WM_MOUSEWHEEL
            {
                for (Control? p = Parent; p != null; p = p.Parent)
                {
                    if (p is PagePanel page)
                    {
                        int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
                        page.Wheel(delta);
                        m.Result = IntPtr.Zero;
                        return;
                    }
                }
            }
            // WM_PRINT / WM_PRINTCLIENT: the page is being captured for the tab-switch and scroll animations.
            // Windows prints dropdowns in its own white style there, so draw the whole thing in the theme's colors.
            if (m.Msg == 0x0317 || m.Msg == 0x0318)
            {
                base.WndProc(ref m);
                if (m.WParam == IntPtr.Zero) return;
                try
                {
                    using var pg = Graphics.FromHdc(m.WParam);
                    var r = ClientRectangle;
                    using (var bg = new SolidBrush(Theme.Input)) pg.FillRectangle(bg, r);
                    int aw = SystemInformation.HorizontalScrollBarArrowWidth + 2;
                    string text = SelectedIndex >= 0 && SelectedIndex < Items.Count ? GetItemText(Items[SelectedIndex]) : Text;
                    TextRenderer.DrawText(pg, text, Font, new Rectangle(r.X + 11, r.Y, Math.Max(1, r.Width - aw - 14), r.Height), Theme.Text,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    DrawFrame(pg);
                }
                catch { }
                return;
            }
            base.WndProc(ref m);
            if (m.Msg != 0x000F || !IsHandleCreated) return;   // WM_PAINT
            try
            {
                using var g = Graphics.FromHwnd(Handle);
                DrawFrame(g);
            }
            catch { }
        }

        // dark arrow button, chevron and border over Windows' light ones
        void DrawFrame(Graphics g)
        {
            var r = ClientRectangle;
            int aw = SystemInformation.HorizontalScrollBarArrowWidth + 2;
            using (var bg = new SolidBrush(Theme.Input))
            {
                g.FillRectangle(bg, r.Right - aw - 1, 1, aw, r.Height - 2);
                using var inner = new Pen(Theme.Input);
                g.DrawRectangle(inner, 1, 1, r.Width - 3, r.Height - 3);
            }
            float cx = r.Right - aw / 2f - 1, cy = r.Height / 2f;
            float a = Math.Max(3f, r.Height / 8f);
            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var chevron = new Pen(Theme.SubText, 1.5f))
                g.DrawLines(chevron, new[] { new PointF(cx - a, cy - a / 2), new PointF(cx, cy + a / 2), new PointF(cx + a, cy - a / 2) });
            g.SmoothingMode = mode;
            using var border = new Pen(Focused || DroppedDown ? Theme.Accent : Theme.Border);
            g.DrawRectangle(border, 0, 0, r.Width - 1, r.Height - 1);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using var b = new SolidBrush(sel ? Theme.Accent : Theme.Input);
            e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    public class AvatarBox : Control
    {
        Image? img;
        public Image? Image { get => img; set { img = value; Invalidate(); } }
        public string Fallback { get; set; } = "?";
        public Color Ring { get; set; } = Color.Empty;

        public AvatarBox() => SetStyle(Theme.PaintStyles, true);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var r = new RectangleF(1.5f, 1.5f, Width - 4, Height - 4);

            if (img != null)
            {
                using var bg = new SolidBrush(Theme.ButtonBg);
                g.FillEllipse(bg, r);
                using var clip = new GraphicsPath();
                clip.AddEllipse(r);
                g.SetClip(clip);
                g.DrawImage(img, r);
                g.ResetClip();
            }
            else
            {
                using var ab = new SolidBrush(Theme.ButtonHover);
                g.FillEllipse(ab, r);
                float fs = Math.Max(7f, Height / 3.0f * 72f / DeviceDpi);
                using var f = new Font(Theme.UiFont("Segoe UI Semibold", fs), fs);
                TextRenderer.DrawText(g, Fallback, f, Rectangle.Round(r), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (!Ring.IsEmpty)
            {
                using var p = new Pen(Ring, 2f);
                g.DrawEllipse(p, r);
            }
        }
    }

    // Small app logo drawn in code (no image files needed)
    public class LogoMark : Control
    {
        public LogoMark() => SetStyle(Theme.PaintStyles, true);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            Theme.DrawLogo(g, new RectangleF(0, 0, Width, Height));
        }
    }

    // Dark colors for the tray menu
    class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Card;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.ButtonHover;
        public override Color MenuItemSelected => Theme.ButtonHover;
        public override Color ImageMarginGradientBegin => Theme.Card;
        public override Color ImageMarginGradientMiddle => Theme.Card;
        public override Color ImageMarginGradientEnd => Theme.Card;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Card;
        public override Color CheckBackground => Theme.ButtonBg;
        public override Color CheckSelectedBackground => Theme.ButtonHover;
        public override Color CheckPressedBackground => Theme.ButtonHover;
    }

    // Row of color dots for the accent setting
    public class AccentPicker : Control
    {
        int hover = -1;
        public string Selected { get; set; } = "Purple";
        public event Action<string>? Picked;

        public AccentPicker()
        {
            SetStyle(Theme.PaintStyles, true);
            Cursor = Cursors.Hand;
        }

        int Slot => Height;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = Enabled ? e.X / Slot : -1;
            if (i != hover) { hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        // turned off (Glass theme): the dots fade out and can't be clicked
        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            hover = -1;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = e.X / Slot;
            if (Enabled && i >= 0 && i < Theme.Accents.Length && Theme.Accents[i].Name != Selected)
            {
                Selected = Theme.Accents[i].Name;
                Invalidate();
                Picked?.Invoke(Selected);
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < Theme.Accents.Length; i++)
            {
                var (name, color) = Theme.Accents[i];
                float pad = Slot * (hover == i ? 0.18f : 0.24f);
                var r = new RectangleF(i * Slot + pad, pad, Slot - pad * 2, Slot - pad * 2);
                using var b = new SolidBrush(Enabled ? color : Theme.Mix(color, Theme.Card, 0.75f));
                g.FillEllipse(b, r);
                if (name == Selected && Enabled)
                {
                    using var ring = new Pen(Theme.Text, 1.6f);
                    g.DrawEllipse(ring, RectangleF.Inflate(r, Slot * 0.1f, Slot * 0.1f));
                }
            }
        }
    }

    // Game icon + name tile for "Recent games"
    public class GameTile : Control
    {
        float hoverT;
        Image? img;
        public Image? Image { get => img; set { img = value; Invalidate(); } }

        public GameTile()
        {
            SetStyle(Theme.PaintStyles, true);
            Cursor = Cursors.Hand;
            Font = Theme.F("Segoe UI", 8.5f);
        }

        // right-click opens the game's menu instead of starting it
        protected override void OnClick(EventArgs e) { if (e is MouseEventArgs m && m.Button != MouseButtons.Left) return; base.OnClick(e); }

        protected override void OnMouseEnter(EventArgs e) { Anim.Run((this, "hover"), hoverT, 1f, 120, v => { hoverT = v; Invalidate(); }); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Anim.Run((this, "hover"), hoverT, 0f, 200, v => { hoverT = v; Invalidate(); }); base.OnMouseLeave(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) img?.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color bg = Theme.SolidBack(this, Theme.Bg);
            Theme.ClearToParent(this, g, bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float rad = LogicalToDeviceUnits(5);
            if (hoverT > 0.01f)
            {
                using var hp = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), rad);
                using var hb = new SolidBrush(Theme.Mix(bg, Theme.Card, hoverT));
                g.FillPath(hb, hp);
            }
            int pad = LogicalToDeviceUnits(6);
            var iconRect = new RectangleF(pad, pad, Width - pad * 2, Width - pad * 2);
            using (var path = Theme.Round(iconRect, rad))
            {
                if (img != null)
                {
                    g.SetClip(path);
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(img, iconRect);
                    g.ResetClip();
                }
                else
                {
                    using var pb = new SolidBrush(Theme.ButtonBg);
                    g.FillPath(pb, path);
                }
            }
            var textRect = new Rectangle(pad, (int)iconRect.Bottom + LogicalToDeviceUnits(4), Width - pad * 2, Height - (int)iconRect.Bottom - LogicalToDeviceUnits(6));
            TextRenderer.DrawText(g, Text, Font, textRect, Theme.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // Sidebar panel. In the Glass theme it gets a thin light edge on its right side.
    public class AuroraPanel : Panel
    {
        public AuroraPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!Theme.IsGlass) return;
            using var edge = new Pen(Color.FromArgb(40, 255, 255, 255));
            e.Graphics.DrawLine(edge, Width - 1, 0, Width - 1, Height);
        }
    }

    // Section heading on a page: a small icon and a title
    public class SectionTitle : Control
    {
        public string Glyph { get; set; } = "";

        public SectionTitle()
        {
            SetStyle(Theme.PaintStyles, true);
            Font = Theme.F("Segoe UI Semibold", 10f);
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding |
                                          TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            int x = 0;
            if (Glyph.Length > 0)
            {
                TextRenderer.DrawText(g, Glyph, Theme.F(Theme.IconFont, 10f), new Rectangle(0, 0, LogicalToDeviceUnits(22), Height), Theme.AccentLight, flags);
                x = LogicalToDeviceUnits(24);
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x, Height), Theme.Text, flags);
        }
    }

    // Small pop-up in the corner of the window: "Saved", "Font set", errors...
    // Slides up, stays a moment, slides away. Clicking it hides it right away.
    public class InAppToast : Control
    {
        readonly System.Windows.Forms.Timer life = new() { Interval = 2800 };
        bool error, showing;
        float t;
        int restY;

        public InAppToast()
        {
            SetStyle(Theme.PaintStyles, true);
            Font = Theme.F("Segoe UI", 9f);
            Visible = false;
            Cursor = Cursors.Hand;
            life.Tick += (_, _) => { life.Stop(); Slide(false); };
            Click += (_, _) => { life.Stop(); Slide(false); };
        }

        // bottomRight: where the toast's bottom-right corner rests (in the parent's coordinates)
        public void ShowMessage(string text, bool isError, Point bottomRight, int maxWidth)
        {
            if (Parent == null || IsDisposed) return;
            Text = text;
            error = isError;
            const TextFormatFlags measure = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            int textW = TextRenderer.MeasureText(text, Font, Size.Empty, measure).Width;
            int w = Math.Min(maxWidth, textW + LogicalToDeviceUnits(58));
            int h = LogicalToDeviceUnits(40);
            restY = bottomRight.Y - h;
            Bounds = new Rectangle(bottomRight.X - w, showing ? Top : restY + LogicalToDeviceUnits(14), w, h);
            using (var path = Theme.Round(new RectangleF(0, 0, w, h), LogicalToDeviceUnits(8)))
            {
                var old = Region;
                Region = new Region(path);
                old?.Dispose();
            }
            Visible = true;
            BringToFront();
            Invalidate();
            Slide(true);
            life.Stop();
            life.Interval = isError ? 4500 : 2800;
            life.Start();
        }

        void Slide(bool show)
        {
            showing = show;
            int drop = LogicalToDeviceUnits(14);
            Anim.Run((this, "slide"), t, show ? 1f : 0f, show ? 240 : 200, v =>
            {
                t = v;
                Top = restY + (int)Math.Round((1 - v) * drop);
                Invalidate();
            }, () => { if (!showing) Visible = false; });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) life.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // fades in with the slide
            Color bg = Theme.Mix(Theme.Bg, Theme.Mix(Theme.Card, Color.White, 0.05f), t);
            g.Clear(bg);
            using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(8)))
            using (var pen = new Pen(Theme.Mix(bg, Theme.Mix(Theme.Border, Color.White, 0.18f), t)))
                g.DrawPath(pen, path);

            Color iconColor = Theme.Mix(bg, error ? Theme.Danger : Theme.Success, t);
            Color textColor = Theme.Mix(bg, Theme.Text, t);
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding |
                                          TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            int pad = LogicalToDeviceUnits(14);
            TextRenderer.DrawText(g, error ? "" : "", Theme.F(Theme.IconFont, 10f),
                new Rectangle(pad, 0, LogicalToDeviceUnits(20), Height), iconColor, flags);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(pad + LogicalToDeviceUnits(26), 0, Width - pad * 2 - LogicalToDeviceUnits(26), Height), textColor, flags);
        }
    }

    // A page in the main window. Its real Windows scrollbar is pushed outside the visible area
    // (OverlayScrollBar draws a thin one instead), and the mouse wheel glides instead of jumping.
    //
    // Why the glide is smooth: moving the real page means every card, label and switch repaints
    // every frame, which can't keep up. So when a scroll starts, one picture of the whole page is
    // made (RenderContent) and ScrollProxy slides that picture: a single cheap draw per frame.
    // When the scroll ends, the real page is moved to the same spot and the picture goes away.
    public class PagePanel : FlowLayoutPanel
    {
        public bool Backdrop { get; set; }
        public event Action? Scrolled;
        public Func<Bitmap?>? RenderContent { get; set; }
        public ScrollProxy? Proxy { get; set; }

        // bumped on any click or key press in the window, so a saved page picture is never stale after you change something
        public static int InputEpoch;

        float target = -1;   // where a glide is heading, -1 when not gliding
        float visual;        // where the glide is right now
        bool gliding;
        Bitmap? frame;
        int frameEpoch = -1, frameLayout = -1, layoutVersion;
        long frameMade;

        public PagePanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        public int MaxScroll => Math.Max(0, DisplayRectangle.Height - ClientSize.Height);
        public int Offset => -AutoScrollPosition.Y;
        public int VisualOffset => gliding ? (int)Math.Round(visual) : Offset;

        public void ScrollTo(int y, bool smooth)
        {
            y = Math.Clamp(y, 0, MaxScroll);
            if (!smooth || !IsHandleCreated || !Visible || !SystemInformation.UIEffectsEnabled)
            {
                target = -1;
                Anim.Run((this, "scroll"), y, y, 1, _ => { });   // stops a glide that's still running
                EndGlide();
                SetOffset(y);
                return;
            }
            target = y;
            if (!gliding && !StartGlide())
            {
                // no picture possible: fall back to moving the real page
                Anim.Run((this, "scroll"), Offset, y, 260, v => SetOffset((int)Math.Round(v)), () => target = -1);
                return;
            }
            Anim.Run((this, "scroll"), visual, y, 320, v =>
            {
                visual = v;
                if (Proxy != null) { Proxy.Offset = (int)Math.Round(v); Proxy.Invalidate(); Proxy.Update(); }
                Scrolled?.Invoke();
            }, () => { target = -1; EndGlide(); });
        }

        public void Wheel(int delta)
        {
            if (MaxScroll <= 0) return;
            float from = target >= 0 ? target : VisualOffset;
            int step = (int)Math.Round(delta / 120f * LogicalToDeviceUnits(110));
            ScrollTo((int)Math.Round(from) - step, smooth: true);
        }

        bool StartGlide()
        {
            if (Proxy == null || RenderContent == null) return false;
            // reuse the last picture if nothing changed since (no clicks, keys or layout changes, and it's fresh)
            bool fresh = frame != null && frameEpoch == InputEpoch && frameLayout == layoutVersion
                         && Environment.TickCount64 - frameMade < 2000;
            if (!fresh)
            {
                Bitmap? made;
                try { made = RenderContent(); } catch { made = null; }
                if (made == null) return false;
                Proxy.Frame = null;
                frame?.Dispose();
                frame = made;
                frameEpoch = InputEpoch;
                frameLayout = layoutVersion;
                frameMade = Environment.TickCount64;
            }
            visual = Offset;
            gliding = true;
            Proxy.Frame = frame;
            Proxy.Offset = Offset;
            Proxy.Visible = true;
            Proxy.Update();
            return true;
        }

        void EndGlide()
        {
            if (!gliding) return;
            gliding = false;
            SetOffset((int)Math.Round(visual));
            // paint the real page under the picture first, so removing the picture shows no flicker
            if (Backdrop) Invalidate(true);
            PaintNow(this);
            if (Proxy != null) { Proxy.Visible = false; Proxy.Frame = null; }
            frameMade = Environment.TickCount64;   // the picture can be reused for a quick next scroll
            Scrolled?.Invoke();
        }

        static void PaintNow(Control c)
        {
            if (!c.Visible) return;
            c.Update();
            foreach (Control child in c.Controls) PaintNow(child);
        }

        void SetOffset(int y)
        {
            if (IsDisposed || y == Offset) return;
            AutoScrollPosition = new Point(0, y);
            Changed();
        }

        void Changed()
        {
            if (Backdrop) Invalidate(true);
            Scrolled?.Invoke();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // no base call: the glide replaces the normal jumpy scroll
            Wheel(e.Delta);
            if (e is HandledMouseEventArgs h) h.Handled = true;
        }

        protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Changed(); }
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            layoutVersion++;
            FixScrollRange();
            Scrolled?.Invoke();
        }

        // Windows works out how far a page can scroll while it's being built (hidden), and doesn't
        // always update it afterwards, so some pages couldn't scroll to their last card.
        // So: measure the bottom of the content ourselves and make sure the page can scroll that far.
        void FixScrollRange()
        {
            int bottom = 0;
            foreach (Control c in Controls)
                if (c.Visible) bottom = Math.Max(bottom, c.Bottom + c.Margin.Bottom);
            bottom += Offset + Padding.Bottom;   // children's positions are on-screen; add the scroll to get page positions
            if (AutoScrollMinSize.Height != bottom) AutoScrollMinSize = new Size(0, bottom);
        }

        // shown again (switching tabs): re-measure, since things may have changed while it was hidden
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) PerformLayout();
        }

        // Tab key / focus jumps to a control: keep the thin scrollbar in sync
        protected override Point ScrollToControl(Control activeControl)
        {
            var p = base.ScrollToControl(activeControl);
            if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(Changed));
            return p;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Proxy != null) Proxy.Frame = null;
                frame?.Dispose();
                frame = null;
            }
            base.Dispose(disposing);
        }
    }

    // Shows the page picture while a glide is running (see PagePanel). Sits over the page, under the scrollbar.
    public class ScrollProxy : Control
    {
        public Bitmap? Frame { get; set; }
        public int Offset { get; set; }
        public PagePanel? Page { get; set; }

        public ScrollProxy()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Visible = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e) { Page?.Wheel(e.Delta); base.OnMouseWheel(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            Theme.ClearToParent(this, g, Theme.Bg);   // your picture (stays still) or the plain page color
            if (Frame != null) g.DrawImageUnscaled(Frame, 0, -Offset);
        }
    }

    // Thin scrollbar drawn over the right edge of a page. Shows whatever is behind it (your picture too).
    // Gets wider when you hover it; drag it or click the track to scroll.
    public class OverlayScrollBar : Control
    {
        PagePanel? page;
        float hoverT;
        bool dragging;
        int dragStartY, dragStartOffset;

        public OverlayScrollBar()
        {
            SetStyle(Theme.PaintStyles, true);
        }

        public void Attach(PagePanel p)
        {
            page = p;
            p.Scrolled += () => { if (!IsDisposed) Invalidate(); };
        }

        Rectangle Thumb()
        {
            if (page == null || page.IsDisposed) return Rectangle.Empty;
            int view = page.ClientSize.Height, content = page.DisplayRectangle.Height;
            if (content <= view || view <= 0 || Height <= 0) return Rectangle.Empty;
            int h = Math.Max(LogicalToDeviceUnits(36), (int)((long)Height * view / content));
            h = Math.Min(h, Height);
            int y = (int)((long)(Height - h) * page.VisualOffset / Math.Max(1, page.MaxScroll));
            return new Rectangle(0, y, Width, h);
        }

        protected override void OnMouseEnter(EventArgs e) { Anim.Run((this, "hover"), hoverT, 1f, 140, v => { hoverT = v; Invalidate(); }); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { if (!dragging) Anim.Run((this, "hover"), hoverT, 0f, 220, v => { hoverT = v; Invalidate(); }); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var t = Thumb();
            if (page == null || t.IsEmpty || e.Button != MouseButtons.Left) return;
            if (t.Contains(e.Location))
            {
                dragging = true;
                dragStartY = e.Y;
                dragStartOffset = page.VisualOffset;
            }
            else
            {
                // click on the track: glide there
                float f = (e.Y - t.Height / 2f) / Math.Max(1, Height - t.Height);
                page.ScrollTo((int)(f * page.MaxScroll), smooth: true);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging || page == null) return;
            var t = Thumb();
            int track = Math.Max(1, Height - t.Height);
            page.ScrollTo(dragStartOffset + (int)((long)(e.Y - dragStartY) * page.MaxScroll / track), smooth: false);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            if (!ClientRectangle.Contains(e.Location)) Anim.Run((this, "hover"), hoverT, 0f, 220, v => { hoverT = v; Invalidate(); });
        }

        protected override void OnMouseWheel(MouseEventArgs e) { page?.Wheel(e.Delta); base.OnMouseWheel(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            var t = Thumb();
            if (t.IsEmpty) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = LogicalToDeviceUnits(4) + LogicalToDeviceUnits(3) * hoverT;
            float x = Width - w - LogicalToDeviceUnits(2);
            using var path = Theme.Round(new RectangleF(x, t.Y + 2, w, t.Height - 4), w / 2);
            using var b = new SolidBrush(Color.FromArgb((int)(95 + 85 * hoverT), Theme.Mix(Theme.SubText, Color.White, 0.25f)));
            g.FillPath(b, path);
        }
    }
}
