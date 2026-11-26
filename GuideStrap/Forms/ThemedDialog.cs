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
    // THEMED DIALOG (replaces plain message boxes)
    public class ThemedDialog : ThemedForm
    {
        readonly ToggleSwitch? check;
        readonly TextBox? input;
        public int Choice { get; private set; } = -1;
        public bool Checked => check?.Checked ?? false;
        public string Input => input?.Text.Trim() ?? "";

        public ThemedDialog(string title, string message, string[] buttons, string? checkText = null, string? inputPlaceholder = null)
        {
            title = L.T(title);
            message = L.T(message);
            buttons = buttons.Select(L.T).ToArray();
            if (checkText != null) checkText = L.T(checkText);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CloseOnEscape = true;
            Text = title;

            int w = D(440), pad = D(24);
            var msgFont = Theme.F("Segoe UI", 9.5f);
            Size msgSize = TextRenderer.MeasureText(message, msgFont, new Size(w - pad * 2, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            var t = new Label
            {
                Text = title, UseMnemonic = false, AutoSize = false,
                Font = Theme.F("Segoe UI Semibold", 12f), ForeColor = Theme.Text, BackColor = Color.Transparent,
                Location = new Point(pad, D(20)), Size = new Size(w - pad * 2, D(28)), AutoEllipsis = true,
            };
            var m = new Label
            {
                Text = message, UseMnemonic = false, AutoSize = false,
                Font = msgFont, ForeColor = Theme.SubText, BackColor = Color.Transparent,
                Location = new Point(pad, D(54)), Size = new Size(w - pad * 2, msgSize.Height + D(6)),
            };
            Controls.Add(t);
            Controls.Add(m);
            int y = m.Bottom + D(14);

            if (inputPlaceholder != null)
            {
                var tb = new TextBox();
                input = tb;
                var box = InputBox(tb, inputPlaceholder, w - pad * 2);
                box.Location = new Point(pad, y);
                tb.KeyDown += (_, e) =>
                {
                    if (e.KeyCode != Keys.Enter) return;
                    e.SuppressKeyPress = true;
                    Choice = 0;
                    DialogResult = DialogResult.OK;
                };
                Controls.Add(box);
                y += D(44);
                Shown += (_, _) => tb.Focus();
            }

            if (checkText != null)
            {
                check = new ToggleSwitch { Size = new Size(D(36), D(18)), Location = new Point(pad, y + D(1)) };
                var cl = Lbl(checkText, 9f);
                cl.Location = new Point(pad + D(46), check.Top + (check.Height - cl.PreferredHeight) / 2);   // centered on the switch
                cl.Cursor = Cursors.Hand;
                cl.Click += (_, _) => check.Checked = !check.Checked;
                Controls.Add(check);
                Controls.Add(cl);
                y += D(32);
            }

            var footer = new Panel { Location = new Point(0, y), Size = new Size(w, D(58)), BackColor = Theme.Bar };
            footer.Paint += (_, e) => { using var p = new Pen(Theme.Border); e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            int x = w - pad;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                int index = i;
                int bw = Math.Max(D(96), TextRenderer.MeasureText(buttons[i], Theme.F("Segoe UI Semibold", 9f)).Width + D(36));
                var b = i == 0 ? Primary(buttons[i], "", bw, D(30)) : Secondary(buttons[i], "", bw, D(30));
                x -= bw;
                b.Location = new Point(x, D(14));
                x -= D(8);
                b.Click += (_, _) => { Choice = index; DialogResult = DialogResult.OK; };
                footer.Controls.Add(b);
            }
            Controls.Add(footer);
            ClientSize = new Size(w, y + footer.Height);
            t.MouseDown += DragWindow;
            m.MouseDown += DragWindow;
        }

        public static int Prompt(IWin32Window? owner, string title, string message, params string[] buttons)
        {
            using var d = new ThemedDialog(title, message, buttons);
            if (owner == null) d.StartPosition = FormStartPosition.CenterScreen;
            d.ShowDialog(owner);
            return d.Choice;
        }
    }
}
