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
    // LAUNCHER WINDOW (small start screen)
    class LauncherForm : ThemedForm
    {
        readonly GuideStrapContext context;

        public LauncherForm(GuideStrapContext context)
        {
            this.context = context;
            Text = "GuideStrap";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(D(600), D(264));
            Controls.Add(BuildTitleBar("GuideStrap", minimize: true));

            var settings = AppSettings.Load();
            var accounts = AccountStore.Load();
            var acc = accounts.FirstOrDefault(a => a.UserId == settings.ActiveAccountId);

            // left: logo, name, links
            // the name and version are centered on the logo as one block, a little clear of the tilted logo's corner
            var logo = new LogoMark { Size = new Size(D(84), D(84)), Location = new Point(D(34), D(66)) };
            var name = At(Lbl("GuideStrap", 19f, semibold: true), D(125), D(82));
            var ver = At(Lbl($"Version {AppInfo.Version}", 9f, Theme.SubText), D(128), D(117));
            var about = new LinkText { Icon = "\uE946", Text = L.T("About GuideStrap") };
            about.Click += (_, _) => OpenSettings("About");
            var folder = new LinkText { Icon = "\uE8B7", Text = L.T("Open GuideStrap folder") };
            folder.Click += (_, _) =>
            {
                Directory.CreateDirectory(Paths.Base);
                Process.Start(new ProcessStartInfo(Paths.Base) { UseShellExecute = true });
            };
            var discord = new LinkText { Icon = "\uE8F2", Text = L.T("Join our Discord") };
            discord.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(AppInfo.DiscordUrl) { UseShellExecute = true }); } catch { }
            };
            // the links form one column with their icons lined up, centered under the logo and name, evenly spaced
            var links = new[] { about, folder, discord };
            int blockCenter = (logo.Left + name.Left + name.PreferredWidth) / 2;
            int colW = links.Max(l => l.Width);
            int colX = Math.Clamp(blockCenter - colW / 2, D(20), Math.Max(D(20), D(286) - colW));
            for (int i = 0; i < links.Length; i++) links[i].Location = new Point(colX, D(164) + i * D(28));
            Controls.AddRange(new Control[] { logo, name, ver, about, folder, discord });

            // right: menu. Launch Roblox is the main action (filled). The column is centered top to bottom,
            // with Accounts set a little apart from the two above (20 px instead of 34) and kept where it was.
            var area = new Rectangle(D(300), D(70), D(276), D(160));
            var launchBtn = new LauncherButton { Icon = "\uE72A", Text = "Launch Roblox", Primary = true, Bounds = new Rectangle(area.X, area.Y, area.Width, D(44)) };
            var configBtn = new LauncherButton { Icon = "\uE713", Text = "Configure settings", Bounds = new Rectangle(area.X, area.Y + D(52), area.Width, D(44)) };
            var accountsBtn = new LauncherButton
            {
                Icon = "\uE716",
                Text = acc != null ? $"Accounts  ·  {acc.DisplayName}" : "Accounts",
                Bounds = new Rectangle(area.X, area.Y + D(116), area.Width, D(44)),
            };
            Controls.AddRange(new Control[] { launchBtn, configBtn, accountsBtn });

            launchBtn.Click += (_, _) =>
            {
                // read again: the account or settings may have changed (tray menu, another window) since this opened
                context.Open(new LoadingForm(context, AppSettings.Load(), AccountStore.Load(), null, ""));
                Close();
            };
            configBtn.Click += (_, _) => OpenSettings("Home");
            accountsBtn.Click += (_, _) => OpenSettings("Accounts");
            Activated += (_, _) => Presence.Screen("In the main menu");
            Shown += async (_, _) =>
            {
                ShowWhatsNew(settings);
                var check = AutoUpdateCheckAsync(settings);   // saves its check time right away, before waiting on GitHub
                PrepareSettingsSoon();
                await check;
            };
            FormClosed += (_, _) => Form1.Discard();   // closed some other way (Launch Roblox, X): drop the ready-made window
            ApplyBackdrop(settings.LauncherBackground);
        }

        void ShowWhatsNew(AppSettings settings)
        {
            if (settings.LastSeenVersion == AppInfo.Version) return;
            // someone who used GuideStrap before (not a fresh install) gets the list of changes
            bool updated = settings.LastSeenVersion.Length > 0 || settings.LastUpdateCheck != default;
            settings.LastSeenVersion = AppInfo.Version;
            settings.Save();
            if (updated) Ask(L.F("What's new in GuideStrap {0}", AppInfo.Version), AppInfo.WhatsNew, "Nice");
        }

        async Task AutoUpdateCheckAsync(AppSettings settings)
        {
            if (!Updater.Configured || DateTime.Now - settings.LastUpdateCheck < TimeSpan.FromHours(20)) return;
            settings.LastUpdateCheck = DateTime.Now;
            settings.Save();
            var found = await Updater.CheckAsync();
            if (found == null || IsDisposed) return;
            if (Ask("Update available", $"GuideStrap {found.Value.Version} is out. Update now?", "Update", "Later") == 0)
            {
                try { await Updater.InstallAsync(found.Value.Url); }
                catch (Exception ex) { Ask("Update failed", ex.Message, "OK"); }
            }
        }

        void OpenSettings(string page)
        {
            context.Open(Form1.Take(page));   // the one built in the background if it's ready, otherwise a new one
            Close();
        }

        // build the settings window once the main menu has been sitting for a moment
        void PrepareSettingsSoon()
        {
            var t = new System.Windows.Forms.Timer { Interval = 800 };
            t.Tick += (_, _) =>
            {
                t.Stop();
                t.Dispose();
                if (IsDisposed || Disposing || !Visible) return;
                Form1.Prepare();
            };
            t.Start();
        }
    }
}
