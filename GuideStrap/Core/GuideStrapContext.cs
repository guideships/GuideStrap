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
    // APP FLOW: launcher window first, settings window when asked
    class GuideStrapContext : ApplicationContext
    {
        int open;
        NotifyIcon? tray;
        bool trayMode;   // started with Windows: lives in the tray until you exit
        public static GuideStrapContext? Current { get; private set; }
        public int OpenCount => open;

        public GuideStrapContext(string[] args)
        {
            Current = this;
            string? launchUri = args.FirstOrDefault(a => a.StartsWith("roblox", StringComparison.OrdinalIgnoreCase));

            // Not the installed copy (e.g. the downloaded .exe): show setup.
            // If GuideStrap is already installed, setup opens in Reinstall / Repair mode.
            if (launchUri == null && Setup.NeedsSetup())
            {
                bool repair = Setup.IsInstalled;
                var current = AppSettings.Load();
                // settings kept from an earlier install are reused (language and theme)
                L.Lang = current.Language.Length > 0 ? current.Language : L.SystemLanguage();
                Theme.Apply(current.ThemeName, current.Accent, current.SeeThrough);
                Open(new SetupForm(this, repair));
                return;
            }

            var settings = AppSettings.Load();
            L.Lang = settings.Language.Length > 0 ? settings.Language : L.SystemLanguage();
            Theme.Apply(settings.ThemeName, settings.Accent, settings.SeeThrough);
            Setup.RefreshEntry();
            Updater.CleanupOld();
            Presence.Configure(settings);
            Presence.Enable(settings.DiscordPresence);
            Presence.WatchEnded += OnWatchEnded;
            Presence.SessionEnded += sum =>
            {
                var st = AppSettings.Load();
                if (st.SortScreenshots) _ = Task.Run(() => { try { ScreenshotSorter.Sort(); } catch { } });
                if (!st.SessionSummary || sum.Length < TimeSpan.FromMinutes(1)) return;
                var lines = new List<string> { L.F("You played for {0}.", FormatLength(sum.Length)) };
                if (sum.Games.Count > 0) lines.Add(L.F("Games: {0}", string.Join(", ", sum.Games.Take(4))));
                if (sum.Location != null && !st.StreamerMode) lines.Add(L.F("Last server: {0}", sum.Location));
                ShowToast(L.T("Session over"), string.Join("\n", lines));
            };
            FriendWatcher.FriendStartedPlaying += (f, name) =>
            {
                var st = AppSettings.Load();
                if (st.StreamerMode) name = L.T("A friend");
                string game = string.IsNullOrWhiteSpace(f.Location) ? L.T("a game") : f.Location!;
                long? place = f.PlaceId ?? f.RootPlaceId;
                if (place == null)
                    ShowToast(L.T("Friend started playing"), L.F("{0} is playing {1}.", name, game));
                else
                    ShowToast(L.T("Friend started playing"), L.F("{0} is playing {1}.", name, game), "Join",
                        () => Open(new LoadingForm(this, AppSettings.Load(), AccountStore.Load(), null, place.Value.ToString(), f.GameId)));
            };
            FriendWatcher.FriendJoinedYou += name => ShowToast(L.T("Friend joined you"), L.F("{0} joined your server.", name));
            FriendWatcher.Start();
            Presence.RobloxCrashed += info => { if (AppSettings.Load().CrashHelper) ShowCrash(info); };
            Presence.ServerLocated += location =>
            {
                var st = AppSettings.Load();
                if (tray != null && st.ShowServerLocation && !st.StreamerMode)
                    tray.ShowBalloonTip(4000, "GuideStrap", L.F("Connected to a server in {0}", location), ToolTipIcon.None);
            };
            // desktop shortcut for one game: GuideStrap.exe --play <place ID>
            int playAt = Array.IndexOf(args, "--play");
            string? playPlace = playAt >= 0 && playAt + 1 < args.Length && long.TryParse(args[playAt + 1], out long shortcutPlace) && shortcutPlace > 0
                ? shortcutPlace.ToString() : null;
            if (launchUri != null)
                Open(new LoadingForm(this, settings, AccountStore.Load(), launchUri, ""));
            else if (playPlace != null)
                Open(new LoadingForm(this, settings, AccountStore.Load(), null, playPlace));
            else if (args.Contains("--tray"))
            {
                trayMode = true;
                EnsureTray();
                MemoryTrim.Now();
            }
            else
                Open(new LauncherForm(this));
        }

        public void Open(Form form)
        {
            open++;
            form.FormClosed += (_, _) =>
            {
                open--;
                if (open <= 0 && !Presence.Watching && !trayMode) ExitThread();
                else if (open <= 0) MemoryTrim.Now();   // only the tray is left, so give memory back
            };
            form.Show();
        }

        public void ShowCrash(CrashInfo info) => Open(new CrashForm(info));

        ToastForm? toast;
        public void ShowToast(string title, string message, string? button = null, Action? onButton = null)
        {
            try { if (toast != null && !toast.IsDisposed) toast.Close(); } catch { }
            toast = new ToastForm(title, message, button, onButton);
            Open(toast);
        }

        static string FormatLength(TimeSpan t) =>
            t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{Math.Max(1, (int)t.TotalMinutes)} min";

        // Keeps GuideStrap running quietly (tray icon) while Roblox is open, for the Discord status.
        public void WatchRoblox()
        {
            if (Presence.Watching) return;
            Presence.WatchRoblox();
            EnsureTray();
        }

        void EnsureTray()
        {
            if (tray != null) return;
            var menu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true, Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) };
            menu.ForeColor = Theme.Text;
            menu.Opening += (_, _) => BuildTrayMenu(menu);
            BuildTrayMenu(menu);
            tray = new NotifyIcon { Icon = Theme.AppIcon, Text = "GuideStrap", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, _) => Open(new LauncherForm(this));
        }

        void BuildTrayMenu(ContextMenuStrip menu)
        {
            menu.Items.Clear();
            var settings = AppSettings.Load();
            var accounts = AccountStore.Load();

            menu.Items.Add(L.T("Launch Roblox"), null, (_, _) => Open(new LoadingForm(this, AppSettings.Load(), AccountStore.Load(), null, "")));

            var accountMenu = new ToolStripMenuItem(L.T("Account")) { ForeColor = Theme.Text };
            if (accountMenu.DropDown is ToolStripDropDownMenu dd) { dd.ShowImageMargin = false; dd.ShowCheckMargin = true; dd.Renderer = menu.Renderer; }
            void AddAccount(string label, long? id)
            {
                var item = new ToolStripMenuItem(label) { Checked = settings.ActiveAccountId == id, ForeColor = Theme.Text };
                item.Click += (_, _) => { var s = AppSettings.Load(); s.ActiveAccountId = id; s.Save(); };
                accountMenu.DropDownItems.Add(item);
            }
            AddAccount(L.T("Roblox login"), null);
            foreach (var a in accounts) AddAccount(a.DisplayName, a.UserId);
            menu.Items.Add(accountMenu);

            var discord = new ToolStripMenuItem(L.T("Discord status")) { Checked = settings.DiscordPresence, ForeColor = Theme.Text };
            discord.Click += (_, _) =>
            {
                var s = AppSettings.Load();
                s.DiscordPresence = !s.DiscordPresence;
                s.Save();
                Presence.Enable(s.DiscordPresence);
            };
            menu.Items.Add(discord);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Open GuideStrap"), null, (_, _) => Open(new LauncherForm(this)));
            menu.Items.Add(L.T("Exit"), null, (_, _) => { Presence.StopWatching(); ExitThread(); });
            foreach (ToolStripItem i in menu.Items) i.ForeColor = Theme.Text;
        }

        void OnWatchEnded()
        {
            if (trayMode) return;   // stays in the tray
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            if (open <= 0) ExitThread();
        }

        protected override void ExitThreadCore()
        {
            FriendWatcher.Stop();
            Presence.Shutdown();
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            base.ExitThreadCore();
        }
    }
}
