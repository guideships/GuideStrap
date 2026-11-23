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
    // Setup window. New users: pick a language and options, install, open the app.
    // Already installed: same window in Reinstall / Repair mode, keeping settings, accounts and mods.
    class SetupForm : ThemedForm
    {
        readonly GuideStrapContext context;
        readonly bool repair;
        readonly List<(Control Control, string English)> texts = new();
        readonly DarkCombo langCombo = new();
        readonly ToggleSwitch desktopToggle = new(), linksToggle = new(), robloxToggle = new(), startupToggle = new();
        readonly Label status = new();
        readonly RoundButton install, cancel;

        public SetupForm(GuideStrapContext context, bool repair)
        {
            this.context = context;
            this.repair = repair;
            string titleText = repair ? "Reinstall GuideStrap" : "GuideStrap Setup";
            Text = "GuideStrap Setup";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(D(520), D(repair ? 570 : 490));
            int W = ClientSize.Width;

            var bar = BuildTitleBar(titleText, minimize: true);
            var barTitle = bar.Controls.OfType<Label>().FirstOrDefault();
            if (barTitle != null) texts.Add((barTitle, titleText));

            string heading = repair ? "Reinstall GuideStrap" : "Welcome to GuideStrap";
            string intro = repair
                ? "GuideStrap is already installed. Reinstalling replaces it with this copy and keeps your settings, accounts and mods."
                : "Pick your language, then install. It only takes a few seconds.";
            var logo = new LogoMark { Size = new Size(D(68), D(68)), Location = new Point(D(24), D(54)) };
            var title = Track(At(Lbl(heading, 15f, semibold: true), D(104), D(56)), heading);
            var sub = Track(Lbl(intro, 9f, Theme.SubText), intro);
            sub.MaximumSize = new Size(W - D(128), 0);
            sub.Location = new Point(D(106), D(90));
            Controls.AddRange(new Control[] { logo, title, sub });

            int y = D(146);
            if (repair)
            {
                var versions = new Label
                {
                    AutoSize = true,
                    Font = Theme.F("Segoe UI", 8.5f),
                    ForeColor = Theme.SubText,
                    Location = new Point(D(26), D(146)),
                    Text = L.F("Installed: {0}  ·  This file: {1}", Setup.InstalledVersion() ?? "?", AppInfo.Version),
                };
                Controls.Add(versions);
                y = D(172);
            }

            foreach (var (_, native) in L.Languages) langCombo.Items.Add(native);
            langCombo.ItemHeight = D(22);
            langCombo.Width = D(170);
            langCombo.SelectedIndex = Math.Max(0, Array.FindIndex(L.Languages, x => x.Code == L.Lang));
            desktopToggle.Size = linksToggle.Size = robloxToggle.Size = startupToggle.Size = new Size(D(40), D(20));
            startupToggle.Checked = repair && Setup.StartsWithWindows;
            desktopToggle.Checked = !repair || Setup.HasDesktopShortcut;
            linksToggle.Checked = !repair || LinkHandler.PointsTo(Setup.InstallExe);
            robloxToggle.Checked = false;

            Controls.Add(Row(y, "Language", null, langCombo)); y += D(60);
            Controls.Add(Row(y, "Desktop shortcut", "Add a GuideStrap icon to your desktop.", desktopToggle)); y += D(60);
            Controls.Add(Row(y, "Open Roblox links with GuideStrap", "Play buttons on roblox.com open GuideStrap.", linksToggle)); y += D(60);
            Controls.Add(Row(y, "Start with Windows", "Opens quietly in the tray when your PC starts.", startupToggle)); y += D(60);
            if (repair)
            {
                Controls.Add(Row(y, "Also repair Roblox", "Downloads a fresh copy of Roblox next time you launch.", robloxToggle));
                y += D(60);
            }

            var note = Track(Lbl("Installs for your Windows user only. Remove it any time from Settings > Apps.", 8.25f, Theme.SubText),
                "Installs for your Windows user only. Remove it any time from Settings > Apps.");
            note.MaximumSize = new Size(W - D(48), 0);
            note.Location = new Point(D(26), y + D(8));
            Controls.Add(note);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = D(56), BackColor = Theme.Bar };
            footer.Paint += (_, e) => { using var p = new Pen(Theme.Border); e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            string action = repair ? "Reinstall" : "Install";
            install = Primary(action, repair ? "" : "", D(130), D(32));
            cancel = Secondary("Cancel", "", D(96), D(32));
            install.Location = new Point(W - D(20) - install.Width, D(12));
            cancel.Location = new Point(install.Left - D(8) - cancel.Width, D(12));
            texts.Add((install, action));
            texts.Add((cancel, "Cancel"));
            status.AutoSize = false;
            status.Size = new Size(cancel.Left - D(36), D(22));
            status.Location = new Point(D(20), D(17));
            status.ForeColor = Theme.SubText;
            status.Font = Theme.F("Segoe UI", 9f);
            footer.Controls.AddRange(new Control[] { status, cancel, install });

            Controls.Add(footer);
            Controls.Add(bar);

            langCombo.SelectedIndexChanged += (_, _) =>
            {
                L.Lang = L.Languages[langCombo.SelectedIndex].Code;
                foreach (var (c, en) in texts) c.Text = L.T(en);
            };
            cancel.Click += (_, _) => Close();
            install.Click += async (_, _) => await InstallAsync();
        }

        T Track<T>(T c, string english) where T : Control
        {
            texts.Add((c, english));
            return c;
        }

        Card Row(int y, string title, string? desc, Control right)
        {
            var card = new Card { Location = new Point(D(24), y), Size = new Size(ClientSize.Width - D(48), D(52)) };
            var t = Track(Lbl(title, 9.75f), title);
            t.Location = new Point(D(16), desc == null ? (card.Height - D(18)) / 2 : D(8));
            card.Controls.Add(t);
            if (desc != null)
            {
                var d = Track(Lbl(desc, 8.25f, Theme.SubText), desc);
                d.Location = new Point(D(16), D(28));
                card.Controls.Add(d);
            }
            right.Location = new Point(card.Width - D(16) - right.Width, (card.Height - right.Height) / 2);
            card.Controls.Add(right);
            return card;
        }

        async Task InstallAsync()
        {
            if (Setup.OtherCopiesRunning())
            {
                if (Ask("GuideStrap is open", "GuideStrap is open right now. Close it and continue?", "Close and continue", "Cancel") != 0) return;
                await Task.Run(Setup.CloseOtherCopies);
            }

            install.Enabled = cancel.Enabled = langCombo.Enabled = false;
            status.Text = L.T(repair ? "Reinstalling..." : "Installing...");
            string language = L.Lang;
            bool desktop = desktopToggle.Checked, links = linksToggle.Checked, fixRoblox = repair && robloxToggle.Checked, startup = startupToggle.Checked;
            try
            {
                await Task.Run(() => Setup.Install(language, desktop, links, fixRoblox, startup));
            }
            catch (Exception ex)
            {
                Ask("Setup failed", ex.Message, "OK");
                install.Enabled = cancel.Enabled = langCombo.Enabled = true;
                status.Text = "";
                return;
            }
            Setup.StartInstalled();
            Close();
        }
    }
}
