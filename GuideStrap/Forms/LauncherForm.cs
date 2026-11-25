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
            var logo = new LogoMark { Size = new Size(D(84), D(84)), Location = new Point(D(34), D(66)) };
            var name = At(Lbl("GuideStrap", 19f, semibold: true), D(122), D(78));
            var ver = At(Lbl($"Version {AppInfo.Version}", 9f, Theme.SubText), D(125), D(113));
            var about = new LinkText { Icon = "\uE946", Text = L.T("About GuideStrap") };
            about.Location = new Point(D(160) - about.Width / 2, D(164));
            about.Click += (_, _) => OpenSettings("About");
            var folder = new LinkText { Icon = "\uE8B7", Text = L.T("Open GuideStrap folder") };
            folder.Location = new Point(D(160) - folder.Width / 2, D(192));
            folder.Click += (_, _) =>
            {
                Directory.CreateDirectory(Paths.Base);
                Process.Start(new ProcessStartInfo(Paths.Base) { UseShellExecute = true });
            };
            var discord = new LinkText { Icon = "\uE8F2", Text = L.T("Join our Discord") };
            discord.Location = new Point(D(160) - discord.Width / 2, D(220));
            discord.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(AppInfo.DiscordUrl) { UseShellExecute = true }); } catch { }
            };
            Controls.AddRange(new Control[] { logo, name, ver, about, folder, discord });

            // right: menu
            var area = new Rectangle(D(300), D(56), D(276), D(184));
            var launchBtn = new LauncherButton { Icon = "\uE72A", Text = "Launch Roblox", Bounds = new Rectangle(area.X, area.Y, area.Width, D(44)) };
            var configBtn = new LauncherButton { Icon = "\uE713", Text = "Configure settings", Bounds = new Rectangle(area.X, area.Y + D(52), area.Width, D(44)) };
            var accountsBtn = new LauncherButton
            {
                Icon = "\uE716",
                Text = acc != null ? $"Accounts  ·  {acc.DisplayName}" : "Accounts",
                Bounds = new Rectangle(area.X, area.Y + D(130), area.Width, D(44)),
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
                await AutoUpdateCheckAsync(settings);
            };
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
            context.Open(new Form1(page));
            Close();
        }
    }
}
