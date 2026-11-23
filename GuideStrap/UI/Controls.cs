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
            if (e.Control != null) L.Apply(e.Control);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(Radius));
            using var b = new SolidBrush(BackColor);
            g.FillPath(b, path);
            using var p = new Pen(BorderColor);
            g.DrawPath(p, path);
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
                Text = titleText, AutoSize = true, Font = new Font("Segoe UI", 9.75f),
                ForeColor = Theme.Text, BackColor = Color.Transparent, Location = new Point(LogicalToDeviceUnits(16), LogicalToDeviceUnits(10)),
            };
            var desc = new Label
            {
                Text = descText, AutoSize = true, Font = new Font("Segoe UI", 8.5f),
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
            Font = new Font("Segoe UI Semibold", 9f);
        }

        void AnimateHover(float to, int ms) => Anim.Run((this, "hover"), hoverT, to, ms, v => { hoverT = v; Invalidate(); });

        protected override void OnMouseEnter(EventArgs e) { AnimateHover(1f, 120); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { down = false; AnimateHover(0f, 200); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color bg = Parent?.BackColor ?? Theme.Bg;
            g.Clear(bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color b0 = BaseColor.A == 0 ? bg : BaseColor;
            Color h0 = HoverColor.A == 0 ? bg : HoverColor;
            Color fill = Theme.Mix(b0, h0, hoverT);
            if (down) fill = Theme.Mix(fill, Color.Black, 0.15f);
            if (!Enabled) fill = Theme.Mix(fill, bg, 0.5f);

            using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(Radius));
            if (!GradientTo.IsEmpty)
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
            Size iconSize = Icon.Length > 0 ? TextRenderer.MeasureText(g, Icon, iconFont, Size.Empty, flags) : Size.Empty;
            Size textSize = Text.Length > 0 ? TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags) : Size.Empty;
            int gap = Icon.Length > 0 && Text.Length > 0 ? LogicalToDeviceUnits(8) : 0;
            int x = (Width - iconSize.Width - gap - textSize.Width) / 2;
            if (Icon.Length > 0)
            {
                TextRenderer.DrawText(g, Icon, iconFont, new Rectangle(x, 0, iconSize.Width + 2, Height), fc, flags);
                x += iconSize.Width + gap;
            }
            if (Text.Length > 0)
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, textSize.Width + 4, Height), fc, flags);
        }
    }

    // Sidebar navigation with a sliding selection indicator and search filtering
    public class NavList : Control
    {
        record Item(string Key, string Icon, string Text, string Keywords);
        readonly List<Item> items = new();
        List<int> visible = new();
        float[] hoverT = Array.Empty<float>();
        int hover = -1;
        string selectedKey = "";
        float indicatorY = -1;
        public int ItemHeight { get; set; } = 40;
        public int Gap { get; set; } = 4;
        public event Action<string>? Navigate;

        public NavList()
        {
            SetStyle(Theme.PaintStyles, true);
            Font = new Font("Segoe UI", 9.5f);
        }

        public void Add(string key, string icon, string text, string keywords)
        {
            items.Add(new Item(key, icon, L.T(text), keywords));
            visible = Enumerable.Range(0, items.Count).ToList();
            hoverT = new float[items.Count];
        }

        public string? FirstVisibleKey => visible.Count > 0 ? items[visible[0]].Key : null;
        public int IndexOf(string key) => items.FindIndex(i => i.Key == key);
        public string? TextOf(string key) => items.Find(i => i.Key == key)?.Text;

        public void SetFilter(string text)
        {
            text = text.Trim();
            visible = Enumerable.Range(0, items.Count)
                .Where(i => text.Length == 0
                            || items[i].Text.Contains(text, StringComparison.OrdinalIgnoreCase)
                            || items[i].Keywords.Contains(text, StringComparison.OrdinalIgnoreCase))
                .ToList();
            hover = -1;
            indicatorY = SlotY(selectedKey);
            Invalidate();
        }

        float SlotY(string key)
        {
            int idx = items.FindIndex(i => i.Key == key);
            int pos = visible.IndexOf(idx);
            return pos < 0 ? -1 : pos * (ItemHeight + Gap);
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
            int pos = y / (ItemHeight + Gap);
            if (pos < 0 || pos >= visible.Count) return -1;
            if (y - pos * (ItemHeight + Gap) > ItemHeight) return -1;
            return visible[pos];
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
            float r = LogicalToDeviceUnits(4);

            if (indicatorY >= 0)
            {
                using var sel = Theme.Round(new RectangleF(0, indicatorY, Width - 1, ItemHeight), r);
                using var sb = new SolidBrush(Theme.NavSelected);
                g.FillPath(sb, sel);
            }

            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var iconFont = Theme.F(Theme.IconFont, 11f);
            for (int pos = 0; pos < visible.Count; pos++)
            {
                int i = visible[pos];
                var it = items[i];
                int y = pos * (ItemHeight + Gap);
                bool isSel = it.Key == selectedKey;
                if (!isSel && hoverT[i] > 0.01f)
                {
                    using var hp = Theme.Round(new RectangleF(0, y, Width - 1, ItemHeight), r);
                    using var hb = new SolidBrush(Color.FromArgb((int)(255 * Math.Clamp(hoverT[i], 0f, 1f)), Theme.NavHover));
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

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color bg = Parent?.BackColor ?? Theme.Card;
            g.Clear(bg);
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
            Color bg = Parent?.BackColor ?? Theme.Card;
            g.Clear(bg);
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
            Font = new Font("Segoe UI", 9.25f);
            Cursor = Cursors.Hand;
        }

        // Windows draws a light border and arrow button on flat combo boxes; paint over them in the theme's colors
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F || !IsHandleCreated) return;   // WM_PAINT
            try
            {
                using var g = Graphics.FromHwnd(Handle);
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
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var chevron = new Pen(Theme.SubText, 1.5f))
                    g.DrawLines(chevron, new[] { new PointF(cx - a, cy - a / 2), new PointF(cx, cy + a / 2), new PointF(cx + a, cy - a / 2) });
                g.SmoothingMode = SmoothingMode.None;
                using var border = new Pen(Focused || DroppedDown ? Theme.Accent : Theme.Border);
                g.DrawRectangle(border, 0, 0, r.Width - 1, r.Height - 1);
            }
            catch { }
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
                using var f = new Font("Segoe UI Semibold", Math.Max(7f, Height / 3.0f * 72f / DeviceDpi));
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
            int i = e.X / Slot;
            if (i != hover) { hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = e.X / Slot;
            if (i >= 0 && i < Theme.Accents.Length && Theme.Accents[i].Name != Selected)
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
                using var b = new SolidBrush(color);
                g.FillEllipse(b, r);
                if (name == Selected)
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
            Color bg = Parent?.BackColor ?? Theme.Bg;
            g.Clear(bg);
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
}
