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
            about.Location = new Point(D(160) - about.Width / 2, D(176));
            about.Click += (_, _) => OpenSettings("About");
            var folder = new LinkText { Icon = "\uE8B7", Text = L.T("Open GuideStrap folder") };
            folder.Location = new Point(D(160) - folder.Width / 2, D(208));
            folder.Click += (_, _) =>
            {
                Directory.CreateDirectory(Paths.Base);
                Process.Start(new ProcessStartInfo(Paths.Base) { UseShellExecute = true });
            };
            Controls.AddRange(new Control[] { logo, name, ver, about, folder });

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
                context.Open(new LoadingForm(context, settings, accounts, null, ""));
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
            if (settings.LauncherBackground is string bgPath && File.Exists(bgPath)) ApplyBackground(bgPath);
        }

        Bitmap? background;

        // The user's picture, cropped to fill the window and darkened so text stays readable
        void ApplyBackground(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                using var src = Image.FromStream(fs);
                int w = ClientSize.Width, h = ClientSize.Height;
                var bmp = new Bitmap(w, h);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Max((float)w / src.Width, (float)h / src.Height);
                    float dw = src.Width * scale, dh = src.Height * scale;
                    g.DrawImage(src, (w - dw) / 2, (h - dh) / 2, dw, dh);
                    using var shade = new SolidBrush(Color.FromArgb(170, Theme.Bg));
                    g.FillRectangle(shade, 0, 0, w, h);
                }
                background = bmp;
                BackgroundImageLayout = ImageLayout.None;
                BackgroundImage = bmp;
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) background?.Dispose();
            base.Dispose(disposing);
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
