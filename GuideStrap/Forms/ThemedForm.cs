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
    // BASE WINDOW: borderless, rounded, shadow, fade in/out, shared helpers
    public class ThemedForm : Form
    {
        bool fading, fadeDone;
        protected bool CloseOnEscape { get; set; }

        public ThemedForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9f);
            Opacity = 0;
            MouseDown += DragWindow;
            try { Icon = Theme.AppIcon; } catch { }
        }

        protected int D(int px) => (int)Math.Round(px * DeviceDpi / 96f);

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;      // drop shadow
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2;
                NativeMethods.DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
                int border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
                NativeMethods.DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
            }
            catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            L.Apply(this);
            Theme.Solidify(this);
            base.OnLoad(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Anim.Run((this, "opacity"), (float)Opacity, 1f, 200, v => Opacity = v);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel || fadeDone || !IsHandleCreated || !Visible || Opacity <= 0.02) return;
            if (e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing or CloseReason.ApplicationExitCall) return;

            e.Cancel = true;
            if (fading) return;
            fading = true;
            var result = DialogResult;
            Anim.Run((this, "opacity"), (float)Opacity, 0f, 140, v => Opacity = v, () =>
            {
                fadeDone = true;
                if (Modal) DialogResult = result == DialogResult.None ? DialogResult.Cancel : result;
                else Close();
            });
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (CloseOnEscape && keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        protected Panel BuildTitleBar(string title, bool minimize)
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = D(36), BackColor = Theme.Bg, Padding = new Padding(0, D(4), D(6), D(4)) };
            var logo = new LogoMark { Size = new Size(D(20), D(20)), Location = new Point(D(10), D(8)) };
            var t = new Label
            {
                Text = L.T(title), AutoSize = true, ForeColor = Theme.Text, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9f), Location = new Point(D(36), D(9)),
            };
            if (minimize)
            {
                var min = new RoundButton
                {
                    Icon = "\uE921", Dock = DockStyle.Right, Width = D(40), Radius = 6,
                    BaseColor = Color.Transparent, HoverColor = Theme.NavHover, ForeColor = Theme.Text, Font = new Font("Segoe UI", 8f),
                };
                min.Click += (_, _) => WindowState = FormWindowState.Minimized;
                bar.Controls.Add(min);
            }
            var close = new RoundButton
            {
                Icon = "\uE8BB", Dock = DockStyle.Right, Width = D(40), Radius = 6,
                BaseColor = Color.Transparent, HoverColor = Theme.Danger, ForeColor = Theme.Text, Font = new Font("Segoe UI", 8f),
            };
            close.Click += (_, _) => Close();
            bar.Controls.Add(close);
            bar.Controls.Add(logo);
            bar.Controls.Add(t);
            bar.MouseDown += DragWindow;
            t.MouseDown += DragWindow;
            logo.MouseDown += DragWindow;
            return bar;
        }

        public static Label Lbl(string text, float size = 10f, Color? color = null, bool semibold = false) => new()
        {
            Text = L.T(text),
            AutoSize = true,
            UseMnemonic = false,
            Font = Theme.F(semibold ? "Segoe UI Semibold" : "Segoe UI", size),
            ForeColor = color ?? Theme.Text,
            BackColor = Color.Transparent,
        };

        protected static Label IconLabel(string glyph, float size, Color color) => new()
        {
            Text = glyph,
            AutoSize = true,
            Font = Theme.F(Theme.IconFont, size),
            ForeColor = color,
            BackColor = Color.Transparent,
        };

        protected static T At<T>(T c, int x, int y) where T : Control { c.Location = new Point(x, y); return c; }

        protected RoundButton Primary(string text, string icon, int w, int h) => new()
        {
            Text = L.T(text), Icon = icon, Size = new Size(w, h),
        };

        protected RoundButton Secondary(string text, string icon, int w, int h) => new()
        {
            Text = L.T(text), Icon = icon, Size = new Size(w, h),
            BaseColor = Theme.ButtonBg, HoverColor = Theme.ButtonHover, BorderColor = Theme.Border, ForeColor = Theme.Text,
        };

        protected Card InputBox(TextBox tb, string placeholder, int width, string icon = "")
        {
            var wrap = new Card { Size = new Size(width, D(30)), BackColor = Theme.Input, Radius = 5, Cursor = Cursors.IBeam };
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = Theme.Input;
            tb.ForeColor = Theme.Text;
            tb.Font = new Font("Segoe UI", 9.25f);
            tb.PlaceholderText = placeholder;
            int iconW = icon.Length > 0 ? D(26) : 0;
            tb.Width = width - D(20) - iconW;
            tb.Location = new Point(D(10), (wrap.Height - tb.PreferredHeight) / 2);
            wrap.Controls.Add(tb);
            if (icon.Length > 0)
            {
                var ic = IconLabel(icon, 9f, Theme.SubText);
                ic.Location = new Point(width - D(28), (wrap.Height - D(16)) / 2);
                ic.Click += (_, _) => tb.Focus();
                wrap.Controls.Add(ic);
            }
            wrap.Click += (_, _) => tb.Focus();
            tb.GotFocus += (_, _) => Anim.Run((wrap, "focus"), 0f, 1f, 160, v => { wrap.BorderColor = Theme.Mix(Theme.Border, Theme.Accent, v); wrap.Invalidate(); });
            tb.LostFocus += (_, _) => Anim.Run((wrap, "focus"), 1f, 0f, 200, v => { wrap.BorderColor = Theme.Mix(Theme.Border, Theme.Accent, v); wrap.Invalidate(); });
            return wrap;
        }

        protected void Later(Action a) => BeginInvoke(a);

        protected int Ask(string title, string message, params string[] buttons) => ThemedDialog.Prompt(this, title, message, buttons);
    }
}
