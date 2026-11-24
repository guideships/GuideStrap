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
    // Small popup in the bottom-right corner that doesn't steal focus
    class ToastForm : ThemedForm
    {
        readonly System.Windows.Forms.Timer life = new() { Interval = 9000 };

        public ToastForm(string title, string message, string? button, Action? onButton)
        {
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "GuideStrap";

            int w = D(340), pad = D(16);
            var t = Lbl(title, 10f, semibold: true);
            t.Location = new Point(pad, D(12));
            var m = Lbl(message, 8.75f, Theme.SubText);
            m.AutoSize = false;
            int textW = w - pad * 2 - D(16);
            var size = TextRenderer.MeasureText(m.Text, m.Font, new Size(textW, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            m.Size = new Size(textW, size.Height + D(2));
            m.Location = new Point(pad, D(36));
            var x = new RoundButton
            {
                Icon = "\uE8BB", Size = new Size(D(26), D(26)), Location = new Point(w - D(34), D(8)), Radius = 6,
                BaseColor = Color.Transparent, HoverColor = Theme.NavHover, ForeColor = Theme.SubText, Font = Theme.F("Segoe UI", 7f),
            };
            x.Click += (_, _) => Close();
            Controls.Add(t);
            Controls.Add(m);
            Controls.Add(x);
            int y = m.Bottom + D(12);
            if (button != null && onButton != null)
            {
                var b = Primary(button, "", D(110), D(28));
                b.Location = new Point(pad, y);
                b.Click += (_, _) => { onButton(); Close(); };
                Controls.Add(b);
                y += D(28) + D(14);
            }
            ClientSize = new Size(w, y);
            var area = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
            Location = new Point(area.Right - w - D(16), area.Bottom - y - D(16));
            life.Tick += (_, _) => { life.Stop(); Close(); };
            Shown += (_, _) => life.Start();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x80;   // no activate, tool window (no taskbar button, no Alt+Tab)
                return cp;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) life.Dispose();
            base.Dispose(disposing);
        }
    }
}
