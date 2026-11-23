// GuideStrap - Roblox launcher by Guide
// Requires the Microsoft.Web.WebView2 NuGet package.

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
    // MAIN WINDOW
    public partial class Form1 : ThemedForm
    {
        enum StatusKind { Idle, Busy, Error }

        // ---------- state ----------
        readonly AppSettings settings = AppSettings.Load();
        List<Account> accounts = AccountStore.Load();
        readonly Dictionary<long, Image?> avatarCache = new();
        bool busy, syncing;
        string currentPage = "";

        // ---------- shell ----------
        readonly Panel sidebar = new(), pageHost = new(), bottomBar = new();
        readonly NavList nav = new();
        readonly TextBox searchBox = new();
        readonly Dictionary<string, FlowLayoutPanel> pages = new();
        readonly Card chip = new();
        readonly AvatarBox chipAvatar = new();
        readonly Label chipName = new(), chipSub = new();
        readonly Label statusIcon = new(), statusLabel = new();
        readonly ProgressLine progress = new();
        readonly PageTransition transition = new();

        // ---------- home ----------
        readonly Label homeGreeting = new(), homeSub = new();
        readonly AvatarBox homeAvatar = new();
        readonly TextBox placeBox = new();
        readonly RoundButton playButton = new();
        readonly Label statGraphics = new(), statFont = new(), statFlags = new();

        // ---------- accounts ----------
        FlowLayoutPanel accountsPage = null!;
        readonly List<Control> accountCards = new();

        // ---------- graphics ----------
        readonly ModernSlider presetSlider = new(), qualitySlider = new();
        readonly Label presetTitle = new(), presetDesc = new(), qualityValue = new();
        readonly DarkCombo textureCombo = new(), msaaCombo = new(), rendererCombo = new();
        readonly ToggleSwitch grassToggle = new(), skyToggle = new(), meshToggle = new(), voxelToggle = new(),
                              grassMotionToggle = new(), dpiToggle = new(), fullscreenToggle = new();
        static readonly string[] MsaaValues = { "", "1", "2", "4", "8" };

        // ---------- fastflags ----------
        readonly Label flagPreview = new();
        Card flagPreviewCard = null!;

        // ---------- font ----------
        readonly Label fontName = new(), fontPreview = new();
        PrivateFontCollection? previewFonts;

        // ---------- settings ----------
        readonly ToggleSwitch websiteAccountToggle = new(), closeAfterToggle = new(), discordToggle = new(),
                              backgroundToggle = new(), locationToggle = new(), multiToggle = new(), linksToggle = new(), crashToggle = new(),
                              startupToggle = new();

        // ---------- mods ----------
        readonly Label deathStatus = new(), cursorStatus = new(), skyStatus = new();
        readonly DarkCombo cursorCombo = new();
        readonly PictureBox cursorPreview = new();
        readonly Label crosshairStatus = new(), footstepStatus = new(), jumpStatus = new(), packStatus = new(), setupStatus = new();
        readonly DarkCombo crosshairCombo = new(), footstepCombo = new(), jumpCombo = new(), packCombo = new(), setupCombo = new();
        readonly PictureBox crosshairPreview = new();
        static readonly string[] CursorKeys = { "default", "guidestrap", "dot", "cross", "circle", "classic", "custom" };
        static readonly string[] CrosshairKeys = { "default", "dot", "cross", "circle", "custom" };
        static readonly string[] SoundModes = { "default", "mute", "custom" };

        // ---------- potato mode + performance ----------
        readonly ToggleSwitch potatoAll = new(), potatoBlur = new(), potatoGrass = new(), potatoMesh = new(), potatoSky = new(), potatoMsaa = new();
        bool potatoSyncing;
        readonly DarkCombo fpsCombo = new(), priorityCombo = new();
        static readonly int[] FpsCaps = { 0, 60, 120, 144, 165, 240 };

        // ---------- discord / friends / setups ----------
        readonly ToggleSwitch discordGameToggle = new(), discordAccountToggle = new(), discordJoinToggle = new(),
                              streamerToggle = new(), friendAlertToggle = new(), summaryToggle = new();
        readonly Label autoStatus = new(), friendsStatus = new();
        FlowLayoutPanel friendsPage = null!;
        readonly List<Control> friendCards = new();
        readonly Dictionary<long, string> friendNames = new();
        DateTime friendsLoaded = DateTime.MinValue;
        bool friendsBusy;
        bool modsSyncing;

        // ---------- recent games ----------
        readonly FlowLayoutPanel recentPanel = new();
        int recentVersion = -1;

        int CW => D(690);   // content width

        public Form1() : this("Home") { }

        public Form1(string startPage)
        {
            InitializeComponent();

            BuildShell();
            BuildHomePage();
            BuildAccountsPage();
            BuildGraphicsPage();
            BuildFlagsPage();
            BuildFontPage();
            BuildModsPage();
            BuildFriendsPage();
            BuildDiscordPage();
            BuildSettingsPage();
            BuildAboutPage();

            RefreshAccounts();
            RefreshGraphicsControls();
            RefreshFlagSummary();
            RefreshFontPreview();
            RefreshMods();
            RefreshHome();
            RefreshRecent();
            ShowPage(startPage, animate: false);

            Load += async (_, _) => await LoadAvatarsAsync();
            Activated += (_, _) =>
            {
                Presence.Screen("Editing settings", nav.TextOf(currentPage));
                if (recentVersion != RecentGames.Version) RefreshRecent();
            };
        }

        // SHELL: title bar, sidebar, bottom bar, page host
        void BuildShell()
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "GuideStrap";
            ClientSize = new Size(D(980), D(640));
            StartPosition = FormStartPosition.CenterScreen;

            var titleBar = BuildTitleBar("GuideStrap", minimize: true);

            sidebar.Dock = DockStyle.Left;
            sidebar.Width = D(220);
            sidebar.BackColor = Theme.Bg;

            bottomBar.Dock = DockStyle.Bottom;
            bottomBar.Height = D(56);
            bottomBar.BackColor = Theme.Bar;
            bottomBar.Paint += (_, e) => { using var p = new Pen(Theme.Border); e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0); };

            pageHost.Dock = DockStyle.Fill;
            pageHost.BackColor = Theme.Bg;
            pageHost.Padding = new Padding(D(22), 0, D(6), 0);
            transition.Dock = DockStyle.Fill;
            transition.Visible = false;
            pageHost.Controls.Add(transition);

            Controls.Add(pageHost);
            Controls.Add(sidebar);
            Controls.Add(bottomBar);
            Controls.Add(titleBar);

            // --- sidebar ---
            int sw = sidebar.Width;
            int sh = ClientSize.Height - D(36) - D(56);

            var search = InputBox(searchBox, "Search", sw - D(24), "\uE721");
            search.Location = new Point(D(12), D(6));
            searchBox.TextChanged += (_, _) => nav.SetFilter(searchBox.Text);
            searchBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (nav.FirstVisibleKey is string k) ShowPage(k);
            };

            nav.ItemHeight = D(34);
            nav.Gap = D(2);
            nav.Add("Home", "\uE768", "Play", "launch join game home start");
            nav.Add("Accounts", "\uE716", "Accounts", "account login switch alt user profile");
            nav.Add("Graphics", "\uE7F4", "Graphics", "fps quality performance texture grass sky preset lag potato blurry cap priority");
            nav.Add("Flags", "\uE7C1", "FastFlags", "fflag flag json fastflag fflags");
            nav.Add("Font", "\uE8D2", "Text fonts", "font text letters typeface");
            nav.Add("Mods", "\uE790", "Mods", "mods death sound oof cursor mouse sky skybox crosshair shift lock footsteps jump pack setup gallery");
            nav.Add("Friends", "\uE716", "Friends", "friends online join playing alerts");
            nav.Add("Discord", "\uE8F2", "Discord", "discord rich presence status join activity");
            nav.Add("Settings", "\uE713", "Settings", "launcher close website folder reinstall crash helper streamer summary");
            nav.Add("About", "\uE946", "About", "about info version what help guide");
            nav.Location = new Point(D(10), D(46));
            nav.Size = new Size(sw - D(20), 10 * (nav.ItemHeight + nav.Gap));
            nav.Navigate += key => ShowPage(key);

            chip.Size = new Size(sw - D(24), D(50));
            chip.Location = new Point(D(12), sh - D(62));
            chip.Cursor = Cursors.Hand;
            chipAvatar.Size = new Size(D(30), D(30));
            chipAvatar.Location = new Point(D(10), D(10));
            chipName.AutoSize = false;
            chipName.Size = new Size(chip.Width - D(60), D(18));
            chipName.Location = new Point(D(48), D(7));
            chipName.Font = new Font("Segoe UI Semibold", 9f);
            chipName.ForeColor = Theme.Text;
            chipName.BackColor = Color.Transparent;
            chipName.AutoEllipsis = true;
            chipSub.AutoSize = false;
            chipSub.Size = new Size(chip.Width - D(60), D(16));
            chipSub.Location = new Point(D(48), D(25));
            chipSub.Font = new Font("Segoe UI", 8f);
            chipSub.ForeColor = Theme.SubText;
            chipSub.BackColor = Color.Transparent;
            chipSub.AutoEllipsis = true;
            chip.Controls.AddRange(new Control[] { chipAvatar, chipName, chipSub });
            foreach (Control c in new Control[] { chip, chipAvatar, chipName, chipSub })
            {
                c.Cursor = Cursors.Hand;
                c.Click += (_, _) => ShowPage("Accounts");
            }

            var menuBtn = Secondary("Main menu", "\uE72B", sw - D(24), D(30));
            menuBtn.BaseColor = Theme.Bg;
            menuBtn.HoverColor = Theme.NavSelected;
            menuBtn.Location = new Point(D(12), chip.Top - D(38));
            menuBtn.Click += (_, _) => BackToMainMenu();

            sidebar.Controls.AddRange(new Control[] { search, nav, menuBtn, chip });
            sidebar.MouseDown += DragWindow;

            // --- bottom bar ---
            int W = ClientSize.Width;
            progress.Dock = DockStyle.Top;
            progress.Height = D(3);
            bottomBar.Controls.Add(progress);

            statusIcon.AutoSize = true;
            statusIcon.Font = new Font(Theme.IconFont, 10f);
            statusIcon.BackColor = Color.Transparent;
            statusIcon.Location = new Point(D(20), D(20));
            statusLabel.AutoSize = false;
            statusLabel.Size = new Size(W - D(380), D(22));
            statusLabel.Location = new Point(D(44), D(17));
            statusLabel.Font = new Font("Segoe UI", 9f);
            statusLabel.ForeColor = Theme.SubText;
            statusLabel.BackColor = Color.Transparent;
            statusLabel.AutoEllipsis = true;

            var closeBtn = Secondary("Close", "", D(88), D(30));
            closeBtn.Location = new Point(W - D(16) - closeBtn.Width, D(13));
            closeBtn.Click += (_, _) => Close();
            var launchBtn = Primary("Launch Roblox", "\uE768", D(150), D(30));
            launchBtn.Location = new Point(closeBtn.Left - D(8) - launchBtn.Width, D(13));
            launchBtn.Click += (_, _) => StartLaunch();

            bottomBar.Controls.AddRange(new Control[] { statusIcon, statusLabel, launchBtn, closeBtn });
            SetStatus("Changes are saved automatically.", null, StatusKind.Idle);
        }

        FlowLayoutPanel NewPage(string key, string title, string subtitle)
        {
            var page = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Theme.Bg,
                Visible = false,
                Padding = new Padding(0, 0, 0, D(28)),
            };
            var t = Lbl(title, 16f, semibold: true);
            t.Margin = new Padding(0, D(2), 0, 0);
            var s = Lbl(subtitle, 9.25f, Theme.SubText);
            s.Margin = new Padding(D(2), D(1), 0, D(14));
            page.Controls.Add(t);
            page.Controls.Add(s);
            pageHost.Controls.Add(page);
            pages[key] = page;
            return page;
        }

        T Add<T>(FlowLayoutPanel page, T c, int bottom = 6) where T : Control
        {
            c.Margin = new Padding(0, 0, 0, D(bottom));
            page.Controls.Add(c);
            return c;
        }

        void Section(FlowLayoutPanel page, string text)
        {
            var l = Lbl(text, 10f, semibold: true);
            l.Margin = new Padding(D(2), D(10), 0, D(7));
            page.Controls.Add(l);
        }

        Card SettingCard(string title, string desc, Control? control, int height = 58)
        {
            var card = new Card { Size = new Size(CW, D(height)) };
            card.Controls.Add(At(Lbl(title, 9.75f), D(16), D(10)));
            card.Controls.Add(At(Lbl(desc, 8.5f, Theme.SubText), D(16), D(31)));
            if (control != null)
            {
                control.Location = new Point(card.Width - D(16) - control.Width, (card.Height - control.Height) / 2);
                card.Controls.Add(control);
            }
            return card;
        }

        void AddRow(Control parent, int width, ref int ry, string title, string desc, Control control, bool last = false)
        {
            int rowH = D(54);
            parent.Controls.Add(At(Lbl(title, 9.75f), D(16), ry + D(9)));
            parent.Controls.Add(At(Lbl(desc, 8.5f, Theme.SubText), D(16), ry + D(29)));
            control.Location = new Point(width - D(16) - control.Width, ry + (rowH - control.Height) / 2);
            parent.Controls.Add(control);
            if (!last)
                parent.Controls.Add(new Panel
                {
                    BackColor = Theme.Border,
                    Location = new Point(D(16), ry + rowH - 1),
                    Size = new Size(width - D(32), 1),
                });
            ry += rowH;
        }

        void ShowPage(string key, bool animate = true)
        {
            if (key == currentPage || !pages.TryGetValue(key, out var next)) return;
            pages.TryGetValue(currentPage, out var prev);
            int dir = nav.IndexOf(key) >= nav.IndexOf(currentPage) ? 1 : -1;   // down the list = slide up
            currentPage = key;
            nav.Select(key, animate);
            if (key == "Home") RefreshHome();
            if (key == "Friends" && DateTime.Now - friendsLoaded > TimeSpan.FromSeconds(30)) _ = RefreshFriendsAsync();
            if (ContainsFocus) Presence.Screen("Editing settings", nav.TextOf(key));

            bool canAnimate = animate && prev != null && prev.Visible && IsHandleCreated && pageHost.Width > 0 && pageHost.Height > 0;
            if (!canAnimate)
            {
                foreach (var (k, p) in pages) p.Visible = k == key;
                return;
            }

            // Snapshot old page, cover with it, swap pages underneath, snapshot new page, then slide both.
            // Moving two bitmaps is far cheaper than moving every control.
            var from = PageTransition.Snapshot(prev!);
            transition.Cover(from);
            foreach (var (k, p) in pages) p.Visible = k == key;
            next.AutoScrollPosition = Point.Empty;
            var to = PageTransition.Snapshot(next);
            transition.Slide(to, dir);
        }

        void BackToMainMenu()
        {
            if (busy) { SetStatus("Wait for Roblox to finish launching first.", null, StatusKind.Busy); return; }
            var ctx = GuideStrapContext.Current;
            if (ctx == null) return;
            ctx.Open(new LauncherForm(ctx));
            Close();
        }

        void SetStatus(string text, float? value = null, StatusKind kind = StatusKind.Busy)
        {
            statusLabel.Text = L.T(text);
            statusLabel.ForeColor = kind == StatusKind.Error ? Theme.Danger : Theme.SubText;
            statusIcon.Text = kind switch { StatusKind.Idle => "\uE73E", StatusKind.Error => "\uE783", _ => "\uE895" };
            statusIcon.ForeColor = kind switch { StatusKind.Idle => Theme.Success, StatusKind.Error => Theme.Danger, _ => Theme.AccentLight };
            if (value.HasValue) progress.Value = value.Value;
        }

        Account? ActiveAccount => accounts.FirstOrDefault(a => a.UserId == settings.ActiveAccountId);
        static string Initial(string s) => s.Length > 0 ? s[..1].ToUpperInvariant() : "?";

        // HOME / PLAY
        void BuildHomePage()
        {
            var page = NewPage("Home", "Play", "Start Roblox with your settings.");

            var hero = new Card { Size = new Size(CW, D(100)) };
            homeAvatar.Size = new Size(D(60), D(60));
            homeAvatar.Location = new Point(D(18), D(20));
            homeAvatar.Ring = Theme.Gold;
            homeGreeting.Font = new Font("Segoe UI Semibold", 14f);
            homeGreeting.ForeColor = Theme.Text;
            homeGreeting.BackColor = Color.Transparent;
            homeGreeting.AutoSize = true;
            homeGreeting.Location = new Point(D(92), D(22));
            homeSub.Font = new Font("Segoe UI", 9f);
            homeSub.ForeColor = Theme.SubText;
            homeSub.BackColor = Color.Transparent;
            homeSub.AutoSize = false;
            homeSub.Size = new Size(CW - D(92) - D(190), D(36));
            homeSub.Location = new Point(D(94), D(54));
            var switchBtn = Secondary("Switch account", "\uE8AB", D(150), D(30));
            switchBtn.Location = new Point(CW - D(166), D(35));
            switchBtn.Click += (_, _) => ShowPage("Accounts");
            hero.Controls.AddRange(new Control[] { homeAvatar, homeGreeting, homeSub, switchBtn });
            Add(page, hero, 12);

            var join = new Card { Size = new Size(CW, D(92)) };
            join.Controls.Add(At(Lbl("Join a game (optional)", 9.75f), D(16), D(10)));
            join.Controls.Add(At(Lbl("Paste a game link or place ID. Leave it empty to just open Roblox.", 8.5f, Theme.SubText), D(16), D(30)));
            var input = InputBox(placeBox, "e.g. https://www.roblox.com/games/920587237", CW - D(32), "\uE71B");
            input.Location = new Point(D(16), D(52));
            join.Controls.Add(input);
            Add(page, join, 12);

            playButton.Text = "Play Roblox";
            playButton.Icon = "\uE768";
            playButton.Font = new Font("Segoe UI Semibold", 11f);
            playButton.Size = new Size(CW, D(42));
            playButton.Radius = 4;
            playButton.Click += (_, _) => StartLaunch();
            Add(page, playButton, 8);

            var serverRow = new Panel { Size = new Size(CW, D(32)), BackColor = Theme.Bg };
            int third = (CW - D(12)) / 3;
            var rejoinBtn = Secondary("Rejoin last server", "\uE72C", third, D(32));
            var hopBtn = Secondary("Server hop", "\uE8AB", third, D(32));
            var smallBtn = Secondary("Small server", "\uE716", third, D(32));
            rejoinBtn.Location = new Point(0, 0);
            hopBtn.Location = new Point(third + D(6), 0);
            smallBtn.Location = new Point((third + D(6)) * 2, 0);
            rejoinBtn.Click += (_, _) => StartServerLaunch(ServerPick.None, rejoin: true);
            hopBtn.Click += (_, _) => StartServerLaunch(ServerPick.Hop, rejoin: false);
            smallBtn.Click += (_, _) => StartServerLaunch(ServerPick.Small, rejoin: false);
            serverRow.Controls.AddRange(new Control[] { rejoinBtn, hopBtn, smallBtn });
            Add(page, serverRow, 8);

            statGraphics.AutoSize = false;
            statGraphics.Size = new Size(CW, D(20));
            statGraphics.Font = Theme.F("Segoe UI", 8.75f);
            statGraphics.ForeColor = Theme.SubText;
            statGraphics.Margin = new Padding(D(2), 0, 0, 0);
            page.Controls.Add(statGraphics);

            Section(page, "Recent games");
            recentPanel.Size = new Size(CW, D(140));
            recentPanel.FlowDirection = FlowDirection.LeftToRight;
            recentPanel.WrapContents = false;
            recentPanel.BackColor = Theme.Bg;
            recentPanel.Margin = new Padding(0);
            page.Controls.Add(recentPanel);
        }

        void RefreshRecent()
        {
            recentVersion = RecentGames.Version;
            recentPanel.SuspendLayout();
            foreach (var c in recentPanel.Controls.Cast<Control>().ToList()) c.Dispose();
            var games = RecentGames.Load().Take(6).ToList();
            if (games.Count == 0)
            {
                var empty = Lbl("Games you join show up here so you can jump back in.", 9f, Theme.SubText);
                empty.Margin = new Padding(D(2), D(4), 0, 0);
                recentPanel.Controls.Add(empty);
            }
            foreach (var g in games)
            {
                var tile = new GameTile { Text = g.Name, Size = new Size(D(108), D(136)), Margin = new Padding(0, 0, D(6), 0) };
                long id = g.PlaceId;
                tile.Click += (_, _) => { placeBox.Text = id.ToString(); StartLaunch(); };
                recentPanel.Controls.Add(tile);
                _ = LoadTileIconAsync(tile, g);
            }
            recentPanel.ResumeLayout();
        }

        static async Task LoadTileIconAsync(GameTile tile, RecentGame g)
        {
            var img = await RecentGames.GetIconAsync(g);
            if (!tile.IsDisposed) tile.Image = img;
        }

        void MakeStat(Panel row, int x, int w, string icon, string caption, Label value, string target)
        {
            var c = new Card { Location = new Point(x, 0), Size = new Size(w, D(66)), Cursor = Cursors.Hand };
            var ic = At(IconLabel(icon, 11f, Theme.AccentLight), D(14), D(12));
            var cap = At(Lbl(caption, 8.5f, Theme.SubText), D(38), D(11));
            value.AutoSize = false;
            value.Size = new Size(w - D(28), D(24));
            value.Location = new Point(D(12), D(33));
            value.Font = new Font("Segoe UI Semibold", 10.5f);
            value.ForeColor = Theme.Text;
            value.BackColor = Color.Transparent;
            value.AutoEllipsis = true;
            c.Controls.AddRange(new Control[] { ic, cap, value });
            foreach (Control ctl in new Control[] { c, ic, cap, value })
            {
                ctl.Cursor = Cursors.Hand;
                ctl.Click += (_, _) => ShowPage(target);
            }
            row.Controls.Add(c);
        }

        void RefreshHome()
        {
            var acc = ActiveAccount;
            if (settings.StreamerMode)
            {
                homeGreeting.Text = L.T("Streamer mode is on");
                homeSub.Text = L.T("Account names are hidden.");
                homeAvatar.Fallback = "G";
                homeAvatar.Image = null;
                chipName.Text = L.T("Streamer mode");
                chipSub.Text = L.T("Names hidden");
                chipAvatar.Fallback = "G";
                chipAvatar.Image = null;
            }
            else if (acc != null)
            {
                homeGreeting.Text = acc.DisplayName;
                homeSub.Text = $"@{acc.Username}";
                homeAvatar.Fallback = Initial(acc.DisplayName);
                homeAvatar.Image = avatarCache.GetValueOrDefault(acc.UserId);
                chipName.Text = acc.DisplayName;
                chipSub.Text = "@" + acc.Username;
                chipAvatar.Fallback = Initial(acc.DisplayName);
                chipAvatar.Image = homeAvatar.Image;
            }
            else
            {
                homeGreeting.Text = L.T("No account selected");
                homeSub.Text = L.T("Roblox will use whoever is logged in on the website.");
                homeAvatar.Fallback = "G";
                homeAvatar.Image = null;
                chipName.Text = L.T("Roblox login");
                chipSub.Text = L.T("Add accounts");
                chipAvatar.Fallback = "G";
                chipAvatar.Image = null;
            }
            string font = settings.FontFile != null ? settings.FontDisplayName ?? L.T("custom font") : L.T("default font");
            statGraphics.Text = L.F("{0} graphics  ·  {1}  ·  {2} FastFlag{3}",
                L.T(PresetInfo(settings.Preset).Title), font, settings.Flags.Count, settings.Flags.Count == 1 ? "" : "s");
        }

        // Rejoin the last server, hop to another one, or join the emptiest one
        void StartServerLaunch(ServerPick pick, bool rejoin)
        {
            var ctx = GuideStrapContext.Current;
            if (ctx == null) return;
            var last = LastServer.Load();
            string place = placeBox.Text.Trim();
            string? job = null;
            if (rejoin)
            {
                if (last == null)
                {
                    SetStatus("Join a game first. GuideStrap remembers the last server you were in.", null, StatusKind.Error);
                    return;
                }
                place = last.PlaceId.ToString();
                job = last.JobId;
            }
            else if (place.Length == 0)
            {
                if (last == null)
                {
                    SetStatus("Paste a game link first, or join a game once.", null, StatusKind.Error);
                    return;
                }
                place = last.PlaceId.ToString();
            }
            else if (RobloxLauncher.ParsePlaceId(place) == null)
            {
                SetStatus("That doesn't look like a game link or place ID.", null, StatusKind.Error);
                return;
            }
            ctx.Open(new LoadingForm(ctx, settings, accounts, null, place, job, rejoin ? ServerPick.None : pick));
            if (settings.CloseAfterLaunch) Close();
        }

        void StartLaunch()
        {
            var ctx = GuideStrapContext.Current;
            if (ctx == null) return;
            if (placeBox.Text.Trim().Length > 0 && RobloxLauncher.ParsePlaceId(placeBox.Text) == null)
            {
                SetStatus("That doesn't look like a game link or place ID.", null, StatusKind.Error);
                return;
            }
            ctx.Open(new LoadingForm(ctx, settings, accounts, null, placeBox.Text));
            if (settings.CloseAfterLaunch) Close();
        }

        // ACCOUNTS
        void BuildAccountsPage()
        {
            accountsPage = NewPage("Accounts", "Accounts", "Saved Roblox accounts.");

            var addBtn = Primary("Add account", "\uE710", D(132), D(30));
            addBtn.Click += async (_, _) => await AddAccountAsync();
            Add(accountsPage, SettingCard("Add an account", "Opens the Roblox login page.", addBtn), 8);

            var note = Lbl("Logins are encrypted and only work for your Windows user on this PC. Don't press \"Log out\" on the Roblox website for a saved account, or its saved login stops working.", 9f, Theme.SubText);
            note.AutoSize = false;
            note.Size = new Size(CW, D(34));
            Add(accountsPage, note, 0);

            Section(accountsPage, "Saved accounts");
        }

        void RefreshAccounts()
        {
            accountsPage.SuspendLayout();
            foreach (var c in accountCards) c.Dispose();
            accountCards.Clear();
            accountCards.Add(AccountCard(null));
            foreach (var a in accounts) accountCards.Add(AccountCard(a));
            foreach (var c in accountCards) Add(accountsPage, c, 8);
            accountsPage.ResumeLayout();
            RefreshHome();
        }

        Card AccountCard(Account? a)
        {
            bool active = a == null ? settings.ActiveAccountId == null : settings.ActiveAccountId == a.UserId;
            var card = new Card { Size = new Size(CW, D(60)), BorderColor = active ? Theme.Accent : Theme.Border };

            var av = new AvatarBox
            {
                Size = new Size(D(40), D(40)),
                Location = new Point(D(12), D(10)),
                Fallback = a == null ? "R" : Initial(a.DisplayName),
                Image = a == null ? null : avatarCache.GetValueOrDefault(a.UserId),
                Ring = active ? Theme.Gold : Color.Empty,
            };
            var name = At(Lbl(a == null ? "Default (Roblox's own login)" : a.DisplayName, 10f, semibold: true), D(62), D(9));
            var sub = At(Lbl(a == null ? "Uses whoever is logged in on the Roblox website" : "@" + a.Username, 8.5f, Theme.SubText), D(63), D(31));
            card.Controls.AddRange(new Control[] { av, name, sub });

            int x = card.Width - D(14);
            if (a != null)
            {
                var remove = Secondary("", "\uE74D", D(34), D(30));
                remove.HoverColor = Theme.Danger;
                x -= remove.Width;
                remove.Location = new Point(x, D(15));
                remove.Click += (_, _) => Later(() => RemoveAccount(a));
                card.Controls.Add(remove);
                x -= D(8);
            }

            RoundButton action;
            if (active)
            {
                action = Secondary("Active", "\uE73E", D(96), D(30));
                action.ForeColor = Theme.Gold;
                action.HoverColor = Theme.ButtonBg;
                action.Cursor = Cursors.Default;
            }
            else
            {
                action = Primary("Use", "", D(96), D(30));
                action.Click += (_, _) => Later(() =>
                {
                    settings.ActiveAccountId = a?.UserId;
                    settings.Save();
                    RefreshAccounts();
                    SetStatus(a == null ? "Using Roblox's own login." : $"Switched to {a.DisplayName}.", null, StatusKind.Idle);
                });
            }
            x -= action.Width;
            action.Location = new Point(x, D(15));
            card.Controls.Add(action);
            return card;
        }

        async Task AddAccountAsync()
        {
            string? cookie;
            using (var login = new LoginForm())
            {
                if (login.ShowDialog(this) != DialogResult.OK) return;
                cookie = login.Cookie;
            }
            if (string.IsNullOrEmpty(cookie)) return;

            try
            {
                var acc = await RobloxApi.GetAccountAsync(cookie);
                accounts.RemoveAll(x => x.UserId == acc.UserId);
                accounts.Add(acc);
                AccountStore.Save(accounts);
                settings.ActiveAccountId = acc.UserId;
                settings.Save();
                avatarCache[acc.UserId] = await RobloxApi.GetAvatarAsync(acc.UserId);
                RefreshAccounts();
                SetStatus($"Added {acc.DisplayName}.", null, StatusKind.Idle);
            }
            catch (Exception ex)
            {
                Ask("Couldn't add that account", ex.Message, "OK");
            }
        }

        void RemoveAccount(Account a)
        {
            if (Ask("Remove account?", $"Remove {a.DisplayName} (@{a.Username}) from GuideStrap?", "Remove", "Cancel") != 0) return;
            accounts.RemoveAll(x => x.UserId == a.UserId);
            if (settings.ActiveAccountId == a.UserId) { settings.ActiveAccountId = null; settings.Save(); }
            AccountStore.Save(accounts);
            RefreshAccounts();
        }

        async Task LoadAvatarsAsync()
        {
            foreach (var a in accounts.ToList())
                avatarCache[a.UserId] = await RobloxApi.GetAvatarAsync(a.UserId);
            if (!IsDisposed) RefreshAccounts();
        }

        // GRAPHICS
        void BuildGraphicsPage()
        {
            var page = NewPage("Graphics", "Graphics", "Pick a preset, or change settings one by one.");

            var presetCard = new Card { Size = new Size(CW, D(136)) };
            presetCard.Controls.Add(At(Lbl("Preset", 8.5f, Theme.SubText), D(16), D(12)));
            presetTitle.Font = new Font("Segoe UI Semibold", 12.5f);
            presetTitle.ForeColor = Theme.Text;
            presetTitle.BackColor = Color.Transparent;
            presetTitle.AutoSize = true;
            presetTitle.Location = new Point(D(14), D(27));
            presetDesc.ForeColor = Theme.SubText;
            presetDesc.BackColor = Color.Transparent;
            presetDesc.AutoSize = false;
            presetDesc.Size = new Size(CW - D(32), D(20));
            presetDesc.Location = new Point(D(16), D(52));
            presetDesc.Font = new Font("Segoe UI", 8.75f);
            presetSlider.Minimum = 0;
            presetSlider.Maximum = 4;
            presetSlider.ShowTicks = true;
            presetSlider.Size = new Size(CW - D(32), D(28));
            presetSlider.Location = new Point(D(16), D(76));
            var left = At(Lbl("Max FPS", 8.5f, Theme.SubText), D(16), D(108));
            var mid = new Label
            {
                Text = "Balanced", AutoSize = false, TextAlign = ContentAlignment.TopCenter,
                Size = new Size(D(160), D(18)), Location = new Point(CW / 2 - D(80), D(108)),
                ForeColor = Theme.SubText, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f),
            };
            var right = new Label
            {
                Text = "Max graphics", AutoSize = false, TextAlign = ContentAlignment.TopRight,
                Size = new Size(D(160), D(18)), Location = new Point(CW - D(176), D(108)),
                ForeColor = Theme.SubText, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f),
            };
            presetCard.Controls.AddRange(new Control[] { presetTitle, presetDesc, presetSlider, left, mid, right });
            Add(page, presetCard, 8);

            // --- potato mode: every low setting at once, or one by one ---
            var potato = new Expander("Potato mode", "Everything low for the most FPS, or turn on parts one by one.", CW, D(58), D(4) + 7 * D(54) + D(4));
            var pc = potato.Content;
            int pw = pc.Width, py = D(4);
            var potatoParts = new[] { potatoBlur, potatoGrass, potatoMesh, potatoSky, potatoMsaa };
            foreach (var tg in potatoParts.Append(potatoAll)) tg.Size = new Size(D(40), D(20));
            var previewBtn = Secondary("Preview", "\uE890", D(110), D(30));
            previewBtn.Click += (_, _) => { using var pf = new TexturePreviewForm(); pf.ShowDialog(this); };
            AddRow(pc, pw, ref py, "Turn on everything", "All of the switches below at once.", potatoAll);
            AddRow(pc, pw, ref py, "Blurry textures", "Lowest texture quality. Works on every game's textures.", potatoBlur);
            AddRow(pc, pw, ref py, "No grass", "Hides terrain grass.", potatoGrass);
            AddRow(pc, pw, ref py, "Low-detail meshes", "Always uses the simplest version of models.", potatoMesh);
            AddRow(pc, pw, ref py, "Gray sky", "Replaces the skybox with flat gray.", potatoSky);
            AddRow(pc, pw, ref py, "Anti-aliasing off", "No edge smoothing. Edges look a bit jagged.", potatoMsaa);
            AddRow(pc, pw, ref py, "Preview", "See what blurry textures look like.", previewBtn, last: true);
            Add(page, potato, 8);

            potatoAll.CheckedChanged += (_, _) =>
            {
                if (potatoSyncing) return;
                potatoSyncing = true;
                foreach (var tg in potatoParts) tg.Checked = potatoAll.Checked;
                potatoSyncing = false;
                ApplyPotatoToControls();
            };
            foreach (var tg in potatoParts)
                tg.CheckedChanged += (_, _) => { if (!potatoSyncing) ApplyPotatoToControls(); };

            // --- advanced (animated expander) ---
            int rows = 11;
            var adv = new Expander("Advanced settings", "Change every graphics setting yourself.", CW, D(58), D(4) + rows * D(54) + D(4));
            var content = adv.Content;
            int cw = content.Width;
            int ry = D(4);

            var qualityBox = new Panel { Size = new Size(D(260), D(28)), BackColor = Theme.Card };
            qualitySlider.Minimum = 0;
            qualitySlider.Maximum = 21;
            qualitySlider.Size = new Size(D(210), D(28));
            qualitySlider.Location = new Point(0, 0);
            qualityValue.AutoSize = false;
            qualityValue.Size = new Size(D(46), D(28));
            qualityValue.Location = new Point(D(214), 0);
            qualityValue.TextAlign = ContentAlignment.MiddleRight;
            qualityValue.ForeColor = Theme.AccentLight;
            qualityValue.BackColor = Theme.Card;
            qualityValue.Font = new Font("Segoe UI Semibold", 9.5f);
            qualityBox.Controls.Add(qualitySlider);
            qualityBox.Controls.Add(qualityValue);

            SetupCombo(textureCombo, D(170), "Auto", "Lowest", "Low", "Medium", "High");
            SetupCombo(msaaCombo, D(170), "Auto", "Off (1x)", "2x", "4x", "8x");
            SetupCombo(rendererCombo, D(170), "Auto", "Direct3D 11", "Vulkan", "OpenGL");
            var toggles = new[] { grassToggle, skyToggle, meshToggle, voxelToggle, grassMotionToggle, dpiToggle, fullscreenToggle };
            foreach (var tg in toggles) tg.Size = new Size(D(40), D(20));

            AddRow(content, cw, ref ry, "Graphics quality", "Roblox's quality level (1-21). Auto = Roblox decides.", qualityBox);
            AddRow(content, cw, ref ry, "Texture quality", "How sharp surfaces and decals look.", textureCombo);
            AddRow(content, cw, ref ry, "Anti-aliasing", "Smooths jagged edges. Costs FPS.", msaaCombo);
            AddRow(content, cw, ref ry, "Rendering API", "Try Vulkan or D3D11 if you crash or stutter.", rendererCombo);
            AddRow(content, cw, ref ry, "Remove grass", "Hides terrain grass. Big FPS boost on grassy maps.", grassToggle);
            AddRow(content, cw, ref ry, "Gray sky", "Replaces the skybox with flat gray.", skyToggle);
            AddRow(content, cw, ref ry, "Low-detail meshes", "Always uses the simplest version of models.", meshToggle);
            AddRow(content, cw, ref ry, "Freeze lighting updates", "Faster, but lighting can look flat or wrong.", voxelToggle);
            AddRow(content, cw, ref ry, "Still grass", "Stops grass from waving around.", grassMotionToggle);
            AddRow(content, cw, ref ry, "Disable DPI scaling", "Sharper on high-res screens, UI gets smaller.", dpiToggle);
            AddRow(content, cw, ref ry, "Alt+Enter real fullscreen", "Exclusive fullscreen, can lower input lag.", fullscreenToggle, last: true);
            Add(page, adv, 0);

            Section(page, "Performance");
            SetupCombo(fpsCombo, D(170), "Roblox default", "60 FPS", "120 FPS", "144 FPS", "165 FPS", "240 FPS");
            fpsCombo.SelectedIndex = Math.Max(0, Array.IndexOf(FpsCaps, settings.FpsCap));
            fpsCombo.SelectedIndexChanged += (_, _) =>
            {
                settings.FpsCap = FpsCaps[Math.Clamp(fpsCombo.SelectedIndex, 0, FpsCaps.Length - 1)];
                settings.Save();
            };
            Add(page, SettingCard("FPS cap", "Sets Roblox's own max frame rate before it opens. Normal Roblox keeps it too.", fpsCombo));
            SetupCombo(priorityCombo, D(170), "Normal", "Above normal", "High");
            priorityCombo.SelectedIndex = Math.Clamp(settings.Priority, 0, 2);
            priorityCombo.SelectedIndexChanged += (_, _) =>
            {
                settings.Priority = Math.Clamp(priorityCombo.SelectedIndex, 0, 2);
                settings.Save();
            };
            Add(page, SettingCard("Roblox priority", "Gives Roblox more CPU time. High can slow OBS while streaming.", priorityCombo), 0);

            presetSlider.ValueChanged += (_, _) => { if (!syncing) ApplyPreset(presetSlider.Value); };
            presetSlider.Released += (_, _) => { if (!syncing && settings.Preset != presetSlider.Value) ApplyPreset(presetSlider.Value); };
            qualitySlider.ValueChanged += (_, _) =>
            {
                qualityValue.Text = L.T(qualitySlider.Value == 0 ? "Auto" : qualitySlider.Value.ToString());
                OnGraphicsChanged();
            };
            foreach (var combo in new[] { textureCombo, msaaCombo, rendererCombo })
                combo.SelectedIndexChanged += (_, _) => OnGraphicsChanged();
            foreach (var tg in toggles)
                tg.CheckedChanged += (_, _) => OnGraphicsChanged();
        }

        DarkCombo SetupCombo(DarkCombo c, int width, params string[] items)
        {
            c.ItemHeight = D(22);
            c.Width = width;
            c.Items.AddRange(items.Select(L.T).ToArray<object>());
            c.SelectedIndex = 0;
            return c;
        }

        static (string Title, string Desc) PresetInfo(int p) => p switch
        {
            0 => ("Max FPS", "Lowest quality, flat lighting, no grass. For weak PCs or pure frames."),
            1 => ("Performance", "Low settings that still look okay. Great for laptops."),
            2 => ("Balanced", "Roblox's normal automatic settings. Nothing changed."),
            3 => ("Quality", "Sharper textures and smoother edges. Needs a decent PC."),
            4 => ("Max Graphics", "Everything maxed out. Best looks, lowest FPS."),
            _ => ("Custom", "You changed settings yourself. Move the slider to pick a preset again."),
        };

        void UpdatePresetLabels()
        {
            var (title, desc) = PresetInfo(settings.Preset);
            presetTitle.Text = L.T(title);
            presetDesc.Text = L.T(desc);
        }

        void ApplyPreset(int p)
        {
            // (quality, texture index, msaa index, no grass, gray sky, low meshes, freeze lighting)
            var (q, tex, msaa, grass, sky, mesh, voxel) = p switch
            {
                0 => (1, 1, 1, true, true, true, true),
                1 => (5, 2, 0, true, false, true, false),
                3 => (16, 4, 3, false, false, false, false),
                4 => (21, 4, 3, false, false, false, false),
                _ => (0, 0, 0, false, false, false, false),
            };
            syncing = true;
            qualitySlider.Value = q;
            textureCombo.SelectedIndex = tex;
            msaaCombo.SelectedIndex = msaa;
            grassToggle.Checked = grass;
            skyToggle.Checked = sky;
            meshToggle.Checked = mesh;
            voxelToggle.Checked = voxel;
            syncing = false;

            ApplyControlsToFlags();
            settings.Preset = p;
            UpdatePresetLabels();
            settings.Save();
            RefreshFlagSummary();
            RefreshHome();
            SyncPotato();
        }

        void OnGraphicsChanged()
        {
            if (syncing) return;
            ApplyControlsToFlags();
            settings.Preset = -1;
            UpdatePresetLabels();
            settings.Save();
            RefreshFlagSummary();
            RefreshHome();
            SyncPotato();
        }

        // Potato switches drive the same controls as Advanced settings, so both always agree.
        void ApplyPotatoToControls()
        {
            syncing = true;
            if (potatoBlur.Checked) textureCombo.SelectedIndex = 1;
            else if (textureCombo.SelectedIndex == 1) textureCombo.SelectedIndex = 0;
            grassToggle.Checked = potatoGrass.Checked;
            meshToggle.Checked = potatoMesh.Checked;
            skyToggle.Checked = potatoSky.Checked;
            if (potatoMsaa.Checked) msaaCombo.SelectedIndex = 1;
            else if (msaaCombo.SelectedIndex == 1) msaaCombo.SelectedIndex = 0;
            syncing = false;
            OnGraphicsChanged();
        }

        void SyncPotato()
        {
            potatoSyncing = true;
            potatoBlur.Checked = textureCombo.SelectedIndex == 1;
            potatoGrass.Checked = grassToggle.Checked;
            potatoMesh.Checked = meshToggle.Checked;
            potatoSky.Checked = skyToggle.Checked;
            potatoMsaa.Checked = msaaCombo.SelectedIndex == 1;
            potatoAll.Checked = potatoBlur.Checked && potatoGrass.Checked && potatoMesh.Checked && potatoSky.Checked && potatoMsaa.Checked;
            potatoSyncing = false;
        }

        void ApplyControlsToFlags()
        {
            var f = settings.Flags;
            void Set(string name, string? value) { if (value == null) f.Remove(name); else f[name] = value; }

            Set(FF.Quality, qualitySlider.Value == 0 ? null : qualitySlider.Value.ToString());
            if (textureCombo.SelectedIndex <= 0) { f.Remove(FF.TexEnabled); f.Remove(FF.Texture); }
            else { f[FF.TexEnabled] = "True"; f[FF.Texture] = (textureCombo.SelectedIndex - 1).ToString(); }
            Set(FF.Msaa, msaaCombo.SelectedIndex <= 0 ? null : MsaaValues[msaaCombo.SelectedIndex]);
            Set(FF.D3D11, rendererCombo.SelectedIndex == 1 ? "True" : null);
            Set(FF.Vulkan, rendererCombo.SelectedIndex == 2 ? "True" : null);
            Set(FF.OpenGL, rendererCombo.SelectedIndex == 3 ? "True" : null);
            Set(FF.GrassMin, grassToggle.Checked ? "0" : null);
            Set(FF.GrassMax, grassToggle.Checked ? "0" : null);
            Set(FF.GraySky, skyToggle.Checked ? "True" : null);
            foreach (var lod in FF.Lod) Set(lod, meshToggle.Checked ? "0" : null);
            Set(FF.PauseVoxelizer, voxelToggle.Checked ? "True" : null);
            Set(FF.GrassMotion, grassMotionToggle.Checked ? "0" : null);
            Set(FF.DisableDpi, dpiToggle.Checked ? "True" : null);
            Set(FF.AltEnter, fullscreenToggle.Checked ? "False" : null);
        }

        void RefreshGraphicsControls()
        {
            var f = settings.Flags;
            bool Is(string name, string value) => f.TryGetValue(name, out var v) && string.Equals(v, value, StringComparison.OrdinalIgnoreCase);

            syncing = true;
            qualitySlider.Value = f.TryGetValue(FF.Quality, out var q) && int.TryParse(q, out var qi) ? Math.Clamp(qi, 1, 21) : 0;
            qualityValue.Text = L.T(qualitySlider.Value == 0 ? "Auto" : qualitySlider.Value.ToString());
            textureCombo.SelectedIndex = Is(FF.TexEnabled, "True") && f.TryGetValue(FF.Texture, out var tx) && int.TryParse(tx, out var ti)
                ? Math.Clamp(ti, 0, 3) + 1 : 0;
            int msaaIdx = f.TryGetValue(FF.Msaa, out var ms) ? Array.IndexOf(MsaaValues, ms) : 0;
            msaaCombo.SelectedIndex = msaaIdx < 0 ? 0 : msaaIdx;
            rendererCombo.SelectedIndex = Is(FF.D3D11, "True") ? 1 : Is(FF.Vulkan, "True") ? 2 : Is(FF.OpenGL, "True") ? 3 : 0;
            grassToggle.Checked = Is(FF.GrassMax, "0");
            skyToggle.Checked = Is(FF.GraySky, "True");
            meshToggle.Checked = Is(FF.Lod[0], "0");
            voxelToggle.Checked = Is(FF.PauseVoxelizer, "True");
            grassMotionToggle.Checked = Is(FF.GrassMotion, "0");
            dpiToggle.Checked = Is(FF.DisableDpi, "True");
            fullscreenToggle.Checked = Is(FF.AltEnter, "False");
            if (settings.Preset >= 0) presetSlider.Value = settings.Preset;
            UpdatePresetLabels();
            syncing = false;
            SyncPotato();
        }

        // FASTFLAGS
        void BuildFlagsPage()
        {
            var page = NewPage("Flags", "FastFlags", "Flags from Roblox's allowlist.");

            var open = Primary("Open editor", "\uE70F", D(132), D(30));
            open.Click += (_, _) => OpenFlagEditor();
            Add(page, SettingCard("FastFlag editor", "Add, import or edit flags.", open), 8);

            Section(page, "Active flags");
            flagPreviewCard = new Card { Size = new Size(CW, D(70)), Cursor = Cursors.Hand };
            flagPreview.AutoSize = true;
            flagPreview.MaximumSize = new Size(CW - D(32), 0);
            flagPreview.Font = new Font("Consolas", 9f);
            flagPreview.BackColor = Color.Transparent;
            flagPreview.Location = new Point(D(16), D(12));
            flagPreview.Cursor = Cursors.Hand;
            flagPreviewCard.Controls.Add(flagPreview);
            flagPreviewCard.Click += (_, _) => OpenFlagEditor();
            flagPreview.Click += (_, _) => OpenFlagEditor();
            Add(page, flagPreviewCard, 8);

            var info = Lbl("Roblox only reads flags on its allowlist, so anything else gets skipped automatically.", 8.5f, Theme.SubText);
            Add(page, info, 0);
        }

        void RefreshFlagSummary()
        {
            int count = settings.Flags.Count;
            if (count == 0)
            {
                flagPreview.Text = "No flags yet. Pick a graphics preset or open the editor.";
                flagPreview.ForeColor = Theme.SubText;
            }
            else
            {
                var lines = settings.Flags.OrderBy(k => k.Key).Take(10).Select(kv => $"{kv.Key} = {kv.Value}").ToList();
                if (count > 10) lines.Add(L.F("+ {0} more", count - 10));
                flagPreview.Text = string.Join("\n", lines);
                flagPreview.ForeColor = Theme.Text;
            }
            flagPreviewCard.Height = flagPreview.GetPreferredSize(new Size(CW - D(32), 0)).Height + D(24);
        }

        void OpenFlagEditor()
        {
            using var editor = new FlagEditorForm(settings.Flags, () =>
            {
                settings.Preset = settings.Flags.Count == 0 ? 2 : -1;
                settings.Save();
                RefreshGraphicsControls();
                RefreshFlagSummary();
                RefreshHome();
            });
            editor.ShowDialog(this);
        }

        // TEXT & FONT
        void BuildFontPage()
        {
            var page = NewPage("Font", "Text fonts", "The font Roblox uses in-game.");

            var card = new Card { Size = new Size(CW, D(206)) };
            card.Controls.Add(At(Lbl("Current font", 8.5f, Theme.SubText), D(16), D(12)));
            fontName.AutoSize = true;
            fontName.Font = new Font("Segoe UI Semibold", 12.5f);
            fontName.ForeColor = Theme.Text;
            fontName.BackColor = Color.Transparent;
            fontName.Location = new Point(D(14), D(27));

            var previewWrap = new Card
            {
                BackColor = Theme.Input, Radius = 6,
                Location = new Point(D(16), D(60)), Size = new Size(CW - D(32), D(88)),
            };
            fontPreview.AutoSize = false;
            fontPreview.Location = new Point(D(14), D(8));
            fontPreview.Size = new Size(previewWrap.Width - D(28), D(72));
            fontPreview.ForeColor = Theme.Text;
            fontPreview.BackColor = Color.Transparent;
            fontPreview.Text = "The quick brown fox jumps over the lazy dog\n0123456789  !?  Hello from GuideStrap";
            previewWrap.Controls.Add(fontPreview);

            var choose = Primary("Choose font file", "\uE8E5", D(168), D(30));
            choose.Location = new Point(D(16), D(162));
            choose.Click += (_, _) => ChooseFont();
            var reset = Secondary("Use default font", "\uE72C", D(160), D(30));
            reset.Location = new Point(D(192), D(162));
            reset.Click += (_, _) =>
            {
                settings.FontFile = null;
                settings.FontDisplayName = null;
                settings.Save();
                RefreshFontPreview();
                RefreshHome();
                SetStatus("Font reset to Roblox's default.", null, StatusKind.Idle);
            };
            card.Controls.AddRange(new Control[] { fontName, previewWrap, choose, reset });
            Add(page, card, 8);

            var tip = Lbl("Works with .ttf and .otf files (free fonts: fonts.google.com). Emoji keep their normal look. Applies next time you press Play.", 8.5f, Theme.SubText);
            tip.AutoSize = false;
            tip.Size = new Size(CW, D(36));
            Add(page, tip, 0);
        }

        void ChooseFont()
        {
            using var dlg = new OpenFileDialog { Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf", Title = L.T("Pick a font") };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                fontPreview.Font = new Font("Segoe UI", 14f);
                previewFonts?.Dispose();
                previewFonts = null;

                Directory.CreateDirectory(Paths.Base);
                foreach (var old in Directory.GetFiles(Paths.Base, "CustomFont.*"))
                    try { File.Delete(old); } catch { }

                string dest = Path.Combine(Paths.Base, "CustomFont" + Path.GetExtension(dlg.FileName).ToLowerInvariant());
                File.Copy(dlg.FileName, dest, true);
                settings.FontFile = dest;
                settings.FontDisplayName = Path.GetFileNameWithoutExtension(dlg.FileName);
                settings.Save();
                SetStatus($"Font set to {settings.FontDisplayName}.", null, StatusKind.Idle);
            }
            catch (Exception ex)
            {
                Ask("Couldn't use that font", ex.Message, "OK");
            }
            RefreshFontPreview();
            RefreshHome();
        }

        void RefreshFontPreview()
        {
            fontPreview.Font = new Font("Segoe UI", 14f);
            previewFonts?.Dispose();
            previewFonts = null;

            if (settings.FontFile == null || !File.Exists(settings.FontFile))
            {
                fontName.Text = L.T("Roblox default");
                return;
            }
            fontName.Text = L.T(settings.FontDisplayName ?? "Custom font");
            try
            {
                previewFonts = new PrivateFontCollection();
                previewFonts.AddFontFile(settings.FontFile);
                var fam = previewFonts.Families[0];
                var style = fam.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
                fontPreview.Font = new Font(fam, 14f, style);
            }
            catch
            {
                fontName.Text += L.T("  (no preview, still works in-game)");
            }
        }

        // SETTINGS
        void BuildModsPage()
        {
            var page = NewPage("Mods", "Mods", "Cosmetic file swaps. Only GuideStrap's copy of Roblox is changed.");

            var browse = Primary("Browse", "\uE71E", D(110), D(30));
            browse.Click += (_, _) =>
            {
                using var g = new GalleryForm(ChangeSettings);
                g.ShowDialog(this);
            };
            Add(page, SettingCard("Mod gallery", "Skies, cursors, crosshairs and sound packs you can install in one click.", browse), 8);

            // death sound
            var soundButtons = ButtonPair(
                Secondary("Choose .ogg", "\uE8E5", D(120), D(30)), ChooseDeathSound,
                Secondary("Reset", "\uE72C", D(80), D(30)), () => { settings.DeathSoundFile = null; settings.Save(); RefreshMods(); });
            Add(page, ModCard("Death sound", deathStatus, soundButtons));

            // footsteps + jump
            SetupCombo(footstepCombo, D(170), "Default", "Muted", "Custom .mp3...");
            footstepCombo.SelectedIndexChanged += (_, _) => { if (!modsSyncing) OnSoundPicked(footstepCombo, "footsteps"); };
            Add(page, ModCard("Footsteps", footstepStatus, footstepCombo));
            SetupCombo(jumpCombo, D(170), "Default", "Muted", "Custom .mp3...");
            jumpCombo.SelectedIndexChanged += (_, _) => { if (!modsSyncing) OnSoundPicked(jumpCombo, "jump"); };
            Add(page, ModCard("Jump sound", jumpStatus, jumpCombo));

            // sound packs
            packCombo.ItemHeight = D(22);
            packCombo.Width = D(150);
            var openPacks = Secondary("", "\uE8B7", D(36), D(30));
            openPacks.Click += (_, _) =>
            {
                Directory.CreateDirectory(Paths.SoundPacks);
                Process.Start(new ProcessStartInfo(Paths.SoundPacks) { UseShellExecute = true });
            };
            var packRight = new Panel { Size = new Size(D(150) + D(6) + D(36), D(30)), BackColor = Theme.Card };
            packCombo.Location = new Point(0, (D(30) - packCombo.Height) / 2);
            openPacks.Location = new Point(D(156), 0);
            packRight.Controls.Add(packCombo);
            packRight.Controls.Add(openPacks);
            packCombo.SelectedIndexChanged += (_, _) =>
            {
                if (modsSyncing) return;
                settings.SoundPack = packCombo.SelectedIndex <= 0 ? null : packCombo.SelectedItem?.ToString();
                settings.Save();
                RefreshMods();
            };
            Add(page, ModCard("Sound pack", packStatus, packRight));

            // cursor
            cursorPreview.Size = new Size(D(32), D(32));
            cursorPreview.SizeMode = PictureBoxSizeMode.Zoom;
            cursorPreview.BackColor = Theme.Input;
            SetupCombo(cursorCombo, D(170), "Default", "GuideStrap arrow", "Small dot", "Small cross", "Circle", "Classic arrow", "Custom image...");
            var cursorRight = new Panel { Size = new Size(D(212), D(32)), BackColor = Theme.Card };
            cursorPreview.Location = new Point(0, 0);
            cursorCombo.Location = new Point(D(42), (D(32) - cursorCombo.Height) / 2);
            cursorRight.Controls.Add(cursorPreview);
            cursorRight.Controls.Add(cursorCombo);
            cursorCombo.SelectedIndexChanged += (_, _) => { if (!modsSyncing) OnCursorPicked(); };
            Add(page, ModCard("Mouse cursor", cursorStatus, cursorRight));

            // shift lock crosshair
            crosshairPreview.Size = new Size(D(32), D(32));
            crosshairPreview.SizeMode = PictureBoxSizeMode.Zoom;
            crosshairPreview.BackColor = Theme.Input;
            SetupCombo(crosshairCombo, D(170), "Default", "Dot", "Small cross", "Circle", "Custom image...");
            var crosshairRight = new Panel { Size = new Size(D(212), D(32)), BackColor = Theme.Card };
            crosshairPreview.Location = new Point(0, 0);
            crosshairCombo.Location = new Point(D(42), (D(32) - crosshairCombo.Height) / 2);
            crosshairRight.Controls.Add(crosshairPreview);
            crosshairRight.Controls.Add(crosshairCombo);
            crosshairCombo.SelectedIndexChanged += (_, _) => { if (!modsSyncing) OnCrosshairPicked(); };
            Add(page, ModCard("Shift lock crosshair", crosshairStatus, crosshairRight));

            // sky
            var skyButtons = ButtonPair(
                Secondary("Choose sky folder", "\uE8B7", D(150), D(30)), ChooseSky,
                Secondary("Reset", "\uE72C", D(80), D(30)), () =>
                {
                    settings.CustomSky = false;
                    settings.Save();
                    try { if (Directory.Exists(Paths.Sky)) Directory.Delete(Paths.Sky, true); } catch { }
                    RefreshMods();
                });
            Add(page, ModCard("Sky", skyStatus, skyButtons));

            Section(page, "Setups");
            setupCombo.ItemHeight = D(22);
            setupCombo.Width = D(150);
            var saveSetup = Secondary("Save", "\uE74E", D(80), D(30));
            var deleteSetup = Secondary("", "\uE74D", D(36), D(30));
            deleteSetup.HoverColor = Theme.Danger;
            var setupRight = new Panel { Size = new Size(D(150) + D(6) + D(80) + D(6) + D(36), D(30)), BackColor = Theme.Card };
            setupCombo.Location = new Point(0, (D(30) - setupCombo.Height) / 2);
            saveSetup.Location = new Point(D(156), 0);
            deleteSetup.Location = new Point(D(242), 0);
            setupRight.Controls.Add(setupCombo);
            setupRight.Controls.Add(saveSetup);
            setupRight.Controls.Add(deleteSetup);
            setupCombo.SelectedIndexChanged += (_, _) =>
            {
                if (modsSyncing || setupCombo.SelectedIndex <= 0) return;
                if (setupCombo.SelectedItem?.ToString() is string name) UseSetup(name);
            };
            saveSetup.Click += (_, _) => SaveSetup();
            deleteSetup.Click += (_, _) => DeleteSetup();
            Add(page, ModCard("Saved setups", setupStatus, setupRight));

            var linkGame = Secondary("Link game", "\uE71B", D(110), D(30));
            var clearLinks = Secondary("", "\uE74D", D(36), D(30));
            clearLinks.HoverColor = Theme.Danger;
            var autoRight = new Panel { Size = new Size(D(110) + D(6) + D(36), D(30)), BackColor = Theme.Card };
            linkGame.Location = new Point(0, 0);
            clearLinks.Location = new Point(D(116), 0);
            autoRight.Controls.Add(linkGame);
            autoRight.Controls.Add(clearLinks);
            linkGame.Click += (_, _) => LinkGameToSetup();
            clearLinks.Click += (_, _) =>
            {
                if (settings.GameSetups.Count == 0) return;
                if (Ask("Remove all links?", "Every game goes back to your normal settings.", "Remove", "Cancel") != 0) return;
                settings.GameSetups.Clear();
                settings.Save();
                RefreshMods();
            };
            Add(page, ModCard("Auto setups", autoStatus, autoRight));

            Section(page, "Your own mods");
            var openMods = Secondary("Open mods folder", "\uE8B7", D(160), D(30));
            openMods.Click += (_, _) =>
            {
                Directory.CreateDirectory(Paths.Mods);
                Process.Start(new ProcessStartInfo(Paths.Mods) { UseShellExecute = true });
            };
            Add(page, SettingCard("Mods folder", "Files here replace the matching files in Roblox (same folder layout).", openMods));

            var note = Lbl("Normal Roblox is never touched. Launching Roblox without GuideStrap always uses the original files, sounds and settings.", 8.5f, Theme.SubText);
            note.AutoSize = false;
            note.Size = new Size(CW, D(34));
            note.Margin = new Padding(D(2), D(6), 0, 0);
            page.Controls.Add(note);
        }

        Card ModCard(string title, Label status, Control right)
        {
            var card = new Card { Size = new Size(CW, D(58)) };
            card.Controls.Add(At(Lbl(title, 9.75f), D(16), D(10)));
            status.AutoSize = false;
            status.Size = new Size(CW - right.Width - D(48), D(18));
            status.Location = new Point(D(16), D(31));
            status.Font = Theme.F("Segoe UI", 8.5f);
            status.ForeColor = Theme.SubText;
            status.AutoEllipsis = true;
            card.Controls.Add(status);
            right.Location = new Point(card.Width - D(16) - right.Width, (card.Height - right.Height) / 2);
            card.Controls.Add(right);
            return card;
        }

        Panel ButtonPair(RoundButton a, Action onA, RoundButton b, Action onB)
        {
            var p = new Panel { Size = new Size(a.Width + D(6) + b.Width, a.Height), BackColor = Theme.Card };
            a.Location = new Point(0, 0);
            b.Location = new Point(a.Width + D(6), 0);
            a.Click += (_, _) => onA();
            b.Click += (_, _) => onB();
            p.Controls.Add(a);
            p.Controls.Add(b);
            return p;
        }

        // Used by the crash helper so its fixes land in this window's settings too
        internal void ChangeSettings(Action<AppSettings> change)
        {
            change(settings);
            settings.Save();
            multiToggle.Checked = settings.MultiInstance;
            RefreshGraphicsControls();
            RefreshFlagSummary();
            RefreshMods();
            RefreshHome();
        }

        void RefreshMods()
        {
            modsSyncing = true;
            deathStatus.Text = L.T(settings.DeathSoundFile != null && File.Exists(settings.DeathSoundFile)
                ? "Custom sound" : "Roblox's default sound");
            cursorCombo.SelectedIndex = Math.Max(0, Array.IndexOf(CursorKeys, settings.CursorPreset));
            cursorStatus.Text = L.T(settings.CursorPreset switch
            {
                "guidestrap" => "Purple arrow made for GuideStrap",
                "dot" => "A small dot, easy to aim with",
                "cross" => "A small cross, easy to aim with",
                "circle" => "A thin circle",
                "classic" => "White arrow with a black outline",
                "custom" => "Your own image",
                _ => "Roblox's default cursor",
            });
            string? dir = ModAssets.CursorDir(settings);
            var old = cursorPreview.Image;
            cursorPreview.Image = null;
            old?.Dispose();
            if (dir != null)
            {
                try
                {
                    using var fs = File.OpenRead(Path.Combine(dir, "ArrowCursor.png"));
                    using var img = Image.FromStream(fs);
                    cursorPreview.Image = new Bitmap(img);
                }
                catch { }
            }
            crosshairCombo.SelectedIndex = Math.Max(0, Array.IndexOf(CrosshairKeys, settings.CrosshairPreset));
            crosshairStatus.Text = L.T(settings.CrosshairPreset switch
            {
                "dot" => "A small dot in the middle of the screen",
                "cross" => "A small cross in the middle of the screen",
                "circle" => "A thin circle in the middle of the screen",
                "custom" => "Your own image",
                _ => "Roblox's default shift lock icon",
            });
            ShowPreview(crosshairPreview, ModAssets.CrosshairFile(settings));

            footstepCombo.SelectedIndex = Math.Max(0, Array.IndexOf(SoundModes, settings.FootstepSound));
            footstepStatus.Text = L.T(settings.FootstepSound switch { "mute" => "No footstep sound", "custom" => "Your own sound", _ => "Roblox's default footsteps" });
            jumpCombo.SelectedIndex = Math.Max(0, Array.IndexOf(SoundModes, settings.JumpSound));
            jumpStatus.Text = L.T(settings.JumpSound switch { "mute" => "No jump or landing sound", "custom" => "Your own sound", _ => "Roblox's default jump sound" });

            var packs = ModAssets.SoundPacks();
            packCombo.Items.Clear();
            packCombo.Items.Add(L.T("None"));
            foreach (var pk in packs) packCombo.Items.Add(pk);
            int packIdx = settings.SoundPack == null ? 0 : packs.IndexOf(settings.SoundPack) + 1;
            packCombo.SelectedIndex = Math.Max(0, packIdx);
            packStatus.Text = packIdx > 0
                ? L.F("Using {0}. Its files replace Roblox's sounds.", settings.SoundPack!)
                : L.T(packs.Count == 0 ? "Open the folder and add a pack: one folder per pack, with .ogg or .mp3 files." : "No pack. Pick one, or open the folder to add more.");

            setupCombo.Items.Clear();
            setupCombo.Items.Add(L.T("Pick a setup"));
            var setupNames = settings.Setups.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var n in setupNames) setupCombo.Items.Add(n);
            int setupIdx = settings.LastSetup == null ? 0 : setupNames.IndexOf(settings.LastSetup) + 1;
            setupCombo.SelectedIndex = Math.Max(0, setupIdx);
            setupStatus.Text = setupNames.Count == 0
                ? L.T("Save your mods and graphics as a setup, like BedWars or Recording.")
                : L.F("{0} saved. Pick one to switch to it.", setupNames.Count);

            var links = settings.GameSetups.Where(kv => settings.Setups.ContainsKey(kv.Value)).ToList();
            var recentGames = RecentGames.Load();
            string GameName(string key) =>
                long.TryParse(key, out var gid) ? recentGames.FirstOrDefault(r => r.PlaceId == gid)?.Name ?? key : key;
            autoStatus.Text = links.Count == 0
                ? L.T("Link a setup to a game and it turns on by itself when you launch that game.")
                : string.Join(",  ", links.Take(3).Select(kv => $"{GameName(kv.Key)} \u2192 {kv.Value}"))
                  + (links.Count > 3 ? L.F("  +{0} more", links.Count - 3) : "");

            int skyFiles = Directory.Exists(Paths.Sky) ? Directory.GetFiles(Paths.Sky, "*.tex").Length : 0;
            skyStatus.Text = L.T(settings.CustomSky && skyFiles > 0
                ? $"Custom sky pack ({skyFiles} files). Games with their own sky keep it."
                : "Roblox's default sky. Gray sky is under Graphics.");
            modsSyncing = false;
        }

        void ChooseDeathSound()
        {
            using var dlg = new OpenFileDialog { Filter = "Ogg sound (*.ogg)|*.ogg", Title = L.T("Pick a death sound") };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var head = new byte[4];
                using (var fs = File.OpenRead(dlg.FileName)) fs.ReadExactly(head);
                if (Encoding.ASCII.GetString(head) != "OggS")
                {
                    Ask("That isn't an Ogg file", "Roblox only plays .ogg sounds here. Convert your sound to .ogg first (any free online converter works).", "OK");
                    return;
                }
                Directory.CreateDirectory(Paths.Managed);
                string dest = Path.Combine(Paths.Managed, "ouch.ogg");
                File.Copy(dlg.FileName, dest, true);
                settings.DeathSoundFile = dest;
                settings.Save();
                SetStatus("Death sound set. It applies next time you launch Roblox.", null, StatusKind.Idle);
            }
            catch (Exception ex) { Ask("Couldn't use that sound", ex.Message, "OK"); }
            RefreshMods();
        }

        void OnCursorPicked()
        {
            string pick = CursorKeys[Math.Clamp(cursorCombo.SelectedIndex, 0, CursorKeys.Length - 1)];
            if (pick == "custom")
            {
                using var dlg = new OpenFileDialog { Filter = "Images (*.png)|*.png", Title = L.T("Pick a cursor image") };
                if (dlg.ShowDialog(this) != DialogResult.OK || !ModAssets.ImportCustomCursor(dlg.FileName))
                {
                    RefreshMods();
                    return;
                }
            }
            settings.CursorPreset = pick;
            settings.Save();
            RefreshMods();
        }

        void OnCrosshairPicked()
        {
            string pick = CrosshairKeys[Math.Clamp(crosshairCombo.SelectedIndex, 0, CrosshairKeys.Length - 1)];
            if (pick == "custom")
            {
                using var dlg = new OpenFileDialog { Filter = "Images (*.png)|*.png", Title = L.T("Pick a crosshair image") };
                if (dlg.ShowDialog(this) != DialogResult.OK || !ModAssets.ImportCustomCrosshair(dlg.FileName))
                {
                    RefreshMods();
                    return;
                }
            }
            settings.CrosshairPreset = pick;
            settings.Save();
            RefreshMods();
        }

        void OnSoundPicked(DarkCombo combo, string kind)
        {
            string mode = SoundModes[Math.Clamp(combo.SelectedIndex, 0, SoundModes.Length - 1)];
            if (mode == "custom")
            {
                using var dlg = new OpenFileDialog { Filter = "MP3 sound (*.mp3)|*.mp3", Title = L.T("Pick a sound") };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    RefreshMods();
                    return;
                }
                try
                {
                    var head = new byte[3];
                    using (var fs = File.OpenRead(dlg.FileName)) fs.ReadExactly(head);
                    bool mp3 = (head[0] == 'I' && head[1] == 'D' && head[2] == '3') || (head[0] == 0xFF && (head[1] & 0xE0) == 0xE0);
                    if (!mp3)
                    {
                        Ask("That isn't an MP3 file", "These Roblox sounds are .mp3 files. Convert your sound to .mp3 first (any free online converter works).", "OK");
                        RefreshMods();
                        return;
                    }
                    Directory.CreateDirectory(ModAssets.SoundsDir);
                    File.Copy(dlg.FileName, Path.Combine(ModAssets.SoundsDir, kind + ".mp3"), true);
                }
                catch (Exception ex)
                {
                    Ask("Couldn't use that sound", ex.Message, "OK");
                    RefreshMods();
                    return;
                }
            }
            if (kind == "footsteps") settings.FootstepSound = mode; else settings.JumpSound = mode;
            settings.Save();
            SetStatus("Sound set. It applies next time you launch Roblox.", null, StatusKind.Idle);
            RefreshMods();
        }

        static void ShowPreview(PictureBox box, string? file)
        {
            var old = box.Image;
            box.Image = null;
            old?.Dispose();
            if (file == null || !File.Exists(file)) return;
            try
            {
                using var fs = File.OpenRead(file);
                using var img = Image.FromStream(fs);
                box.Image = new Bitmap(img);
            }
            catch { }
        }

        // SETUPS: saved combos of mods + graphics, switched in one click
        SavedSetup CaptureSetup() => new()
        {
            Preset = settings.Preset,
            Flags = new Dictionary<string, string>(settings.Flags),
            CursorPreset = settings.CursorPreset,
            CrosshairPreset = settings.CrosshairPreset,
            DeathSoundFile = settings.DeathSoundFile,
            FootstepSound = settings.FootstepSound,
            JumpSound = settings.JumpSound,
            SoundPack = settings.SoundPack,
            CustomSky = settings.CustomSky,
            FpsCap = settings.FpsCap,
            Priority = settings.Priority,
        };

        void SaveSetup()
        {
            string? name = AskText("Save setup", "Name this setup. Saving with an existing name replaces it.", "BedWars", "Save");
            if (name == null) return;
            if (name.Length > 40) name = name[..40];
            settings.Setups[name] = CaptureSetup();
            settings.LastSetup = name;
            settings.Save();
            RefreshMods();
            SetStatus(L.F("Saved {0}.", name), null, StatusKind.Idle);
        }

        void UseSetup(string name)
        {
            if (!settings.Setups.TryGetValue(name, out var s)) return;
            s.ApplyTo(settings);
            settings.LastSetup = name;
            settings.Save();
            fpsCombo.SelectedIndex = Math.Max(0, Array.IndexOf(FpsCaps, settings.FpsCap));
            priorityCombo.SelectedIndex = Math.Clamp(settings.Priority, 0, 2);
            RefreshGraphicsControls();
            RefreshFlagSummary();
            RefreshMods();
            RefreshHome();
            SetStatus(L.F("Switched to {0}. It applies next time you launch Roblox.", name), null, StatusKind.Idle);
        }

        void DeleteSetup()
        {
            if (setupCombo.SelectedIndex <= 0 || setupCombo.SelectedItem?.ToString() is not string name)
            {
                Ask("Pick a setup first", "Choose the setup you want to delete from the list.", "OK");
                return;
            }
            if (Ask("Delete setup?", L.F("Delete {0}? Your current settings stay the same.", name), "Delete", "Cancel") != 0) return;
            settings.Setups.Remove(name);
            foreach (var k in settings.GameSetups.Where(kv => kv.Value == name).Select(kv => kv.Key).ToList())
                settings.GameSetups.Remove(k);
            if (settings.LastSetup == name) settings.LastSetup = null;
            settings.Save();
            RefreshMods();
        }

        void LinkGameToSetup()
        {
            if (setupCombo.SelectedIndex <= 0 || setupCombo.SelectedItem?.ToString() is not string name)
            {
                Ask("Pick a setup first", "Choose a saved setup in the list above, then link it to a game.", "OK");
                return;
            }
            string? text = AskText(L.F("Link {0} to a game", name),
                "Paste the game link or place ID. GuideStrap switches to this setup whenever you launch that game.",
                "https://www.roblox.com/games/...", "Link");
            if (text == null) return;
            long? id = RobloxLauncher.ParsePlaceId(text);
            if (id == null)
            {
                Ask("That doesn't look like a game link", "Paste a link like roblox.com/games/123456, or just the number.", "OK");
                return;
            }
            settings.GameSetups[id.Value.ToString()] = name;
            settings.Save();
            RefreshMods();
            SetStatus(L.F("Linked {0}.", name), null, StatusKind.Idle);
        }

        string? AskText(string title, string message, string placeholder, string ok)
        {
            using var d = new ThemedDialog(title, message, new[] { ok, "Cancel" }, null, placeholder);
            d.ShowDialog(this);
            return d.Choice == 0 && d.Input.Length > 0 ? d.Input : null;
        }

        void ChooseLauncherBackground()
        {
            using var dlg = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp", Title = L.T("Pick a picture") };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                using (var fs = File.OpenRead(dlg.FileName))
                using (var test = Image.FromStream(fs))
                {
                    if (test.Width < 16 || test.Height < 16) throw new Exception(L.T("That picture is too small."));
                }
                Directory.CreateDirectory(Paths.Base);
                foreach (var old in Directory.GetFiles(Paths.Base, "launcher-background.*"))
                    try { File.Delete(old); } catch { }
                string dest = Path.Combine(Paths.Base, "launcher-background" + Path.GetExtension(dlg.FileName).ToLowerInvariant());
                File.Copy(dlg.FileName, dest, true);
                settings.LauncherBackground = dest;
                settings.Save();
                SetStatus("Background set. It shows on the start screen.", null, StatusKind.Idle);
            }
            catch (Exception ex) { Ask("Couldn't use that picture", ex.Message, "OK"); }
        }

        void ChooseSky()
        {
            using var dlg = new FolderBrowserDialog { Description = L.T("Pick a folder with sky .tex files (from a Roblox sky mod pack)"), UseDescriptionForTitle = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var files = Directory.GetFiles(dlg.SelectedPath, "*.tex", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                Ask("No sky files found", "Sky packs are .tex files like sky512_up.tex. Pick the folder that contains them.", "OK");
                return;
            }
            try
            {
                if (Directory.Exists(Paths.Sky)) Directory.Delete(Paths.Sky, true);
                Directory.CreateDirectory(Paths.Sky);
                foreach (var f in files) File.Copy(f, Path.Combine(Paths.Sky, Path.GetFileName(f)), true);
                settings.CustomSky = true;
                settings.Save();
            }
            catch (Exception ex) { Ask("Couldn't use that sky", ex.Message, "OK"); }
            RefreshMods();
        }

        ToggleSwitch Tg(ToggleSwitch t, bool value, Action<bool> changed)
        {
            t.Size = new Size(D(40), D(20));
            t.Checked = value;
            t.CheckedChanged += (_, _) => changed(t.Checked);
            return t;
        }

        // FRIENDS
        void BuildFriendsPage()
        {
            friendsPage = NewPage("Friends", "Friends", "See who's online and join them.");
            var refresh = Secondary("Refresh", "\uE72C", D(110), D(30));
            refresh.Click += async (_, _) => await RefreshFriendsAsync();
            Add(friendsPage, ModCard("Online friends", friendsStatus, refresh));
            Add(friendsPage, SettingCard("Friend alerts", "A small popup when a friend starts playing a game.",
                Tg(friendAlertToggle, settings.FriendAlerts, v => { settings.FriendAlerts = v; settings.Save(); })), 8);
            friendsStatus.Text = L.T("Open this page to load your friends.");
        }

        async Task RefreshFriendsAsync()
        {
            if (friendsBusy) return;
            friendsBusy = true;
            try
            {
                foreach (var c in friendCards) c.Dispose();
                friendCards.Clear();
                var acc = ActiveAccount;
                if (acc == null)
                {
                    friendsStatus.Text = L.T("Pick a saved account in Accounts to see your friends.");
                    return;
                }
                friendsStatus.Text = L.T("Loading...");
                var ids = await RobloxApi.GetFriendIdsAsync(acc.UserId, acc.Cookie);
                var presence = ids.Count == 0 ? new List<FriendPresence>() : await RobloxApi.GetPresenceAsync(acc.Cookie, ids);
                var online = presence.Where(x => x.Type > 0).OrderByDescending(x => x.Type == 2).ThenBy(x => x.UserId).ToList();
                await RobloxApi.FillNamesAsync(online.Select(x => x.UserId), friendNames);
                if (IsDisposed) return;
                friendsLoaded = DateTime.Now;
                friendsStatus.Text = online.Count == 0
                    ? L.F("None of your {0} friends are online right now.", ids.Count)
                    : L.F("{0} of {1} friends online.", online.Count, ids.Count);
                friendsPage.SuspendLayout();
                foreach (var f in online.Take(60))
                {
                    var card = FriendCard(f);
                    friendCards.Add(card);
                    Add(friendsPage, card, 6);
                }
                friendsPage.ResumeLayout();
            }
            catch (Exception ex)
            {
                if (!IsDisposed) friendsStatus.Text = L.F("Couldn't load friends. {0}", ex.Message);
            }
            finally { friendsBusy = false; }
        }

        Card FriendCard(FriendPresence f)
        {
            var card = new Card { Size = new Size(CW, D(58)) };
            string name = friendNames.GetValueOrDefault(f.UserId, f.UserId.ToString());
            string where = f.Type switch
            {
                2 => L.F("Playing {0}", string.IsNullOrWhiteSpace(f.Location) ? L.T("a game") : f.Location!),
                3 => L.T("In Roblox Studio"),
                _ => L.T("Online"),
            };
            var title = At(Lbl(name, 9.75f, semibold: true), D(16), D(9));
            title.UseMnemonic = false;
            var sub = Lbl(where, 8.5f, f.Type == 2 ? Theme.AccentLight : Theme.SubText);
            sub.AutoSize = false;
            sub.AutoEllipsis = true;
            sub.Size = new Size(CW - D(160), D(18));
            sub.Location = new Point(D(16), D(31));
            card.Controls.Add(title);
            card.Controls.Add(sub);
            long? place = f.PlaceId ?? f.RootPlaceId;
            if (f.Type == 2 && place != null)
            {
                var join = Secondary(f.GameId != null ? "Join" : "Join game", "\uE768", D(110), D(30));
                join.Location = new Point(CW - D(126), D(14));
                join.Click += (_, _) =>
                {
                    var ctx = GuideStrapContext.Current;
                    if (ctx == null) return;
                    ctx.Open(new LoadingForm(ctx, settings, accounts, null, place.Value.ToString(), f.GameId));
                    if (settings.CloseAfterLaunch) Close();
                };
                card.Controls.Add(join);
            }
            return card;
        }

        // DISCORD
        void BuildDiscordPage()
        {
            var page = NewPage("Discord", "Discord", "What your Discord profile shows while you use GuideStrap.");
            Add(page, SettingCard("Discord status", "Shows GuideStrap and what you're doing on your Discord profile.",
                Tg(discordToggle, settings.DiscordPresence, v =>
                {
                    settings.DiscordPresence = v;
                    settings.Save();
                    Presence.Configure(settings);
                    Presence.Enable(v);
                    if (v) Presence.Screen("Editing settings", "Discord");
                })), 8);

            Section(page, "What to show");
            Add(page, SettingCard("Show the game you're in", "The game's name, picture and a View game button.",
                Tg(discordGameToggle, settings.DiscordShowGame, v => { settings.DiscordShowGame = v; settings.Save(); Presence.Configure(settings); })));
            Add(page, SettingCard("Show the account you're using", "Adds \"Playing as\" with your display name.",
                Tg(discordAccountToggle, settings.DiscordShowAccount, v => { settings.DiscordShowAccount = v; settings.Save(); Presence.Configure(settings); })));
            Add(page, SettingCard("Let friends join you", "Adds a Join server button that puts friends in your server.",
                Tg(discordJoinToggle, settings.DiscordAllowJoin, v => { settings.DiscordAllowJoin = v; settings.Save(); Presence.Configure(settings); })));

            var note = Lbl("Discord has to be open on this PC. Streamer mode (in Settings) hides your account, server location and the Join button.", 8.5f, Theme.SubText);
            note.AutoSize = false;
            note.Size = new Size(CW, D(34));
            note.Margin = new Padding(D(2), D(6), 0, 0);
            page.Controls.Add(note);
        }

        void BuildSettingsPage()
        {
            var page = NewPage("Settings", "Settings", "Launcher options.");

            ToggleSwitch T(ToggleSwitch t, bool value, Action<bool> changed)
            {
                t.Size = new Size(D(40), D(20));
                t.Checked = value;
                t.CheckedChanged += (_, _) => changed(t.Checked);
                return t;
            }

            Section(page, "Behavior");
            Add(page, SettingCard("Use my selected account for website joins", "Play buttons on roblox.com join as the account picked here.",
                T(websiteAccountToggle, settings.UseAccountForWebsite, v => { settings.UseAccountForWebsite = v; settings.Save(); })));
            Add(page, SettingCard("Close this window when launching", "Only the loading screen stays open while Roblox starts.",
                T(closeAfterToggle, settings.CloseAfterLaunch, v => { settings.CloseAfterLaunch = v; settings.Save(); })));
            Add(page, SettingCard("Stay in the tray while Roblox is open", "Needed for Discord status, server location and recent games.",
                T(backgroundToggle, settings.RunInBackground, v => { settings.RunInBackground = v; settings.Save(); })));
            var crashTest = Secondary("Preview", "\uE890", D(100), D(30));
            crashTest.Click += (_, _) =>
            {
                using var f = new CrashForm(new CrashInfo { Code = 0xC0000005 });
                f.ShowDialog(this);
            };
            var crashRight = new Panel { Size = new Size(D(100) + D(12) + D(40), D(30)), BackColor = Theme.Card };
            crashTest.Location = new Point(0, 0);
            T(crashToggle, settings.CrashHelper, v => { settings.CrashHelper = v; settings.Save(); });
            crashToggle.Location = new Point(D(112), (D(30) - crashToggle.Height) / 2);
            crashRight.Controls.Add(crashTest);
            crashRight.Controls.Add(crashToggle);
            Add(page, SettingCard("Crash helper", "If Roblox crashes, shows what went wrong and how to fix it.", crashRight));
            Add(page, SettingCard("Start with Windows", "Opens quietly in the tray when your PC starts, so friend alerts work.",
                T(startupToggle, Setup.StartsWithWindows, v =>
                {
                    if (v && !File.Exists(Setup.InstallExe))
                    {
                        Ask("Install GuideStrap first", "This only works for the installed GuideStrap, not a copy you're running from somewhere else.", "OK");
                        startupToggle.Checked = false;
                        return;
                    }
                    Setup.SetStartup(v);
                })));
            Add(page, SettingCard("Streamer mode", "Hides account names and server location in GuideStrap and on Discord.",
                T(streamerToggle, settings.StreamerMode, v =>
                {
                    settings.StreamerMode = v;
                    settings.Save();
                    RefreshHome();
                    Presence.Configure(settings);
                })));
            Add(page, SettingCard("Session summary", "After you close Roblox, a small popup shows how long you played.",
                T(summaryToggle, settings.SessionSummary, v => { settings.SessionSummary = v; settings.Save(); })));
            Add(page, SettingCard("Show server location", "Pops up where the server you joined is, like \"Dallas, Texas, US\".",
                T(locationToggle, settings.ShowServerLocation, v => { settings.ShowServerLocation = v; settings.Save(); })));
            Add(page, SettingCard("Allow multiple Roblox windows", "Run more than one account at once. Roblox sometimes blocks this.",
                T(multiToggle, settings.MultiInstance, v => { settings.MultiInstance = v; settings.Save(); })));
            Add(page, SettingCard("Open Roblox links with GuideStrap", "Off gives Play buttons on roblox.com back to normal Roblox.",
                T(linksToggle, LinkHandler.IsOurs(), v =>
                {
                    try
                    {
                        if (v) LinkHandler.Register(settings); else LinkHandler.Restore(settings);
                        settings.Save();
                        SetStatus(v ? "Play buttons on roblox.com now open GuideStrap." : "Play buttons on roblox.com open normal Roblox again.", null, StatusKind.Idle);
                    }
                    catch (Exception ex) { Ask("Couldn't change that", ex.Message, "OK"); }
                })));

            Section(page, "Appearance");
            var picker = new AccentPicker { Selected = settings.Accent };
            picker.Size = new Size(D(30) * Theme.Accents.Length, D(30));
            picker.Picked += name =>
            {
                settings.Accent = name;
                settings.Save();
                if (Ask("Restart to apply", "The new color shows after GuideStrap restarts.", "Restart now", "Later") == 0)
                    Application.Restart();
            };
            var themeCombo = new DarkCombo { ItemHeight = D(22), Width = D(170) };
            foreach (var t in Theme.Themes) themeCombo.Items.Add(L.T(t));
            themeCombo.SelectedIndex = Math.Max(0, Array.IndexOf(Theme.Themes, settings.ThemeName));
            themeCombo.SelectedIndexChanged += (_, _) =>
            {
                string pick = Theme.Themes[themeCombo.SelectedIndex];
                if (pick == settings.ThemeName) return;
                settings.ThemeName = pick;
                settings.Save();
                if (Ask("Restart to apply", "The new theme shows after GuideStrap restarts.", "Restart now", "Later") == 0)
                    Application.Restart();
            };
            Add(page, SettingCard("Theme", "Purple, or plain black with no color.", themeCombo));
            Add(page, SettingCard("Accent color", "Used by the Purple theme for buttons, switches and highlights.", picker));

            var bgButtons = ButtonPair(
                Secondary("Choose image", "\uE8B9", D(130), D(30)), ChooseLauncherBackground,
                Secondary("Reset", "\uE72C", D(80), D(30)), () =>
                {
                    settings.LauncherBackground = null;
                    settings.Save();
                    SetStatus("The start screen is back to plain.", null, StatusKind.Idle);
                });
            Add(page, SettingCard("Start screen background", "Your own picture behind the start screen. It's darkened so text stays readable.", bgButtons));

            var langCombo = new DarkCombo { ItemHeight = D(22), Width = D(170) };
            foreach (var (_, native) in L.Languages) langCombo.Items.Add(native);
            langCombo.SelectedIndex = Math.Max(0, Array.FindIndex(L.Languages, x => x.Code == L.Lang));
            langCombo.SelectedIndexChanged += (_, _) =>
            {
                string code = L.Languages[langCombo.SelectedIndex].Code;
                settings.Language = code;
                settings.Save();
                if (code != L.Lang && Ask("Restart to apply", "The new language shows after GuideStrap restarts.", "Restart now", "Later") == 0)
                    Application.Restart();
            };
            Add(page, SettingCard("Language", "The language GuideStrap uses.", langCombo));

            Section(page, "Profiles");
            var profileButtons = ButtonPair(
                Secondary("Export", "\uE74E", D(96), D(30)), ExportProfile,
                Secondary("Import", "\uE8E5", D(96), D(30)), ImportProfile);
            Add(page, SettingCard("Settings profile", "Share your graphics, FastFlags, cursor and color as one file.", profileButtons));

            Section(page, "Tools");
            var folder = Secondary("Open folder", "\uE8B7", D(130), D(30));
            folder.Click += (_, _) =>
            {
                Directory.CreateDirectory(Paths.Base);
                Process.Start(new ProcessStartInfo(Paths.Base) { UseShellExecute = true });
            };
            Add(page, SettingCard("GuideStrap folder", "Settings, mods and the downloaded Roblox live here.", folder));

            var clean = Secondary("Clear cache", "\uE74D", D(130), D(30));
            clean.Click += async (_, _) =>
            {
                clean.Enabled = false;
                long freed = await Task.Run(() => Cleaner.ClearCache());
                clean.Enabled = true;
                SetStatus($"Cleared {freed / 1024.0 / 1024.0:0.#} MB of old Roblox cache and logs.", null, StatusKind.Idle);
            };
            Add(page, SettingCard("Clear cache", "Deletes Roblox's temporary files, old logs and cached pictures.", clean));

            var reinstall = Secondary("Repair", "\uE90F", D(120), D(30));
            reinstall.HoverColor = Theme.Danger;
            reinstall.Click += (_, _) =>
            {
                if (Ask("Repair Roblox?", "This deletes GuideStrap's copy of Roblox. It downloads a fresh one next time you launch.", "Repair", "Cancel") != 0) return;
                try
                {
                    if (Directory.Exists(Paths.Versions)) Directory.Delete(Paths.Versions, true);
                    SetStatus("Roblox will re-download next time you launch.", 0, StatusKind.Idle);
                }
                catch (Exception ex) { Ask("Close Roblox first", ex.Message, "OK"); }
            };
            Add(page, SettingCard("Repair Roblox", "Fixes crashes or a broken install by downloading Roblox again.", reinstall));

            var uninstall = Secondary("Uninstall", "\uE74D", D(120), D(30));
            uninstall.HoverColor = Theme.Danger;
            uninstall.Click += (_, _) =>
            {
                if (!File.Exists(Setup.InstallExe))
                {
                    Ask("GuideStrap isn't installed", "This copy isn't installed, so there's nothing to remove.", "OK");
                    return;
                }
                if (Setup.Uninstall(this))
                {
                    Presence.Shutdown();
                    Application.Exit();
                }
            };
            Add(page, SettingCard("Uninstall GuideStrap", "Removes GuideStrap, its settings and its copy of Roblox. Normal Roblox stays.", uninstall));

            var update = Secondary("Check now", "\uE895", D(120), D(30));
            update.Click += async (_, _) => await CheckForUpdatesAsync(manual: true);
            Add(page, SettingCard("Updates", $"You're on GuideStrap {AppInfo.Version}.", update), 0);
        }

        async Task CheckForUpdatesAsync(bool manual)
        {
            if (!Updater.Configured)
            {
                if (manual) Ask("Updates aren't set up yet", "Once GuideStrap is published on GitHub, set UpdateRepo in the code and it will update itself.", "OK");
                return;
            }
            var found = await Updater.CheckAsync();
            if (found == null)
            {
                if (manual) SetStatus("GuideStrap is up to date.", null, StatusKind.Idle);
                return;
            }
            if (Ask("Update available", $"GuideStrap {found.Value.Version} is out. Update now?", "Update", "Later") == 0)
            {
                SetStatus("Downloading the update...", null, StatusKind.Busy);
                try { await Updater.InstallAsync(found.Value.Url); }
                catch (Exception ex) { Ask("Update failed", ex.Message, "OK"); }
            }
        }

        void ExportProfile()
        {
            using var dlg = new SaveFileDialog { Filter = "GuideStrap profile (*.gsprofile)|*.gsprofile", FileName = "GuideStrap profile.gsprofile" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var p = new Profile
            {
                Preset = settings.Preset,
                Flags = new Dictionary<string, string>(settings.Flags),
                CursorPreset = settings.CursorPreset == "custom" ? "default" : settings.CursorPreset,
                Accent = settings.Accent,
            };
            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
            SetStatus("Profile saved.", null, StatusKind.Idle);
        }

        void ImportProfile()
        {
            using var dlg = new OpenFileDialog { Filter = "GuideStrap profile (*.gsprofile)|*.gsprofile" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            Profile? p;
            try { p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(dlg.FileName)); }
            catch { p = null; }
            if (p == null) { Ask("That profile can't be read", "The file is damaged or isn't a GuideStrap profile.", "OK"); return; }
            if (Ask("Use this profile?", "Your graphics, FastFlags, cursor and accent color will be replaced.", "Use profile", "Cancel") != 0) return;

            settings.Preset = p.Preset;
            settings.Flags = p.Flags.Where(kv => FF.IsAllowed(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            settings.CursorPreset = p.CursorPreset is "guidestrap" or "dot" or "cross" or "circle" or "classic" ? p.CursorPreset : "default";
            bool colorChanged = p.Accent != settings.Accent && Theme.Accents.Any(a => a.Name == p.Accent);
            if (colorChanged) settings.Accent = p.Accent;
            settings.Save();
            RefreshGraphicsControls();
            RefreshFlagSummary();
            RefreshMods();
            RefreshHome();
            SetStatus("Profile loaded.", null, StatusKind.Idle);
            if (colorChanged && Ask("Restart to apply", "The profile's color shows after GuideStrap restarts.", "Restart now", "Later") == 0)
                Application.Restart();
        }

        void BuildAboutPage()
        {
            var page = NewPage("About", "About", "What GuideStrap is.");

            var hero = new Card { Size = new Size(CW, D(96)) };
            var logo = new LogoMark { Size = new Size(D(64), D(64)), Location = new Point(D(16), D(16)) };
            var name = At(Lbl("GuideStrap", 16f, semibold: true), D(92), D(18));
            var ver = At(Lbl($"Version {AppInfo.Version}  ·  by Guide", 9f, Theme.SubText), D(94), D(54));
            hero.Controls.AddRange(new Control[] { logo, name, ver });
            Add(page, hero, 8);

            var body = new Card { Size = new Size(CW, D(10)) };
            var text = Lbl(AppInfo.AboutText, 9.25f, Theme.Text);
            text.MaximumSize = new Size(CW - D(32), 0);
            text.Location = new Point(D(16), D(14));
            body.Controls.Add(text);
            body.Height = text.GetPreferredSize(new Size(CW - D(32), 0)).Height + D(28);
            Add(page, body, 0);
        }
    }

    // FASTFLAG EDITOR (pop-up window)
    public class FlagEditorForm : ThemedForm
    {
        readonly Dictionary<string, string> flags;
        readonly Action changed;
        readonly FlowLayoutPanel list = new();
        readonly DarkCombo picker = new();
        readonly TextBox valueBox = new(), importBox = new(), filterBox = new();
        readonly Label pickDesc = new(), countLabel = new(), resultLabel = new();
        readonly CollapsePanel addPanel, importPanel;
        readonly int rowWidth;

        public FlagEditorForm(Dictionary<string, string> flags, Action changed)
        {
            this.flags = flags;
            this.changed = changed;
            Text = "FastFlag editor";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CloseOnEscape = true;
            ClientSize = new Size(D(760), D(580));
            int W = ClientSize.Width;
            int innerW = W - D(48);
            rowWidth = innerW - D(22);

            var bar = BuildTitleBar("FastFlag editor", minimize: false);
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(D(24), D(2), D(24), D(6)) };
            var footer = new Panel { Dock = DockStyle.Bottom, Height = D(54), BackColor = Theme.Bar };
            footer.Paint += (_, e) => { using var p = new Pen(Theme.Border); e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(bar);

            // --- header + toolbar ---
            var header = new Panel { Dock = DockStyle.Top, Height = D(102), BackColor = Theme.Bg };
            header.Controls.Add(At(Lbl("FastFlag editor", 14f, semibold: true), 0, 0));
            header.Controls.Add(At(Lbl("Only flags on Roblox's allowlist are kept. Adding one you already have asks before replacing it.", 8.75f, Theme.SubText), D(2), D(30)));

            var addBtn = Primary("Add flag", "\uE710", D(112), D(30));
            addBtn.Location = new Point(0, D(62));
            var importBtn = Secondary("Import JSON", "\uE896", D(132), D(30));
            importBtn.Location = new Point(D(120), D(62));
            var copyBtn = Secondary("Copy JSON", "\uE8C8", D(120), D(30));
            copyBtn.Location = new Point(D(260), D(62));
            var clearBtn = Secondary("Clear all", "\uE74D", D(104), D(30));
            clearBtn.HoverColor = Theme.Danger;
            clearBtn.Location = new Point(D(388), D(62));
            var filter = InputBox(filterBox, "Filter", D(170), "\uE721");
            filter.Location = new Point(innerW - D(170), D(62));
            header.Controls.AddRange(new Control[] { addBtn, importBtn, copyBtn, clearBtn, filter });

            // --- add panel ---
            addPanel = new CollapsePanel(innerW, D(100), D(8));
            var ac = addPanel.Inner;
            foreach (var a in FF.Allowed) picker.Items.Add(a.Name);
            picker.ItemHeight = D(22);
            picker.Width = D(360);
            picker.Location = new Point(D(16), D(16));
            picker.SelectedIndex = 0;
            var valueWrap = InputBox(valueBox, "Value", D(160));
            valueWrap.Location = new Point(D(388), D(14));
            var addConfirm = Primary("Add", "\uE710", D(96), D(30));
            addConfirm.Location = new Point(innerW - D(112), D(14));
            pickDesc.AutoSize = true;
            pickDesc.ForeColor = Theme.SubText;
            pickDesc.BackColor = Color.Transparent;
            pickDesc.Font = new Font("Segoe UI", 8.75f);
            pickDesc.Location = new Point(D(16), D(52));
            pickDesc.Text = L.T(FF.Describe(picker.SelectedItem as string));
            var addHint = At(Lbl("Tip: press Enter in the value box to add.", 8f, Theme.SubText), D(16), D(73));
            ac.Controls.AddRange(new Control[] { picker, valueWrap, addConfirm, pickDesc, addHint });

            // --- import panel ---
            importPanel = new CollapsePanel(innerW, D(200), D(8));
            var ic = importPanel.Inner;
            ic.Controls.Add(At(Lbl("Paste FastFlags JSON", 9.75f), D(16), D(10)));
            var importWrap = new Card
            {
                BackColor = Theme.Input, Radius = 6, Padding = new Padding(D(12), D(8), D(4), D(8)),
                Location = new Point(D(16), D(34)), Size = new Size(innerW - D(32), D(114)),
            };
            importBox.Multiline = true;
            importBox.AcceptsReturn = true;
            importBox.ScrollBars = ScrollBars.Vertical;
            importBox.BorderStyle = BorderStyle.None;
            importBox.BackColor = Theme.Input;
            importBox.ForeColor = Theme.Text;
            importBox.Font = new Font("Consolas", 9f);
            importBox.Dock = DockStyle.Fill;
            importBox.PlaceholderText = "{\n  \"DFIntTextureQualityOverride\": \"0\",\n  \"FFlagDebugSkyGray\": \"True\"\n}";
            importWrap.Controls.Add(importBox);
            var importConfirm = Primary("Import", "\uE896", D(104), D(30));
            importConfirm.Location = new Point(innerW - D(120), D(158));
            ic.Controls.Add(importWrap);
            ic.Controls.Add(At(Lbl("Flags that aren't on Roblox's allowlist are skipped.", 8f, Theme.SubText), D(16), D(164)));
            ic.Controls.Add(importConfirm);

            // --- list ---
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.BackColor = Theme.Bg;

            body.Controls.Add(list);
            body.Controls.Add(importPanel);
            body.Controls.Add(addPanel);
            body.Controls.Add(header);

            // --- footer ---
            countLabel.AutoSize = true;
            countLabel.ForeColor = Theme.Text;
            countLabel.BackColor = Color.Transparent;
            countLabel.Font = new Font("Segoe UI Semibold", 9.5f);
            countLabel.Location = new Point(D(24), D(17));
            resultLabel.AutoSize = false;
            resultLabel.Size = new Size(W - D(280), D(20));
            resultLabel.ForeColor = Theme.Gold;
            resultLabel.BackColor = Color.Transparent;
            resultLabel.Font = new Font("Segoe UI", 9f);
            resultLabel.Location = new Point(D(110), D(18));
            resultLabel.AutoEllipsis = true;
            var done = Primary("Done", "", D(96), D(30));
            done.Location = new Point(W - D(120), D(12));
            done.Click += (_, _) => Close();
            footer.Controls.AddRange(new Control[] { countLabel, resultLabel, done });

            // --- events ---
            addBtn.Click += (_, _) =>
            {
                importPanel.Collapse();
                addPanel.Toggle();
                if (addPanel.IsOpen) valueBox.Focus();
            };
            importBtn.Click += (_, _) =>
            {
                addPanel.Collapse();
                importPanel.Toggle();
                if (importPanel.IsOpen) importBox.Focus();
            };
            copyBtn.Click += (_, _) =>
            {
                if (flags.Count == 0) { ShowResult("Nothing to copy yet."); return; }
                Clipboard.SetText(JsonSerializer.Serialize(flags.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value),
                    new JsonSerializerOptions { WriteIndented = true }));
                ShowResult($"Copied {flags.Count} flags to your clipboard.");
            };
            clearBtn.Click += (_, _) =>
            {
                if (flags.Count == 0) return;
                if (Ask("Clear every flag?", $"This removes all {flags.Count} flags (and resets Graphics to Balanced).", "Clear all", "Cancel") != 0) return;
                flags.Clear();
                changed();
                RefreshList();
                ShowResult("All flags cleared.");
            };
            filterBox.TextChanged += (_, _) => RefreshList();
            picker.SelectedIndexChanged += (_, _) => pickDesc.Text = L.T(FF.Describe(picker.SelectedItem as string));
            addConfirm.Click += (_, _) => AddFromPicker();
            valueBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddFromPicker(); } };
            importConfirm.Click += (_, _) => ImportJson();

            RefreshList();
            Activated += (_, _) => Presence.Screen("Editing settings", "FastFlag editor");
        }

        void ShowResult(string text)
        {
            resultLabel.Text = L.T(text);
            Anim.Run((resultLabel, "flash"), 0f, 1f, 500, v => resultLabel.ForeColor = Theme.Mix(Theme.Bar, Theme.Gold, v));
        }

        void AddFromPicker()
        {
            string? name = picker.SelectedItem as string;
            string value = valueBox.Text.Trim();
            if (name == null) return;
            if (value.Length == 0) { valueBox.Focus(); ShowResult("Type a value first."); return; }
            var r = Merge(new List<(string, string)> { (name, value) });
            valueBox.Clear();
            ShowResult(r.Summary(0));
        }

        void ImportJson()
        {
            string text = importBox.Text.Trim();
            if (text.Length == 0) { ShowResult("Paste some JSON first."); return; }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
            }
            catch (JsonException)
            {
                Ask("That isn't valid JSON", "Make sure it looks like { \"FlagName\": \"Value\", ... } with quotes around names.", "OK");
                return;
            }
            if (root is not JsonObject obj)
            {
                Ask("That isn't a flag list", "The JSON should be an object like { \"FlagName\": \"Value\" }.", "OK");
                return;
            }

            var incoming = new List<(string, string)>();
            int skipped = 0;
            foreach (var (key, node) in obj)
            {
                if (!FF.IsAllowed(key)) { skipped++; continue; }
                string val = node switch
                {
                    JsonValue jv when jv.TryGetValue<bool>(out var b) => b ? "True" : "False",
                    JsonValue jv when jv.TryGetValue<string>(out var s) => s,
                    null => "",
                    _ => node.ToJsonString(),
                };
                if (val.Trim().Length == 0) { skipped++; continue; }
                incoming.Add((key, val.Trim()));
            }

            var r = Merge(incoming);
            ShowResult(r.Summary(skipped));
            if (r.Added + r.Updated > 0 || skipped > 0) importBox.Clear();
            if (r.Added + r.Updated > 0) importPanel.Collapse();
        }

        record MergeResult(int Added, int Updated, int Kept, int Same)
        {
            public string Summary(int skipped)
            {
                var parts = new List<string>();
                if (Added > 0) parts.Add(L.F("Added {0}", Added));
                if (Updated > 0) parts.Add(L.F("Overwrote {0}", Updated));
                if (Kept > 0) parts.Add(L.F("Kept {0} original", Kept));
                if (Same > 0) parts.Add(L.F("{0} already the same", Same));
                if (skipped > 0) parts.Add(L.F("Skipped {0} not on the allowlist", skipped));
                return parts.Count == 0 ? L.T("Nothing changed.") : string.Join("  •  ", parts);
            }
        }

        // Adds flags; if one already exists with a different value, asks: overwrite or keep original.
        MergeResult Merge(List<(string Name, string Value)> incoming)
        {
            int added = 0, updated = 0, kept = 0, same = 0;
            int conflictsLeft = incoming.Count(i => flags.TryGetValue(i.Name, out var cur) && cur != i.Value);
            bool? applyAll = null;
            var touched = new HashSet<string>();

            foreach (var (name, value) in incoming)
            {
                if (!flags.TryGetValue(name, out var current))
                {
                    flags[name] = value;
                    added++;
                    touched.Add(name);
                    continue;
                }
                if (current == value) { same++; continue; }

                bool overwrite;
                if (applyAll.HasValue) overwrite = applyAll.Value;
                else
                {
                    using var dlg = new ThemedDialog(
                        "This flag is already in your list",
                        $"{name}\n\nCurrent value:   {current}\nNew value:   {value}\n\nDo you want to overwrite it with the new value, or keep the original?",
                        new[] { "Overwrite", "Keep original" },
                        conflictsLeft > 1 ? $"Do the same for the other {conflictsLeft - 1} duplicate{(conflictsLeft - 1 == 1 ? "" : "s")}" : null);
                    dlg.ShowDialog(this);
                    overwrite = dlg.Choice == 0;
                    if (dlg.Checked) applyAll = overwrite;
                }
                conflictsLeft--;

                if (overwrite) { flags[name] = value; updated++; touched.Add(name); }
                else kept++;
            }

            if (added + updated > 0) changed();
            RefreshList(touched);
            return new MergeResult(added, updated, kept, same);
        }

        void RefreshList(HashSet<string>? highlight = null)
        {
            list.SuspendLayout();
            foreach (var c in list.Controls.Cast<Control>().ToList()) c.Dispose();

            string f = filterBox.Text.Trim();
            var items = flags.Where(kv => f.Length == 0 || kv.Key.Contains(f, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(kv => kv.Key).ToList();

            if (items.Count == 0)
            {
                var empty = Lbl(flags.Count == 0 ? "No flags yet. Press \"Add flag\" or \"Import JSON\" to get started." : "No flags match your filter.", 10f, Theme.SubText);
                empty.Margin = new Padding(D(2), D(12), 0, 0);
                list.Controls.Add(empty);
            }

            foreach (var (name, value) in items)
            {
                string key = name;
                var row = new Card { Size = new Size(rowWidth, D(50)), Margin = new Padding(0, 0, 0, D(6)) };
                row.Controls.Add(At(Lbl(name, 9.25f, semibold: true), D(14), D(7)));
                row.Controls.Add(At(Lbl(FF.Describe(name), 8f, Theme.SubText), D(14), D(27)));

                var tb = new TextBox();
                var wrap = InputBox(tb, "", D(140));
                tb.Text = value;
                wrap.Location = new Point(rowWidth - D(196), D(10));
                void Commit()
                {
                    string v = tb.Text.Trim();
                    if (v.Length == 0 || !flags.ContainsKey(key) || flags[key] == v) return;
                    flags[key] = v;
                    changed();
                    ShowResult($"Updated {key}.");
                }
                tb.Leave += (_, _) => Commit();
                tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Commit(); } };

                var del = Secondary("", "\uE74D", D(36), D(30));
                del.HoverColor = Theme.Danger;
                del.Location = new Point(rowWidth - D(48), D(10));
                del.Click += (_, _) => Later(() =>
                {
                    flags.Remove(key);
                    changed();
                    RefreshList();
                    ShowResult($"Removed {key}.");
                });

                row.Controls.Add(wrap);
                row.Controls.Add(del);
                list.Controls.Add(row);

                if (highlight != null && highlight.Contains(key))
                    Anim.Run((row, "glow"), 1f, 0f, 1600, v => { row.BorderColor = Theme.Mix(Theme.Border, Theme.Gold, v); row.Invalidate(); });
            }

            countLabel.Text = L.T($"{flags.Count} flag{(flags.Count == 1 ? "" : "s")}");
            list.ResumeLayout();
        }
    }

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
            var msgFont = new Font("Segoe UI", 9.5f);
            Size msgSize = TextRenderer.MeasureText(message, msgFont, new Size(w - pad * 2, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            var t = new Label
            {
                Text = title, UseMnemonic = false, AutoSize = false,
                Font = new Font("Segoe UI Semibold", 12f), ForeColor = Theme.Text, BackColor = Color.Transparent,
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
                var cl = At(Lbl(checkText, 9f), pad + D(46), y);
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
                int bw = Math.Max(D(96), TextRenderer.MeasureText(buttons[i], new Font("Segoe UI Semibold", 9f)).Width + D(36));
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

    // ANIMATION ENGINE (one shared ~60fps timer, ease-out curves)
    public static class Anim
    {
        sealed class Tween
        {
            public object Key = null!;
            public float From, To;
            public double Start, Duration;
            public Action<float> Set = null!;
            public Action? Done;
        }

        static readonly List<Tween> tweens = new();
        static readonly List<(object Key, Action<double> Frame)> loops = new();
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static System.Windows.Forms.Timer? timer;
        static double lastTick = -1;
        static bool hiRes;

        public static void Run(object key, float from, float to, int ms, Action<float> set, Action? done = null)
        {
            tweens.RemoveAll(t => Equals(t.Key, key));
            tweens.Add(new Tween { Key = key, From = from, To = to, Start = clock.Elapsed.TotalMilliseconds, Duration = Math.Max(1, ms), Set = set, Done = done });
            Wake();
        }

        // Runs every frame until stopped; gets the milliseconds since the last frame.
        public static void Loop(object key, Action<double> frame)
        {
            loops.RemoveAll(l => Equals(l.Key, key));
            loops.Add((key, frame));
            Wake();
        }

        public static void StopLoop(object key) => loops.RemoveAll(l => Equals(l.Key, key));

        static void Wake()
        {
            if (timer == null)
            {
                timer = new System.Windows.Forms.Timer { Interval = 8 };
                timer.Tick += Tick;
            }
            if (timer.Enabled) return;
            // Windows timers normally tick every ~15.6ms, which looks choppy.
            // Ask for 1ms timer precision only while something is animating.
            if (!hiRes) { NativeMethods.timeBeginPeriod(1); hiRes = true; }
            lastTick = -1;
            timer.Start();
        }

        static void Tick(object? sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalMilliseconds;
            double dt = lastTick < 0 ? 16 : Math.Min(50, now - lastTick);
            lastTick = now;

            foreach (var t in tweens.ToArray())
            {
                if (!tweens.Contains(t)) continue;
                double p = Math.Clamp((now - t.Start) / t.Duration, 0, 1);
                float eased = 1f - (float)Math.Pow(1 - p, 4);   // ease-out quart
                bool ok = true;
                try { t.Set(t.From + (t.To - t.From) * eased); } catch { ok = false; }
                if (!ok || p >= 1)
                {
                    tweens.Remove(t);
                    if (ok) { try { t.Done?.Invoke(); } catch { } }
                }
            }
            foreach (var l in loops.ToArray())
            {
                try { l.Frame(dt); } catch { loops.Remove(l); }
            }

            if (tweens.Count == 0 && loops.Count == 0)
            {
                timer?.Stop();
                lastTick = -1;
                if (hiRes) { NativeMethods.timeEndPeriod(1); hiRes = false; }
            }
        }
    }

    // THEME
    public static class Theme
    {
        public static Color Bg = Color.FromArgb(14, 11, 22);
        public static Color Bar = Color.FromArgb(18, 14, 29);
        public static Color Card = Color.FromArgb(24, 19, 38);
        public static Color Input = Color.FromArgb(16, 12, 26);
        public static Color ButtonBg = Color.FromArgb(35, 28, 55);
        public static Color ButtonHover = Color.FromArgb(47, 38, 73);
        public static Color Border = Color.FromArgb(39, 31, 61);
        public static Color Track = Color.FromArgb(62, 50, 94);
        public static Color Accent = Color.FromArgb(124, 72, 232);
        public static Color AccentHover = Color.FromArgb(143, 96, 242);
        public static Color AccentLight = Color.FromArgb(185, 156, 255);

        public static readonly (string Name, Color Color)[] Accents =
        {
            ("Purple", Color.FromArgb(124, 72, 232)),
            ("Blue", Color.FromArgb(59, 120, 236)),
            ("Rose", Color.FromArgb(220, 70, 128)),
            ("Emerald", Color.FromArgb(22, 163, 116)),
            ("Amber", Color.FromArgb(222, 142, 28)),
        };

        // the logo is always the black version, in every theme
        public static readonly Color LogoDark = Color.FromArgb(24, 24, 24);
        public static readonly Color LogoLight = Color.FromArgb(50, 50, 50);

        public static readonly string[] Themes = { "Purple", "Black" };

        // Call once at startup, before any window is made.
        public static void Apply(string theme, string accent)
        {
            if (theme == "Black")
            {
                // plain dark greys, no tint. Text kept bright for contrast.
                Bg = Color.FromArgb(10, 10, 10);
                Bar = Color.FromArgb(15, 15, 15);
                Card = Color.FromArgb(21, 21, 21);
                Input = Color.FromArgb(13, 13, 13);
                ButtonBg = Color.FromArgb(34, 34, 34);
                ButtonHover = Color.FromArgb(46, 46, 46);
                Border = Color.FromArgb(40, 40, 40);
                Track = Color.FromArgb(52, 52, 52);
                NavHover = Color.FromArgb(19, 19, 19);
                NavSelected = Color.FromArgb(30, 30, 30);
                Text = Color.FromArgb(240, 240, 240);
                SubText = Color.FromArgb(165, 165, 165);
                Accent = Color.FromArgb(100, 100, 100);       // white text on it stays readable
                AccentHover = Color.FromArgb(120, 120, 120);
                AccentLight = Color.FromArgb(215, 215, 215);
                Magenta = Color.FromArgb(120, 120, 120);
            }
            else SetAccent(accent);
        }

        public static void SetAccent(string name)
        {
            var a = Accents.FirstOrDefault(x => x.Name == name);
            if (a.Name == null) a = Accents[0];
            Accent = a.Color;
            AccentHover = Mix(a.Color, Color.White, 0.14f);
            AccentLight = Mix(a.Color, Color.White, 0.42f);
        }
        public static Color Magenta = Color.FromArgb(168, 56, 214);
        public static readonly Color Gold = Color.FromArgb(242, 194, 88);
        public static Color Text = Color.FromArgb(241, 237, 250);
        public static Color SubText = Color.FromArgb(155, 145, 180);
        public static Color NavHover = Color.FromArgb(23, 18, 37);
        public static Color NavSelected = Color.FromArgb(33, 26, 53);
        public static readonly Color Danger = Color.FromArgb(214, 62, 86);
        public static readonly Color Success = Color.FromArgb(92, 214, 140);

        public static readonly string IconFont = DetectIconFont();

        static string DetectIconFont()
        {
            try
            {
                using var fonts = new InstalledFontCollection();
                return fonts.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            }
            catch { return "Segoe MDL2 Assets"; }
        }

        static Icon? appIcon;
        public static Icon AppIcon => appIcon ??= MakeIcon();

        static Icon MakeIcon()
        {
            using var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                DrawLogo(g, new RectangleF(0, 0, 64, 64));
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        // GuideStrap logo: tilted dark tile with white outline strokes and a compass in the middle
        public static void DrawLogo(Graphics g, RectangleF r, float spin = 0f, float scale = 0.80f)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float side = Math.Min(r.Width, r.Height) * scale;
            var state = g.Save();
            g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);
            g.RotateTransform(-18f + spin);

            var tile = new RectangleF(-side / 2, -side / 2, side, side);
            float rad = side * 0.10f;
            using (var path = Round(tile, rad))
            {
                using (var b = new SolidBrush(LogoDark)) g.FillPath(b, path);
                g.SetClip(path, CombineMode.Intersect);
                using (var lb = new SolidBrush(LogoLight))
                    g.FillPolygon(lb, new[] { new PointF(tile.Left, tile.Top), new PointF(tile.Right, tile.Top), new PointF(tile.Left, tile.Bottom) });
                g.ResetClip();
            }

            float sw = Math.Max(1f, side * 0.045f);
            using (var outer = Round(RectangleF.Inflate(tile, -sw / 2, -sw / 2), rad))
            using (var p = new Pen(Color.FromArgb(245, 245, 250), sw))
                g.DrawPath(p, outer);
            if (side >= 24)
            {
                float ins = side * 0.13f;
                using var inner = Round(RectangleF.Inflate(tile, -ins, -ins), rad * 0.5f);
                using var p2 = new Pen(Color.FromArgb(200, 245, 245, 250), Math.Max(1f, sw * 0.5f));
                g.DrawPath(p2, inner);
            }

            // compass (emoji style): gold rim, cream face, red north needle
            float cr = side * 0.25f, face = cr * 0.8f;
            using (var rim = new SolidBrush(Color.FromArgb(232, 164, 52))) g.FillEllipse(rim, -cr, -cr, cr * 2, cr * 2);
            using (var fb = new SolidBrush(Color.FromArgb(250, 244, 230))) g.FillEllipse(fb, -face, -face, face * 2, face * 2);
            if (side >= 40)
            {
                using var tick = new Pen(Color.FromArgb(150, 120, 90), Math.Max(1f, side * 0.014f));
                g.DrawLine(tick, 0, -face * 0.92f, 0, -face * 0.74f);
                g.DrawLine(tick, 0, face * 0.92f, 0, face * 0.74f);
                g.DrawLine(tick, -face * 0.92f, 0, -face * 0.74f, 0);
                g.DrawLine(tick, face * 0.92f, 0, face * 0.74f, 0);
            }
            g.RotateTransform(45f);
            float len = face * 0.82f, w = face * 0.22f;
            using (var red = new SolidBrush(Color.FromArgb(222, 52, 52)))
                g.FillPolygon(red, new[] { new PointF(0, -len), new PointF(w, 0), new PointF(-w, 0) });
            using (var gray = new SolidBrush(Color.FromArgb(190, 196, 206)))
                g.FillPolygon(gray, new[] { new PointF(0, len), new PointF(w, 0), new PointF(-w, 0) });
            float pin = face * 0.12f;
            using (var pb = new SolidBrush(Color.FromArgb(70, 60, 60))) g.FillEllipse(pb, -pin, -pin, pin * 2, pin * 2);
            g.Restore(state);
        }

        // Shared font cache: fewer GDI objects, no per-frame allocations while animating
        static readonly Dictionary<(string, float, FontStyle), Font> fontCache = new();
        public static Font F(string name, float size, FontStyle style = FontStyle.Regular)
        {
            var key = (name, size, style);
            if (!fontCache.TryGetValue(key, out var f)) fontCache[key] = f = new Font(name, size, style);
            return f;
        }

        // Give transparent labels a solid background (much cheaper to paint)
        // Clears a custom control to its parent's color, or to the matching piece of the parent's background picture
        public static void ClearToParent(Control c, Graphics g, Color fallback)
        {
            var parent = c.Parent;
            g.Clear(parent?.BackColor ?? fallback);
            if (parent?.BackgroundImage is Image img && parent.BackgroundImageLayout == ImageLayout.None)
                g.DrawImage(img, new Rectangle(-c.Left, -c.Top, img.Width, img.Height));
        }

        public static void Solidify(Control root)
        {
            foreach (Control c in root.Controls)
            {
                // labels over a background picture stay see-through
                if (c is Label l && l.BackColor == Color.Transparent && root.BackColor.A == 255 && root.BackgroundImage == null) l.BackColor = root.BackColor;
                if (c.HasChildren) Solidify(c);
            }
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public const ControlStyles PaintStyles = ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                                                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor;
    }

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

    // LOGIN WINDOW (the real Roblox login page inside WebView2)
    class LoginForm : Form
    {
        readonly WebView2 web = new();
        readonly System.Windows.Forms.Timer poll = new() { Interval = 1000 };
        readonly string dataDir = Path.Combine(Path.GetTempPath(), "GuideStrapLogin_" + Guid.NewGuid().ToString("N"));
        bool done;
        public string? Cookie { get; private set; }

        public LoginForm()
        {
            Text = "Log in to Roblox - GuideStrap";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(LogicalToDeviceUnits(520), LogicalToDeviceUnits(760));
            BackColor = Theme.Bg;
            web.Dock = DockStyle.Fill;
            Controls.Add(web);

            Load += async (_, _) => await InitAsync();
            poll.Tick += async (_, _) => await CheckCookieAsync();
            FormClosed += (_, _) =>
            {
                poll.Stop();
                web.Dispose();
                try { Directory.Delete(dataDir, true); } catch { }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int dark = 1; NativeMethods.DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); } catch { }
        }

        async Task InitAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
                await web.EnsureCoreWebView2Async(env);
                web.CoreWebView2.Navigate("https://www.roblox.com/login");
                poll.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The login window needs Microsoft Edge WebView2.\n\n" + ex.Message, "GuideStrap");
                Close();
            }
        }

        async Task CheckCookieAsync()
        {
            if (done || web.CoreWebView2 == null) return;
            var cookies = await web.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
            var c = cookies.FirstOrDefault(x => x.Name == ".ROBLOSECURITY");
            if (c == null || string.IsNullOrEmpty(c.Value)) return;
            done = true;
            Cookie = c.Value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // DATA: settings, accounts, paths, encryption
    static class Paths
    {
        public static readonly string Base = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuideStrap");
        public static readonly string Versions = Path.Combine(Base, "Versions");
        public static readonly string Avatars = Path.Combine(Base, "Avatars");
        public static readonly string Icons = Path.Combine(Base, "GameIcons");
        public static readonly string Mods = Path.Combine(Base, "Mods");
        public static readonly string Managed = Path.Combine(Base, "ManagedMods");
        public static readonly string Sky = Path.Combine(Managed, "sky");
        public static readonly string SoundPacks = Path.Combine(Base, "SoundPacks");
        public static readonly string SkipModsMarker = Path.Combine(Base, "skip-mods-once");
        public static readonly string Downloads = Path.Combine(Base, "Downloads");
    }

    class AppSettings
    {
        public int Preset { get; set; } = 2;     // 0-4 = preset, -1 = custom
        public Dictionary<string, string> Flags { get; set; } = new();
        public string? FontFile { get; set; }
        public string? FontDisplayName { get; set; }
        public long? ActiveAccountId { get; set; }
        public bool UseAccountForWebsite { get; set; } = true;
        public bool CloseAfterLaunch { get; set; } = true;
        public bool DiscordPresence { get; set; }
        public bool DiscordShowGame { get; set; } = true;
        public bool DiscordShowAccount { get; set; }
        public bool DiscordAllowJoin { get; set; }
        public bool StreamerMode { get; set; }
        public bool FriendAlerts { get; set; } = true;
        public bool SessionSummary { get; set; } = true;
        public Dictionary<string, string> GameSetups { get; set; } = new();
        public int SettingsVersion { get; set; }
        public bool RunInBackground { get; set; } = true;
        public bool ShowServerLocation { get; set; } = true;
        public bool MultiInstance { get; set; }
        public bool CrashHelper { get; set; } = true;
        public string? DeathSoundFile { get; set; }
        public string CursorPreset { get; set; } = "default";
        public string CrosshairPreset { get; set; } = "default";
        public string FootstepSound { get; set; } = "default";
        public string JumpSound { get; set; } = "default";
        public string? SoundPack { get; set; }
        public int FpsCap { get; set; }
        public int Priority { get; set; }
        public Dictionary<string, SavedSetup> Setups { get; set; } = new();
        public string? LastSetup { get; set; }
        public bool CustomSky { get; set; }
        public string Accent { get; set; } = "Purple";
        public string ThemeName { get; set; } = "Black";
        public string? LauncherBackground { get; set; }
        public string LastSeenVersion { get; set; } = "";
        public string Language { get; set; } = "";
        public Dictionary<string, string> PreviousLinkHandlers { get; set; } = new();
        public DateTime LastUpdateCheck { get; set; }

        static string FilePath => Path.Combine(Paths.Base, "settings.json");

        public static AppSettings Load()
        {
            AppSettings s;
            try { s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
            catch { s = new(); }
            if (s.SettingsVersion < 2)
            {
                // 4.5: Discord status is now off until you turn it on in the Discord tab
                s.DiscordPresence = false;
                s.SettingsVersion = 2;
                try { s.Save(); } catch { }
            }
            return s;
        }

        public AppSettings Clone() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this)) ?? new();

        public void Save()
        {
            Directory.CreateDirectory(Paths.Base);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    class SavedSetup
    {
        public int Preset { get; set; } = 2;
        public Dictionary<string, string> Flags { get; set; } = new();
        public string CursorPreset { get; set; } = "default";
        public string CrosshairPreset { get; set; } = "default";
        public string? DeathSoundFile { get; set; }
        public string FootstepSound { get; set; } = "default";
        public string JumpSound { get; set; } = "default";
        public string? SoundPack { get; set; }
        public bool CustomSky { get; set; }
        public int FpsCap { get; set; }
        public int Priority { get; set; }

        public void ApplyTo(AppSettings s)
        {
            s.Preset = Preset;
            s.Flags = Flags.Where(kv => FF.IsAllowed(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            s.CursorPreset = CursorPreset;
            s.CrosshairPreset = CrosshairPreset;
            s.DeathSoundFile = DeathSoundFile;
            s.FootstepSound = FootstepSound;
            s.JumpSound = JumpSound;
            s.SoundPack = SoundPack;
            s.CustomSky = CustomSky;
            s.FpsCap = FpsCap;
            s.Priority = Priority;
        }
    }

    class Account
    {
        public long UserId { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Cookie { get; set; } = "";
    }

    static class AccountStore
    {
        static string FilePath => Path.Combine(Paths.Base, "accounts.dat");

        public static List<Account> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                string json = Encoding.UTF8.GetString(Dpapi.Unprotect(File.ReadAllBytes(FilePath)));
                return JsonSerializer.Deserialize<List<Account>>(json) ?? new();
            }
            catch { return new(); }
        }

        public static void Save(List<Account> list)
        {
            Directory.CreateDirectory(Paths.Base);
            byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(list));
            File.WriteAllBytes(FilePath, Dpapi.Protect(json));
        }
    }

    // Windows DPAPI: only your Windows user on this PC can decrypt the accounts file
    static class Dpapi
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DataBlob { public int cbData; public IntPtr pbData; }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptProtectData(ref DataBlob dataIn, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr mem);

        public static byte[] Protect(byte[] data) => Run(data, true);
        public static byte[] Unprotect(byte[] data) => Run(data, false);

        static byte[] Run(byte[] input, bool protect)
        {
            var inBlob = new DataBlob { cbData = input.Length, pbData = Marshal.AllocHGlobal(Math.Max(1, input.Length)) };
            var outBlob = new DataBlob();
            try
            {
                Marshal.Copy(input, 0, inBlob.pbData, input.Length);
                bool ok = protect
                    ? CryptProtectData(ref inBlob, "GuideStrap", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref outBlob)
                    : CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref outBlob);
                if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
                var result = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
            }
        }
    }

    static class NativeMethods
    {
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
        [DllImport("shell32.dll")] public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
    }

    // FASTFLAGS (Roblox's allowlist only)
    static class FF
    {
        public const string Quality = "DFIntDebugFRMQualityLevelOverride";
        public const string TexEnabled = "DFFlagTextureQualityOverrideEnabled";
        public const string Texture = "DFIntTextureQualityOverride";
        public const string Msaa = "FIntDebugForceMSAASamples";
        public const string D3D11 = "FFlagDebugGraphicsPreferD3D11";
        public const string Vulkan = "FFlagDebugGraphicsPreferVulkan";
        public const string OpenGL = "FFlagDebugGraphicsPreferOpenGL";
        public const string GrassMin = "FIntFRMMinGrassDistance";
        public const string GrassMax = "FIntFRMMaxGrassDistance";
        public const string GraySky = "FFlagDebugSkyGray";
        public const string PauseVoxelizer = "DFFlagDebugPauseVoxelizer";
        public const string GrassMotion = "FIntGrassMovementReducedMotionFactor";
        public const string DisableDpi = "DFFlagDisableDPIScale";
        public const string AltEnter = "FFlagHandleAltEnterFullscreenManually";
        public static readonly string[] Lod =
        {
            "DFIntCSGLevelOfDetailSwitchingDistance",
            "DFIntCSGLevelOfDetailSwitchingDistanceL12",
            "DFIntCSGLevelOfDetailSwitchingDistanceL23",
            "DFIntCSGLevelOfDetailSwitchingDistanceL34",
        };

        // Roblox's official allowlist. If Roblox adds more later, add them here.
        public static readonly (string Name, string Desc)[] Allowed =
        {
            (Quality, "Graphics quality level, 1 (lowest) to 21 (highest)"),
            (TexEnabled, "True = use the texture quality flag"),
            (Texture, "Texture quality: 0 lowest, 1 low, 2 medium, 3 high"),
            (Msaa, "Anti-aliasing samples: 1, 2, 4 or 8"),
            (D3D11, "True = render with Direct3D 11"),
            (Vulkan, "True = render with Vulkan"),
            (OpenGL, "True = render with OpenGL"),
            (GrassMin, "Grass draw distance (min). 0 with max = no grass"),
            (GrassMax, "Grass draw distance (max). 0 = no grass"),
            (GraySky, "True = gray sky instead of the skybox"),
            (PauseVoxelizer, "True = freeze lighting updates (faster)"),
            (GrassMotion, "Grass movement. 0 = still grass"),
            (DisableDpi, "True = turn off DPI scaling"),
            (AltEnter, "False = Alt+Enter uses exclusive fullscreen"),
            (Lod[0], "Mesh detail switching distance"),
            (Lod[1], "Mesh detail distance, level 1 to 2"),
            (Lod[2], "Mesh detail distance, level 2 to 3"),
            (Lod[3], "Mesh detail distance, level 3 to 4"),
        };

        public static bool IsAllowed(string name) => Allowed.Any(a => a.Name == name);
        public static string Describe(string? name) => L.T(Allowed.FirstOrDefault(a => a.Name == name).Desc ?? "");
    }

    // MODS: write FastFlags + custom font into the Roblox install
    static class Mods
    {
        // Copies mod files into the GuideStrap copy of Roblox. Originals are backed up first, and every
        // launch starts by putting the originals back, so turning a mod off really removes it.
        static void ApplyOverlay(string versionDir, Dictionary<string, string> files)
        {
            string backupDir = Path.Combine(versionDir, "_guidestrap_originals");
            string manifest = Path.Combine(backupDir, "applied.txt");
            if (File.Exists(manifest))
            {
                foreach (var rel in File.ReadAllLines(manifest))
                {
                    if (rel.Length == 0) continue;
                    string target = Path.Combine(versionDir, rel), backup = Path.Combine(backupDir, rel);
                    try
                    {
                        if (File.Exists(backup)) File.Copy(backup, target, true);
                        else if (File.Exists(target)) File.Delete(target);
                    }
                    catch { }
                }
            }

            var applied = new List<string>();
            foreach (var (rel, source) in files)
            {
                if (!File.Exists(source) || rel.StartsWith("_guidestrap", StringComparison.OrdinalIgnoreCase)) continue;
                string target = Path.GetFullPath(Path.Combine(versionDir, rel));
                if (!target.StartsWith(Path.GetFullPath(versionDir), StringComparison.OrdinalIgnoreCase)) continue;
                string backup = Path.Combine(backupDir, rel);
                try
                {
                    if (File.Exists(target) && !File.Exists(backup))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                        File.Copy(target, backup);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target, true);
                    applied.Add(rel);
                }
                catch { }
            }
            Directory.CreateDirectory(backupDir);
            File.WriteAllLines(manifest, applied);
        }

        public static void Apply(string versionDir, AppSettings settings)
        {
            string csDir = Path.Combine(versionDir, "ClientSettings");
            Directory.CreateDirectory(csDir);
            var flags = settings.Flags.Where(kv => FF.IsAllowed(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            File.WriteAllText(Path.Combine(csDir, "ClientAppSettings.json"),
                JsonSerializer.Serialize(flags, new JsonSerializerOptions { WriteIndented = true }));

            // file mods: your mods folder + death sound + cursor + sky
            var overlay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(Paths.Mods))
                foreach (var f in Directory.GetFiles(Paths.Mods, "*", SearchOption.AllDirectories))
                    overlay[Path.GetRelativePath(Paths.Mods, f)] = f;
            string? pack = ModAssets.SoundPackDir(settings);
            if (pack != null)
                foreach (var f in Directory.GetFiles(pack, "*", SearchOption.AllDirectories))
                    overlay[Path.Combine("content", "sounds", Path.GetRelativePath(pack, f))] = f;
            if (settings.DeathSoundFile != null && File.Exists(settings.DeathSoundFile))
                overlay[@"content\sounds\ouch.ogg"] = settings.DeathSoundFile;
            string? steps = ModAssets.SoundFile(settings.FootstepSound, "footsteps");
            if (steps != null) overlay[@"content\sounds\action_footsteps_plastic.mp3"] = steps;
            string? jump = ModAssets.SoundFile(settings.JumpSound, "jump");
            if (jump != null)
            {
                overlay[@"content\sounds\action_jump.mp3"] = jump;
                if (settings.JumpSound == "mute") overlay[@"content\sounds\action_jump_land.mp3"] = jump;
            }
            string? crosshair = ModAssets.CrosshairFile(settings);
            if (crosshair != null) overlay[@"content\textures\MouseLockedCursor.png"] = crosshair;
            string? cursorDir = ModAssets.CursorDir(settings);
            if (cursorDir != null)
                foreach (var name in new[] { "ArrowCursor.png", "ArrowFarCursor.png" })
                    overlay[$@"content\textures\Cursors\KeyboardMouse\{name}"] = Path.Combine(cursorDir, name);
            if (settings.CustomSky && Directory.Exists(Paths.Sky))
                foreach (var f in Directory.GetFiles(Paths.Sky, "*.tex"))
                    overlay[$@"PlatformContent\pc\textures\sky\{Path.GetFileName(f)}"] = f;
            bool skipMods = File.Exists(Paths.SkipModsMarker);   // set by the crash helper, for one launch
            if (skipMods) overlay.Clear();
            ApplyOverlay(versionDir, overlay);
            if (skipMods) try { File.Delete(Paths.SkipModsMarker); } catch { }

            string fontsDir = Path.Combine(versionDir, "content", "fonts");
            string familiesDir = Path.Combine(fontsDir, "families");
            if (!Directory.Exists(familiesDir)) return;

            string backupDir = Path.Combine(fontsDir, "families_original");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
                foreach (var f in Directory.GetFiles(familiesDir, "*.json"))
                    File.Copy(f, Path.Combine(backupDir, Path.GetFileName(f)));
            }
            else
            {
                foreach (var f in Directory.GetFiles(backupDir, "*.json"))
                    File.Copy(f, Path.Combine(familiesDir, Path.GetFileName(f)), true);
            }
            foreach (var old in Directory.GetFiles(fontsDir, "CustomFont.*"))
                File.Delete(old);

            if (skipMods || settings.FontFile == null || !File.Exists(settings.FontFile)) return;

            string destName = "CustomFont" + Path.GetExtension(settings.FontFile).ToLowerInvariant();
            File.Copy(settings.FontFile, Path.Combine(fontsDir, destName), true);

            foreach (var file in Directory.GetFiles(familiesDir, "*.json"))
            {
                if (Path.GetFileName(file).Contains("emoji", StringComparison.OrdinalIgnoreCase)) continue;
                var root = JsonNode.Parse(File.ReadAllText(file));
                if (root?["faces"] is not JsonArray faces) continue;
                foreach (var face in faces)
                    if (face is JsonObject o) o["assetId"] = "rbxasset://fonts/" + destName;
                File.WriteAllText(file, root.ToJsonString());
            }
        }
    }

    // ROBLOX WEB API
    static class RobloxApi
    {
        public static readonly HttpClient Http = new();
        static readonly HttpClient AuthHttp = new(new HttpClientHandler { UseCookies = false });

        public static async Task<string> GetLatestVersionAsync(CancellationToken ct = default)
        {
            string json = await Http.GetStringAsync("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer", ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("clientVersionUpload").GetString()
                   ?? throw new Exception("Roblox didn't return a version.");
        }

        public static async Task<Account> GetAccountAsync(string cookie)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
            req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
            using var resp = await AuthHttp.SendAsync(req);
            if (!resp.IsSuccessStatusCode) throw new Exception($"Roblox said no (HTTP {(int)resp.StatusCode}).");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var r = doc.RootElement;
            return new Account
            {
                UserId = r.GetProperty("id").GetInt64(),
                Username = r.GetProperty("name").GetString() ?? "",
                DisplayName = r.GetProperty("displayName").GetString() ?? "",
                Cookie = cookie,
            };
        }

        public static async Task<Image?> GetAvatarAsync(long userId)
        {
            string path = Path.Combine(Paths.Avatars, userId + ".png");
            try
            {
                if (!File.Exists(path))
                {
                    string json = await Http.GetStringAsync(
                        $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=false");
                    using var doc = JsonDocument.Parse(json);
                    string? url = doc.RootElement.GetProperty("data")[0].GetProperty("imageUrl").GetString();
                    if (string.IsNullOrEmpty(url)) return null;
                    Directory.CreateDirectory(Paths.Avatars);
                    await File.WriteAllBytesAsync(path, await Http.GetByteArrayAsync(url));
                }
                using var fs = File.OpenRead(path);
                using var img = Image.FromStream(fs);
                return new Bitmap(img);
            }
            catch { return null; }
        }

        // Public server list. smallest = fewest players first, otherwise a random open server.
        public static async Task<string?> FindServerAsync(long placeId, bool smallest, string? exclude, CancellationToken ct)
        {
            string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?sortOrder={(smallest ? "Asc" : "Desc")}&limit=100&excludeFullGames=true";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));
            var open = new List<(string Id, int Playing)>();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var sv in data.EnumerateArray())
                {
                    string? id = sv.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
                    int playing = sv.TryGetProperty("playing", out var pl) && pl.ValueKind == JsonValueKind.Number ? pl.GetInt32() : 0;
                    int max = sv.TryGetProperty("maxPlayers", out var mx) && mx.ValueKind == JsonValueKind.Number ? mx.GetInt32() : 0;
                    if (id == null || id == exclude || (max > 0 && playing >= max)) continue;
                    open.Add((id, playing));
                }
            }
            if (open.Count == 0) return null;
            return smallest ? open.OrderBy(x => x.Playing).First().Id : open[Random.Shared.Next(open.Count)].Id;
        }

        static bool TryProp(JsonElement e, string name, out JsonElement value)
        {
            value = default;
            if (e.ValueKind != JsonValueKind.Object) return false;
            foreach (var prop in e.EnumerateObject())
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) { value = prop.Value; return true; }
            return false;
        }

        static long? Num(JsonElement e, string name) =>
            TryProp(e, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;

        static string? Str(JsonElement e, string name) =>
            TryProp(e, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        // Signed-in POST that handles Roblox's CSRF token handshake
        static async Task<string> PostAuthedAsync(string url, string cookie, string json)
        {
            string? csrf = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                if (csrf != null) req.Headers.Add("X-CSRF-TOKEN", csrf);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await AuthHttp.SendAsync(req);
                if (csrf == null && resp.Headers.TryGetValues("x-csrf-token", out var token) && !resp.IsSuccessStatusCode)
                {
                    csrf = token.First();
                    continue;
                }
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new Exception("This account's saved login expired. Remove it and add it again.");
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
            throw new Exception("Roblox didn't accept the request. Try again.");
        }

        public static async Task<List<long>> GetFriendIdsAsync(long userId, string cookie)
        {
            var ids = new List<long>();
            string? cursor = null;
            for (int page = 0; page < 4; page++)
            {
                string url = $"https://friends.roblox.com/v1/users/{userId}/friends/find?limit=50" +
                             (cursor != null ? "&cursor=" + Uri.EscapeDataString(cursor) : "");
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                using var resp = await AuthHttp.SendAsync(req);
                if (!resp.IsSuccessStatusCode) break;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (TryProp(doc.RootElement, "PageItems", out var items) && items.ValueKind == JsonValueKind.Array)
                    foreach (var it in items.EnumerateArray())
                        if (Num(it, "id") is long id) ids.Add(id);
                cursor = Str(doc.RootElement, "NextCursor");
                if (string.IsNullOrEmpty(cursor)) break;
            }
            if (ids.Count == 0)
            {
                // older endpoint, kept as a fallback
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://friends.roblox.com/v1/users/{userId}/friends");
                req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                using var resp = await AuthHttp.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    if (TryProp(doc.RootElement, "data", out var data) && data.ValueKind == JsonValueKind.Array)
                        foreach (var it in data.EnumerateArray())
                            if (Num(it, "id") is long id) ids.Add(id);
                }
            }
            return ids.Distinct().ToList();
        }

        public static async Task<List<FriendPresence>> GetPresenceAsync(string cookie, List<long> userIds)
        {
            var list = new List<FriendPresence>();
            foreach (var chunk in userIds.Chunk(50))
            {
                string json = JsonSerializer.Serialize(new { userIds = chunk });
                using var doc = JsonDocument.Parse(await PostAuthedAsync("https://presence.roblox.com/v1/presence/users", cookie, json));
                if (!TryProp(doc.RootElement, "userPresences", out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var e in arr.EnumerateArray())
                {
                    if (Num(e, "userId") is not long uid) continue;
                    list.Add(new FriendPresence
                    {
                        UserId = uid,
                        Type = (int)(Num(e, "userPresenceType") ?? 0),
                        Location = Str(e, "lastLocation"),
                        PlaceId = Num(e, "placeId"),
                        RootPlaceId = Num(e, "rootPlaceId"),
                        GameId = Str(e, "gameId") is string g && Guid.TryParse(g, out _) ? g : null,
                    });
                }
            }
            return list;
        }

        // Display names for user IDs (cached in the given dictionary)
        public static async Task FillNamesAsync(IEnumerable<long> userIds, Dictionary<long, string> cache)
        {
            var missing = userIds.Where(id => !cache.ContainsKey(id)).Distinct().ToList();
            foreach (var chunk in missing.Chunk(100))
            {
                try
                {
                    string json = JsonSerializer.Serialize(new { userIds = chunk, excludeBannedUsers = false });
                    using var resp = await Http.PostAsync("https://users.roblox.com/v1/users", new StringContent(json, Encoding.UTF8, "application/json"));
                    if (!resp.IsSuccessStatusCode) continue;
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    if (!TryProp(doc.RootElement, "data", out var data) || data.ValueKind != JsonValueKind.Array) continue;
                    foreach (var u in data.EnumerateArray())
                        if (Num(u, "id") is long id) cache[id] = Str(u, "displayName") ?? Str(u, "name") ?? id.ToString();
                }
                catch { }
            }
        }

        // A one-time ticket that lets the Roblox client log in as a saved account
        public static async Task<string> GetAuthTicketAsync(string cookie)
        {
            string? csrf = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket");
                req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                req.Headers.Referrer = new Uri("https://www.roblox.com/");
                req.Headers.Add("Origin", "https://www.roblox.com");
                req.Headers.Add("RBXAuthenticationNegotiation", "1");
                if (csrf != null) req.Headers.Add("X-CSRF-TOKEN", csrf);
                req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                using var resp = await AuthHttp.SendAsync(req);
                if (resp.Headers.TryGetValues("rbx-authentication-ticket", out var ticket)) return ticket.First();
                if (resp.Headers.TryGetValues("x-csrf-token", out var token)) { csrf = token.First(); continue; }
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new Exception("This account's saved login expired. Remove it and add it again.");
                throw new Exception($"Couldn't log in this account (HTTP {(int)resp.StatusCode}).");
            }
            throw new Exception("Couldn't log in this account. Try again.");
        }

        public static string BuildLaunchUri(string ticket, long? placeId, string? jobId = null)
        {
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string tracker = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
            if (placeId == null)
                return $"roblox-player:1+launchmode:app+gameinfo:{ticket}+launchtime:{time}+browsertrackerid:{tracker}" +
                       "+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";

            string placeUrl = Uri.EscapeDataString(jobId != null
                ? $"https://www.roblox.com/Game/PlaceLauncher.ashx?request=RequestGameJob&browserTrackerId={tracker}" +
                  $"&placeId={placeId}&gameId={jobId}&isPlayTogetherGame=false&joinAttemptId={Guid.NewGuid()}&joinAttemptOrigin=publicServerListJoin"
                : $"https://www.roblox.com/Game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId={tracker}" +
                  $"&placeId={placeId}&isPlayTogetherGame=false&joinAttemptId={Guid.NewGuid()}&joinAttemptOrigin=PlayButton");
            return $"roblox-player:1+launchmode:play+gameinfo:{ticket}+launchtime:{time}+placelauncherurl:{placeUrl}" +
                   $"+browsertrackerid:{tracker}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
        }
    }

    // INSTALLER: downloads Roblox straight from Roblox's servers
    static class Installer
    {
        static readonly Dictionary<string, string> PackageMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["RobloxApp.zip"] = "",
            ["redist.zip"] = "",
            ["WebView2.zip"] = "",
            ["shaders.zip"] = "shaders/",
            ["ssl.zip"] = "ssl/",
            ["WebView2RuntimeInstaller.zip"] = "WebView2RuntimeInstaller/",
            ["content-avatar.zip"] = "content/avatar/",
            ["content-configs.zip"] = "content/configs/",
            ["content-fonts.zip"] = "content/fonts/",
            ["content-sky.zip"] = "content/sky/",
            ["content-sounds.zip"] = "content/sounds/",
            ["content-textures2.zip"] = "content/textures/",
            ["content-models.zip"] = "content/models/",
            ["content-platform-fonts.zip"] = "PlatformContent/pc/fonts/",
            ["content-platform-dictionaries.zip"] = "PlatformContent/pc/shared_compression_dictionaries/",
            ["content-terrain.zip"] = "PlatformContent/pc/terrain/",
            ["content-textures3.zip"] = "PlatformContent/pc/textures/",
            ["extracontent-luapackages.zip"] = "ExtraContent/LuaPackages/",
            ["extracontent-translations.zip"] = "ExtraContent/translations/",
            ["extracontent-models.zip"] = "ExtraContent/models/",
            ["extracontent-textures.zip"] = "ExtraContent/textures/",
            ["extracontent-places.zip"] = "ExtraContent/places/",
        };

        public static async Task InstallAsync(string versionGuid, string versionDir, Action<string, float> report, CancellationToken ct = default)
        {
            string tempDir = versionDir + "_installing";
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            string cdn = $"https://setup.rbxcdn.com/{versionGuid}-";
            report("Getting the latest Roblox...", -1f);
            string manifest = await RobloxApi.Http.GetStringAsync(cdn + "rbxPkgManifest.txt", ct);
            string[] lines = manifest.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            // "v0", then groups of 4 lines: file name, MD5, download size, unpacked size
            var packages = new List<(string Name, string Md5, long Size)>();
            for (int i = 1; i + 2 < lines.Length; i += 4)
            {
                if (!lines[i].EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                packages.Add((lines[i], lines[i + 1].ToLowerInvariant(), long.Parse(lines[i + 2])));
            }

            long total = Math.Max(1, packages.Sum(p => p.Size));
            long done = 0;
            int lastPct = -1;
            Directory.CreateDirectory(Paths.Downloads);

            foreach (var pkg in packages)
            {
                // packages that didn't change since the last Roblox update are reused instead of downloaded again
                string cached = Path.Combine(Paths.Downloads, pkg.Md5 + ".zip");
                if (File.Exists(cached))
                {
                    byte[] old = await File.ReadAllBytesAsync(cached, ct);
                    if (Convert.ToHexString(MD5.HashData(old)).ToLowerInvariant() == pkg.Md5)
                    {
                        done += pkg.Size;
                        report($"Installing Roblox... {Math.Min((int)(done * 100 / total), 100)}%", (float)done / total);
                        string dest = PackageMap.TryGetValue(pkg.Name, out var oldSub) ? Path.Combine(tempDir, oldSub) : tempDir;
                        await Task.Run(() => ExtractZip(old, dest), ct);
                        continue;
                    }
                    try { File.Delete(cached); } catch { }
                }

                using var resp = await RobloxApi.Http.GetAsync(cdn + pkg.Name, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var ms = new MemoryStream();
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    ms.Write(buffer, 0, read);
                    done += read;
                    int pct = (int)(done * 100 / total);
                    if (pct != lastPct)
                    {
                        lastPct = pct;
                        report($"Installing Roblox... {Math.Min(pct, 100)}%", (float)done / total);
                    }
                }

                byte[] data = ms.ToArray();
                if (Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant() != pkg.Md5)
                    throw new Exception($"{pkg.Name} downloaded corrupted. Try again.");
                try { await File.WriteAllBytesAsync(cached, data, ct); } catch { }

                string target = PackageMap.TryGetValue(pkg.Name, out var sub) ? Path.Combine(tempDir, sub) : tempDir;
                await Task.Run(() => ExtractZip(data, target), ct);
            }

            File.WriteAllText(Path.Combine(tempDir, "AppSettings.xml"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Settings>\r\n" +
                "\t<ContentFolder>content</ContentFolder>\r\n" +
                "\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n</Settings>\r\n");

            if (Directory.Exists(versionDir)) Directory.Delete(versionDir, true);
            Directory.Move(tempDir, versionDir);

            // keep only the packages this version uses
            var keep = packages.Select(p => p.Md5 + ".zip").ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.GetFiles(Paths.Downloads))
                if (!keep.Contains(Path.GetFileName(f))) try { File.Delete(f); } catch { }
        }

        static void ExtractZip(byte[] data, string targetDir)
        {
            string root = Path.GetFullPath(targetDir);
            using var zip = new ZipArchive(new MemoryStream(data));
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
                string dest = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('\\', '/')));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, true);
            }
        }

        public static void CleanupOldVersions(string keep)
        {
            if (!Directory.Exists(Paths.Versions)) return;
            foreach (var dir in Directory.GetDirectories(Paths.Versions))
            {
                if (Path.GetFileName(dir) == keep) continue;
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }

    // APP INFO + ABOUT TEXT
    static class AppInfo
    {
        public const string Version = "4.6.4";
        // GitHub repo for self-updates, e.g. "yourname/GuideStrap". Leave empty until you publish releases.
        public const string UpdateRepo = "guideships/GuideStrap";
        public const string DiscordAppId = "1574254036398375062";
        public const string SiteUrl = "https://guideships.github.io/GuideStrap";
        // Discord loads the status picture from this link, so it works without uploading art in the Discord portal
        // Shown once after GuideStrap updates itself
        public const string WhatsNew =
            "\u2022 The logo is now black in every theme, Purple included\n" +
            "\u2022 Fixed desktop and Start menu icons keeping the old look after an update\n" +
            "\u2022 Black is now the default theme (Purple is still in Settings > Appearance)\n" +
            "\u2022 New black logo for the app, the taskbar, Discord and the website\n" +
            "\u2022 Uninstalling can now keep your settings, accounts and mods for a reinstall";

        public const string LogoUrl = "https://raw.githubusercontent.com/guideships/GuideStrap/master/docs/logo-black.png";   // new name so Discord doesn't show its cached old picture
        public const string GalleryUrl = "https://raw.githubusercontent.com/guideships/GuideStrap/master/gallery/index.json";

        public const string AboutText =
            "GuideStrap is a Roblox launcher made by Guide. It downloads Roblox from Roblox's own servers, " +
            "keeps it updated, and starts it with your settings applied.\n\n" +
            "Besides launching, it handles graphics presets, FastFlags (only the ones Roblox allows), " +
            "a custom in-game font, and switching between saved accounts.\n\n" +
            "It never touches the game while it's running. It only changes launch files and settings that " +
            "Roblox reads on its own, so it isn't an exploit. Saved logins are encrypted for your Windows user.\n\n" +
            "Everything happens in GuideStrap's own copy of Roblox. Your normal Roblox install is never changed, so " +
            "launching Roblox without GuideStrap always gives you the original game, sounds and settings.\n\n" +
            "GuideStrap is a fan project and isn't affiliated with Roblox Corporation.";

        public static string PresetName(int p) => p switch
        {
            0 => "Max FPS",
            1 => "Performance",
            2 => "Balanced",
            3 => "Quality",
            4 => "Max Graphics",
            _ => "Custom",
        };
    }

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
                Theme.Apply(current.ThemeName, current.Accent);
                Open(new SetupForm(this, repair));
                return;
            }

            var settings = AppSettings.Load();
            L.Lang = settings.Language.Length > 0 ? settings.Language : L.SystemLanguage();
            Theme.Apply(settings.ThemeName, settings.Accent);
            Setup.RefreshEntry();
            Updater.CleanupOld();
            Presence.Configure(settings);
            Presence.Enable(settings.DiscordPresence);
            Presence.WatchEnded += OnWatchEnded;
            Presence.SessionEnded += sum =>
            {
                var st = AppSettings.Load();
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
            FriendWatcher.Start();
            Presence.RobloxCrashed += info => { if (AppSettings.Load().CrashHelper) ShowCrash(info); };
            Presence.ServerLocated += location =>
            {
                var st = AppSettings.Load();
                if (tray != null && st.ShowServerLocation && !st.StreamerMode)
                    tray.ShowBalloonTip(4000, "GuideStrap", L.F("Connected to a server in {0}", location), ToolTipIcon.None);
            };
            if (launchUri != null)
                Open(new LoadingForm(this, settings, AccountStore.Load(), launchUri, ""));
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

    // Discord status. Shows where you are in GuideStrap, and the game you're in while Roblox runs.
    // Roblox doesn't tell other programs what game you joined, so this follows Roblox's log file (like Bloxstrap does).
    static class Presence
    {
        static DiscordRpcClient? client;
        static readonly DateTime appStart = DateTime.UtcNow;
        static string screen = "In the main menu";
        static string? screenDetail;
        static bool inRoblox;
        static long placeId;
        static string? gameName, gameCreator, gameIcon, serverLocation, lastServerIp;
        public static event Action<string>? ServerLocated;
        static DateTime robloxStart, gameStart;
        static CancellationTokenSource? watchCts;

        public static bool Watching { get; private set; }
        public static event Action? WatchEnded;
        public static event Action<CrashInfo>? RobloxCrashed;
        public static event Action<SessionSummary>? SessionEnded;
        static bool showGame = true, showAccount, allowJoin, streamer;
        static volatile string? account;
        static string? jobId, sessionLocation;
        static readonly List<string> sessionGames = new();

        public static void Configure(AppSettings s)
        {
            showGame = s.DiscordShowGame;
            showAccount = s.DiscordShowAccount;
            allowJoin = s.DiscordAllowJoin;
            streamer = s.StreamerMode;
            Push();
        }

        // The saved account being launched (null = Roblox login). Only shown if the user turns it on.
        public static void SetAccount(string? name) => account = name;

        static string LogPath => Path.Combine(Paths.Base, "discord.log");

        static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(Paths.Base);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  {msg}{Environment.NewLine}");
            }
            catch { }
        }

        public static void Enable(bool on)
        {
            if (on && client == null)
            {
                try
                {
                    try { if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 200_000) File.Delete(LogPath); } catch { }
                    client = new DiscordRpcClient(AppInfo.DiscordAppId);
                    client.OnReady += (_, e) => Log($"connected to Discord as {e.User?.Username}");
                    client.OnError += (_, e) => Log($"Discord error: {e.Message}");
                    client.OnConnectionFailed += (_, _) => Log("couldn't reach Discord (is the Discord app open?)");
                    client.Initialize();
                    Push();
                }
                catch (Exception ex) { Log("start failed: " + ex.Message); client = null; }
            }
            else if (!on && client != null)
            {
                try { client.ClearPresence(); client.Dispose(); } catch { }
                client = null;
            }
        }

        public static void Shutdown()
        {
            watchCts?.Cancel();
            Enable(false);
        }

        // Called by the windows: "In the main menu", "Editing settings" + page, "Launching Roblox"...
        public static void Screen(string text, string? detail = null)
        {
            screen = text;
            screenDetail = detail;
            Push();
        }

        static string Cut(string s, int max = 120) => s.Length <= max ? s : s[..(max - 1)] + "…";

        static void Push()
        {
            if (client == null) return;
            try
            {
                var p = new DiscordRPC.RichPresence();
                string? who = account;
                string? playingAs = showAccount && !streamer && who != null ? Cut("Playing as " + who) : null;
                if (inRoblox && placeId != 0 && showGame)
                {
                    string name = gameName ?? "a game";
                    p.Details = Cut("In " + name);
                    p.State = playingAs ?? (gameCreator != null ? Cut("by " + gameCreator) : null);
                    p.Timestamps = new Timestamps(gameStart);
                    p.Assets = new Assets
                    {
                        LargeImageKey = gameIcon is { Length: > 0 and <= 256 } ? gameIcon : AppInfo.LogoUrl,
                        LargeImageText = Cut(name),
                        SmallImageKey = AppInfo.LogoUrl,
                        SmallImageText = serverLocation != null && !streamer ? Cut("Server: " + serverLocation) : "GuideStrap",
                    };
                    var buttons = new List<DiscordRPC.Button>();
                    if (allowJoin && !streamer && jobId != null)
                        buttons.Add(new DiscordRPC.Button { Label = "Join server", Url = $"{AppInfo.SiteUrl}/join.html?placeId={placeId}&gameInstanceId={jobId}" });
                    buttons.Add(new DiscordRPC.Button { Label = "View game", Url = $"https://www.roblox.com/games/{placeId}" });
                    p.Buttons = buttons.ToArray();
                }
                else if (inRoblox)
                {
                    p.Details = placeId != 0 ? "Playing Roblox" : "In the Roblox app";
                    p.State = playingAs;
                    p.Timestamps = new Timestamps(placeId != 0 ? gameStart : robloxStart);
                    p.Assets = new Assets { LargeImageKey = AppInfo.LogoUrl, LargeImageText = "GuideStrap" };
                }
                else
                {
                    p.Details = screen;
                    p.State = screenDetail;
                    p.Timestamps = new Timestamps(appStart);
                    p.Assets = new Assets { LargeImageKey = AppInfo.LogoUrl, LargeImageText = "GuideStrap" };
                }
                client.SetPresence(p);
            }
            catch (Exception ex) { Log("couldn't set status: " + ex.Message); }
        }

        public static void WatchRoblox()
        {
            if (Watching) return;
            Watching = true;
            inRoblox = true;
            placeId = 0;
            jobId = null;
            sessionLocation = null;
            sessionGames.Clear();
            robloxStart = DateTime.UtcNow;
            Push();
            watchCts = new CancellationTokenSource();
            _ = WatchAsync(watchCts.Token);
        }

        public static void StopWatching() => watchCts?.Cancel();

        static bool RobloxRunning()
        {
            var procs = Process.GetProcessesByName("RobloxPlayerBeta");
            foreach (var p in procs) p.Dispose();
            return procs.Length > 0;
        }

        static async Task WatchAsync(CancellationToken ct)
        {
            var started = DateTime.Now;
            CrashWatch.Begin();
            StreamReader? reader = null;
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
                int missed = 0;
                while (!ct.IsCancellationRequested)
                {
                    // find Roblox's new log file (it appears a moment after Roblox starts)
                    if (reader == null && Directory.Exists(logDir))
                    {
                        // Roblox writes several logs (crash handler, installer...). The game one has "Player" in its name.
                        var file = new DirectoryInfo(logDir).GetFiles("*.log")
                            .Where(f => f.Name.Contains("Player", StringComparison.OrdinalIgnoreCase)
                                        && !f.Name.Contains("Crash", StringComparison.OrdinalIgnoreCase)
                                        && f.CreationTime >= started.AddSeconds(-90))
                            .OrderByDescending(f => f.CreationTime)
                            .FirstOrDefault();
                        if (file != null)
                        {
                            Log("following " + file.Name);
                            reader = new StreamReader(new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
                        }
                    }

                    if (reader != null)
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync(ct)) != null)
                        {
                            CrashWatch.Line(line);
                            await HandleLineAsync(line);
                        }
                    }

                    // Roblox can restart itself, so only stop after it's been gone for a few seconds
                    CrashWatch.Poll();
                    if (RobloxRunning()) missed = 0;
                    else if (++missed >= 4) break;

                    await Task.Delay(1000, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log("watch error: " + ex.Message); }
            finally
            {
                reader?.Dispose();
                Watching = false;
                inRoblox = false;
                placeId = 0;
                jobId = null;
                gameName = gameCreator = gameIcon = serverLocation = lastServerIp = null;
                Log("Roblox closed");
                Push();
                var crash = CrashWatch.End(report: !ct.IsCancellationRequested);
                if (crash != null)
                {
                    Log($"Roblox crashed: 0x{crash.Code:X8}");
                    try { RobloxCrashed?.Invoke(crash); }
                    catch (Exception ex) { Log("crash helper error: " + ex.Message); }
                }
                else if (!ct.IsCancellationRequested)
                {
                    try { SessionEnded?.Invoke(new SessionSummary(DateTime.UtcNow - robloxStart, sessionGames.ToList(), sessionLocation)); }
                    catch (Exception ex) { Log("summary error: " + ex.Message); }
                }
                WatchEnded?.Invoke();
            }
        }

        static async Task HandleLineAsync(string line)
        {
            if (line.Contains("! Joining game") || line.Contains("Joining game"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"place (\d+)");
                var jm = System.Text.RegularExpressions.Regex.Match(line, @"Joining game '([0-9a-fA-F-]{36})'");
                if (m.Success && jm.Success && long.TryParse(m.Groups[1].Value, out long joined))
                {
                    jobId = jm.Groups[1].Value;
                    LastServer.Save(joined, jobId);
                    if (joined == placeId) Push();
                }
                if (m.Success && long.TryParse(m.Groups[1].Value, out long id) && id != placeId)
                {
                    Log("joining place " + id);
                    placeId = id;
                    gameStart = DateTime.UtcNow;
                    gameName = null; gameCreator = null; gameIcon = null; serverLocation = null; lastServerIp = null;
                    Push();
                    await LoadGameInfoAsync(id);
                }
            }
            else if (line.Contains("UDMUX Address = ") || line.Contains("Connection accepted from"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"(?:UDMUX Address = |Connection accepted from )(\d+\.\d+\.\d+\.\d+)");
                if (m.Success && m.Groups[1].Value != lastServerIp)
                {
                    lastServerIp = m.Groups[1].Value;
                    await LocateServerAsync(lastServerIp);
                }
            }
            else if (line.Contains("Time to disconnect replication data") || line.Contains("leaveUGCGameInternal"))
            {
                if (placeId != 0) Log("left game");
                placeId = 0;
                jobId = null;
                gameName = gameCreator = gameIcon = serverLocation = lastServerIp = null;
                Push();
            }
        }

        static async Task LocateServerAsync(string ip)
        {
            try
            {
                using var doc = JsonDocument.Parse(await RobloxApi.Http.GetStringAsync($"https://ipinfo.io/{ip}/json"));
                string Get(string key) => doc.RootElement.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
                var parts = new[] { Get("city"), Get("region"), Get("country") }.Where(s => s.Length > 0).Distinct().ToList();
                if (parts.Count == 0 || ip != lastServerIp) return;
                serverLocation = string.Join(", ", parts);
                sessionLocation = serverLocation;
                Log("server in " + serverLocation);
                Push();
                ServerLocated?.Invoke(serverLocation);
            }
            catch (Exception ex) { Log("server location error: " + ex.Message); }
        }

        static async Task LoadGameInfoAsync(long id)
        {
            try
            {
                var http = RobloxApi.Http;
                using var u = JsonDocument.Parse(await http.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{id}/universe"));
                long universe = u.RootElement.GetProperty("universeId").GetInt64();
                using var g = JsonDocument.Parse(await http.GetStringAsync($"https://games.roblox.com/v1/games?universeIds={universe}"));
                var game = g.RootElement.GetProperty("data")[0];
                string? name = game.GetProperty("name").GetString();
                string? creator = game.GetProperty("creator").GetProperty("name").GetString();
                using var t = JsonDocument.Parse(await http.GetStringAsync($"https://thumbnails.roblox.com/v1/games/icons?universeIds={universe}&size=512x512&format=Png&isCircular=false"));
                string? icon = t.RootElement.GetProperty("data")[0].GetProperty("imageUrl").GetString();
                if (id != placeId) return;   // left or teleported meanwhile
                gameName = name; gameCreator = creator; gameIcon = icon;
                if (name != null && !sessionGames.Contains(name)) sessionGames.Add(name);
                Log($"in game: {name}");
                Push();
                RecentGames.Add(new RecentGame { PlaceId = id, Name = name ?? "Game", Creator = creator ?? "", IconUrl = icon ?? "", LastPlayed = DateTime.Now });
            }
            catch (Exception ex) { Log("game info error: " + ex.Message); }
        }
    }

    // SHARED LAUNCH LOGIC (used by the launcher and the settings window)
    static class RobloxLauncher
    {
        // report(text, progress): progress from 0 to 1 fills the bar, a negative value means "busy, unknown length".
        // Returns the Roblox process, or null if the game page was opened in the browser instead.
        public static async Task<Process?> LaunchAsync(AppSettings settings, List<Account> accounts, string? launchUri, string placeText,
            Action<string, float> report, CancellationToken ct, string? jobId = null, ServerPick pick = ServerPick.None)
        {
            report("Connecting to Roblox...", -1f);
            string versionGuid = await RobloxApi.GetLatestVersionAsync(ct);
            string versionDir = Path.Combine(Paths.Versions, versionGuid);
            string exe = Path.Combine(versionDir, "RobloxPlayerBeta.exe");

            if (!File.Exists(exe))
            {
                try { await Installer.InstallAsync(versionGuid, versionDir, report, ct); }
                catch (OperationCanceledException)
                {
                    try { Directory.Delete(versionDir + "_installing", true); } catch { }
                    throw;
                }
            }
            ct.ThrowIfCancellationRequested();
            Installer.CleanupOldVersions(versionGuid);

            long? placeId = launchUri != null ? ParsePlaceIdFromLink(launchUri) : ParsePlaceId(placeText);
            if (launchUri == null && placeText.Trim().Length > 0 && placeId == null)
                throw new Exception("That doesn't look like a game link or place ID.");

            // a setup linked to this game is used for this launch only
            var effective = settings;
            if (placeId != null && settings.GameSetups.TryGetValue(placeId.Value.ToString(), out var setupName)
                && settings.Setups.TryGetValue(setupName, out var setup))
            {
                effective = settings.Clone();
                setup.ApplyTo(effective);
                report($"Applying your {setupName} setup...", -1f);
            }
            else report("Applying your settings...", -1f);
            Mods.Apply(versionDir, effective);

            var acc = accounts.FirstOrDefault(a => a.UserId == settings.ActiveAccountId);
            Presence.SetAccount(acc?.DisplayName);
            string signingIn = settings.StreamerMode || acc == null ? "Signing in..." : $"Signing in as {acc.DisplayName}...";

            if (launchUri == null && pick != ServerPick.None)
            {
                if (placeId == null) throw new Exception("Paste a game link first, or join a game once.");
                report(pick == ServerPick.Small ? "Finding a small server..." : "Finding another server...", -1f);
                jobId = await RobloxApi.FindServerAsync(placeId.Value, pick == ServerPick.Small, LastServer.Load()?.JobId, ct)
                        ?? throw new Exception("Couldn't find an open server for that game. Try again in a moment.");
            }
            if (jobId != null && !Guid.TryParse(jobId, out _)) jobId = null;

            string arg;
            if (launchUri != null)   // opened from the website's Play button
            {
                arg = launchUri;
                if (acc != null && settings.UseAccountForWebsite && launchUri.Contains("gameinfo:"))
                {
                    report(signingIn, -1f);
                    string ticket = await RobloxApi.GetAuthTicketAsync(acc.Cookie);
                    arg = Regex.Replace(launchUri, @"gameinfo:[^+]*", _ => "gameinfo:" + ticket);
                }
            }
            else if (acc == null)
            {
                if (placeId != null && jobId != null)
                    arg = $"roblox://experiences/start?placeId={placeId}&gameInstanceId={jobId}";
                else if (placeId != null)
                {
                    Process.Start(new ProcessStartInfo($"https://www.roblox.com/games/{placeId}") { UseShellExecute = true });
                    return null;
                }
                else arg = "--app";
            }
            else
            {
                report(signingIn, -1f);
                string ticket = await RobloxApi.GetAuthTicketAsync(acc.Cookie);
                arg = RobloxApi.BuildLaunchUri(ticket, placeId, jobId);
            }
            ct.ThrowIfCancellationRequested();

            report("Starting Roblox...", -1f);
            if (effective.FpsCap > 0) RobloxTweaks.SetFpsCap(effective.FpsCap);
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = versionDir, UseShellExecute = false };
            psi.ArgumentList.Add(arg);
            var proc = Process.Start(psi);
            if (effective.Priority > 0)
                RobloxTweaks.BoostPriority(effective.Priority == 2 ? ProcessPriorityClass.High : ProcessPriorityClass.AboveNormal);
            return proc;
        }

        // Place ID inside a roblox-player: or roblox:// link (the website's Play button)
        public static long? ParsePlaceIdFromLink(string link)
        {
            try
            {
                var m = Regex.Match(Uri.UnescapeDataString(link), @"placeId=(\d+)", RegexOptions.IgnoreCase);
                return m.Success && long.TryParse(m.Groups[1].Value, out var id) ? id : null;
            }
            catch { return null; }
        }

        public static long? ParsePlaceId(string text)
        {
            text = text.Trim();
            if (text.Length == 0) return null;
            var m = Regex.Match(text, @"games/(\d+)");
            if (m.Success) return long.Parse(m.Groups[1].Value);
            m = Regex.Match(text, @"[?&]placeId=(\d+)", RegexOptions.IgnoreCase);
            if (m.Success) return long.Parse(m.Groups[1].Value);
            return long.TryParse(text, out var id) ? id : null;
        }
    }

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

    // Loading screen shown while Roblox is updated and started
    class LoadingForm : ThemedForm
    {
        readonly GuideStrapContext context;
        readonly AppSettings settings;
        readonly List<Account> accounts;
        readonly string? launchUri;
        readonly string placeText;
        readonly string? jobId;
        readonly ServerPick pick;
        readonly SpinningLogo logo = new();
        readonly Label status = new();
        readonly LoadingBar bar = new();
        readonly RoundButton cancel;
        readonly CancellationTokenSource cts = new();
        bool finished;

        public LoadingForm(GuideStrapContext context, AppSettings settings, List<Account> accounts, string? launchUri, string placeText,
            string? jobId = null, ServerPick pick = ServerPick.None)
        {
            this.context = context;
            this.jobId = jobId;
            this.pick = pick;
            this.settings = settings;
            this.accounts = accounts;
            this.launchUri = launchUri;
            this.placeText = placeText;
            Text = "GuideStrap";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(D(540), D(284));
            int W = ClientSize.Width;

            var acc = accounts.FirstOrDefault(a => a.UserId == settings.ActiveAccountId);
            string who = settings.StreamerMode ? L.T("hidden") : acc != null ? acc.Username : L.T("Roblox login");
            var left = At(Lbl(L.F("Account: {0}", who), 8.25f, Theme.SubText), D(12), D(10));
            var right = new Label
            {
                Text = $"GuideStrap {AppInfo.Version}", AutoSize = false, TextAlign = ContentAlignment.TopRight,
                Size = new Size(D(200), D(18)), Location = new Point(W - D(212), D(10)),
                Font = Theme.F("Segoe UI", 8.25f), ForeColor = Theme.SubText, BackColor = Theme.Bg,
            };

            logo.Size = new Size(D(112), D(112));
            logo.Location = new Point((W - logo.Width) / 2, D(30));

            status.AutoSize = false;
            status.TextAlign = ContentAlignment.MiddleCenter;
            status.Size = new Size(W - D(40), D(30));
            status.Location = new Point(D(20), D(148));
            status.Font = Theme.F("Segoe UI", 13f);
            status.ForeColor = Theme.Text;
            status.BackColor = Theme.Bg;
            status.Text = L.T("Starting...");

            bar.Location = new Point(D(36), D(194));
            bar.Size = new Size(W - D(72), D(8));

            cancel = Secondary("Cancel", "", D(120), D(30));
            cancel.Location = new Point((W - cancel.Width) / 2, D(226));
            cancel.Click += (_, _) => Cancel();

            Controls.AddRange(new Control[] { left, right, logo, status, bar, cancel });
            foreach (Control c in new Control[] { left, right, logo, status }) c.MouseDown += DragWindow;
            CloseOnEscape = true;

            Shown += async (_, _) => { Presence.Screen("Launching Roblox"); await RunAsync(); };
            FormClosing += (_, _) => { if (!finished) cts.Cancel(); };
        }

        void Cancel()
        {
            if (finished) return;
            cancel.Enabled = false;
            status.Text = L.T("Cancelling...");
            cts.Cancel();
        }

        void Report(string text, float progress)
        {
            if (finished || IsDisposed) return;
            status.Text = L.T(text);
            if (progress < 0) bar.Indeterminate = true;
            else
            {
                bar.Indeterminate = false;
                bar.Value = progress;
            }
        }

        async Task RunAsync()
        {
            logo.Spinning = true;
            bar.Indeterminate = true;
            try
            {
                Report("Checking Roblox's servers...", -1f);
                while (true)
                {
                    var health = await RobloxStatus.CheckAsync();
                    if (finished || IsDisposed) return;
                    if (health == RobloxStatus.Result.Offline)
                    {
                        if (Ask("You're offline", "GuideStrap can't reach the internet. Check your Wi-Fi or cable, then try again.", "Try again", "Cancel") == 0) continue;
                        Finish(false);
                        return;
                    }
                    if (health == RobloxStatus.Result.Down)
                    {
                        int choice = Ask("Roblox looks down", "Roblox's servers aren't answering right now, so games might not load. This is on Roblox's side, not your PC.",
                            "Try anyway", "Status page", "Cancel");
                        if (choice == 1) Process.Start(new ProcessStartInfo("https://status.roblox.com") { UseShellExecute = true });
                        if (choice != 0) { Finish(false); return; }
                    }
                    break;
                }

                // run the heavy work (download, unzip, file writes) off the UI thread so the animation stays smooth
                void SafeReport(string t, float p)
                {
                    if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(() => Report(t, p)));
                }
                var token = cts.Token;
                if (settings.MultiInstance) MultiInstance.Hold();
                var proc = await Task.Run(() => RobloxLauncher.LaunchAsync(settings, accounts, launchUri, placeText, SafeReport, token, jobId, pick));
                cancel.Enabled = false;
                if (proc == null)
                {
                    status.Text = L.T("Opened the game page in your browser");
                    await Task.Delay(1500);
                }
                else
                {
                    // keep the loading screen up until Roblox's window shows (max ~15s)
                    for (int i = 0; i < 60; i++)
                    {
                        await Task.Delay(250);
                        try
                        {
                            proc.Refresh();
                            if (proc.HasExited || proc.MainWindowHandle != IntPtr.Zero) break;
                        }
                        catch { break; }
                    }
                    CrashInfo? early = null;
                    try { if (proc.HasExited) early = CrashWatch.FromExitCode(unchecked((uint)proc.ExitCode)); }
                    catch { }
                    if (early != null)
                    {
                        if (settings.CrashHelper) context.ShowCrash(early);
                    }
                    else if (settings.RunInBackground || settings.MultiInstance)
                        context.WatchRoblox();
                }
                Finish(proc != null);
            }
            catch (OperationCanceledException)
            {
                Finish(false);
            }
            catch (Exception ex)
            {
                logo.Spinning = false;
                bar.Indeterminate = false;
                Ask("Couldn't launch Roblox", ex.Message, "OK");
                Finish(false);
            }
        }

        void Finish(bool launched)
        {
            if (finished) return;
            finished = true;
            logo.Spinning = false;
            bar.Indeterminate = false;
            // nothing else open and Roblox didn't start: go back to the start screen
            if (!launched && context.OpenCount <= 1) context.Open(new LauncherForm(context));
            Close();
        }
    }

    // The logo, doing a smooth full turn with a short pause in between.
    // Drawn once into a bitmap, then only that bitmap is rotated each frame (cheap and smooth).
    // The logo is drawn smaller than the box so its corners never get cut off while turning.
    public class SpinningLogo : Control
    {
        bool spinning;
        double time;
        float angle;
        Bitmap? cache;

        public SpinningLogo() => SetStyle(Theme.PaintStyles, true);

        public bool Spinning
        {
            get => spinning;
            set
            {
                if (spinning == value) return;
                spinning = value;
                if (value)
                {
                    time = 0;
                    Anim.Loop((this, "spin"), dt =>
                    {
                        time = (time + dt) % 1700;
                        double p = Math.Min(1, time / 1250);
                        double eased = p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;   // ease in-out
                        angle = (float)(360 * eased);
                        Invalidate();
                        Update();
                    });
                }
                else
                {
                    Anim.StopLoop((this, "spin"));
                    Anim.Run((this, "settle"), angle % 360, angle > 180 ? 360 : 0, 300, v => { angle = v; Invalidate(); });
                }
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            cache?.Dispose();
            cache = null;
        }

        protected override void Dispose(bool disposing)
        {
            Anim.StopLoop((this, "spin"));
            cache?.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            if (cache == null)
            {
                cache = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var cg = Graphics.FromImage(cache);
                cg.Clear(Color.Transparent);
                // tile diagonal must stay inside the box at every angle: 0.66 * 1.414 < 1
                Theme.DrawLogo(cg, new RectangleF(0, 0, Width, Height), 0f, 0.66f);
            }
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(Width / 2f, Height / 2f);
            g.RotateTransform(angle);
            g.DrawImage(cache, -Width / 2f, -Height / 2f, Width, Height);
        }
    }

    // Progress bar: a segment slides across while waiting, then it fills while installing
    public class LoadingBar : Control
    {
        bool indeterminate;
        float shown;
        double phase;

        public LoadingBar() => SetStyle(Theme.PaintStyles, true);

        public float Value
        {
            set
            {
                float target = Math.Clamp(value, 0f, 1f);
                Anim.Run((this, "fill"), shown, target, 250, v => { shown = v; Invalidate(); });
            }
        }

        public bool Indeterminate
        {
            get => indeterminate;
            set
            {
                if (indeterminate == value) return;
                indeterminate = value;
                if (value)
                {
                    phase = 0;
                    Anim.Loop((this, "marquee"), dt => { phase = (phase + dt / 1400.0) % 1.0; Invalidate(); });
                }
                else
                {
                    Anim.StopLoop((this, "marquee"));
                    Invalidate();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            Anim.StopLoop((this, "marquee"));
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float h = Height - 1, rad = h / 2;
            var track = new RectangleF(0, 0, Width - 1, h);
            using (var tp = Theme.Round(track, rad))
            using (var tb = new SolidBrush(Theme.ButtonBg))
                g.FillPath(tb, tp);

            using var fb = new SolidBrush(Theme.Accent);
            if (indeterminate)
            {
                float segW = Width * 0.3f;
                double p = phase < 0.5 ? 2 * phase * phase : 1 - Math.Pow(-2 * phase + 2, 2) / 2;   // ease in-out
                float x = (float)(-segW + (Width + segW) * p);
                using var clip = Theme.Round(track, rad);
                g.SetClip(clip);
                using var seg = Theme.Round(new RectangleF(x, 0, segW, h), rad);
                g.FillPath(fb, seg);
                g.ResetClip();
            }
            else if (shown > 0.003f)
            {
                using var fill = Theme.Round(new RectangleF(0, 0, Math.Max(h, (Width - 1) * shown), h), rad);
                g.FillPath(fb, fill);
            }
        }
    }

    // Big menu button with icon, text and a chevron that nudges on hover
    public class LauncherButton : Control
    {
        float hoverT;
        bool down;
        public string Icon { get; set; } = "";

        public LauncherButton()
        {
            SetStyle(Theme.PaintStyles, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10f);
            ForeColor = Theme.Text;
        }

        void AnimateHover(float to, int ms) => Anim.Run((this, "hover"), hoverT, to, ms, v => { hoverT = v; Invalidate(); });
        protected override void OnMouseEnter(EventArgs e) { AnimateHover(1f, 120); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { down = false; AnimateHover(0f, 220); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = Theme.Mix(Theme.Card, Theme.ButtonBg, hoverT);
            if (down) fill = Theme.Mix(fill, Color.Black, 0.2f);
            using var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(6));
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            using (var p = new Pen(Theme.Mix(Theme.Border, Theme.Track, hoverT * 0.6f))) g.DrawPath(p, path);

            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var iconFont = Theme.F(Theme.IconFont, 12f);
            var chevFont = Theme.F(Theme.IconFont, 8f);
            TextRenderer.DrawText(g, Icon, iconFont, new Rectangle(LogicalToDeviceUnits(16), 0, LogicalToDeviceUnits(28), Height), Theme.Text, flags);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(LogicalToDeviceUnits(52), 0, Width - LogicalToDeviceUnits(96), Height), ForeColor, flags);
            int cx = Width - LogicalToDeviceUnits(30) + (int)(LogicalToDeviceUnits(3) * hoverT);
            TextRenderer.DrawText(g, "", chevFont, new Rectangle(cx, 0, LogicalToDeviceUnits(20), Height),
                Theme.Mix(Theme.SubText, Theme.Text, hoverT), flags);
        }
    }

    // Small accent-colored link with an icon
    public class LinkText : Control
    {
        bool hover;
        public string Icon { get; set; } = "";

        public LinkText()
        {
            SetStyle(Theme.PaintStyles, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            var iconFont = Theme.F(Theme.IconFont, 9.5f);
            var a = Icon.Length > 0 ? TextRenderer.MeasureText(Icon, iconFont) : Size.Empty;
            var b = TextRenderer.MeasureText(Text, Font);
            Size = new Size(a.Width + b.Width + LogicalToDeviceUnits(2), Math.Max(a.Height, b.Height) + LogicalToDeviceUnits(4));
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.ClearToParent(this, g, Theme.Bg);
            Color c = hover ? Theme.Text : Theme.AccentLight;
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine;
            var iconFont = Theme.F(Theme.IconFont, 9.5f);
            int x = 0;
            if (Icon.Length > 0)
            {
                var a = TextRenderer.MeasureText(g, Icon, iconFont);
                TextRenderer.DrawText(g, Icon, iconFont, new Rectangle(0, 0, a.Width, Height), c, flags);
                x = a.Width;
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x, Height), c, flags);
        }
    }

    // Recently played games (saved list + cached icons)
    class RecentGame
    {
        public long PlaceId { get; set; }
        public string Name { get; set; } = "";
        public string Creator { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public DateTime LastPlayed { get; set; }
    }

    static class RecentGames
    {
        static string FilePath => Path.Combine(Paths.Base, "recent.json");
        public static int Version { get; private set; }

        public static List<RecentGame> Load()
        {
            try { return JsonSerializer.Deserialize<List<RecentGame>>(File.ReadAllText(FilePath)) ?? new(); }
            catch { return new(); }
        }

        public static void Add(RecentGame game)
        {
            var list = Load();
            list.RemoveAll(g => g.PlaceId == game.PlaceId);
            list.Insert(0, game);
            if (list.Count > 12) list.RemoveRange(12, list.Count - 12);
            try
            {
                Directory.CreateDirectory(Paths.Base);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
            }
            catch { }
            Version++;
        }

        public static async Task<Image?> GetIconAsync(RecentGame game)
        {
            string path = Path.Combine(Paths.Icons, game.PlaceId + ".png");
            try
            {
                if (!File.Exists(path))
                {
                    if (game.IconUrl.Length == 0) return null;
                    Directory.CreateDirectory(Paths.Icons);
                    await File.WriteAllBytesAsync(path, await RobloxApi.Http.GetByteArrayAsync(game.IconUrl));
                }
                using var fs = File.OpenRead(path);
                using var img = Image.FromStream(fs);
                return new Bitmap(img, 128, 128);
            }
            catch { return null; }
        }
    }

    // Cursor files: built-in presets are drawn in code, custom images are resized to 64x64
    enum ServerPick { None, Hop, Small }

    // Quick check before launching: are Roblox's servers answering, and is the internet working at all?
    static class RobloxStatus
    {
        public enum Result { Up, Down, Offline }

        static readonly HttpClient probe = new() { Timeout = TimeSpan.FromSeconds(5) };

        static async Task<bool> Ok(string url)
        {
            try
            {
                using var r = await probe.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                return (int)r.StatusCode < 500;
            }
            catch { return false; }
        }

        public static async Task<Result> CheckAsync()
        {
            var roblox = await Task.WhenAll(
                Ok("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer"),
                Ok("https://users.roblox.com/v1/users/1"),
                Ok("https://www.roblox.com/"));
            if (roblox.Count(x => !x) < 2) return Result.Up;
            var internet = await Task.WhenAll(Ok("https://www.google.com/generate_204"), Ok("https://www.cloudflare.com/cdn-cgi/trace"));
            return internet.Any(x => x) ? Result.Down : Result.Offline;
        }
    }

    record SessionSummary(TimeSpan Length, List<string> Games, string? Location);

    class FriendPresence
    {
        public long UserId { get; init; }
        public int Type { get; init; }          // 0 offline, 1 online, 2 in a game, 3 in Studio
        public string? Location { get; init; }
        public long? PlaceId { get; init; }
        public long? RootPlaceId { get; init; }
        public string? GameId { get; init; }    // the server, when the friend's privacy allows joining
    }

    class ServerInfo
    {
        public long PlaceId { get; set; }
        public string JobId { get; set; } = "";
        public DateTime Time { get; set; }
    }

    // The last server you were in (read from Roblox's log), for Rejoin and the crash helper
    static class LastServer
    {
        static string FilePath => Path.Combine(Paths.Base, "lastserver.json");

        public static void Save(long placeId, string jobId)
        {
            try
            {
                Directory.CreateDirectory(Paths.Base);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(new ServerInfo { PlaceId = placeId, JobId = jobId, Time = DateTime.Now }));
            }
            catch { }
        }

        public static ServerInfo? Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var s = JsonSerializer.Deserialize<ServerInfo>(File.ReadAllText(FilePath));
                return s != null && s.PlaceId > 0 && Guid.TryParse(s.JobId, out _) ? s : null;
            }
            catch { return null; }
        }
    }

    // Checks friends every 90 seconds (one small request) and tells the app when one starts playing
    static class FriendWatcher
    {
        static System.Windows.Forms.Timer? timer;
        static List<long> friendIds = new();
        static DateTime idsLoaded;
        static long idsFor;
        static readonly Dictionary<long, string> names = new();
        static Dictionary<long, (int Type, long Place)> last = new();
        static bool first = true, busy;
        public static event Action<FriendPresence, string>? FriendStartedPlaying;

        static Mutex? owner;

        public static void Start()
        {
            if (timer != null) return;
            try
            {
                owner = new Mutex(true, @"Local\GuideStrapFriendWatcher", out bool mine);
                if (!mine) { owner.Dispose(); owner = null; return; }
            }
            catch { owner = null; }
            // first check after 15 seconds, then every 90 (ticks run on the UI thread)
            timer = new System.Windows.Forms.Timer { Interval = 15_000 };
            timer.Tick += async (_, _) =>
            {
                if (timer != null) timer.Interval = 90_000;
                await PollAsync();
            };
            timer.Start();
        }

        public static void Stop()
        {
            timer?.Stop();
            timer?.Dispose();
            timer = null;
            try { owner?.ReleaseMutex(); } catch { }
            owner?.Dispose();
            owner = null;
        }

        static async Task PollAsync()
        {
            if (busy) return;
            busy = true;
            try
            {
                var s = AppSettings.Load();
                var acc = s.FriendAlerts && s.ActiveAccountId != null
                    ? AccountStore.Load().FirstOrDefault(a => a.UserId == s.ActiveAccountId) : null;
                if (acc == null)
                {
                    last.Clear();
                    first = true;
                    return;
                }
                if (idsFor != acc.UserId)
                {
                    idsFor = acc.UserId;
                    friendIds.Clear();
                    last.Clear();
                    first = true;
                }
                if (friendIds.Count == 0 || DateTime.Now - idsLoaded > TimeSpan.FromMinutes(10))
                {
                    friendIds = await RobloxApi.GetFriendIdsAsync(acc.UserId, acc.Cookie);
                    idsLoaded = DateTime.Now;
                }
                if (friendIds.Count == 0) return;

                var presence = await RobloxApi.GetPresenceAsync(acc.Cookie, friendIds);
                var now = new Dictionary<long, (int Type, long Place)>();
                var started = new List<FriendPresence>();
                foreach (var f in presence)
                {
                    long place = f.RootPlaceId ?? f.PlaceId ?? 0;
                    now[f.UserId] = (f.Type, place);
                    if (first || f.Type != 2) continue;
                    bool same = last.TryGetValue(f.UserId, out var before) && before.Type == 2 && before.Place == place;
                    if (!same) started.Add(f);
                }
                last = now;
                first = false;
                if (started.Count == 0) return;
                await RobloxApi.FillNamesAsync(started.Select(f => f.UserId), names);
                var pick = started[0];
                FriendStartedPlaying?.Invoke(pick, names.GetValueOrDefault(pick.UserId, L.T("A friend")));
            }
            catch { }
            finally { busy = false; }
        }
    }

    // Gives memory back to Windows when GuideStrap is only sitting in the tray
    static class MemoryTrim
    {
        [DllImport("psapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EmptyWorkingSet(IntPtr process);

        public static void Now()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using var me = Process.GetCurrentProcess();
                EmptyWorkingSet(me.Handle);
            }
            catch { }
        }
    }

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
                BaseColor = Color.Transparent, HoverColor = Theme.NavHover, ForeColor = Theme.SubText, Font = new Font("Segoe UI", 7f),
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

    class GalleryItem
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string? Preview { get; set; }
    }

    class GalleryIndex
    {
        public List<GalleryItem> Items { get; set; } = new();
    }

    // MOD GALLERY: a list kept in GuideStrap's GitHub repo, installed with one click
    class GalleryForm : ThemedForm
    {
        const long MaxBytes = 60_000_000;
        readonly Action<Action<AppSettings>> apply;
        readonly Label status;
        readonly Panel list;
        readonly List<Image> images = new();

        public GalleryForm(Action<Action<AppSettings>> apply)
        {
            this.apply = apply;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CloseOnEscape = true;
            Text = L.T("Mod gallery");

            int w = D(580), pad = D(20);
            var bar = BuildTitleBar("Mod gallery", false);
            int y = D(36) + D(8);
            Controls.Add(At(Lbl("Mod gallery", 13f, semibold: true), pad, y));
            y += D(30);
            status = Lbl("Loading the gallery...", 9f, Theme.SubText);
            status.Location = new Point(pad, y);
            Controls.Add(status);
            y += D(28);
            list = new Panel { Location = new Point(pad, y), Size = new Size(w - pad * 2, D(390)), AutoScroll = true, BackColor = Theme.Bg };
            Controls.Add(list);
            y += list.Height + D(10);
            var note = Lbl("Only cosmetic files: images, skies and sounds. Everything installs into GuideStrap's copy of Roblox.", 8.5f, Theme.SubText);
            note.AutoSize = false;
            note.Size = new Size(w - pad * 2, D(34));
            note.Location = new Point(pad, y);
            Controls.Add(note);
            y += D(34);
            Controls.Add(bar);
            ClientSize = new Size(w, y + pad);
            Shown += async (_, _) => await LoadAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var i in images) i.Dispose();
            base.Dispose(disposing);
        }

        static string TypeName(string type) => type switch
        {
            "sky" => "Sky",
            "cursor" => "Cursor",
            "crosshair" => "Crosshair",
            _ => "Sound pack",
        };

        async Task LoadAsync()
        {
            try
            {
                string json = await RobloxApi.Http.GetStringAsync(AppInfo.GalleryUrl);
                var index = JsonSerializer.Deserialize<GalleryIndex>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var items = (index?.Items ?? new())
                    .Where(i => i.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    .Where(i => i.Type.ToLowerInvariant() is "sky" or "cursor" or "crosshair" or "soundpack")
                    .ToList();
                if (IsDisposed) return;
                status.Text = items.Count == 0 ? L.T("The gallery is empty right now.") : L.F("{0} mods", items.Count);
                int cardW = list.Width - SystemInformation.VerticalScrollBarWidth - D(4);
                int ly = 0;
                foreach (var item in items)
                {
                    var card = ItemCard(item, cardW);
                    card.Location = new Point(0, ly);
                    list.Controls.Add(card);
                    ly += card.Height + D(8);
                }
            }
            catch
            {
                if (!IsDisposed) status.Text = L.T("Couldn't load the gallery. Check your internet and try again.");
            }
        }

        Card ItemCard(GalleryItem item, int width)
        {
            string type = item.Type.ToLowerInvariant();
            var card = new Card { Size = new Size(width, D(80)) };
            var pic = new PictureBox
            {
                Size = new Size(D(52), D(52)), Location = new Point(D(14), D(14)),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Input,
            };
            var name = At(Lbl(item.Name, 10f, semibold: true), D(80), D(10));
            name.UseMnemonic = false;
            var sub = At(Lbl(L.T(TypeName(type)) + (item.Author.Length > 0 ? "  \u00B7  " + L.F("by {0}", item.Author) : ""), 8.5f, Theme.AccentLight), D(80), D(33));
            var desc = Lbl(item.Description, 8.5f, Theme.SubText);
            desc.AutoSize = false;
            desc.AutoEllipsis = true;
            desc.Size = new Size(width - D(80) - D(140), D(18));
            desc.Location = new Point(D(80), D(53));
            var install = Primary("Install", "\uE896", D(110), D(30));
            install.Location = new Point(width - D(126), D(25));
            install.Click += async (_, _) =>
            {
                install.Enabled = false;
                install.Text = L.T("Installing...");
                try
                {
                    await InstallAsync(item, type);
                    install.Text = L.T("Installed");
                }
                catch (Exception ex)
                {
                    install.Text = L.T("Install");
                    install.Enabled = true;
                    Ask("Couldn't install that", ex.Message, "OK");
                }
            };
            card.Controls.AddRange(new Control[] { pic, name, sub, desc, install });

            string? previewUrl = item.Preview ?? (type is "cursor" or "crosshair" ? item.Url : null);
            if (previewUrl != null && previewUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                _ = LoadPreviewAsync(pic, previewUrl);
            return card;
        }

        async Task LoadPreviewAsync(PictureBox pic, string url)
        {
            try
            {
                byte[] data = await RobloxApi.Http.GetByteArrayAsync(url);
                if (data.Length > 4_000_000) return;
                using var ms = new MemoryStream(data);
                using var img = Image.FromStream(ms);
                var copy = new Bitmap(img);
                if (pic.IsDisposed) { copy.Dispose(); return; }
                images.Add(copy);
                pic.Image = copy;
            }
            catch { }
        }

        static async Task<byte[]> DownloadAsync(string url)
        {
            using var resp = await RobloxApi.Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength > MaxBytes) throw new Exception(L.T("That mod is too big."));
            byte[] data = await resp.Content.ReadAsByteArrayAsync();
            if (data.Length > MaxBytes) throw new Exception(L.T("That mod is too big."));
            return data;
        }

        // Files in a zip with the given extensions. A single top folder is removed, so "Pack/ouch.ogg" becomes "ouch.ogg".
        static List<(string Path, byte[] Data)> ReadZip(byte[] zipData, params string[] extensions)
        {
            var files = new List<(string Path, byte[] Data)>();
            using (var zip = new ZipArchive(new MemoryStream(zipData)))
            {
                foreach (var entry in zip.Entries)
                {
                    string rel = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (rel.Length == 0 || rel.EndsWith('/') || rel.Contains("..")) continue;
                    if (!extensions.Any(e => rel.EndsWith(e, StringComparison.OrdinalIgnoreCase))) continue;
                    if (entry.Length > 30_000_000) continue;
                    using var es = entry.Open();
                    using var ms = new MemoryStream();
                    es.CopyTo(ms);
                    files.Add((rel, ms.ToArray()));
                }
            }
            if (files.Count > 0 && files.All(f => f.Path.Contains('/')))
            {
                string top = files[0].Path[..files[0].Path.IndexOf('/')];
                if (files.All(f => f.Path.StartsWith(top + "/", StringComparison.OrdinalIgnoreCase)))
                    files = files.Select(f => (f.Path[(top.Length + 1)..], f.Data)).ToList();
            }
            return files;
        }

        static string SafeName(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            string clean = new string(name.Where(c => !bad.Contains(c)).ToArray()).Trim().TrimEnd('.');
            if (clean.Length > 40) clean = clean[..40];
            return clean.Length == 0 ? "Pack" : clean;
        }

        async Task InstallAsync(GalleryItem item, string type)
        {
            byte[] data = await DownloadAsync(item.Url);
            switch (type)
            {
                case "cursor":
                case "crosshair":
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "guidestrap-" + Guid.NewGuid().ToString("N") + ".png");
                    await File.WriteAllBytesAsync(tmp, data);
                    try
                    {
                        bool ok = type == "cursor" ? ModAssets.ImportCustomCursor(tmp) : ModAssets.ImportCustomCrosshair(tmp);
                        if (!ok) throw new Exception(L.T("That image couldn't be read."));
                    }
                    finally { try { File.Delete(tmp); } catch { } }
                    if (type == "cursor") apply(s => s.CursorPreset = "custom");
                    else apply(s => s.CrosshairPreset = "custom");
                    break;
                }
                case "sky":
                {
                    var tex = ReadZip(data, ".tex");
                    if (tex.Count == 0) throw new Exception(L.T("That sky pack has no .tex files."));
                    if (Directory.Exists(Paths.Sky)) Directory.Delete(Paths.Sky, true);
                    Directory.CreateDirectory(Paths.Sky);
                    foreach (var (path, bytes) in tex)
                        await File.WriteAllBytesAsync(Path.Combine(Paths.Sky, Path.GetFileName(path)), bytes);
                    apply(s => s.CustomSky = true);
                    break;
                }
                default:
                {
                    var sounds = ReadZip(data, ".ogg", ".mp3");
                    if (sounds.Count == 0) throw new Exception(L.T("That sound pack has no .ogg or .mp3 files."));
                    string name = SafeName(item.Name);
                    string dir = Path.Combine(Paths.SoundPacks, name);
                    string root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    foreach (var (path, bytes) in sounds)
                    {
                        string dest = Path.GetFullPath(Path.Combine(dir, path));
                        if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        await File.WriteAllBytesAsync(dest, bytes);
                    }
                    apply(s => s.SoundPack = name);
                    break;
                }
            }
        }
    }

    class CrashInfo
    {
        public uint Code { get; init; }
        public bool WhileStarting { get; init; }
        public string? Clue { get; init; }   // "gpu" or "memory", read from Roblox's log
    }

    // Notices when Roblox closes with an error code instead of closing normally.
    // Keeps a lightweight handle to each Roblox process so the exit code can be read after it closes.
    static class CrashWatch
    {
        const uint QueryLimited = 0x1000, StillActive = 259;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetExitCodeProcess(IntPtr process, out uint code);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr handle);

        static readonly Dictionary<int, (IntPtr Handle, DateTime Seen)> tracked = new();
        static readonly List<(uint Code, bool Early)> exits = new();
        static readonly Queue<string> lines = new();

        // Normal closes, leaving a game and Task Manager use small codes. Real crashes use Windows error codes.
        // 0xFFFFFFFF (-1) is what tools use when they force-close a program, so it doesn't count.
        public static bool IsCrash(uint code) => code >= 0x80000000 && code != 0xFFFFFFFF;

        public static void Begin()
        {
            CloseAll();
            exits.Clear();
            lines.Clear();
        }

        public static void Line(string line)
        {
            lines.Enqueue(line);
            while (lines.Count > 80) lines.Dequeue();
        }

        public static void Poll()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
                {
                    using (p)
                    {
                        if (tracked.ContainsKey(p.Id)) continue;
                        IntPtr h = OpenProcess(QueryLimited, false, p.Id);
                        if (h != IntPtr.Zero) tracked[p.Id] = (h, DateTime.Now);
                    }
                }
                foreach (var kv in tracked.ToList())
                {
                    var (handle, seen) = kv.Value;
                    bool ok = GetExitCodeProcess(handle, out uint code);
                    if (ok && code == StillActive) continue;
                    if (ok) exits.Add((code, DateTime.Now - seen < TimeSpan.FromSeconds(25)));
                    CloseHandle(handle);
                    tracked.Remove(kv.Key);
                }
            }
            catch { }
        }

        public static CrashInfo? End(bool report)
        {
            if (report) Poll();
            CloseAll();
            if (!report) return null;
            var crashes = exits.Where(e => IsCrash(e.Code)).ToList();
            if (crashes.Count == 0) return null;
            var last = crashes[^1];
            return new CrashInfo { Code = last.Code, WhileStarting = last.Early, Clue = Clue() };
        }

        // For a crash right after launch, before the watcher started
        public static CrashInfo? FromExitCode(uint code) =>
            IsCrash(code) ? new CrashInfo { Code = code, WhileStarting = true } : null;

        static void CloseAll()
        {
            foreach (var t in tracked.Values) CloseHandle(t.Handle);
            tracked.Clear();
        }

        static string? Clue()
        {
            string text = string.Join("\n", lines).ToLowerInvariant();
            if (text.Contains("device_removed") || text.Contains("device removed") || text.Contains("device lost") || text.Contains("device_lost"))
                return "gpu";
            if (text.Contains("out of memory") || text.Contains("outofmemory"))
                return "memory";
            return null;
        }
    }

    // "Roblox crashed" window: the error, then fixes in order, with one-click buttons where possible
    class CrashForm : ThemedForm
    {
        sealed record Fix(string Title, string Desc, string? Button, Func<string?>? Run);

        public CrashForm(CrashInfo info)
        {
            StartPosition = FormStartPosition.CenterScreen;
            CloseOnEscape = true;
            Text = L.T("Roblox crashed");

            int w = D(480), pad = D(20);
            var bar = BuildTitleBar("Roblox crashed", false);
            int y = D(36) + D(8);
            Controls.Add(At(Lbl(info.WhileStarting ? "Roblox crashed while starting" : "Roblox crashed", 13f, semibold: true), pad, y));
            y += D(32);
            Controls.Add(At(Lbl(L.F("Error {0}: {1}", $"0x{info.Code:X8}", L.T(CodeName(info.Code))), 9f, Theme.SubText), pad, y));
            y += D(22);
            Controls.Add(At(Lbl("Try these fixes, starting with the first one.", 9f, Theme.SubText), pad, y));
            y += D(30);

            var fixes = BuildFixes(info);
            var list = new Panel { Location = new Point(pad, y), Width = w - pad * 2, AutoScroll = true, BackColor = Theme.Bg };
            int total = fixes.Sum(f => { using var probe = FixCard(f, list.Width); return probe.Height + D(8); });
            int cardW = total > D(400) ? list.Width - SystemInformation.VerticalScrollBarWidth - D(4) : list.Width;
            int ly = 0;
            foreach (var fix in fixes)
            {
                var card = FixCard(fix, cardW);
                card.Location = new Point(0, ly);
                list.Controls.Add(card);
                ly += card.Height + D(8);
            }
            list.Height = Math.Min(ly, D(400));
            Controls.Add(list);
            y += list.Height + D(8);

            var logs = Secondary("Open Roblox logs", "\uE8B7", D(160), D(30));
            logs.Location = new Point(pad, y);
            logs.Click += (_, _) =>
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
                if (Directory.Exists(dir)) Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
            };
            var close = Primary("Close", "", D(96), D(30));
            close.Location = new Point(w - pad - D(96), y);
            close.Click += (_, _) => Close();
            Controls.Add(logs);
            Controls.Add(close);
            y += D(30) + pad;

            Controls.Add(bar);
            ClientSize = new Size(w, y);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        static string CodeName(uint code) => code switch
        {
            0xC0000005 => "memory access error",
            0xC0000409 => "Roblox stopped itself (security check)",
            0xC00000FD => "stack overflow",
            0xC0000374 => "memory corruption",
            0xC0000017 or 0xC000012D => "out of memory",
            0xE06D7363 => "unhandled error",
            0x887A0005 or 0x887A0006 or 0x887A0007 => "the graphics driver stopped responding",
            0x80000003 => "internal error",
            _ => "unknown error",
        };

        Card FixCard(Fix fix, int width)
        {
            var card = new Card { Size = new Size(width, D(60)) };
            int btnW = 0;
            if (fix.Button != null)
                btnW = Math.Max(D(96), TextRenderer.MeasureText(L.T(fix.Button), Theme.F("Segoe UI Semibold", 9f)).Width + D(30));
            int textW = width - D(32) - (btnW > 0 ? btnW + D(12) : 0);

            var title = Lbl(fix.Title, 9.75f, semibold: true);
            title.Location = new Point(D(16), D(10));
            var desc = Lbl(fix.Desc, 8.75f, Theme.SubText);
            desc.AutoSize = false;
            var size = TextRenderer.MeasureText(desc.Text, desc.Font, new Size(textW, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            desc.Size = new Size(textW, size.Height + D(2));
            desc.Location = new Point(D(16), D(32));
            card.Height = Math.Max(D(60), desc.Bottom + D(12));
            card.Controls.Add(title);
            card.Controls.Add(desc);

            if (fix.Button != null && fix.Run != null)
            {
                var b = Secondary(fix.Button, "", btnW, D(30));
                b.Location = new Point(width - D(16) - btnW, (card.Height - D(30)) / 2);
                b.Click += (_, _) =>
                {
                    string? result = fix.Run();
                    if (result == null) return;
                    b.Text = L.T(result);
                    b.Enabled = false;
                };
                card.Controls.Add(b);
            }
            return card;
        }

        static void ChangeSettings(Action<AppSettings> change)
        {
            var open = Application.OpenForms.OfType<Form1>().FirstOrDefault();
            if (open != null) { open.ChangeSettings(change); return; }
            var s = AppSettings.Load();
            change(s);
            s.Save();
        }

        static bool HasMods(AppSettings s)
        {
            if (s.DeathSoundFile != null || s.FontFile != null || s.CustomSky || s.SoundPack != null ||
                s.CursorPreset != "default" || s.CrosshairPreset != "default" || s.FootstepSound != "default" || s.JumpSound != "default")
                return true;
            try { return Directory.Exists(Paths.Mods) && Directory.EnumerateFiles(Paths.Mods, "*", SearchOption.AllDirectories).Any(); }
            catch { return false; }
        }

        List<Fix> BuildFixes(CrashInfo info)
        {
            var s = AppSettings.Load();
            var fixes = new List<Fix>();
            uint c = info.Code;
            bool gpu = info.Clue == "gpu" || c is 0x887A0005 or 0x887A0006 or 0x887A0007;
            bool memory = info.Clue == "memory" || c is 0xC0000017 or 0xC000012D;
            bool security = c == 0xC0000409;
            var last = LastServer.Load();
            if (last != null && DateTime.Now - last.Time < TimeSpan.FromMinutes(20) && GuideStrapContext.Current is GuideStrapContext ctx)
                fixes.Add(new Fix("Rejoin your server", "Jump straight back into the server you were in, if it's still running.", "Rejoin", () =>
                {
                    ctx.Open(new LoadingForm(ctx, AppSettings.Load(), AccountStore.Load(), null, last.PlaceId.ToString(), last.JobId));
                    Close();
                    return null;
                }));
            var overlays = new Fix("Close overlays and macro tools",
                "Roblox closes itself when another program hooks into it, like FPS unlockers, macros, cheats or some recording overlays.", null, null);

            if (gpu || c == 0xC0000005)
            {
                bool onD3D = s.Flags.TryGetValue(FF.D3D11, out var v) && v.Equals("True", StringComparison.OrdinalIgnoreCase);
                fixes.Add(new Fix("Switch the rendering API",
                    onD3D ? "Your graphics driver may not like Direct3D 11. Try Vulkan next time." : "Your graphics driver may not like the current one. Try Direct3D 11 next time.",
                    onD3D ? "Use Vulkan" : "Use D3D11", () =>
                    {
                        ChangeSettings(x =>
                        {
                            x.Flags.Remove(FF.D3D11);
                            x.Flags.Remove(FF.Vulkan);
                            x.Flags.Remove(FF.OpenGL);
                            x.Flags[onD3D ? FF.Vulkan : FF.D3D11] = "True";
                            x.Preset = -1;
                        });
                        return "Done";
                    }));
            }
            if (memory)
                fixes.Add(new Fix("Free up memory", "Close browser tabs and other games, and turn on Potato mode in Graphics.", null, null));
            if (security)
                fixes.Add(overlays);
            if (HasMods(s))
                fixes.Add(new Fix("Launch once without mods",
                    "A mod or custom font can break after a Roblox update. Your mods stay saved and come back the launch after.", "Skip mods", () =>
                    {
                        try
                        {
                            Directory.CreateDirectory(Paths.Base);
                            File.WriteAllText(Paths.SkipModsMarker, "");
                            return "Done";
                        }
                        catch { return null; }
                    }));
            if (s.Flags.Count > 0)
                fixes.Add(new Fix("Reset FastFlags",
                    "A flag can stop working after a Roblox update. This clears your flags and sets Graphics back to Balanced.", "Reset flags", () =>
                    {
                        ChangeSettings(x => { x.Flags.Clear(); x.Preset = 2; });
                        return "Done";
                    }));
            if (s.MultiInstance)
                fixes.Add(new Fix("Turn off multiple Roblox windows", "Running more than one Roblox at once can make it crash.", "Turn off", () =>
                {
                    ChangeSettings(x => x.MultiInstance = false);
                    return "Done";
                }));
            fixes.Add(new Fix("Update your graphics driver",
                "Get the newest driver from NVIDIA, AMD or Intel. Old drivers are the most common cause of crashes.", null, null));
            fixes.Add(new Fix("Repair Roblox",
                "Downloads a fresh copy of Roblox next time you launch. Fixes broken or half-updated files.", "Repair", () =>
                {
                    try
                    {
                        if (Directory.Exists(Paths.Versions)) Directory.Delete(Paths.Versions, true);
                        return "Done";
                    }
                    catch
                    {
                        Ask("Close Roblox first", "Roblox is still using its files. Close every Roblox window, then press Repair again.", "OK");
                        return null;
                    }
                }));
            if (!security)
                fixes.Add(overlays);
            return fixes;
        }
    }

    // Windows-side and Roblox-settings tweaks used at launch
    static class RobloxTweaks
    {
        static bool RobloxRunning()
        {
            var procs = Process.GetProcessesByName("RobloxPlayerBeta");
            foreach (var p in procs) p.Dispose();
            return procs.Length > 0;
        }

        // Roblox's own max frame rate setting (the same one as in its menu). Only written while Roblox is
        // closed, because Roblox saves this file when it exits and would overwrite the change.
        public static void SetFpsCap(int fps)
        {
            try
            {
                if (RobloxRunning()) return;
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");
                if (!Directory.Exists(dir)) return;
                string? file = Directory.GetFiles(dir, "GlobalBasicSettings_*.xml")
                    .Where(f => !Path.GetFileName(f).Contains("Studio", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .FirstOrDefault();
                if (file == null) return;
                string xml = File.ReadAllText(file);
                var re = new Regex(@"(<int name=""FramerateCap"">)-?\d+(</int>)");
                if (!re.IsMatch(xml)) return;
                string updated = re.Replace(xml, m => m.Groups[1].Value + fps + m.Groups[2].Value, 1);
                if (updated != xml) File.WriteAllText(file, updated);
            }
            catch { }
        }

        // Gives Roblox more CPU time. Checks for a little while because Roblox can take a moment to start.
        public static void BoostPriority(ProcessPriorityClass level)
        {
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 15; i++)
                {
                    await Task.Delay(2000);
                    foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
                    {
                        using (p)
                        {
                            try { if (!p.HasExited && p.PriorityClass != level) p.PriorityClass = level; }
                            catch { }
                        }
                    }
                }
            });
        }
    }

    // Shows a made-up brick wall and grass tile, normal vs blurry textures
    class TexturePreviewForm : ThemedForm
    {
        readonly List<Bitmap> images = new();

        public TexturePreviewForm()
        {
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CloseOnEscape = true;
            Text = L.T("Texture preview");

            int tile = D(150), pad = D(20), gap = D(12);
            var bar = BuildTitleBar("Texture preview", false);
            int y = D(36) + D(8);
            string[] cols = { "Normal", "Blurry textures" };
            for (int c = 0; c < 2; c++)
                Controls.Add(At(Lbl(cols[c], 9f, Theme.SubText), pad + c * (tile + gap), y));
            y += D(22);

            using var brick = MakeBrick(256);
            using var grass = MakeGrass(256);
            foreach (var src in new[] { brick, grass })
            {
                for (int c = 0; c < 2; c++)
                {
                    Bitmap img;
                    if (c == 0) img = ScaleTo(src, tile);
                    else
                    {
                        using var small = ScaleTo(src, 12);
                        img = ScaleTo(small, tile);
                    }
                    images.Add(img);
                    Controls.Add(new PictureBox
                    {
                        Image = img, Size = new Size(tile, tile), Location = new Point(pad + c * (tile + gap), y),
                        SizeMode = PictureBoxSizeMode.Normal, BackColor = Theme.Card,
                    });
                }
                y += tile + gap;
            }

            var note = Lbl("Made-up example. In real games the blur looks the same, the textures just differ.", 8.5f, Theme.SubText);
            note.AutoSize = false;
            note.Size = new Size(tile * 2 + gap, D(34));
            note.Location = new Point(pad, y);
            Controls.Add(note);
            y += D(34);

            Controls.Add(bar);
            ClientSize = new Size(pad * 2 + tile * 2 + gap, y + pad);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var b in images) b.Dispose();
            base.Dispose(disposing);
        }

        static Bitmap ScaleTo(Bitmap src, int size)
        {
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            using var attr = new System.Drawing.Imaging.ImageAttributes();
            attr.SetWrapMode(WrapMode.TileFlipXY);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, new Rectangle(0, 0, size, size), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
            return bmp;
        }

        static Bitmap MakeBrick(int s)
        {
            var bmp = new Bitmap(s, s);
            var r = new Random(7);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(185, 178, 166));
            int bh = s / 8, bw = s / 4;
            using var dot = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
            for (int y = 0; y < s; y += bh)
            {
                int off = (y / bh) % 2 == 1 ? bw / 2 : 0;
                for (int x = -bw; x < s + bw; x += bw)
                {
                    int v = r.Next(-20, 21);
                    using (var b = new SolidBrush(Color.FromArgb(150 + v, 68 + v / 2, 52 + v / 2)))
                        g.FillRectangle(b, x + off + 3, y + 3, bw - 6, bh - 6);
                    for (int i = 0; i < 40; i++)
                        g.FillRectangle(dot, x + off + 3 + r.Next(0, bw - 8), y + 3 + r.Next(0, bh - 8), 2, 2);
                }
            }
            return bmp;
        }

        static Bitmap MakeGrass(int s)
        {
            var bmp = new Bitmap(s, s);
            var r = new Random(3);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(79, 138, 46));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < 1800; i++)
            {
                float x = (float)r.NextDouble() * s, y = (float)r.NextDouble() * s, len = 4 + (float)r.NextDouble() * 9;
                int v = r.Next(-30, 31);
                using var p = new Pen(Color.FromArgb(70 + v, 140 + v, 45 + v / 2), 1.5f);
                g.DrawLine(p, x, y, x + ((float)r.NextDouble() - 0.5f) * 4, y - len);
            }
            return bmp;
        }
    }

    static class ModAssets
    {
        public static string? CursorDir(AppSettings s) => s.CursorPreset switch
        {
            "guidestrap" or "dot" or "cross" or "circle" or "classic" => EnsurePreset(s.CursorPreset),
            "custom" => File.Exists(Path.Combine(Paths.Managed, "cursor-custom", "ArrowCursor.png")) ? Path.Combine(Paths.Managed, "cursor-custom") : null,
            _ => null,
        };

        static string? EnsurePreset(string name)
        {
            string dir = Path.Combine(Paths.Managed, "cursor-" + name);
            string file = Path.Combine(dir, "ArrowCursor.png");
            try
            {
                if (!File.Exists(file))
                {
                    Directory.CreateDirectory(dir);
                    using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        if (name is "dot" or "cross" or "circle")
                        {
                            DrawMark(g, name);
                        }
                        else if (name == "classic")
                        {
                            var pts = new[] { new PointF(0, 0), new PointF(0, 19), new PointF(5, 14.5f), new PointF(8.5f, 22),
                                              new PointF(12, 20.5f), new PointF(8.5f, 13), new PointF(14.5f, 13) };
                            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(pts[i].X + 32, pts[i].Y + 32);
                            using var fill = new SolidBrush(Color.White);
                            g.FillPolygon(fill, pts);
                            using var pen = new Pen(Color.Black, 1.6f) { LineJoin = LineJoin.Round };
                            g.DrawPolygon(pen, pts);
                        }
                        else
                        {
                            // Roblox centers cursors, so the arrow tip sits in the middle of the image
                            var pts = new[] { new PointF(0, 0), new PointF(0, 19), new PointF(5, 14.5f), new PointF(8.5f, 22),
                                              new PointF(12, 20.5f), new PointF(8.5f, 13), new PointF(14.5f, 13) };
                            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(pts[i].X + 32, pts[i].Y + 32);
                            using var fill = new SolidBrush(Color.FromArgb(124, 72, 232));
                            g.FillPolygon(fill, pts);
                            using var pen = new Pen(Color.White, 1.6f) { LineJoin = LineJoin.Round };
                            g.DrawPolygon(pen, pts);
                        }
                    }
                    bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                    File.Copy(file, Path.Combine(dir, "ArrowFarCursor.png"), true);
                }
                return dir;
            }
            catch { return null; }
        }

        // Small marks drawn white with a dark outline, centered in a 64x64 image (Roblox centers these)
        static void DrawMark(Graphics g, string name)
        {
            const float c = 32f;
            using var outline = new Pen(Color.FromArgb(220, 0, 0, 0), 3.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var white = new Pen(Color.White, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            switch (name)
            {
                case "dot":
                    using (var o = new SolidBrush(Color.FromArgb(220, 0, 0, 0))) g.FillEllipse(o, c - 5f, c - 5f, 10, 10);
                    using (var f = new SolidBrush(Color.White)) g.FillEllipse(f, c - 3.5f, c - 3.5f, 7, 7);
                    break;
                case "cross":
                    foreach (var pen in new[] { outline, white })
                    {
                        g.DrawLine(pen, c - 9, c, c - 3, c);
                        g.DrawLine(pen, c + 3, c, c + 9, c);
                        g.DrawLine(pen, c, c - 9, c, c - 3);
                        g.DrawLine(pen, c, c + 3, c, c + 9);
                    }
                    break;
                case "circle":
                    g.DrawEllipse(outline, c - 7, c - 7, 14, 14);
                    g.DrawEllipse(white, c - 7, c - 7, 14, 14);
                    break;
            }
        }

        // Shift lock crosshair: content\textures\MouseLockedCursor.png
        public static string? CrosshairFile(AppSettings s)
        {
            if (s.CrosshairPreset == "custom")
            {
                string custom = Path.Combine(Paths.Managed, "crosshair-custom", "MouseLockedCursor.png");
                return File.Exists(custom) ? custom : null;
            }
            if (s.CrosshairPreset is not ("dot" or "cross" or "circle")) return null;
            string dir = Path.Combine(Paths.Managed, "crosshair-" + s.CrosshairPreset);
            string file = Path.Combine(dir, "MouseLockedCursor.png");
            try
            {
                if (!File.Exists(file))
                {
                    Directory.CreateDirectory(dir);
                    using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        DrawMark(g, s.CrosshairPreset);
                    }
                    bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                }
                return file;
            }
            catch { return null; }
        }

        public static bool ImportCustomCrosshair(string source)
        {
            try
            {
                string dir = Path.Combine(Paths.Managed, "crosshair-custom");
                Directory.CreateDirectory(dir);
                using var src = Image.FromFile(source);
                using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Min(1f, Math.Min(64f / src.Width, 64f / src.Height));
                    float w = src.Width * scale, h = src.Height * scale;
                    g.DrawImage(src, (64 - w) / 2, (64 - h) / 2, w, h);
                }
                bmp.Save(Path.Combine(dir, "MouseLockedCursor.png"), System.Drawing.Imaging.ImageFormat.Png);
                return true;
            }
            catch { return false; }
        }

        // Character sounds: muted = a tiny silent mp3, custom = the user's own file
        public static readonly string SoundsDir = Path.Combine(Paths.Managed, "sounds");

        const string SilentMp3 = "//sQxAADwAABpAAAACAAADSAAAAETEFNRTMuMTAwVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+xLEKYPAAAGkAAAAIAAANIAAAARVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+xDEU4PAAAGkAAAAIAAANIAAAARVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVf/7EsR9A8AAAaQAAAAgAAA0gAAABFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVf/7EMSnA8AAAaQAAAAgAAA0gAAABFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV";

        public static string? SoundFile(string mode, string kind)
        {
            try
            {
                if (mode == "mute")
                {
                    string silent = Path.Combine(SoundsDir, "silent.mp3");
                    if (!File.Exists(silent))
                    {
                        Directory.CreateDirectory(SoundsDir);
                        File.WriteAllBytes(silent, Convert.FromBase64String(SilentMp3));
                    }
                    return silent;
                }
                if (mode == "custom")
                {
                    string file = Path.Combine(SoundsDir, kind + ".mp3");
                    return File.Exists(file) ? file : null;
                }
            }
            catch { }
            return null;
        }

        // Sound packs: one folder per pack in GuideStrap\SoundPacks, files go into content\sounds
        public static List<string> SoundPacks()
        {
            try
            {
                if (!Directory.Exists(Paths.SoundPacks)) return new();
                return Directory.GetDirectories(Paths.SoundPacks).Select(d => Path.GetFileName(d))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch { return new(); }
        }

        public static string? SoundPackDir(AppSettings s)
        {
            if (string.IsNullOrEmpty(s.SoundPack) || s.SoundPack.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
            string dir = Path.Combine(Paths.SoundPacks, s.SoundPack);
            return Directory.Exists(dir) ? dir : null;
        }

        public static bool ImportCustomCursor(string source)
        {
            try
            {
                string dir = Path.Combine(Paths.Managed, "cursor-custom");
                Directory.CreateDirectory(dir);
                using var src = Image.FromFile(source);
                using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Min(1f, Math.Min(64f / src.Width, 64f / src.Height));
                    float w = src.Width * scale, h = src.Height * scale;
                    g.DrawImage(src, (64 - w) / 2, (64 - h) / 2, w, h);
                }
                string file = Path.Combine(dir, "ArrowCursor.png");
                bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                File.Copy(file, Path.Combine(dir, "ArrowFarCursor.png"), true);
                return true;
            }
            catch { return false; }
        }
    }

    // Which app opens roblox:// links (Play buttons on the website). Turning it off restores normal Roblox.
    static class LinkHandler
    {
        static readonly string[] Protocols = { "roblox-player", "roblox" };

        static string ReadCommand(string protocol)
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{protocol}\shell\open\command");
            return key?.GetValue("") as string ?? "";
        }

        static void WriteCommand(string protocol, string command)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}");
            key.SetValue("", "URL: Roblox Protocol");
            key.SetValue("URL Protocol", "");
            using var cmd = key.CreateSubKey(@"shell\open\command");
            cmd.SetValue("", command);
        }

        static string Exe => Environment.ProcessPath ?? "GuideStrap.exe";

        public static bool IsOurs() => Protocols.All(p => ReadCommand(p).Contains(Exe, StringComparison.OrdinalIgnoreCase));

        public static void Register(AppSettings settings, string? exePath = null)
        {
            string exe = exePath ?? Exe;
            foreach (var p in Protocols)
            {
                string old = ReadCommand(p);
                if (old.Length > 0 && !old.Contains("GuideStrap", StringComparison.OrdinalIgnoreCase))
                    settings.PreviousLinkHandlers[p] = old;
                WriteCommand(p, $"\"{exe}\" \"%1\"");
            }
        }

        public static bool PointsTo(string exePath) => Protocols.Any(p => ReadCommand(p).Contains(exePath, StringComparison.OrdinalIgnoreCase));

        public static void Restore(AppSettings settings)
        {
            string? official = FindRobloxLauncher();
            foreach (var p in Protocols)
            {
                string? old = settings.PreviousLinkHandlers.GetValueOrDefault(p);
                if (old != null && !old.Contains("GuideStrap", StringComparison.OrdinalIgnoreCase)) WriteCommand(p, old);
                else if (official != null) WriteCommand(p, $"\"{official}\" %1");
                else Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{p}", false);
            }
        }

        static string? FindRobloxLauncher()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
            if (!Directory.Exists(dir)) return null;
            return new DirectoryInfo(dir).GetDirectories()
                .OrderByDescending(d => d.LastWriteTime)
                .Select(d => Path.Combine(d.FullName, "RobloxPlayerBeta.exe"))
                .FirstOrDefault(File.Exists);
        }
    }

    // Lets more than one Roblox window run by claiming Roblox's "only one copy" lock first
    static class MultiInstance
    {
        static readonly List<IDisposable> held = new();

        public static void Hold()
        {
            if (held.Count > 0) return;
            foreach (var name in new[] { "ROBLOX_singletonMutex", "ROBLOX_singletonEvent" })
            {
                try { held.Add(new Mutex(true, name)); } catch { }
            }
        }
    }

    static class Cleaner
    {
        public static long ClearCache()
        {
            long freed = 0;
            void Delete(string dir, Func<FileInfo, bool> keepIf)
            {
                if (!Directory.Exists(dir)) return;
                foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    if (keepIf(f)) continue;
                    try { long len = f.Length; f.Delete(); freed += len; } catch { }
                }
            }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Delete(Path.Combine(Path.GetTempPath(), "Roblox"), _ => false);
            Delete(Path.Combine(local, "Roblox", "logs"), f => f.LastWriteTime > DateTime.Now.AddDays(-3));
            Delete(Paths.Avatars, _ => false);
            Delete(Paths.Icons, _ => false);
            return freed;
        }
    }

    // Self-update from GitHub releases (only when AppInfo.UpdateRepo is set)
    static class Updater
    {
        public static bool Configured => AppInfo.UpdateRepo.Length > 0;

        public static async Task<(string Version, string Url)?> CheckAsync()
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{AppInfo.UpdateRepo}/releases/latest");
                req.Headers.UserAgent.ParseAdd("GuideStrap");
                using var resp = await RobloxApi.Http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                string tag = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
                if (!System.Version.TryParse(tag, out var latest) || !System.Version.TryParse(AppInfo.Version, out var mine) || latest <= mine)
                    return null;
                foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        return (tag, asset.GetProperty("browser_download_url").GetString() ?? "");
                }
            }
            catch { }
            return null;
        }

        public static async Task InstallAsync(string url)
        {
            string exe = Environment.ProcessPath!;
            string fresh = exe + ".new", old = exe + ".old";
            await File.WriteAllBytesAsync(fresh, await RobloxApi.Http.GetByteArrayAsync(url));
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);      // Windows lets a running .exe be renamed
            File.Move(fresh, exe);
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            Application.Exit();
        }

        public static void CleanupOld()
        {
            try
            {
                string old = (Environment.ProcessPath ?? "") + ".old";
                if (File.Exists(old)) File.Delete(old);
            }
            catch { }
        }
    }

    class Profile
    {
        public int Preset { get; set; } = 2;
        public Dictionary<string, string> Flags { get; set; } = new();
        public string CursorPreset { get; set; } = "default";
        public string Accent { get; set; } = "Purple";
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

    // Translations: English text is the key. Text with {0}, {1}... is matched as a template.
    static class L
    {
        public static readonly (string Code, string Native)[] Languages =
        {
            ("en", "English"), ("es", "Español"), ("pt", "Português"),
            ("fr", "Français"), ("de", "Deutsch"), ("tr", "Türkçe"), ("fil", "Filipino"),
        };
        static string lang = "en";
        static Dictionary<string, string>? table;
        static readonly List<(Regex Pattern, string Translation)> templates = new();

        public static string Lang
        {
            get => lang;
            set
            {
                string v = Languages.Any(x => x.Code == value) ? value : "en";
                if (v == lang) return;
                lang = v;
                table = null;
                templates.Clear();
            }
        }

        public static string SystemLanguage()
        {
            string c = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (c == "tl") c = "fil";
            return Languages.Any(x => x.Code == c) ? c : "en";
        }

        static void Load()
        {
            if (table != null) return;
            table = Translations.For(lang);
            foreach (var (key, value) in table.Where(kv => kv.Key.Contains("{0}")).OrderByDescending(kv => kv.Key.Length))
            {
                string pattern = "^" + Regex.Replace(Regex.Escape(key), @"\\\{(\d+)}", m => $"(?<g{m.Groups[1].Value}>.*?)") + "$";
                templates.Add((new Regex(pattern, RegexOptions.Singleline), value));
            }
        }

        public static string T(string text)
        {
            if (lang == "en" || string.IsNullOrEmpty(text)) return text;
            Load();
            if (table!.TryGetValue(text, out var hit)) return hit;
            foreach (var (pattern, translation) in templates)
            {
                var m = pattern.Match(text);
                if (m.Success) return Regex.Replace(translation, @"\{(\d+)\}", g => m.Groups["g" + g.Groups[1].Value].Value);
            }
            return text;
        }

        public static string F(string template, params object[] args) => string.Format(T(template), args);

        // Translates text that was set directly on controls (labels, buttons, placeholders).
        public static void Apply(Control root)
        {
            if (lang == "en") return;
            switch (root)
            {
                case TextBox tb: tb.PlaceholderText = T(tb.PlaceholderText); break;
                case Label or RoundButton or LauncherButton or LinkText: root.Text = T(root.Text); break;
            }
            foreach (Control child in root.Controls) Apply(child);
        }
    }

    static class Translations
    {
        public static Dictionary<string, string> For(string lang)
        {
            var d = new Dictionary<string, string>(Data.Length);
            if (lang is "es" or "pt")
            {
                int col = lang == "es" ? 1 : 2;
                foreach (var row in Data) d[row[0]] = row[col];
            }
            else
            {
                int col = lang switch { "fr" => 1, "de" => 2, "tr" => 3, "fil" => 4, _ => 0 };
                if (col > 0) foreach (var row in More) d[row[0]] = row[col];
            }
            return d;
        }

        // English, Español, Português
        static readonly string[][] Data =
        {
            new[] { "Join a game (optional)", "Unirse a un juego (opcional)", "Entrar em um jogo (opcional)" },
            new[] { "Paste a game link or place ID. Leave it empty to just open Roblox.", "Pega el enlace de un juego o su ID. Déjalo vacío para solo abrir Roblox.", "Cole o link de um jogo ou o ID. Deixe vazio para só abrir o Roblox." },
            new[] { "Games you join show up here so you can jump back in.", "Los juegos a los que entres aparecen aquí para volver rápido.", "Os jogos em que você entrar aparecem aqui para voltar rápido." },
            new[] { "Logins are encrypted and only work for your Windows user on this PC. Don't press \"Log out\" on the Roblox website for a saved account, or its saved login stops working.", "Los inicios de sesión están cifrados y solo funcionan para tu usuario de Windows en esta PC. No pulses \"Cerrar sesión\" en la web de Roblox para una cuenta guardada, o dejará de funcionar.", "Os logins são criptografados e só funcionam para o seu usuário do Windows neste PC. Não clique em \"Sair\" no site do Roblox em uma conta salva, ou o login salvo para de funcionar." },
            new[] { "Preset", "Preajuste", "Predefinição" },
            new[] { "Max FPS", "Máx. FPS", "Máx. FPS" },
            new[] { "Roblox only reads flags on its allowlist, so anything else gets skipped automatically.", "Roblox solo lee los flags de su lista permitida, así que los demás se omiten automáticamente.", "O Roblox só lê as flags da lista permitida, então o resto é ignorado automaticamente." },
            new[] { "Current font", "Fuente actual", "Fonte atual" },
            new[] { "Works with .ttf and .otf files (free fonts: fonts.google.com). Emoji keep their normal look. Applies next time you press Play.", "Funciona con archivos .ttf y .otf (fuentes gratis: fonts.google.com). Los emoji no cambian. Se aplica la próxima vez que juegues.", "Funciona com arquivos .ttf e .otf (fontes grátis: fonts.google.com). Os emojis não mudam. Vale na próxima vez que você jogar." },
            new[] { "Normal Roblox is never touched. Launching Roblox without GuideStrap always uses the original files, sounds and settings.", "El Roblox normal nunca se modifica. Abrir Roblox sin GuideStrap siempre usa los archivos, sonidos y ajustes originales.", "O Roblox normal nunca é alterado. Abrir o Roblox sem o GuideStrap sempre usa os arquivos, sons e configurações originais." },
            new[] { "Version {0}  ·  by Guide", "Versión {0}  ·  por Guide", "Versão {0}  ·  por Guide" },
            new[] { "FastFlag editor", "Editor de FastFlags", "Editor de FastFlags" },
            new[] { "Only flags on Roblox's allowlist are kept. Adding one you already have asks before replacing it.", "Solo se guardan los flags de la lista permitida de Roblox. Si agregas uno que ya tienes, te pregunta antes de reemplazarlo.", "Só ficam as flags da lista permitida do Roblox. Se adicionar uma que já existe, ele pergunta antes de substituir." },
            new[] { "Tip: press Enter in the value box to add.", "Consejo: pulsa Enter en el valor para agregar.", "Dica: aperte Enter no valor para adicionar." },
            new[] { "Paste FastFlags JSON", "Pega el JSON de FastFlags", "Cole o JSON de FastFlags" },
            new[] { "Flags that aren't on Roblox's allowlist are skipped.", "Los flags que no están en la lista permitida se omiten.", "Flags fora da lista permitida são ignoradas." },
            new[] { "Account: {0}", "Cuenta: {0}", "Conta: {0}" },
            new[] { "Launch Roblox", "Iniciar Roblox", "Iniciar Roblox" },
            new[] { "Add account", "Agregar cuenta", "Adicionar conta" },
            new[] { "Use", "Usar", "Usar" },
            new[] { "Open editor", "Abrir editor", "Abrir editor" },
            new[] { "Choose font file", "Elegir fuente", "Escolher fonte" },
            new[] { "Add flag", "Agregar flag", "Adicionar flag" },
            new[] { "Add", "Agregar", "Adicionar" },
            new[] { "Import", "Importar", "Importar" },
            new[] { "Done", "Listo", "Pronto" },
            new[] { "Main menu", "Menú principal", "Menu principal" },
            new[] { "Close", "Cerrar", "Fechar" },
            new[] { "Switch account", "Cambiar cuenta", "Trocar conta" },
            new[] { "Active", "Activa", "Ativa" },
            new[] { "Use default font", "Usar fuente normal", "Usar fonte padrão" },
            new[] { "Choose .ogg", "Elegir .ogg", "Escolher .ogg" },
            new[] { "Reset", "Restablecer", "Redefinir" },
            new[] { "Choose sky folder", "Elegir carpeta de cielo", "Escolher pasta do céu" },
            new[] { "Open mods folder", "Abrir carpeta de mods", "Abrir pasta de mods" },
            new[] { "Export", "Exportar", "Exportar" },
            new[] { "Open folder", "Abrir carpeta", "Abrir pasta" },
            new[] { "Clear cache", "Borrar caché", "Limpar cache" },
            new[] { "Repair", "Reparar", "Reparar" },
            new[] { "Check now", "Buscar ahora", "Verificar agora" },
            new[] { "Import JSON", "Importar JSON", "Importar JSON" },
            new[] { "Copy JSON", "Copiar JSON", "Copiar JSON" },
            new[] { "Clear all", "Borrar todo", "Limpar tudo" },
            new[] { "Cancel", "Cancelar", "Cancelar" },
            new[] { "Add an account", "Agregar una cuenta", "Adicionar uma conta" },
            new[] { "Opens the Roblox login page.", "Abre la página de inicio de sesión de Roblox.", "Abre a página de login do Roblox." },
            new[] { "Add, import or edit flags.", "Agrega, importa o edita flags.", "Adicione, importe ou edite flags." },
            new[] { "Mods folder", "Carpeta de mods", "Pasta de mods" },
            new[] { "Files here replace the matching files in Roblox (same folder layout).", "Los archivos de aquí reemplazan los mismos archivos en Roblox (misma estructura de carpetas).", "Os arquivos daqui substituem os mesmos arquivos no Roblox (mesma estrutura de pastas)." },
            new[] { "Use my selected account for website joins", "Usar mi cuenta elegida al entrar desde la web", "Usar minha conta escolhida ao entrar pelo site" },
            new[] { "Play buttons on roblox.com join as the account picked here.", "Los botones de Jugar en roblox.com entran con la cuenta elegida aquí.", "Os botões Jogar no roblox.com entram com a conta escolhida aqui." },
            new[] { "Close this window when launching", "Cerrar esta ventana al iniciar", "Fechar esta janela ao iniciar" },
            new[] { "Only the loading screen stays open while Roblox starts.", "Solo la pantalla de carga queda abierta mientras Roblox arranca.", "Só a tela de carregamento fica aberta enquanto o Roblox abre." },
            new[] { "Stay in the tray while Roblox is open", "Quedarse en la bandeja mientras Roblox esté abierto", "Ficar na bandeja enquanto o Roblox estiver aberto" },
            new[] { "Needed for Discord status, server location and recent games.", "Necesario para el estado de Discord, la ubicación del servidor y los juegos recientes.", "Necessário para o status do Discord, a localização do servidor e os jogos recentes." },
            new[] { "Show server location", "Mostrar ubicación del servidor", "Mostrar localização do servidor" },
            new[] { "Pops up where the server you joined is, like \"Dallas, Texas, US\".", "Muestra dónde está el servidor al que entraste, como \"Dallas, Texas, US\".", "Mostra onde fica o servidor em que você entrou, como \"Dallas, Texas, US\"." },
            new[] { "Allow multiple Roblox windows", "Permitir varias ventanas de Roblox", "Permitir várias janelas do Roblox" },
            new[] { "Run more than one account at once. Roblox sometimes blocks this.", "Usa más de una cuenta a la vez. Roblox a veces lo bloquea.", "Use mais de uma conta ao mesmo tempo. O Roblox às vezes bloqueia isso." },
            new[] { "Open Roblox links with GuideStrap", "Abrir enlaces de Roblox con GuideStrap", "Abrir links do Roblox com o GuideStrap" },
            new[] { "Off gives Play buttons on roblox.com back to normal Roblox.", "Desactivado, los botones de Jugar de roblox.com vuelven al Roblox normal.", "Desligado, os botões Jogar do roblox.com voltam para o Roblox normal." },
            new[] { "Discord status", "Estado de Discord", "Status do Discord" },
            new[] { "Shows on your profile what you're doing in GuideStrap and which game you're in.", "Muestra en tu perfil qué haces en GuideStrap y en qué juego estás.", "Mostra no seu perfil o que você está fazendo no GuideStrap e em qual jogo está." },
            new[] { "Accent color", "Color de acento", "Cor de destaque" },
            new[] { "The color used for buttons, switches and highlights.", "El color de botones, interruptores y resaltados.", "A cor dos botões, chaves e destaques." },
            new[] { "Settings profile", "Perfil de ajustes", "Perfil de configurações" },
            new[] { "Share your graphics, FastFlags, cursor and color as one file.", "Comparte tus gráficos, FastFlags, cursor y color en un solo archivo.", "Compartilhe seus gráficos, FastFlags, cursor e cor em um só arquivo." },
            new[] { "GuideStrap folder", "Carpeta de GuideStrap", "Pasta do GuideStrap" },
            new[] { "Settings, mods and the downloaded Roblox live here.", "Aquí están los ajustes, los mods y el Roblox descargado.", "Aqui ficam as configurações, os mods e o Roblox baixado." },
            new[] { "Deletes Roblox's temporary files, old logs and cached pictures.", "Borra los archivos temporales de Roblox, registros viejos e imágenes en caché.", "Apaga os arquivos temporários do Roblox, logs antigos e imagens em cache." },
            new[] { "Repair Roblox", "Reparar Roblox", "Reparar Roblox" },
            new[] { "Fixes crashes or a broken install by downloading Roblox again.", "Arregla cierres o una instalación rota descargando Roblox de nuevo.", "Corrige travamentos ou uma instalação quebrada baixando o Roblox de novo." },
            new[] { "Updates", "Actualizaciones", "Atualizações" },
            new[] { "You're on GuideStrap {0}.", "Tienes GuideStrap {0}.", "Você está no GuideStrap {0}." },
            new[] { "Recent games", "Juegos recientes", "Jogos recentes" },
            new[] { "Active flags", "Flags activos", "Flags ativas" },
            new[] { "Your own mods", "Tus propios mods", "Seus próprios mods" },
            new[] { "Behavior", "Comportamiento", "Comportamento" },
            new[] { "Discord", "Discord", "Discord" },
            new[] { "Appearance", "Apariencia", "Aparência" },
            new[] { "Profiles", "Perfiles", "Perfis" },
            new[] { "Tools", "Herramientas", "Ferramentas" },
            new[] { "Play", "Jugar", "Jogar" },
            new[] { "Start Roblox with your settings.", "Abre Roblox con tus ajustes.", "Abra o Roblox com suas configurações." },
            new[] { "Accounts", "Cuentas", "Contas" },
            new[] { "Saved Roblox accounts.", "Cuentas de Roblox guardadas.", "Contas do Roblox salvas." },
            new[] { "Graphics", "Gráficos", "Gráficos" },
            new[] { "Pick a preset, or change settings one by one.", "Elige un preajuste o cambia los ajustes uno por uno.", "Escolha uma predefinição ou mude as configurações uma por uma." },
            new[] { "FastFlags", "FastFlags", "FastFlags" },
            new[] { "Flags from Roblox's allowlist.", "Flags de la lista permitida de Roblox.", "Flags da lista permitida do Roblox." },
            new[] { "Text fonts", "Fuentes de texto", "Fontes de texto" },
            new[] { "The font Roblox uses in-game.", "La fuente que Roblox usa en el juego.", "A fonte que o Roblox usa no jogo." },
            new[] { "Mods", "Mods", "Mods" },
            new[] { "Cosmetic file swaps. Only GuideStrap's copy of Roblox is changed.", "Cambios estéticos de archivos. Solo cambia la copia de Roblox de GuideStrap.", "Trocas visuais de arquivos. Só a cópia do Roblox do GuideStrap muda." },
            new[] { "Settings", "Ajustes", "Configurações" },
            new[] { "Launcher options.", "Opciones del launcher.", "Opções do launcher." },
            new[] { "About", "Acerca de", "Sobre" },
            new[] { "What GuideStrap is.", "Qué es GuideStrap.", "O que é o GuideStrap." },
            new[] { "Remove account?", "¿Quitar cuenta?", "Remover conta?" },
            new[] { "Remove {0} (@{1}) from GuideStrap?", "¿Quitar a {0} (@{1}) de GuideStrap?", "Remover {0} (@{1}) do GuideStrap?" },
            new[] { "Remove", "Quitar", "Remover" },
            new[] { "That isn't an Ogg file", "Eso no es un archivo Ogg", "Isso não é um arquivo Ogg" },
            new[] { "Roblox only plays .ogg sounds here. Convert your sound to .ogg first (any free online converter works).", "Roblox solo reproduce sonidos .ogg aquí. Convierte tu sonido a .ogg primero (cualquier conversor gratis en línea sirve).", "O Roblox só toca sons .ogg aqui. Converta seu som para .ogg antes (qualquer conversor online grátis serve)." },
            new[] { "No sky files found", "No se encontraron archivos de cielo", "Nenhum arquivo de céu encontrado" },
            new[] { "Sky packs are .tex files like sky512_up.tex. Pick the folder that contains them.", "Los paquetes de cielo son archivos .tex como sky512_up.tex. Elige la carpeta que los tiene.", "Pacotes de céu são arquivos .tex como sky512_up.tex. Escolha a pasta que os contém." },
            new[] { "Restart to apply", "Reinicia para aplicar", "Reinicie para aplicar" },
            new[] { "The new color shows after GuideStrap restarts.", "El nuevo color se verá cuando GuideStrap se reinicie.", "A nova cor aparece depois que o GuideStrap reiniciar." },
            new[] { "The new language shows after GuideStrap restarts.", "El nuevo idioma se verá cuando GuideStrap se reinicie.", "O novo idioma aparece depois que o GuideStrap reiniciar." },
            new[] { "Repair Roblox?", "¿Reparar Roblox?", "Reparar o Roblox?" },
            new[] { "This deletes GuideStrap's copy of Roblox. It downloads a fresh one next time you launch.", "Esto borra la copia de Roblox de GuideStrap. Se descargará una nueva la próxima vez que inicies.", "Isso apaga a cópia do Roblox do GuideStrap. Uma nova é baixada na próxima vez que você iniciar." },
            new[] { "Updates aren't set up yet", "Las actualizaciones aún no están configuradas", "As atualizações ainda não estão configuradas" },
            new[] { "Once GuideStrap is published on GitHub, set UpdateRepo in the code and it will update itself.", "Cuando GuideStrap esté publicado en GitHub, pon UpdateRepo en el código y se actualizará solo.", "Quando o GuideStrap estiver publicado no GitHub, defina UpdateRepo no código e ele vai se atualizar sozinho." },
            new[] { "Update available", "Actualización disponible", "Atualização disponível" },
            new[] { "GuideStrap {0} is out. Update now?", "Salió GuideStrap {0}. ¿Actualizar ahora?", "Saiu o GuideStrap {0}. Atualizar agora?" },
            new[] { "That profile can't be read", "No se puede leer ese perfil", "Não dá para ler esse perfil" },
            new[] { "The file is damaged or isn't a GuideStrap profile.", "El archivo está dañado o no es un perfil de GuideStrap.", "O arquivo está danificado ou não é um perfil do GuideStrap." },
            new[] { "Use this profile?", "¿Usar este perfil?", "Usar este perfil?" },
            new[] { "Use profile", "Usar perfil", "Usar perfil" },
            new[] { "Your graphics, FastFlags, cursor and accent color will be replaced.", "Se reemplazarán tus gráficos, FastFlags, cursor y color de acento.", "Seus gráficos, FastFlags, cursor e cor de destaque serão substituídos." },
            new[] { "The profile's color shows after GuideStrap restarts.", "El color del perfil se verá cuando GuideStrap se reinicie.", "A cor do perfil aparece depois que o GuideStrap reiniciar." },
            new[] { "Clear every flag?", "¿Borrar todos los flags?", "Limpar todas as flags?" },
            new[] { "This removes all {0} flags (and resets Graphics to Balanced).", "Esto quita los {0} flags (y pone Gráficos en Equilibrado).", "Isso remove todas as {0} flags (e volta Gráficos para Equilibrado)." },
            new[] { "That isn't valid JSON", "Eso no es un JSON válido", "Isso não é um JSON válido" },
            new[] { "Make sure it looks like { \"FlagName\": \"Value\", ... } with quotes around names.", "Asegúrate de que se vea así: { \"NombreFlag\": \"Valor\", ... } con comillas en los nombres.", "Confira se está assim: { \"NomeFlag\": \"Valor\", ... } com aspas nos nomes." },
            new[] { "That isn't a flag list", "Eso no es una lista de flags", "Isso não é uma lista de flags" },
            new[] { "The JSON should be an object like { \"FlagName\": \"Value\" }.", "El JSON debe ser un objeto como { \"NombreFlag\": \"Valor\" }.", "O JSON deve ser um objeto como { \"NomeFlag\": \"Valor\" }." },
            new[] { "Changes are saved automatically.", "Los cambios se guardan automáticamente.", "As alterações são salvas automaticamente." },
            new[] { "Wait for Roblox to finish launching first.", "Espera a que Roblox termine de iniciar.", "Espere o Roblox terminar de abrir." },
            new[] { "That doesn't look like a game link or place ID.", "Eso no parece un enlace de juego ni un ID.", "Isso não parece um link de jogo nem um ID." },
            new[] { "Added {0}.", "Se agregó {0}.", "{0} adicionado." },
            new[] { "Font reset to Roblox's default.", "Fuente restablecida a la de Roblox.", "Fonte voltou para a padrão do Roblox." },
            new[] { "Font set to {0}.", "Fuente cambiada a {0}.", "Fonte alterada para {0}." },
            new[] { "Death sound set. It applies next time you launch Roblox.", "Sonido de muerte listo. Se aplica la próxima vez que inicies Roblox.", "Som de morte definido. Vale na próxima vez que você abrir o Roblox." },
            new[] { "Cleared {0} MB of old Roblox cache and logs.", "Se borraron {0} MB de caché y registros viejos de Roblox.", "{0} MB de cache e logs antigos do Roblox apagados." },
            new[] { "Roblox will re-download next time you launch.", "Roblox se descargará de nuevo la próxima vez que inicies.", "O Roblox vai ser baixado de novo na próxima vez que você iniciar." },
            new[] { "GuideStrap is up to date.", "GuideStrap está actualizado.", "O GuideStrap está atualizado." },
            new[] { "Downloading the update...", "Descargando la actualización...", "Baixando a atualização..." },
            new[] { "Profile saved.", "Perfil guardado.", "Perfil salvo." },
            new[] { "Profile loaded.", "Perfil cargado.", "Perfil carregado." },
            new[] { "Nothing to copy yet.", "Aún no hay nada que copiar.", "Ainda não há nada para copiar." },
            new[] { "Copied {0} flags to your clipboard.", "Se copiaron {0} flags al portapapeles.", "{0} flags copiadas para a área de transferência." },
            new[] { "All flags cleared.", "Se borraron todos los flags.", "Todas as flags foram removidas." },
            new[] { "Type a value first.", "Escribe un valor primero.", "Digite um valor primeiro." },
            new[] { "Paste some JSON first.", "Pega un JSON primero.", "Cole um JSON primeiro." },
            new[] { "Updated {0}.", "Se actualizó {0}.", "{0} atualizada." },
            new[] { "Removed {0}.", "Se quitó {0}.", "{0} removida." },
            new[] { "Play Roblox", "Jugar Roblox", "Jogar Roblox" },
            new[] { "No account selected", "Ninguna cuenta elegida", "Nenhuma conta escolhida" },
            new[] { "Roblox will use whoever is logged in on the website.", "Roblox usará la cuenta con la que hayas iniciado sesión en la web.", "O Roblox vai usar a conta que estiver logada no site." },
            new[] { "Roblox login", "Sesión de Roblox", "Login do Roblox" },
            new[] { "Add accounts", "Agregar cuentas", "Adicionar contas" },
            new[] { "{0} graphics  ·  {1}  ·  {2} FastFlag{3}", "Gráficos: {0}  ·  {1}  ·  {2} FastFlag{3}", "Gráficos: {0}  ·  {1}  ·  {2} FastFlag{3}" },
            new[] { "custom font", "fuente personalizada", "fonte personalizada" },
            new[] { "default font", "fuente normal", "fonte padrão" },
            new[] { "Custom font", "Fuente personalizada", "Fonte personalizada" },
            new[] { "  (no preview, still works in-game)", "  (sin vista previa, igual funciona en el juego)", "  (sem prévia, mas funciona no jogo)" },
            new[] { "Balanced", "Equilibrado", "Equilibrado" },
            new[] { "Max graphics", "Máx. gráficos", "Máx. gráficos" },
            new[] { "No flags yet. Pick a graphics preset or open the editor.", "Aún no hay flags. Elige un preajuste de gráficos o abre el editor.", "Ainda não há flags. Escolha uma predefinição de gráficos ou abra o editor." },
            new[] { "The quick brown fox jumps over the lazy dog\n0123456789  !?  Hello from GuideStrap", "El veloz murciélago hindú comía feliz cardillo y kiwi\n0123456789  ¡!¿?  Hola desde GuideStrap", "Um pequeno jabuti xereta viu dez cegonhas felizes\n0123456789  !?  Olá do GuideStrap" },
            new[] { "Roblox default", "Normal de Roblox", "Padrão do Roblox" },
            new[] { "{0} flag{1}", "{0} flag{1}", "{0} flag{1}" },
            new[] { "+ {0} more", "+ {0} más", "+ {0} mais" },
            new[] { "About GuideStrap", "Acerca de GuideStrap", "Sobre o GuideStrap" },
            new[] { "Open GuideStrap folder", "Abrir carpeta de GuideStrap", "Abrir pasta do GuideStrap" },
            new[] { "Configure settings", "Configurar ajustes", "Configurar" },
            new[] { "Accounts  ·  {0}", "Cuentas  ·  {0}", "Contas  ·  {0}" },
            new[] { "Starting...", "Iniciando...", "Iniciando..." },
            new[] { "Cancelling...", "Cancelando...", "Cancelando..." },
            new[] { "Opened the game page in your browser", "Se abrió la página del juego en tu navegador", "A página do jogo foi aberta no navegador" },
            new[] { "Roblox didn't return a version.", "Roblox no devolvió una versión.", "O Roblox não devolveu uma versão." },
            new[] { "Roblox said no (HTTP {0}).", "Roblox lo rechazó (HTTP {0}).", "O Roblox recusou (HTTP {0})." },
            new[] { "This account's saved login expired. Remove it and add it again.", "El inicio de sesión guardado de esta cuenta expiró. Quítala y agrégala de nuevo.", "O login salvo desta conta expirou. Remova e adicione de novo." },
            new[] { "Couldn't log in this account (HTTP {0}).", "No se pudo iniciar sesión con esta cuenta (HTTP {0}).", "Não deu para entrar com esta conta (HTTP {0})." },
            new[] { "Couldn't log in this account. Try again.", "No se pudo iniciar sesión con esta cuenta. Inténtalo de nuevo.", "Não deu para entrar com esta conta. Tente de novo." },
            new[] { "{0} downloaded corrupted. Try again.", "{0} se descargó dañado. Inténtalo de nuevo.", "{0} foi baixado corrompido. Tente de novo." },
            new[] { "Getting the latest Roblox...", "Obteniendo lo último de Roblox...", "Pegando o Roblox mais recente..." },
            new[] { "Installing Roblox... {0}%", "Instalando Roblox... {0}%", "Instalando o Roblox... {0}%" },
            new[] { "Connecting to Roblox...", "Conectando con Roblox...", "Conectando ao Roblox..." },
            new[] { "Applying your settings...", "Aplicando tus ajustes...", "Aplicando suas configurações..." },
            new[] { "Signing in as {0}...", "Iniciando sesión como {0}...", "Entrando como {0}..." },
            new[] { "Starting Roblox...", "Abriendo Roblox...", "Abrindo o Roblox..." },
            new[] { "Death sound", "Sonido de muerte", "Som de morte" },
            new[] { "Mouse cursor", "Cursor del mouse", "Cursor do mouse" },
            new[] { "Sky", "Cielo", "Céu" },
            new[] { "Custom sound", "Sonido personalizado", "Som personalizado" },
            new[] { "Roblox's default sound", "Sonido normal de Roblox", "Som padrão do Roblox" },
            new[] { "Roblox's default cursor", "Cursor normal de Roblox", "Cursor padrão do Roblox" },
            new[] { "Purple arrow made for GuideStrap", "Flecha morada hecha para GuideStrap", "Seta roxa feita para o GuideStrap" },
            new[] { "A small dot, easy to aim with", "Un punto pequeño, fácil para apuntar", "Um pontinho, fácil de mirar" },
            new[] { "Your own image", "Tu propia imagen", "Sua própria imagem" },
            new[] { "Custom sky pack ({0} files). Games with their own sky keep it.", "Paquete de cielo personalizado ({0} archivos). Los juegos con cielo propio lo mantienen.", "Pacote de céu personalizado ({0} arquivos). Jogos com céu próprio mantêm o deles." },
            new[] { "Roblox's default sky. Gray sky is under Graphics.", "Cielo normal de Roblox. El cielo gris está en Gráficos.", "Céu padrão do Roblox. O céu cinza fica em Gráficos." },
            new[] { "Lowest quality, flat lighting, no grass. For weak PCs or pure frames.", "Calidad mínima, luz plana, sin pasto. Para PCs débiles o máximo de FPS.", "Qualidade mínima, luz chapada, sem grama. Para PCs fracos ou só FPS." },
            new[] { "Performance", "Rendimiento", "Desempenho" },
            new[] { "Low settings that still look okay. Great for laptops.", "Ajustes bajos que aún se ven bien. Ideal para laptops.", "Configurações baixas que ainda ficam boas. Ótimo para notebooks." },
            new[] { "Roblox's normal automatic settings. Nothing changed.", "Los ajustes automáticos normales de Roblox. Sin cambios.", "As configurações automáticas normais do Roblox. Nada mudou." },
            new[] { "Quality", "Calidad", "Qualidade" },
            new[] { "Sharper textures and smoother edges. Needs a decent PC.", "Texturas más nítidas y bordes más suaves. Necesita una PC decente.", "Texturas mais nítidas e bordas mais suaves. Precisa de um PC razoável." },
            new[] { "Max Graphics", "Gráficos máximos", "Gráficos máximos" },
            new[] { "Everything maxed out. Best looks, lowest FPS.", "Todo al máximo. Se ve mejor, con menos FPS.", "Tudo no máximo. Visual melhor, FPS menor." },
            new[] { "Custom", "Personalizado", "Personalizado" },
            new[] { "You changed settings yourself. Move the slider to pick a preset again.", "Cambiaste ajustes por tu cuenta. Mueve el control para elegir un preajuste otra vez.", "Você mudou configurações por conta própria. Mova o controle para escolher uma predefinição de novo." },
            new[] { "Pick a font", "Elige una fuente", "Escolha uma fonte" },
            new[] { "Pick a death sound", "Elige un sonido de muerte", "Escolha um som de morte" },
            new[] { "Pick a cursor image", "Elige una imagen de cursor", "Escolha uma imagem de cursor" },
            new[] { "Pick a folder with sky .tex files (from a Roblox sky mod pack)", "Elige una carpeta con archivos .tex de cielo (de un paquete de mods)", "Escolha uma pasta com arquivos .tex de céu (de um pacote de mods)" },
            new[] { "Overwrite", "Sobrescribir", "Substituir" },
            new[] { "Keep original", "Mantener original", "Manter original" },
            new[] { "Auto", "Auto", "Auto" },
            new[] { "Lowest", "Mínima", "Mínima" },
            new[] { "Low", "Baja", "Baixa" },
            new[] { "Medium", "Media", "Média" },
            new[] { "High", "Alta", "Alta" },
            new[] { "Off (1x)", "Apagado (1x)", "Desligado (1x)" },
            new[] { "Default", "Predeterminado", "Padrão" },
            new[] { "GuideStrap arrow", "Flecha de GuideStrap", "Seta do GuideStrap" },
            new[] { "Small dot", "Punto pequeño", "Pontinho" },
            new[] { "Custom image...", "Imagen propia...", "Imagem própria..." },
            new[] { "Couldn't add that account", "No se pudo agregar esa cuenta", "Não deu para adicionar essa conta" },
            new[] { "Couldn't use that font", "No se pudo usar esa fuente", "Não deu para usar essa fonte" },
            new[] { "Couldn't use that sound", "No se pudo usar ese sonido", "Não deu para usar esse som" },
            new[] { "Couldn't use that sky", "No se pudo usar ese cielo", "Não deu para usar esse céu" },
            new[] { "Couldn't change that", "No se pudo cambiar eso", "Não deu para mudar isso" },
            new[] { "Restart now", "Reiniciar ahora", "Reiniciar agora" },
            new[] { "Later", "Después", "Depois" },
            new[] { "Close Roblox first", "Cierra Roblox primero", "Feche o Roblox primeiro" },
            new[] { "Update", "Actualizar", "Atualizar" },
            new[] { "Update failed", "La actualización falló", "A atualização falhou" },
            new[] { "Couldn't launch Roblox", "No se pudo iniciar Roblox", "Não deu para abrir o Roblox" },
            new[] { "Something went wrong", "Algo salió mal", "Algo deu errado" },
            new[] { "Graphics quality level, 1 (lowest) to 21 (highest)", "Nivel de calidad gráfica, 1 (mínimo) a 21 (máximo)", "Nível de qualidade gráfica, 1 (mínimo) a 21 (máximo)" },
            new[] { "True = use the texture quality flag", "True = usar el flag de calidad de texturas", "True = usar a flag de qualidade de textura" },
            new[] { "Texture quality: 0 lowest, 1 low, 2 medium, 3 high", "Calidad de texturas: 0 mínima, 1 baja, 2 media, 3 alta", "Qualidade de textura: 0 mínima, 1 baixa, 2 média, 3 alta" },
            new[] { "Anti-aliasing samples: 1, 2, 4 or 8", "Muestras de antialiasing: 1, 2, 4 u 8", "Amostras de anti-aliasing: 1, 2, 4 ou 8" },
            new[] { "True = render with Direct3D 11", "True = renderizar con Direct3D 11", "True = renderizar com Direct3D 11" },
            new[] { "True = render with Vulkan", "True = renderizar con Vulkan", "True = renderizar com Vulkan" },
            new[] { "True = render with OpenGL", "True = renderizar con OpenGL", "True = renderizar com OpenGL" },
            new[] { "Grass draw distance (min). 0 with max = no grass", "Distancia del pasto (mín.). 0 con máx. = sin pasto", "Distância da grama (mín.). 0 com máx. = sem grama" },
            new[] { "Grass draw distance (max). 0 = no grass", "Distancia del pasto (máx.). 0 = sin pasto", "Distância da grama (máx.). 0 = sem grama" },
            new[] { "True = gray sky instead of the skybox", "True = cielo gris en vez del skybox", "True = céu cinza no lugar do skybox" },
            new[] { "True = freeze lighting updates (faster)", "True = congelar la iluminación (más rápido)", "True = congelar a iluminação (mais rápido)" },
            new[] { "Grass movement. 0 = still grass", "Movimiento del pasto. 0 = pasto quieto", "Movimento da grama. 0 = grama parada" },
            new[] { "True = turn off DPI scaling", "True = desactivar el escalado DPI", "True = desligar a escala de DPI" },
            new[] { "False = Alt+Enter uses exclusive fullscreen", "False = Alt+Enter usa pantalla completa exclusiva", "False = Alt+Enter usa tela cheia exclusiva" },
            new[] { "Mesh detail switching distance", "Distancia de cambio de detalle de mallas", "Distância de troca de detalhe das malhas" },
            new[] { "Mesh detail distance, level 1 to 2", "Distancia de detalle de mallas, nivel 1 a 2", "Distância de detalhe das malhas, nível 1 a 2" },
            new[] { "Mesh detail distance, level 2 to 3", "Distancia de detalle de mallas, nivel 2 a 3", "Distância de detalhe das malhas, nível 2 a 3" },
            new[] { "Mesh detail distance, level 3 to 4", "Distancia de detalle de mallas, nivel 3 a 4", "Distância de detalhe das malhas, nível 3 a 4" },
            new[] { "Connected to a server in {0}", "Conectado a un servidor en {0}", "Conectado a um servidor em {0}" },
            new[] { "Account", "Cuenta", "Conta" },
            new[] { "Open GuideStrap", "Abrir GuideStrap", "Abrir o GuideStrap" },
            new[] { "Exit", "Salir", "Sair" },
            new[] { "Search", "Buscar", "Pesquisar" },
            new[] { "Filter", "Filtrar", "Filtrar" },
            new[] { "Value", "Valor", "Valor" },
            new[] { "e.g. https://www.roblox.com/games/920587237", "p. ej. https://www.roblox.com/games/920587237", "ex.: https://www.roblox.com/games/920587237" },
            new[] { "Saved accounts", "Cuentas guardadas", "Contas salvas" },
            new[] { "Default (Roblox's own login)", "Predeterminada (sesión de Roblox)", "Padrão (login do Roblox)" },
            new[] { "Uses whoever is logged in on the Roblox website", "Usa la cuenta con sesión iniciada en la web de Roblox", "Usa a conta logada no site do Roblox" },
            new[] { "Using Roblox's own login.", "Usando la sesión de Roblox.", "Usando o login do Roblox." },
            new[] { "Switched to {0}.", "Cambiaste a {0}.", "Trocado para {0}." },
            new[] { "Advanced settings", "Ajustes avanzados", "Configurações avançadas" },
            new[] { "Change every graphics setting yourself.", "Cambia cada ajuste gráfico por tu cuenta.", "Mude cada configuração gráfica você mesmo." },
            new[] { "Graphics quality", "Calidad gráfica", "Qualidade gráfica" },
            new[] { "Roblox's quality level (1-21). Auto = Roblox decides.", "Nivel de calidad de Roblox (1-21). Auto = decide Roblox.", "Nível de qualidade do Roblox (1-21). Auto = o Roblox decide." },
            new[] { "Texture quality", "Calidad de texturas", "Qualidade de textura" },
            new[] { "How sharp surfaces and decals look.", "Qué tan nítidas se ven las superficies y calcomanías.", "O quão nítidas ficam as superfícies e decalques." },
            new[] { "Anti-aliasing", "Antialiasing", "Anti-aliasing" },
            new[] { "Smooths jagged edges. Costs FPS.", "Suaviza los bordes dentados. Cuesta FPS.", "Suaviza bordas serrilhadas. Custa FPS." },
            new[] { "Rendering API", "API de renderizado", "API de renderização" },
            new[] { "Try Vulkan or D3D11 if you crash or stutter.", "Prueba Vulkan o D3D11 si se cierra o da tirones.", "Tente Vulkan ou D3D11 se travar ou engasgar." },
            new[] { "Remove grass", "Quitar pasto", "Remover grama" },
            new[] { "Hides terrain grass. Big FPS boost on grassy maps.", "Oculta el pasto del terreno. Mucho más FPS en mapas con pasto.", "Esconde a grama do terreno. Bem mais FPS em mapas com grama." },
            new[] { "Gray sky", "Cielo gris", "Céu cinza" },
            new[] { "Replaces the skybox with flat gray.", "Cambia el cielo por gris liso.", "Troca o céu por um cinza liso." },
            new[] { "Low-detail meshes", "Mallas de bajo detalle", "Malhas de pouco detalhe" },
            new[] { "Always uses the simplest version of models.", "Usa siempre la versión más simple de los modelos.", "Sempre usa a versão mais simples dos modelos." },
            new[] { "Freeze lighting updates", "Congelar iluminación", "Congelar iluminação" },
            new[] { "Faster, but lighting can look flat or wrong.", "Más rápido, pero la luz puede verse plana o rara.", "Mais rápido, mas a luz pode ficar chapada ou estranha." },
            new[] { "Still grass", "Pasto quieto", "Grama parada" },
            new[] { "Stops grass from waving around.", "Evita que el pasto se mueva.", "Impede a grama de balançar." },
            new[] { "Disable DPI scaling", "Desactivar escalado DPI", "Desligar escala de DPI" },
            new[] { "Sharper on high-res screens, UI gets smaller.", "Más nítido en pantallas de alta resolución; la interfaz se achica.", "Mais nítido em telas de alta resolução; a interface fica menor." },
            new[] { "Alt+Enter real fullscreen", "Pantalla completa real con Alt+Enter", "Tela cheia real com Alt+Enter" },
            new[] { "Exclusive fullscreen, can lower input lag.", "Pantalla completa exclusiva, puede bajar el retraso de entrada.", "Tela cheia exclusiva, pode diminuir o atraso de entrada." },
            new[] { "Play buttons on roblox.com now open GuideStrap.", "Los botones de Jugar en roblox.com ahora abren GuideStrap.", "Os botões Jogar no roblox.com agora abrem o GuideStrap." },
            new[] { "Play buttons on roblox.com open normal Roblox again.", "Los botones de Jugar en roblox.com vuelven a abrir el Roblox normal.", "Os botões Jogar no roblox.com voltam a abrir o Roblox normal." },
            new[] { "Language", "Idioma", "Idioma" },
            new[] { "English, Español or Português.", "English, Español o Português.", "English, Español ou Português." },
            new[] { "Added {0}", "Agregados {0}", "{0} adicionadas" },
            new[] { "Overwrote {0}", "Sobrescritos {0}", "{0} substituídas" },
            new[] { "Kept {0} original", "Se mantuvieron {0} originales", "{0} originais mantidas" },
            new[] { "{0} already the same", "{0} ya eran iguales", "{0} já eram iguais" },
            new[] { "Skipped {0} not on the allowlist", "Omitidos {0} fuera de la lista permitida", "{0} ignoradas fora da lista permitida" },
            new[] { "Nothing changed.", "No cambió nada.", "Nada mudou." },
            new[] { "This flag is already in your list", "Este flag ya está en tu lista", "Esta flag já está na sua lista" },
            new[] { "{0}\n\nCurrent value:   {1}\nNew value:   {2}\n\nDo you want to overwrite it with the new value, or keep the original?", "{0}\n\nValor actual:   {1}\nValor nuevo:   {2}\n\n¿Quieres sobrescribirlo con el valor nuevo o mantener el original?", "{0}\n\nValor atual:   {1}\nValor novo:   {2}\n\nQuer substituir pelo valor novo ou manter o original?" },
            new[] { "Do the same for the other {0} duplicate{1}", "Hacer lo mismo con los otros {0} duplicado{1}", "Fazer o mesmo com as outras {0} duplicada{1}" },
            new[] { "No flags yet. Press \"Add flag\" or \"Import JSON\" to get started.", "Aún no hay flags. Pulsa \"Agregar flag\" o \"Importar JSON\" para empezar.", "Ainda não há flags. Clique em \"Adicionar flag\" ou \"Importar JSON\" para começar." },
            new[] { "No flags match your filter.", "Ningún flag coincide con tu filtro.", "Nenhuma flag combina com o filtro." },
            new[] { "GuideStrap Setup", "Instalación de GuideStrap", "Instalação do GuideStrap" },
            new[] { "Welcome to GuideStrap", "Bienvenido a GuideStrap", "Bem-vindo ao GuideStrap" },
            new[] { "Pick your language, then install. It only takes a few seconds.", "Elige tu idioma y luego instala. Solo toma unos segundos.", "Escolha seu idioma e instale. Leva só alguns segundos." },
            new[] { "Desktop shortcut", "Acceso directo en el escritorio", "Atalho na área de trabalho" },
            new[] { "Add a GuideStrap icon to your desktop.", "Agrega un ícono de GuideStrap a tu escritorio.", "Coloca um ícone do GuideStrap na área de trabalho." },
            new[] { "Play buttons on roblox.com open GuideStrap.", "Los botones de Jugar en roblox.com abren GuideStrap.", "Os botões Jogar no roblox.com abrem o GuideStrap." },
            new[] { "Install", "Instalar", "Instalar" },
            new[] { "Installing...", "Instalando...", "Instalando..." },
            new[] { "Setup failed", "La instalación falló", "A instalação falhou" },
            new[] { "Installs for your Windows user only. Remove it any time from Settings > Apps.", "Se instala solo para tu usuario de Windows. Quítalo cuando quieras desde Configuración > Aplicaciones.", "Instala só para o seu usuário do Windows. Remova quando quiser em Configurações > Aplicativos." },
            new[] { "Uninstall GuideStrap?", "¿Desinstalar GuideStrap?", "Desinstalar o GuideStrap?" },
            new[] { "This removes GuideStrap, its copy of Roblox, your settings, mods and saved logins. Normal Roblox stays installed.", "Esto quita GuideStrap, su copia de Roblox, tus ajustes, mods e inicios de sesión guardados. El Roblox normal sigue instalado.", "Isso remove o GuideStrap, a cópia do Roblox dele, suas configurações, mods e logins salvos. O Roblox normal continua instalado." },
            new[] { "Uninstall", "Desinstalar", "Desinstalar" },
            new[] { "Close GuideStrap first", "Cierra GuideStrap primero", "Feche o GuideStrap primeiro" },
            new[] { "GuideStrap is still running (check the tray near the clock). Close it, then try again.", "GuideStrap sigue abierto (revisa la bandeja junto al reloj). Ciérralo e inténtalo de nuevo.", "O GuideStrap ainda está aberto (veja a bandeja perto do relógio). Feche e tente de novo." },
            new[] { "GuideStrap was uninstalled.", "GuideStrap se desinstaló.", "O GuideStrap foi desinstalado." },
            new[] { "GuideStrap is a Roblox launcher made by Guide. It downloads Roblox from Roblox's own servers, keeps it updated, and starts it with your settings applied.\n\nBesides launching, it handles graphics presets, FastFlags (only the ones Roblox allows), a custom in-game font, and switching between saved accounts.\n\nIt never touches the game while it's running. It only changes launch files and settings that Roblox reads on its own, so it isn't an exploit. Saved logins are encrypted for your Windows user.\n\nEverything happens in GuideStrap's own copy of Roblox. Your normal Roblox install is never changed, so launching Roblox without GuideStrap always gives you the original game, sounds and settings.\n\nGuideStrap is a fan project and isn't affiliated with Roblox Corporation.", "GuideStrap es un launcher de Roblox hecho por Guide. Descarga Roblox desde los servidores de Roblox, lo mantiene actualizado y lo abre con tus ajustes aplicados.\n\nAdemás de iniciar el juego, maneja preajustes de gráficos, FastFlags (solo los que Roblox permite), una fuente personalizada en el juego y el cambio entre cuentas guardadas.\n\nNunca toca el juego mientras está abierto. Solo cambia archivos de inicio y ajustes que Roblox lee por su cuenta, así que no es un exploit. Los inicios de sesión guardados están cifrados para tu usuario de Windows.\n\nTodo pasa en la copia de Roblox de GuideStrap. Tu instalación normal de Roblox nunca se modifica, así que abrir Roblox sin GuideStrap siempre te da el juego, los sonidos y los ajustes originales.\n\nGuideStrap es un proyecto de fans y no está afiliado a Roblox Corporation.", "O GuideStrap é um launcher de Roblox feito pelo Guide. Ele baixa o Roblox direto dos servidores do Roblox, mantém tudo atualizado e abre o jogo com as suas configurações.\n\nAlém de abrir o jogo, ele cuida de predefinições de gráficos, FastFlags (só as que o Roblox permite), uma fonte personalizada no jogo e a troca entre contas salvas.\n\nEle nunca mexe no jogo enquanto está aberto. Só altera arquivos de inicialização e configurações que o próprio Roblox lê, então não é um exploit. Os logins salvos são criptografados para o seu usuário do Windows.\n\nTudo acontece na cópia do Roblox do GuideStrap. Sua instalação normal do Roblox nunca é alterada, então abrir o Roblox sem o GuideStrap sempre traz o jogo, os sons e as configurações originais.\n\nO GuideStrap é um projeto de fã e não tem ligação com a Roblox Corporation." },
            new[] { "Reinstall GuideStrap", "Reinstalar GuideStrap", "Reinstalar o GuideStrap" },
            new[] { "GuideStrap is already installed. Reinstalling replaces it with this copy and keeps your settings, accounts and mods.", "GuideStrap ya está instalado. Reinstalar lo reemplaza con esta copia y conserva tus ajustes, cuentas y mods.", "O GuideStrap já está instalado. Reinstalar troca ele por esta cópia e mantém suas configurações, contas e mods." },
            new[] { "Installed: {0}  ·  This file: {1}", "Instalado: {0}  ·  Este archivo: {1}", "Instalado: {0}  ·  Este arquivo: {1}" },
            new[] { "Also repair Roblox", "Reparar Roblox también", "Reparar o Roblox também" },
            new[] { "Downloads a fresh copy of Roblox next time you launch.", "Descarga una copia nueva de Roblox la próxima vez que inicies.", "Baixa uma cópia nova do Roblox na próxima vez que você iniciar." },
            new[] { "Reinstall", "Reinstalar", "Reinstalar" },
            new[] { "Reinstalling...", "Reinstalando...", "Reinstalando..." },
            new[] { "GuideStrap is open", "GuideStrap está abierto", "O GuideStrap está aberto" },
            new[] { "GuideStrap is open right now. Close it and continue?", "GuideStrap está abierto ahora. ¿Cerrarlo y continuar?", "O GuideStrap está aberto agora. Fechar e continuar?" },
            new[] { "Close and continue", "Cerrar y continuar", "Fechar e continuar" },
            new[] { "Theme", "Tema", "Tema" },
            new[] { "Purple", "Morado", "Roxo" },
            new[] { "Black", "Negro", "Preto" },
            new[] { "Purple, or plain black with no color.", "Morado, o negro sin color.", "Roxo, ou preto sem cor." },
            new[] { "The new theme shows after GuideStrap restarts.", "El nuevo tema se verá cuando GuideStrap se reinicie.", "O novo tema aparece depois que o GuideStrap reiniciar." },
            new[] { "Used by the Purple theme for buttons, switches and highlights.", "Lo usa el tema Morado para botones, interruptores y resaltados.", "Usada pelo tema Roxo em botões, chaves e destaques." },
            new[] { "Potato mode", "Modo papa", "Modo batata" },
            new[] { "Everything low for the most FPS, or turn on parts one by one.", "Todo al mínimo para más FPS, o activa partes una por una.", "Tudo no mínimo para mais FPS, ou ative partes uma por uma." },
            new[] { "Turn on everything", "Activar todo", "Ativar tudo" },
            new[] { "All of the switches below at once.", "Todos los interruptores de abajo a la vez.", "Todas as chaves abaixo de uma vez." },
            new[] { "Blurry textures", "Texturas borrosas", "Texturas borradas" },
            new[] { "Lowest texture quality. Works on every game's textures.", "Calidad de textura más baja. Funciona en las texturas de cualquier juego.", "Qualidade de textura mais baixa. Funciona nas texturas de qualquer jogo." },
            new[] { "No grass", "Sin césped", "Sem grama" },
            new[] { "Hides terrain grass.", "Oculta el césped del terreno.", "Esconde a grama do terreno." },
            new[] { "Anti-aliasing off", "Antialiasing desactivado", "Anti-aliasing desligado" },
            new[] { "No edge smoothing. Edges look a bit jagged.", "Sin suavizado de bordes. Los bordes se ven un poco dentados.", "Sem suavização de bordas. As bordas ficam um pouco serrilhadas." },
            new[] { "Preview", "Vista previa", "Prévia" },
            new[] { "See what blurry textures look like.", "Mira cómo se ven las texturas borrosas.", "Veja como ficam as texturas borradas." },
            new[] { "Texture preview", "Vista previa de texturas", "Prévia de texturas" },
            new[] { "Normal", "Normal", "Normal" },
            new[] { "Made-up example. In real games the blur looks the same, the textures just differ.", "Ejemplo inventado. En juegos reales el desenfoque se ve igual, solo cambian las texturas.", "Exemplo inventado. Em jogos reais o desfoque fica igual, só as texturas mudam." },
            new[] { "Performance", "Rendimiento", "Desempenho" },
            new[] { "Roblox default", "Predeterminado de Roblox", "Padrão do Roblox" },
            new[] { "FPS cap", "Límite de FPS", "Limite de FPS" },
            new[] { "Sets Roblox's own max frame rate before it opens. Normal Roblox keeps it too.", "Ajusta el límite de FPS propio de Roblox antes de abrirlo. Roblox normal también lo conserva.", "Define o limite de FPS do próprio Roblox antes de abrir. O Roblox normal também mantém." },
            new[] { "Above normal", "Superior a lo normal", "Acima do normal" },
            new[] { "High", "Alta", "Alta" },
            new[] { "Roblox priority", "Prioridad de Roblox", "Prioridade do Roblox" },
            new[] { "Gives Roblox more CPU time. High can slow OBS while streaming.", "Da más tiempo de CPU a Roblox. Alta puede ralentizar OBS al transmitir.", "Dá mais tempo de CPU ao Roblox. Alta pode deixar o OBS lento durante a live." },
            new[] { "Muted", "Silenciado", "Mudo" },
            new[] { "Custom .mp3...", ".mp3 propio...", ".mp3 próprio..." },
            new[] { "Footsteps", "Pasos", "Passos" },
            new[] { "Jump sound", "Sonido de salto", "Som de pulo" },
            new[] { "Sound pack", "Paquete de sonidos", "Pacote de sons" },
            new[] { "None", "Ninguno", "Nenhum" },
            new[] { "Using {0}. Its files replace Roblox's sounds.", "Usando {0}. Sus archivos reemplazan los sonidos de Roblox.", "Usando {0}. Os arquivos dele substituem os sons do Roblox." },
            new[] { "Open the folder and add a pack: one folder per pack, with .ogg or .mp3 files.", "Abre la carpeta y agrega un paquete: una carpeta por paquete, con archivos .ogg o .mp3.", "Abra a pasta e adicione um pacote: uma pasta por pacote, com arquivos .ogg ou .mp3." },
            new[] { "No pack. Pick one, or open the folder to add more.", "Sin paquete. Elige uno o abre la carpeta para agregar más.", "Sem pacote. Escolha um ou abra a pasta para adicionar mais." },
            new[] { "No footstep sound", "Sin sonido de pasos", "Sem som de passos" },
            new[] { "Your own sound", "Tu propio sonido", "Seu próprio som" },
            new[] { "Roblox's default footsteps", "Pasos normales de Roblox", "Passos padrão do Roblox" },
            new[] { "No jump or landing sound", "Sin sonido de salto ni de aterrizaje", "Sem som de pulo nem de pouso" },
            new[] { "Roblox's default jump sound", "Sonido de salto normal de Roblox", "Som de pulo padrão do Roblox" },
            new[] { "Pick a sound", "Elige un sonido", "Escolha um som" },
            new[] { "That isn't an MP3 file", "Eso no es un archivo MP3", "Isso não é um arquivo MP3" },
            new[] { "These Roblox sounds are .mp3 files. Convert your sound to .mp3 first (any free online converter works).", "Estos sonidos de Roblox son archivos .mp3. Convierte tu sonido a .mp3 primero (sirve cualquier convertidor gratis en línea).", "Esses sons do Roblox são arquivos .mp3. Converta seu som para .mp3 primeiro (qualquer conversor online grátis serve)." },
            new[] { "Sound set. It applies next time you launch Roblox.", "Sonido listo. Se aplica la próxima vez que abras Roblox.", "Som definido. Vale na próxima vez que você abrir o Roblox." },
            new[] { "Small cross", "Cruz pequeña", "Cruz pequena" },
            new[] { "Circle", "Círculo", "Círculo" },
            new[] { "Classic arrow", "Flecha clásica", "Seta clássica" },
            new[] { "Dot", "Punto", "Ponto" },
            new[] { "A small cross, easy to aim with", "Una cruz pequeña, fácil para apuntar", "Uma cruz pequena, fácil de mirar" },
            new[] { "A thin circle", "Un círculo fino", "Um círculo fino" },
            new[] { "White arrow with a black outline", "Flecha blanca con borde negro", "Seta branca com contorno preto" },
            new[] { "Shift lock crosshair", "Mira de shift lock", "Mira do shift lock" },
            new[] { "A small dot in the middle of the screen", "Un punto pequeño en el centro de la pantalla", "Um ponto pequeno no meio da tela" },
            new[] { "A small cross in the middle of the screen", "Una cruz pequeña en el centro de la pantalla", "Uma cruz pequena no meio da tela" },
            new[] { "A thin circle in the middle of the screen", "Un círculo fino en el centro de la pantalla", "Um círculo fino no meio da tela" },
            new[] { "Roblox's default shift lock icon", "Icono de shift lock normal de Roblox", "Ícone de shift lock padrão do Roblox" },
            new[] { "Pick a crosshair image", "Elige una imagen de mira", "Escolha uma imagem de mira" },
            new[] { "Setups", "Configuraciones", "Configurações salvas" },
            new[] { "Saved setups", "Configuraciones guardadas", "Configurações salvas" },
            new[] { "Save", "Guardar", "Salvar" },
            new[] { "Pick a setup", "Elige una configuración", "Escolha uma configuração" },
            new[] { "Save your mods and graphics as a setup, like BedWars or Recording.", "Guarda tus mods y gráficos como una configuración, como BedWars o Grabación.", "Salve seus mods e gráficos como uma configuração, como BedWars ou Gravação." },
            new[] { "{0} saved. Pick one to switch to it.", "{0} guardadas. Elige una para cambiar a ella.", "{0} salvas. Escolha uma para trocar para ela." },
            new[] { "Save setup", "Guardar configuración", "Salvar configuração" },
            new[] { "Name this setup. Saving with an existing name replaces it.", "Ponle nombre. Si usas un nombre que ya existe, se reemplaza.", "Dê um nome. Se usar um nome que já existe, ele é substituído." },
            new[] { "Saved {0}.", "{0} guardada.", "{0} salva." },
            new[] { "Switched to {0}. It applies next time you launch Roblox.", "Cambiado a {0}. Se aplica la próxima vez que abras Roblox.", "Trocado para {0}. Vale na próxima vez que você abrir o Roblox." },
            new[] { "Pick a setup first", "Primero elige una configuración", "Escolha uma configuração primeiro" },
            new[] { "Choose the setup you want to delete from the list.", "Elige de la lista la configuración que quieres borrar.", "Escolha na lista a configuração que você quer apagar." },
            new[] { "Delete setup?", "¿Borrar configuración?", "Apagar configuração?" },
            new[] { "Delete {0}? Your current settings stay the same.", "¿Borrar {0}? Tus ajustes actuales no cambian.", "Apagar {0}? Suas configurações atuais continuam iguais." },
            new[] { "Delete", "Borrar", "Apagar" },
            new[] { "Crash helper", "Ayuda con cierres", "Ajuda com travamentos" },
            new[] { "If Roblox crashes, shows what went wrong and how to fix it.", "Si Roblox se cierra por un error, muestra qué pasó y cómo arreglarlo.", "Se o Roblox travar, mostra o que deu errado e como resolver." },
            new[] { "Roblox crashed", "Roblox se cerró por un error", "O Roblox travou" },
            new[] { "Roblox crashed while starting", "Roblox se cerró al iniciar", "O Roblox travou ao abrir" },
            new[] { "Error {0}: {1}", "Error {0}: {1}", "Erro {0}: {1}" },
            new[] { "Try these fixes, starting with the first one.", "Prueba estas soluciones, empezando por la primera.", "Tente estas soluções, começando pela primeira." },
            new[] { "Open Roblox logs", "Abrir registros de Roblox", "Abrir logs do Roblox" },
            new[] { "memory access error", "error de acceso a memoria", "erro de acesso à memória" },
            new[] { "Roblox stopped itself (security check)", "Roblox se detuvo solo (control de seguridad)", "O Roblox se fechou sozinho (verificação de segurança)" },
            new[] { "stack overflow", "desbordamiento de pila", "estouro de pilha" },
            new[] { "memory corruption", "memoria dañada", "memória corrompida" },
            new[] { "out of memory", "sin memoria", "sem memória" },
            new[] { "unhandled error", "error no controlado", "erro não tratado" },
            new[] { "the graphics driver stopped responding", "el controlador de gráficos dejó de responder", "o driver de vídeo parou de responder" },
            new[] { "internal error", "error interno", "erro interno" },
            new[] { "unknown error", "error desconocido", "erro desconhecido" },
            new[] { "Close overlays and macro tools", "Cierra overlays y programas de macros", "Feche overlays e programas de macro" },
            new[] { "Roblox closes itself when another program hooks into it, like FPS unlockers, macros, cheats or some recording overlays.", "Roblox se cierra solo cuando otro programa se mete en él, como desbloqueadores de FPS, macros, trucos o algunos overlays de grabación.", "O Roblox se fecha sozinho quando outro programa se conecta a ele, como desbloqueadores de FPS, macros, cheats ou alguns overlays de gravação." },
            new[] { "Switch the rendering API", "Cambia la API de gráficos", "Troque a API gráfica" },
            new[] { "Your graphics driver may not like Direct3D 11. Try Vulkan next time.", "Puede que tu controlador de gráficos no se lleve bien con Direct3D 11. Prueba Vulkan la próxima vez.", "Talvez seu driver de vídeo não se dê bem com o Direct3D 11. Tente o Vulkan na próxima vez." },
            new[] { "Your graphics driver may not like the current one. Try Direct3D 11 next time.", "Puede que tu controlador de gráficos no se lleve bien con la actual. Prueba Direct3D 11 la próxima vez.", "Talvez seu driver de vídeo não se dê bem com a atual. Tente o Direct3D 11 na próxima vez." },
            new[] { "Use Vulkan", "Usar Vulkan", "Usar Vulkan" },
            new[] { "Use D3D11", "Usar D3D11", "Usar D3D11" },
            new[] { "Done", "Listo", "Pronto" },
            new[] { "Free up memory", "Libera memoria", "Libere memória" },
            new[] { "Close browser tabs and other games, and turn on Potato mode in Graphics.", "Cierra pestañas del navegador y otros juegos, y activa el Modo papa en Gráficos.", "Feche abas do navegador e outros jogos, e ative o Modo batata em Gráficos." },
            new[] { "Launch once without mods", "Abre una vez sin mods", "Abra uma vez sem mods" },
            new[] { "A mod or custom font can break after a Roblox update. Your mods stay saved and come back the launch after.", "Un mod o una fuente propia puede fallar tras una actualización de Roblox. Tus mods siguen guardados y vuelven en la siguiente apertura.", "Um mod ou fonte própria pode quebrar depois de uma atualização do Roblox. Seus mods continuam salvos e voltam na abertura seguinte." },
            new[] { "Skip mods", "Saltar mods", "Pular mods" },
            new[] { "Reset FastFlags", "Restablecer FastFlags", "Redefinir FastFlags" },
            new[] { "A flag can stop working after a Roblox update. This clears your flags and sets Graphics back to Balanced.", "Un flag puede dejar de funcionar tras una actualización de Roblox. Esto borra tus flags y vuelve Gráficos a Equilibrado.", "Uma flag pode parar de funcionar depois de uma atualização do Roblox. Isso apaga suas flags e volta Gráficos para Equilibrado." },
            new[] { "Reset flags", "Restablecer flags", "Redefinir flags" },
            new[] { "Turn off multiple Roblox windows", "Desactiva varias ventanas de Roblox", "Desative várias janelas do Roblox" },
            new[] { "Running more than one Roblox at once can make it crash.", "Abrir más de un Roblox a la vez puede hacer que se cierre.", "Abrir mais de um Roblox ao mesmo tempo pode fazer ele travar." },
            new[] { "Turn off", "Desactivar", "Desativar" },
            new[] { "Update your graphics driver", "Actualiza tu controlador de gráficos", "Atualize seu driver de vídeo" },
            new[] { "Get the newest driver from NVIDIA, AMD or Intel. Old drivers are the most common cause of crashes.", "Descarga el controlador más nuevo de NVIDIA, AMD o Intel. Los controladores viejos son la causa más común de cierres.", "Baixe o driver mais novo da NVIDIA, AMD ou Intel. Drivers antigos são a causa mais comum de travamentos." },
            new[] { "Downloads a fresh copy of Roblox next time you launch. Fixes broken or half-updated files.", "Descarga una copia nueva de Roblox la próxima vez que lo abras. Arregla archivos dañados o a medio actualizar.", "Baixa uma cópia nova do Roblox na próxima vez que você abrir. Corrige arquivos quebrados ou atualizados pela metade." },
            new[] { "Close Roblox first", "Cierra Roblox primero", "Feche o Roblox primeiro" },
            new[] { "Roblox is still using its files. Close every Roblox window, then press Repair again.", "Roblox todavía está usando sus archivos. Cierra todas las ventanas de Roblox y vuelve a pulsar Reparar.", "O Roblox ainda está usando os arquivos. Feche todas as janelas do Roblox e aperte Reparar de novo." },
            new[] { "Close", "Cerrar", "Fechar" },
            new[] { "Friends", "Amigos", "Amigos" },
            new[] { "Discord", "Discord", "Discord" },
            new[] { "Rejoin last server", "Volver al último servidor", "Voltar ao último servidor" },
            new[] { "Server hop", "Cambiar de servidor", "Trocar de servidor" },
            new[] { "Small server", "Servidor pequeño", "Servidor pequeno" },
            new[] { "Join a game first. GuideStrap remembers the last server you were in.", "Entra a un juego primero. GuideStrap recuerda el último servidor en el que estuviste.", "Entre em um jogo primeiro. O GuideStrap lembra o último servidor em que você esteve." },
            new[] { "Paste a game link first, or join a game once.", "Pega primero un enlace de juego, o entra a un juego una vez.", "Cole primeiro um link de jogo, ou entre em um jogo uma vez." },
            new[] { "Finding a small server...", "Buscando un servidor pequeño...", "Procurando um servidor pequeno..." },
            new[] { "Finding another server...", "Buscando otro servidor...", "Procurando outro servidor..." },
            new[] { "Couldn't find an open server for that game. Try again in a moment.", "No se encontró un servidor libre para ese juego. Intenta de nuevo en un momento.", "Não foi encontrado um servidor livre para esse jogo. Tente de novo daqui a pouco." },
            new[] { "Applying your {0} setup...", "Aplicando tu configuración {0}...", "Aplicando sua configuração {0}..." },
            new[] { "Signing in...", "Iniciando sesión...", "Entrando..." },
            new[] { "Streamer mode is on", "El modo streamer está activado", "O modo streamer está ligado" },
            new[] { "Account names are hidden.", "Los nombres de cuenta están ocultos.", "Os nomes das contas estão ocultos." },
            new[] { "Streamer mode", "Modo streamer", "Modo streamer" },
            new[] { "Names hidden", "Nombres ocultos", "Nomes ocultos" },
            new[] { "hidden", "oculta", "oculta" },
            new[] { "Hides account names and server location in GuideStrap and on Discord.", "Oculta los nombres de cuenta y la ubicación del servidor en GuideStrap y en Discord.", "Oculta os nomes das contas e o local do servidor no GuideStrap e no Discord." },
            new[] { "Session summary", "Resumen de la sesión", "Resumo da sessão" },
            new[] { "After you close Roblox, a small popup shows how long you played.", "Al cerrar Roblox, un aviso pequeño muestra cuánto jugaste.", "Quando você fecha o Roblox, um aviso pequeno mostra quanto tempo você jogou." },
            new[] { "Session over", "Sesión terminada", "Sessão encerrada" },
            new[] { "You played for {0}.", "Jugaste {0}.", "Você jogou por {0}." },
            new[] { "Games: {0}", "Juegos: {0}", "Jogos: {0}" },
            new[] { "Last server: {0}", "Último servidor: {0}", "Último servidor: {0}" },
            new[] { "Friend started playing", "Un amigo empezó a jugar", "Um amigo começou a jogar" },
            new[] { "{0} is playing {1}.", "{0} está jugando {1}.", "{0} está jogando {1}." },
            new[] { "A friend", "Un amigo", "Um amigo" },
            new[] { "a game", "un juego", "um jogo" },
            new[] { "Join", "Unirse", "Entrar" },
            new[] { "Join game", "Unirse al juego", "Entrar no jogo" },
            new[] { "See who's online and join them.", "Mira quién está en línea y únete.", "Veja quem está online e entre com eles." },
            new[] { "Refresh", "Actualizar", "Atualizar" },
            new[] { "Online friends", "Amigos en línea", "Amigos online" },
            new[] { "Friend alerts", "Avisos de amigos", "Avisos de amigos" },
            new[] { "A small popup when a friend starts playing a game.", "Un aviso pequeño cuando un amigo empieza a jugar.", "Um aviso pequeno quando um amigo começa a jogar." },
            new[] { "Open this page to load your friends.", "Abre esta página para cargar a tus amigos.", "Abra esta página para carregar seus amigos." },
            new[] { "Pick a saved account in Accounts to see your friends.", "Elige una cuenta guardada en Cuentas para ver a tus amigos.", "Escolha uma conta salva em Contas para ver seus amigos." },
            new[] { "Loading...", "Cargando...", "Carregando..." },
            new[] { "None of your {0} friends are online right now.", "Ninguno de tus {0} amigos está en línea ahora.", "Nenhum dos seus {0} amigos está online agora." },
            new[] { "{0} of {1} friends online.", "{0} de {1} amigos en línea.", "{0} de {1} amigos online." },
            new[] { "Couldn't load friends. {0}", "No se pudieron cargar los amigos. {0}", "Não foi possível carregar os amigos. {0}" },
            new[] { "Playing {0}", "Jugando {0}", "Jogando {0}" },
            new[] { "In Roblox Studio", "En Roblox Studio", "No Roblox Studio" },
            new[] { "Online", "En línea", "Online" },
            new[] { "What your Discord profile shows while you use GuideStrap.", "Lo que muestra tu perfil de Discord mientras usas GuideStrap.", "O que seu perfil do Discord mostra enquanto você usa o GuideStrap." },
            new[] { "Discord status", "Estado de Discord", "Status do Discord" },
            new[] { "Shows GuideStrap and what you're doing on your Discord profile.", "Muestra GuideStrap y lo que haces en tu perfil de Discord.", "Mostra o GuideStrap e o que você está fazendo no seu perfil do Discord." },
            new[] { "What to show", "Qué mostrar", "O que mostrar" },
            new[] { "Show the game you're in", "Mostrar el juego en el que estás", "Mostrar o jogo em que você está" },
            new[] { "The game's name, picture and a View game button.", "El nombre del juego, su imagen y un botón Ver juego.", "O nome do jogo, a imagem e um botão Ver jogo." },
            new[] { "Show the account you're using", "Mostrar la cuenta que usas", "Mostrar a conta que você está usando" },
            new[] { "Adds \"Playing as\" with your display name.", "Agrega \"Jugando como\" con tu nombre visible.", "Adiciona \"Jogando como\" com seu nome de exibição." },
            new[] { "Let friends join you", "Dejar que tus amigos se unan", "Deixar amigos entrarem com você" },
            new[] { "Adds a Join server button that puts friends in your server.", "Agrega un botón Unirse al servidor que lleva a tus amigos a tu servidor.", "Adiciona um botão Entrar no servidor que coloca seus amigos no seu servidor." },
            new[] { "Discord has to be open on this PC. Streamer mode (in Settings) hides your account, server location and the Join button.", "Discord tiene que estar abierto en esta PC. El modo streamer (en Ajustes) oculta tu cuenta, la ubicación del servidor y el botón Unirse.", "O Discord precisa estar aberto neste PC. O modo streamer (em Configurações) oculta sua conta, o local do servidor e o botão Entrar." },
            new[] { "Mod gallery", "Galería de mods", "Galeria de mods" },
            new[] { "Skies, cursors, crosshairs and sound packs you can install in one click.", "Cielos, cursores, miras y paquetes de sonidos que instalas con un clic.", "Céus, cursores, miras e pacotes de sons que você instala com um clique." },
            new[] { "Browse", "Explorar", "Explorar" },
            new[] { "Loading the gallery...", "Cargando la galería...", "Carregando a galeria..." },
            new[] { "Only cosmetic files: images, skies and sounds. Everything installs into GuideStrap's copy of Roblox.", "Solo archivos cosméticos: imágenes, cielos y sonidos. Todo se instala en la copia de Roblox de GuideStrap.", "Só arquivos cosméticos: imagens, céus e sons. Tudo é instalado na cópia do Roblox do GuideStrap." },
            new[] { "The gallery is empty right now.", "La galería está vacía por ahora.", "A galeria está vazia por enquanto." },
            new[] { "{0} mods", "{0} mods", "{0} mods" },
            new[] { "Couldn't load the gallery. Check your internet and try again.", "No se pudo cargar la galería. Revisa tu internet e intenta de nuevo.", "Não foi possível carregar a galeria. Verifique sua internet e tente de novo." },
            new[] { "Sky", "Cielo", "Céu" },
            new[] { "Cursor", "Cursor", "Cursor" },
            new[] { "Crosshair", "Mira", "Mira" },
            new[] { "by {0}", "por {0}", "por {0}" },
            new[] { "Install", "Instalar", "Instalar" },
            new[] { "Installing...", "Instalando...", "Instalando..." },
            new[] { "Installed", "Instalado", "Instalado" },
            new[] { "Couldn't install that", "No se pudo instalar", "Não foi possível instalar" },
            new[] { "That mod is too big.", "Ese mod es demasiado grande.", "Esse mod é grande demais." },
            new[] { "That image couldn't be read.", "No se pudo leer esa imagen.", "Não foi possível ler essa imagem." },
            new[] { "That sky pack has no .tex files.", "Ese paquete de cielo no tiene archivos .tex.", "Esse pacote de céu não tem arquivos .tex." },
            new[] { "That sound pack has no .ogg or .mp3 files.", "Ese paquete de sonidos no tiene archivos .ogg ni .mp3.", "Esse pacote de sons não tem arquivos .ogg nem .mp3." },
            new[] { "Link game", "Vincular juego", "Vincular jogo" },
            new[] { "Auto setups", "Configuraciones automáticas", "Configurações automáticas" },
            new[] { "Remove all links?", "¿Quitar todos los vínculos?", "Remover todos os vínculos?" },
            new[] { "Every game goes back to your normal settings.", "Todos los juegos vuelven a tus ajustes normales.", "Todos os jogos voltam para suas configurações normais." },
            new[] { "Remove", "Quitar", "Remover" },
            new[] { "Link a setup to a game and it turns on by itself when you launch that game.", "Vincula una configuración a un juego y se activará sola al abrir ese juego.", "Vincule uma configuração a um jogo e ela liga sozinha quando você abre esse jogo." },
            new[] { "  +{0} more", "  +{0} más", "  +{0} mais" },
            new[] { "Choose a saved setup in the list above, then link it to a game.", "Elige una configuración guardada en la lista de arriba y luego vincúlala a un juego.", "Escolha uma configuração salva na lista acima e depois vincule a um jogo." },
            new[] { "Link {0} to a game", "Vincular {0} a un juego", "Vincular {0} a um jogo" },
            new[] { "Paste the game link or place ID. GuideStrap switches to this setup whenever you launch that game.", "Pega el enlace del juego o el ID del lugar. GuideStrap usará esta configuración cada vez que abras ese juego.", "Cole o link do jogo ou o ID do lugar. O GuideStrap usa esta configuração sempre que você abrir esse jogo." },
            new[] { "Link", "Vincular", "Vincular" },
            new[] { "That doesn't look like a game link", "Eso no parece un enlace de juego", "Isso não parece um link de jogo" },
            new[] { "Paste a link like roblox.com/games/123456, or just the number.", "Pega un enlace como roblox.com/games/123456, o solo el número.", "Cole um link como roblox.com/games/123456, ou só o número." },
            new[] { "Linked {0}.", "{0} vinculada.", "{0} vinculada." },
            new[] { "Rejoin your server", "Vuelve a tu servidor", "Volte ao seu servidor" },
            new[] { "Jump straight back into the server you were in, if it's still running.", "Vuelve directo al servidor en el que estabas, si sigue abierto.", "Volte direto para o servidor em que você estava, se ele ainda estiver aberto." },
            new[] { "Rejoin", "Volver a unirse", "Voltar" },
            new[] { "Checking Roblox's servers...", "Revisando los servidores de Roblox...", "Verificando os servidores do Roblox..." },
            new[] { "You're offline", "No tienes conexión", "Você está offline" },
            new[] { "GuideStrap can't reach the internet. Check your Wi-Fi or cable, then try again.", "GuideStrap no puede conectarse a internet. Revisa tu Wi-Fi o cable y vuelve a intentarlo.", "O GuideStrap não consegue acessar a internet. Verifique seu Wi-Fi ou cabo e tente de novo." },
            new[] { "Try again", "Intentar de nuevo", "Tentar de novo" },
            new[] { "Roblox looks down", "Roblox parece caído", "O Roblox parece fora do ar" },
            new[] { "Roblox's servers aren't answering right now, so games might not load. This is on Roblox's side, not your PC.", "Los servidores de Roblox no responden ahora, así que los juegos podrían no cargar. Es un problema de Roblox, no de tu PC.", "Os servidores do Roblox não estão respondendo agora, então os jogos podem não carregar. O problema é do Roblox, não do seu PC." },
            new[] { "Try anyway", "Intentar igual", "Tentar mesmo assim" },
            new[] { "Status page", "Página de estado", "Página de status" },
            new[] { "Start screen background", "Fondo de la pantalla de inicio", "Fundo da tela inicial" },
            new[] { "Your own picture behind the start screen. It's darkened so text stays readable.", "Tu propia imagen detrás de la pantalla de inicio. Se oscurece para que el texto se lea bien.", "Sua própria imagem atrás da tela inicial. Ela fica mais escura para o texto continuar legível." },
            new[] { "Choose image", "Elegir imagen", "Escolher imagem" },
            new[] { "The start screen is back to plain.", "La pantalla de inicio volvió a ser lisa.", "A tela inicial voltou ao normal." },
            new[] { "Pick a picture", "Elige una imagen", "Escolha uma imagem" },
            new[] { "That picture is too small.", "Esa imagen es demasiado pequeña.", "Essa imagem é pequena demais." },
            new[] { "Background set. It shows on the start screen.", "Fondo listo. Se ve en la pantalla de inicio.", "Fundo definido. Ele aparece na tela inicial." },
            new[] { "Couldn't use that picture", "No se pudo usar esa imagen", "Não foi possível usar essa imagem" },
            new[] { "Start with Windows", "Iniciar con Windows", "Iniciar com o Windows" },
            new[] { "Opens quietly in the tray when your PC starts, so friend alerts work.", "Se abre en silencio en la bandeja al encender la PC, para que funcionen los avisos de amigos.", "Abre discretamente na bandeja quando o PC liga, para os avisos de amigos funcionarem." },
            new[] { "Opens quietly in the tray when your PC starts.", "Se abre en silencio en la bandeja al encender la PC.", "Abre discretamente na bandeja quando o PC liga." },
            new[] { "Install GuideStrap first", "Instala GuideStrap primero", "Instale o GuideStrap primeiro" },
            new[] { "This only works for the installed GuideStrap, not a copy you're running from somewhere else.", "Esto solo funciona con el GuideStrap instalado, no con una copia que abras desde otro lugar.", "Isso só funciona com o GuideStrap instalado, não com uma cópia aberta de outro lugar." },
            new[] { "Uninstall", "Desinstalar", "Desinstalar" },
            new[] { "Uninstall GuideStrap", "Desinstalar GuideStrap", "Desinstalar o GuideStrap" },
            new[] { "Removes GuideStrap, its settings and its copy of Roblox. Normal Roblox stays.", "Quita GuideStrap, sus ajustes y su copia de Roblox. Roblox normal se queda.", "Remove o GuideStrap, as configurações e a cópia do Roblox dele. O Roblox normal continua." },
            new[] { "GuideStrap isn't installed", "GuideStrap no está instalado", "O GuideStrap não está instalado" },
            new[] { "This copy isn't installed, so there's nothing to remove.", "Esta copia no está instalada, así que no hay nada que quitar.", "Esta cópia não está instalada, então não há nada para remover." },
            new[] { "The language GuideStrap uses.", "El idioma que usa GuideStrap.", "O idioma que o GuideStrap usa." },
            new[] { "What's new in GuideStrap {0}", "Novedades de GuideStrap {0}", "Novidades do GuideStrap {0}" },
            new[] { "Nice", "Genial", "Legal" },
            new[] { "• GuideStrap checks if Roblox is down before launching\n• Pick your own picture for the start screen (Settings > Appearance)\n• Start GuideStrap with Windows, quietly in the tray (Settings)\n• Uninstall GuideStrap from Settings > Tools\n• New languages: Français, Deutsch, Türkçe and Filipino\n• This window, after every update", "• GuideStrap revisa si Roblox está caído antes de abrirlo\n• Elige tu propia imagen para la pantalla de inicio (Ajustes > Apariencia)\n• Inicia GuideStrap con Windows, en silencio en la bandeja (Ajustes)\n• Desinstala GuideStrap desde Ajustes > Herramientas\n• Idiomas nuevos: Français, Deutsch, Türkçe y Filipino\n• Esta ventana, después de cada actualización", "• O GuideStrap verifica se o Roblox está fora do ar antes de abrir\n• Escolha sua própria imagem para a tela inicial (Configurações > Aparência)\n• Inicie o GuideStrap com o Windows, discretamente na bandeja (Configurações)\n• Desinstale o GuideStrap em Configurações > Ferramentas\n• Idiomas novos: Français, Deutsch, Türkçe e Filipino\n• Esta janela, depois de cada atualização" },
            new[] { "This removes GuideStrap and its copy of Roblox. Normal Roblox stays installed.", "Esto quita GuideStrap y su copia de Roblox. Roblox normal sigue instalado.", "Isso remove o GuideStrap e a cópia do Roblox dele. O Roblox normal continua instalado." },
            new[] { "Keep my settings, accounts and mods in case I reinstall", "Guardar mis ajustes, cuentas y mods por si lo reinstalo", "Manter minhas configurações, contas e mods caso eu reinstale" },
            new[] { "GuideStrap was uninstalled. Your settings, accounts and mods are kept for next time.", "GuideStrap se desinstaló. Tus ajustes, cuentas y mods se guardan para la próxima vez.", "O GuideStrap foi desinstalado. Suas configurações, contas e mods ficam guardados para a próxima vez." },
            new[] { "• Black is now the default theme (Purple is still in Settings > Appearance)\n• New black logo for the app, the taskbar, Discord and the website\n• Uninstalling can now keep your settings, accounts and mods for a reinstall", "• Negro es ahora el tema predeterminado (Morado sigue en Ajustes > Apariencia)\n• Nuevo logo negro para la app, la barra de tareas, Discord y la web\n• Al desinstalar ahora puedes guardar tus ajustes, cuentas y mods para reinstalar", "• Preto agora é o tema padrão (Roxo continua em Configurações > Aparência)\n• Novo logo preto no app, na barra de tarefas, no Discord e no site\n• Ao desinstalar, agora dá para manter suas configurações, contas e mods para reinstalar" },
            new[] { "• The logo is now black in every theme, Purple included\n• Fixed desktop and Start menu icons keeping the old look after an update\n• Black is now the default theme (Purple is still in Settings > Appearance)\n• New black logo for the app, the taskbar, Discord and the website\n• Uninstalling can now keep your settings, accounts and mods for a reinstall", "• El logo ahora es negro en todos los temas, incluido Morado\n• Arreglado: los iconos del escritorio y del menú Inicio ya no se quedan con el aspecto viejo tras actualizar\n• Negro es ahora el tema predeterminado (Morado sigue en Ajustes > Apariencia)\n• Nuevo logo negro para la app, la barra de tareas, Discord y la web\n• Al desinstalar ahora puedes guardar tus ajustes, cuentas y mods para reinstalar", "• O logo agora é preto em todos os temas, incluindo o Roxo\n• Corrigido: os ícones da área de trabalho e do menu Iniciar não ficam mais com a aparência antiga depois de atualizar\n• Preto agora é o tema padrão (Roxo continua em Configurações > Aparência)\n• Novo logo preto no app, na barra de tarefas, no Discord e no site\n• Ao desinstalar, agora dá para manter suas configurações, contas e mods para reinstalar" }
        };

        // English, Français, Deutsch, Türkçe, Filipino
        static readonly string[][] More =
        {
            new[] { "Join a game (optional)", "Rejoindre un jeu (facultatif)", "Einem Spiel beitreten (optional)", "Bir oyuna katıl (isteğe bağlı)", "Sumali sa isang laro (opsyonal)" },
            new[] { "Paste a game link or place ID. Leave it empty to just open Roblox.", "Colle un lien de jeu ou un ID de place. Laisse vide pour juste ouvrir Roblox.", "Füge einen Spiellink oder eine Place-ID ein. Leer lassen, um nur Roblox zu öffnen.", "Bir oyun bağlantısı veya place ID yapıştır. Sadece Roblox'u açmak için boş bırak.", "Mag-paste ng link ng laro o place ID. Iwanang blangko para buksan lang ang Roblox." },
            new[] { "Games you join show up here so you can jump back in.", "Les jeux que tu rejoins s'affichent ici pour y retourner vite.", "Spiele, denen du beitrittst, erscheinen hier, damit du schnell zurückkannst.", "Katıldığın oyunlar burada görünür, böylece hemen geri dönebilirsin.", "Lalabas dito ang mga larong sinalihan mo para madali kang makabalik." },
            new[] { "Logins are encrypted and only work for your Windows user on this PC. Don't press \"Log out\" on the Roblox website for a saved account, or its saved login stops working.", "Les connexions sont chiffrées et ne marchent que pour ton utilisateur Windows sur ce PC. N'appuie pas sur \"Se déconnecter\" sur le site Roblox pour un compte enregistré, sinon sa connexion enregistrée ne marchera plus.", "Anmeldungen sind verschlüsselt und funktionieren nur für deinen Windows-Benutzer auf diesem PC. Drück auf der Roblox-Website bei einem gespeicherten Konto nicht auf \"Abmelden\", sonst funktioniert die gespeicherte Anmeldung nicht mehr.", "Girişler şifrelenir ve sadece bu bilgisayardaki Windows kullanıcın için çalışır. Kayıtlı bir hesap için Roblox sitesinde \"Çıkış yap\"a basma, yoksa kayıtlı girişi çalışmaz.", "Naka-encrypt ang mga login at gagana lang para sa Windows user mo sa PC na ito. Huwag pindutin ang \"Log out\" sa Roblox website para sa naka-save na account, kundi titigil na ang naka-save na login nito." },
            new[] { "Preset", "Préréglage", "Voreinstellung", "Ön ayar", "Preset" },
            new[] { "Max FPS", "FPS max", "Max. FPS", "Maks FPS", "Max FPS" },
            new[] { "Roblox only reads flags on its allowlist, so anything else gets skipped automatically.", "Roblox ne lit que les flags de sa liste autorisée, le reste est ignoré automatiquement.", "Roblox liest nur Flags von seiner Erlaubnisliste, alles andere wird automatisch übersprungen.", "Roblox sadece izin listesindeki flag'leri okur, geri kalanı otomatik olarak atlanır.", "Ang mga flag lang na nasa allowlist ang binabasa ng Roblox, kaya awtomatikong nilalaktawan ang iba." },
            new[] { "Current font", "Police actuelle", "Aktuelle Schriftart", "Mevcut yazı tipi", "Kasalukuyang font" },
            new[] { "Works with .ttf and .otf files (free fonts: fonts.google.com). Emoji keep their normal look. Applies next time you press Play.", "Fonctionne avec les fichiers .ttf et .otf (polices gratuites : fonts.google.com). Les emojis gardent leur look normal. S'applique au prochain clic sur Jouer.", "Funktioniert mit .ttf- und .otf-Dateien (kostenlose Schriften: fonts.google.com). Emojis behalten ihr normales Aussehen. Gilt beim nächsten Klick auf Spielen.", ".ttf ve .otf dosyalarıyla çalışır (ücretsiz yazı tipleri: fonts.google.com). Emojiler normal görünür. Bir sonraki Oyna'ya basışında uygulanır.", "Gumagana sa .ttf at .otf na file (libreng font: fonts.google.com). Normal pa rin ang itsura ng emoji. Gagana sa susunod mong pagpindot ng Play." },
            new[] { "Normal Roblox is never touched. Launching Roblox without GuideStrap always uses the original files, sounds and settings.", "Roblox normal n'est jamais modifié. Lancer Roblox sans GuideStrap utilise toujours les fichiers, sons et réglages d'origine.", "Normales Roblox wird nie verändert. Roblox ohne GuideStrap zu starten nutzt immer die Originaldateien, -sounds und -einstellungen.", "Normal Roblox'a asla dokunulmaz. Roblox'u GuideStrap olmadan açmak her zaman orijinal dosyaları, sesleri ve ayarları kullanır.", "Hindi kailanman ginagalaw ang normal na Roblox. Kapag binuksan ang Roblox nang walang GuideStrap, laging orihinal na files, tunog at settings ang gamit." },
            new[] { "Version {0}  ·  by Guide", "Version {0}  ·  par Guide", "Version {0}  ·  von Guide", "Sürüm {0}  ·  Guide tarafından", "Bersyon {0}  ·  gawa ni Guide" },
            new[] { "FastFlag editor", "Éditeur de FastFlags", "FastFlag-Editor", "FastFlag düzenleyici", "FastFlag editor" },
            new[] { "Only flags on Roblox's allowlist are kept. Adding one you already have asks before replacing it.", "Seuls les flags de la liste autorisée de Roblox sont gardés. Si tu ajoutes un flag que tu as déjà, on te demande avant de le remplacer.", "Nur Flags von Roblox' Erlaubnisliste werden behalten. Fügst du ein Flag hinzu, das du schon hast, wirst du vor dem Ersetzen gefragt.", "Sadece Roblox'un izin listesindeki flag'ler tutulur. Zaten olan bir flag'i eklersen değiştirmeden önce sorulur.", "Ang mga flag lang na nasa allowlist ng Roblox ang itinatabi. Kapag nagdagdag ka ng flag na meron ka na, tatanungin ka muna bago palitan." },
            new[] { "Tip: press Enter in the value box to add.", "Astuce : appuie sur Entrée dans la case de valeur pour ajouter.", "Tipp: Drück Enter im Wertfeld, um hinzuzufügen.", "İpucu: Eklemek için değer kutusunda Enter'a bas.", "Tip: pindutin ang Enter sa value box para magdagdag." },
            new[] { "Paste FastFlags JSON", "Coller du JSON de FastFlags", "FastFlags-JSON einfügen", "FastFlags JSON yapıştır", "I-paste ang FastFlags JSON" },
            new[] { "Flags that aren't on Roblox's allowlist are skipped.", "Les flags qui ne sont pas sur la liste autorisée de Roblox sont ignorés.", "Flags, die nicht auf Roblox' Erlaubnisliste stehen, werden übersprungen.", "Roblox'un izin listesinde olmayan flag'ler atlanır.", "Nilalaktawan ang mga flag na wala sa allowlist ng Roblox." },
            new[] { "Account: {0}", "Compte : {0}", "Konto: {0}", "Hesap: {0}", "Account: {0}" },
            new[] { "Launch Roblox", "Lancer Roblox", "Roblox starten", "Roblox'u başlat", "I-launch ang Roblox" },
            new[] { "Add account", "Ajouter un compte", "Konto hinzufügen", "Hesap ekle", "Magdagdag ng account" },
            new[] { "Use", "Utiliser", "Verwenden", "Kullan", "Gamitin" },
            new[] { "Open editor", "Ouvrir l'éditeur", "Editor öffnen", "Düzenleyiciyi aç", "Buksan ang editor" },
            new[] { "Choose font file", "Choisir un fichier de police", "Schriftdatei wählen", "Yazı tipi dosyası seç", "Pumili ng font file" },
            new[] { "Add flag", "Ajouter un flag", "Flag hinzufügen", "Flag ekle", "Magdagdag ng flag" },
            new[] { "Add", "Ajouter", "Hinzufügen", "Ekle", "Idagdag" },
            new[] { "Import", "Importer", "Importieren", "İçe aktar", "I-import" },
            new[] { "Done", "Terminé", "Fertig", "Tamam", "Tapos na" },
            new[] { "Main menu", "Menu principal", "Hauptmenü", "Ana menü", "Main menu" },
            new[] { "Close", "Fermer", "Schließen", "Kapat", "Isara" },
            new[] { "Switch account", "Changer de compte", "Konto wechseln", "Hesap değiştir", "Magpalit ng account" },
            new[] { "Active", "Actif", "Aktiv", "Aktif", "Aktibo" },
            new[] { "Use default font", "Police par défaut", "Standardschrift verwenden", "Varsayılan yazı tipini kullan", "Gamitin ang default na font" },
            new[] { "Choose .ogg", "Choisir un .ogg", ".ogg wählen", ".ogg seç", "Pumili ng .ogg" },
            new[] { "Reset", "Réinitialiser", "Zurücksetzen", "Sıfırla", "I-reset" },
            new[] { "Choose sky folder", "Choisir un dossier de ciel", "Himmel-Ordner wählen", "Gökyüzü klasörü seç", "Pumili ng sky folder" },
            new[] { "Open mods folder", "Ouvrir le dossier des mods", "Mod-Ordner öffnen", "Mod klasörünü aç", "Buksan ang mods folder" },
            new[] { "Export", "Exporter", "Exportieren", "Dışa aktar", "I-export" },
            new[] { "Open folder", "Ouvrir le dossier", "Ordner öffnen", "Klasörü aç", "Buksan ang folder" },
            new[] { "Clear cache", "Vider le cache", "Cache leeren", "Önbelleği temizle", "I-clear ang cache" },
            new[] { "Repair", "Réparer", "Reparieren", "Onar", "Ayusin" },
            new[] { "Check now", "Vérifier", "Jetzt prüfen", "Şimdi kontrol et", "Tingnan ngayon" },
            new[] { "Import JSON", "Importer du JSON", "JSON importieren", "JSON içe aktar", "I-import ang JSON" },
            new[] { "Copy JSON", "Copier le JSON", "JSON kopieren", "JSON kopyala", "Kopyahin ang JSON" },
            new[] { "Clear all", "Tout effacer", "Alle löschen", "Hepsini temizle", "Burahin lahat" },
            new[] { "Cancel", "Annuler", "Abbrechen", "İptal", "Kanselahin" },
            new[] { "Add an account", "Ajouter un compte", "Konto hinzufügen", "Hesap ekle", "Magdagdag ng account" },
            new[] { "Opens the Roblox login page.", "Ouvre la page de connexion Roblox.", "Öffnet die Roblox-Anmeldeseite.", "Roblox giriş sayfasını açar.", "Binubuksan ang login page ng Roblox." },
            new[] { "Add, import or edit flags.", "Ajoute, importe ou modifie des flags.", "Flags hinzufügen, importieren oder bearbeiten.", "Flag ekle, içe aktar veya düzenle.", "Magdagdag, mag-import o mag-edit ng mga flag." },
            new[] { "Mods folder", "Dossier des mods", "Mod-Ordner", "Mod klasörü", "Mods folder" },
            new[] { "Files here replace the matching files in Roblox (same folder layout).", "Les fichiers ici remplacent ceux de Roblox qui ont le même nom (même structure de dossiers).", "Dateien hier ersetzen die passenden Dateien in Roblox (gleiche Ordnerstruktur).", "Buradaki dosyalar Roblox'taki aynı adlı dosyaların yerine geçer (aynı klasör düzeni).", "Pinapalitan ng mga file dito ang katugmang file sa Roblox (parehong ayos ng folder)." },
            new[] { "Use my selected account for website joins", "Utiliser mon compte choisi pour le site", "Mein gewähltes Konto für Website-Beitritte nutzen", "Site katılımlarında seçtiğim hesabı kullan", "Gamitin ang napili kong account sa pagsali mula sa website" },
            new[] { "Play buttons on roblox.com join as the account picked here.", "Les boutons Jouer de roblox.com rejoignent avec le compte choisi ici.", "Spielen-Buttons auf roblox.com treten mit dem hier gewählten Konto bei.", "roblox.com'daki Oyna düğmeleri burada seçilen hesapla katılır.", "Ang mga Play button sa roblox.com ay sasali gamit ang account na napili dito." },
            new[] { "Close this window when launching", "Fermer cette fenêtre au lancement", "Dieses Fenster beim Start schließen", "Başlatırken bu pencereyi kapat", "Isara ang window na ito pag nag-launch" },
            new[] { "Only the loading screen stays open while Roblox starts.", "Seul l'écran de chargement reste ouvert pendant que Roblox démarre.", "Nur der Ladebildschirm bleibt offen, während Roblox startet.", "Roblox açılırken sadece yükleme ekranı açık kalır.", "Loading screen lang ang naiiwang bukas habang nagsisimula ang Roblox." },
            new[] { "Stay in the tray while Roblox is open", "Rester dans la barre des tâches pendant que Roblox est ouvert", "Im Infobereich bleiben, solange Roblox offen ist", "Roblox açıkken sistem tepsisinde kal", "Manatili sa tray habang bukas ang Roblox" },
            new[] { "Needed for Discord status, server location and recent games.", "Nécessaire pour le statut Discord, la position du serveur et les jeux récents.", "Nötig für den Discord-Status, den Serverstandort und zuletzt gespielte Spiele.", "Discord durumu, sunucu konumu ve son oyunlar için gerekli.", "Kailangan para sa Discord status, lokasyon ng server at mga recent na laro." },
            new[] { "Show server location", "Afficher la position du serveur", "Serverstandort anzeigen", "Sunucu konumunu göster", "Ipakita ang lokasyon ng server" },
            new[] { "Pops up where the server you joined is, like \"Dallas, Texas, US\".", "Indique où se trouve le serveur rejoint, comme \"Dallas, Texas, US\".", "Zeigt, wo der beigetretene Server steht, z. B. \"Dallas, Texas, US\".", "Katıldığın sunucunun nerede olduğunu gösterir, örneğin \"Dallas, Texas, US\".", "Lalabas kung nasaan ang server na sinalihan mo, gaya ng \"Dallas, Texas, US\"." },
            new[] { "Allow multiple Roblox windows", "Autoriser plusieurs fenêtres Roblox", "Mehrere Roblox-Fenster erlauben", "Birden fazla Roblox penceresine izin ver", "Payagan ang maraming Roblox window" },
            new[] { "Run more than one account at once. Roblox sometimes blocks this.", "Lance plusieurs comptes en même temps. Roblox bloque parfois ça.", "Mehrere Konten gleichzeitig spielen. Roblox blockiert das manchmal.", "Aynı anda birden fazla hesap aç. Roblox bazen bunu engeller.", "Maglaro gamit ang higit sa isang account nang sabay. Minsan hinaharangan ito ng Roblox." },
            new[] { "Open Roblox links with GuideStrap", "Ouvrir les liens Roblox avec GuideStrap", "Roblox-Links mit GuideStrap öffnen", "Roblox bağlantılarını GuideStrap ile aç", "Buksan ang mga Roblox link gamit ang GuideStrap" },
            new[] { "Off gives Play buttons on roblox.com back to normal Roblox.", "Désactivé, les boutons Jouer de roblox.com repassent à Roblox normal.", "Aus gibt die Spielen-Buttons auf roblox.com an normales Roblox zurück.", "Kapalıyken roblox.com'daki Oyna düğmeleri normal Roblox'a geri döner.", "Kapag naka-off, babalik sa normal na Roblox ang mga Play button sa roblox.com." },
            new[] { "Discord status", "Statut Discord", "Discord-Status", "Discord durumu", "Discord status" },
            new[] { "Shows on your profile what you're doing in GuideStrap and which game you're in.", "Affiche sur ton profil ce que tu fais dans GuideStrap et dans quel jeu tu es.", "Zeigt auf deinem Profil, was du in GuideStrap machst und in welchem Spiel du bist.", "Profilinde GuideStrap'te ne yaptığını ve hangi oyunda olduğunu gösterir.", "Ipinapakita sa profile mo kung ano ang ginagawa mo sa GuideStrap at kung anong laro ka." },
            new[] { "Accent color", "Couleur d'accent", "Akzentfarbe", "Vurgu rengi", "Accent color" },
            new[] { "The color used for buttons, switches and highlights.", "La couleur des boutons, interrupteurs et surlignages.", "Die Farbe für Buttons, Schalter und Hervorhebungen.", "Düğmeler, anahtarlar ve vurgular için kullanılan renk.", "Ang kulay ng mga button, switch at highlight." },
            new[] { "Settings profile", "Profil de réglages", "Einstellungsprofil", "Ayar profili", "Settings profile" },
            new[] { "Share your graphics, FastFlags, cursor and color as one file.", "Partage tes graphismes, FastFlags, curseur et couleur dans un seul fichier.", "Teile deine Grafik, FastFlags, deinen Cursor und deine Farbe als eine Datei.", "Grafiklerini, FastFlags'lerini, imlecini ve rengini tek bir dosya olarak paylaş.", "I-share ang graphics, FastFlags, cursor at kulay mo bilang isang file." },
            new[] { "GuideStrap folder", "Dossier de GuideStrap", "GuideStrap-Ordner", "GuideStrap klasörü", "GuideStrap folder" },
            new[] { "Settings, mods and the downloaded Roblox live here.", "Les réglages, les mods et le Roblox téléchargé sont ici.", "Hier liegen Einstellungen, Mods und das heruntergeladene Roblox.", "Ayarlar, modlar ve indirilen Roblox burada durur.", "Nandito ang settings, mods at ang na-download na Roblox." },
            new[] { "Deletes Roblox's temporary files, old logs and cached pictures.", "Supprime les fichiers temporaires de Roblox, les vieux journaux et les images en cache.", "Löscht Roblox' temporäre Dateien, alte Logs und zwischengespeicherte Bilder.", "Roblox'un geçici dosyalarını, eski kayıtlarını ve önbellekteki resimleri siler.", "Binubura ang mga pansamantalang file ng Roblox, lumang log at naka-cache na larawan." },
            new[] { "Repair Roblox", "Réparer Roblox", "Roblox reparieren", "Roblox'u onar", "Ayusin ang Roblox" },
            new[] { "Fixes crashes or a broken install by downloading Roblox again.", "Corrige les plantages ou une installation cassée en retéléchargeant Roblox.", "Behebt Abstürze oder eine kaputte Installation, indem Roblox neu heruntergeladen wird.", "Roblox'u yeniden indirerek çökmeleri veya bozuk kurulumu düzeltir.", "Inaayos ang crash o sirang install sa pag-download ulit ng Roblox." },
            new[] { "Updates", "Mises à jour", "Updates", "Güncellemeler", "Updates" },
            new[] { "You're on GuideStrap {0}.", "Tu as GuideStrap {0}.", "Du hast GuideStrap {0}.", "GuideStrap {0} kullanıyorsun.", "GuideStrap {0} ang gamit mo." },
            new[] { "Recent games", "Jeux récents", "Zuletzt gespielt", "Son oyunlar", "Mga recent na laro" },
            new[] { "Active flags", "Flags actifs", "Aktive Flags", "Aktif flag'ler", "Mga aktibong flag" },
            new[] { "Your own mods", "Tes propres mods", "Deine eigenen Mods", "Kendi modların", "Sarili mong mods" },
            new[] { "Behavior", "Comportement", "Verhalten", "Davranış", "Gawi" },
            new[] { "Discord", "Discord", "Discord", "Discord", "Discord" },
            new[] { "Appearance", "Apparence", "Aussehen", "Görünüm", "Itsura" },
            new[] { "Profiles", "Profils", "Profile", "Profiller", "Mga profile" },
            new[] { "Tools", "Outils", "Werkzeuge", "Araçlar", "Mga tool" },
            new[] { "Play", "Jouer", "Spielen", "Oyna", "Maglaro" },
            new[] { "Start Roblox with your settings.", "Lance Roblox avec tes réglages.", "Starte Roblox mit deinen Einstellungen.", "Roblox'u ayarlarınla başlat.", "Simulan ang Roblox gamit ang settings mo." },
            new[] { "Accounts", "Comptes", "Konten", "Hesaplar", "Mga account" },
            new[] { "Saved Roblox accounts.", "Comptes Roblox enregistrés.", "Gespeicherte Roblox-Konten.", "Kayıtlı Roblox hesapları.", "Mga naka-save na Roblox account." },
            new[] { "Graphics", "Graphismes", "Grafik", "Grafikler", "Graphics" },
            new[] { "Pick a preset, or change settings one by one.", "Choisis un préréglage, ou change les réglages un par un.", "Wähle eine Voreinstellung oder ändere Einstellungen einzeln.", "Bir ön ayar seç veya ayarları tek tek değiştir.", "Pumili ng preset, o baguhin ang settings isa-isa." },
            new[] { "FastFlags", "FastFlags", "FastFlags", "FastFlags", "FastFlags" },
            new[] { "Flags from Roblox's allowlist.", "Flags de la liste autorisée de Roblox.", "Flags von Roblox' Erlaubnisliste.", "Roblox'un izin listesindeki flag'ler.", "Mga flag mula sa allowlist ng Roblox." },
            new[] { "Text fonts", "Polices du texte", "Schriftarten", "Yazı tipleri", "Mga font ng text" },
            new[] { "The font Roblox uses in-game.", "La police que Roblox utilise en jeu.", "Die Schriftart, die Roblox im Spiel nutzt.", "Roblox'un oyun içinde kullandığı yazı tipi.", "Ang font na gamit ng Roblox sa loob ng laro." },
            new[] { "Mods", "Mods", "Mods", "Modlar", "Mods" },
            new[] { "Cosmetic file swaps. Only GuideStrap's copy of Roblox is changed.", "Changements de fichiers cosmétiques. Seule la copie de Roblox de GuideStrap est modifiée.", "Kosmetischer Dateitausch. Nur GuideStraps Kopie von Roblox wird verändert.", "Görsel dosya değişiklikleri. Sadece GuideStrap'in Roblox kopyası değişir.", "Pagpapalit ng cosmetic na file. Ang kopya lang ng Roblox ng GuideStrap ang binabago." },
            new[] { "Settings", "Réglages", "Einstellungen", "Ayarlar", "Settings" },
            new[] { "Launcher options.", "Options du lanceur.", "Launcher-Optionen.", "Başlatıcı seçenekleri.", "Mga opsyon ng launcher." },
            new[] { "About", "À propos", "Über", "Hakkında", "Tungkol" },
            new[] { "What GuideStrap is.", "Ce qu'est GuideStrap.", "Was GuideStrap ist.", "GuideStrap nedir.", "Ano ang GuideStrap." },
            new[] { "Remove account?", "Supprimer le compte ?", "Konto entfernen?", "Hesap kaldırılsın mı?", "Tanggalin ang account?" },
            new[] { "Remove {0} (@{1}) from GuideStrap?", "Supprimer {0} (@{1}) de GuideStrap ?", "{0} (@{1}) aus GuideStrap entfernen?", "{0} (@{1}) GuideStrap'ten kaldırılsın mı?", "Tanggalin si {0} (@{1}) sa GuideStrap?" },
            new[] { "Remove", "Supprimer", "Entfernen", "Kaldır", "Tanggalin" },
            new[] { "That isn't an Ogg file", "Ce n'est pas un fichier Ogg", "Das ist keine Ogg-Datei", "Bu bir Ogg dosyası değil", "Hindi iyan Ogg file" },
            new[] { "Roblox only plays .ogg sounds here. Convert your sound to .ogg first (any free online converter works).", "Roblox ne lit que des sons .ogg ici. Convertis ton son en .ogg d'abord (n'importe quel convertisseur gratuit en ligne marche).", "Roblox spielt hier nur .ogg-Sounds ab. Wandle deinen Sound zuerst in .ogg um (jeder kostenlose Online-Konverter geht).", "Roblox burada sadece .ogg sesleri çalar. Önce sesini .ogg'a çevir (herhangi bir ücretsiz çevrimiçi dönüştürücü olur).", ".ogg na tunog lang ang pinapatugtog ng Roblox dito. I-convert muna sa .ogg ang tunog mo (kahit anong libreng online converter ay pwede)." },
            new[] { "No sky files found", "Aucun fichier de ciel trouvé", "Keine Himmel-Dateien gefunden", "Gökyüzü dosyası bulunamadı", "Walang nakitang sky file" },
            new[] { "Sky packs are .tex files like sky512_up.tex. Pick the folder that contains them.", "Les packs de ciel sont des fichiers .tex comme sky512_up.tex. Choisis le dossier qui les contient.", "Himmel-Pakete sind .tex-Dateien wie sky512_up.tex. Wähle den Ordner, der sie enthält.", "Gökyüzü paketleri sky512_up.tex gibi .tex dosyalarıdır. Onları içeren klasörü seç.", "Ang mga sky pack ay .tex na file gaya ng sky512_up.tex. Piliin ang folder na naglalaman nito." },
            new[] { "Restart to apply", "Redémarrer pour appliquer", "Neustart zum Übernehmen", "Uygulamak için yeniden başlat", "I-restart para gumana" },
            new[] { "The new color shows after GuideStrap restarts.", "La nouvelle couleur s'affiche après le redémarrage de GuideStrap.", "Die neue Farbe erscheint nach einem Neustart von GuideStrap.", "Yeni renk GuideStrap yeniden başladıktan sonra görünür.", "Lalabas ang bagong kulay pagka-restart ng GuideStrap." },
            new[] { "The new language shows after GuideStrap restarts.", "La nouvelle langue s'affiche après le redémarrage de GuideStrap.", "Die neue Sprache erscheint nach einem Neustart von GuideStrap.", "Yeni dil GuideStrap yeniden başladıktan sonra görünür.", "Lalabas ang bagong wika pagka-restart ng GuideStrap." },
            new[] { "Repair Roblox?", "Réparer Roblox ?", "Roblox reparieren?", "Roblox onarılsın mı?", "Ayusin ang Roblox?" },
            new[] { "This deletes GuideStrap's copy of Roblox. It downloads a fresh one next time you launch.", "Ceci supprime la copie de Roblox de GuideStrap. Une nouvelle est téléchargée au prochain lancement.", "Das löscht GuideStraps Kopie von Roblox. Beim nächsten Start wird eine frische heruntergeladen.", "Bu, GuideStrap'in Roblox kopyasını siler. Bir sonraki başlatışta yenisi indirilir.", "Buburahin nito ang kopya ng Roblox ng GuideStrap. Magda-download ng bago sa susunod mong pag-launch." },
            new[] { "Updates aren't set up yet", "Les mises à jour ne sont pas encore configurées", "Updates sind noch nicht eingerichtet", "Güncellemeler henüz ayarlanmadı", "Hindi pa naka-set up ang updates" },
            new[] { "Once GuideStrap is published on GitHub, set UpdateRepo in the code and it will update itself.", "Une fois GuideStrap publié sur GitHub, mets UpdateRepo dans le code et il se mettra à jour tout seul.", "Sobald GuideStrap auf GitHub veröffentlicht ist, trag UpdateRepo im Code ein und es aktualisiert sich selbst.", "GuideStrap GitHub'da yayınlanınca koda UpdateRepo'yu yaz, kendini güncelleyecek.", "Kapag na-publish na ang GuideStrap sa GitHub, ilagay ang UpdateRepo sa code at kusa na itong mag-a-update." },
            new[] { "Update available", "Mise à jour disponible", "Update verfügbar", "Güncelleme var", "May update" },
            new[] { "GuideStrap {0} is out. Update now?", "GuideStrap {0} est sorti. Mettre à jour maintenant ?", "GuideStrap {0} ist da. Jetzt aktualisieren?", "GuideStrap {0} çıktı. Şimdi güncellensin mi?", "Labas na ang GuideStrap {0}. Mag-update ngayon?" },
            new[] { "That profile can't be read", "Ce profil ne peut pas être lu", "Dieses Profil kann nicht gelesen werden", "Bu profil okunamıyor", "Hindi mabasa ang profile na iyan" },
            new[] { "The file is damaged or isn't a GuideStrap profile.", "Le fichier est abîmé ou ce n'est pas un profil GuideStrap.", "Die Datei ist beschädigt oder kein GuideStrap-Profil.", "Dosya bozuk ya da bir GuideStrap profili değil.", "Sira ang file o hindi ito GuideStrap profile." },
            new[] { "Use this profile?", "Utiliser ce profil ?", "Dieses Profil verwenden?", "Bu profil kullanılsın mı?", "Gamitin ang profile na ito?" },
            new[] { "Use profile", "Utiliser le profil", "Profil verwenden", "Profili kullan", "Gamitin ang profile" },
            new[] { "Your graphics, FastFlags, cursor and accent color will be replaced.", "Tes graphismes, FastFlags, curseur et couleur d'accent seront remplacés.", "Deine Grafik, FastFlags, dein Cursor und deine Akzentfarbe werden ersetzt.", "Grafiklerin, FastFlags'lerin, imlecin ve vurgu rengin değiştirilecek.", "Papalitan ang graphics, FastFlags, cursor at accent color mo." },
            new[] { "The profile's color shows after GuideStrap restarts.", "La couleur du profil s'affiche après le redémarrage de GuideStrap.", "Die Farbe des Profils erscheint nach einem Neustart von GuideStrap.", "Profilin rengi GuideStrap yeniden başladıktan sonra görünür.", "Lalabas ang kulay ng profile pagka-restart ng GuideStrap." },
            new[] { "Clear every flag?", "Effacer tous les flags ?", "Alle Flags löschen?", "Tüm flag'ler temizlensin mi?", "Burahin lahat ng flag?" },
            new[] { "This removes all {0} flags (and resets Graphics to Balanced).", "Ceci supprime les {0} flags (et remet les Graphismes sur Équilibré).", "Das entfernt alle {0} Flags (und setzt Grafik auf Ausgewogen zurück).", "Bu, {0} flag'in hepsini kaldırır (ve Grafikleri Dengeli'ye döndürür).", "Tatanggalin nito ang lahat ng {0} flag (at ibabalik ang Graphics sa Balanced)." },
            new[] { "That isn't valid JSON", "Ce n'est pas du JSON valide", "Das ist kein gültiges JSON", "Bu geçerli bir JSON değil", "Hindi iyan valid na JSON" },
            new[] { "Make sure it looks like { \"FlagName\": \"Value\", ... } with quotes around names.", "Vérifie que ça ressemble à { \"FlagName\": \"Value\", ... } avec des guillemets autour des noms.", "Es sollte so aussehen: { \"FlagName\": \"Value\", ... } mit Anführungszeichen um die Namen.", "{ \"FlagName\": \"Value\", ... } gibi göründüğünden ve isimlerin tırnak içinde olduğundan emin ol.", "Siguraduhing ganito ang itsura: { \"FlagName\": \"Value\", ... } na may quotes sa mga pangalan." },
            new[] { "That isn't a flag list", "Ce n'est pas une liste de flags", "Das ist keine Flag-Liste", "Bu bir flag listesi değil", "Hindi iyan listahan ng flag" },
            new[] { "The JSON should be an object like { \"FlagName\": \"Value\" }.", "Le JSON doit être un objet comme { \"FlagName\": \"Value\" }.", "Das JSON sollte ein Objekt sein wie { \"FlagName\": \"Value\" }.", "JSON { \"FlagName\": \"Value\" } gibi bir nesne olmalı.", "Dapat object ang JSON gaya ng { \"FlagName\": \"Value\" }." },
            new[] { "Changes are saved automatically.", "Les changements sont enregistrés automatiquement.", "Änderungen werden automatisch gespeichert.", "Değişiklikler otomatik kaydedilir.", "Kusang nase-save ang mga pagbabago." },
            new[] { "Wait for Roblox to finish launching first.", "Attends d'abord que Roblox ait fini de se lancer.", "Warte zuerst, bis Roblox fertig gestartet ist.", "Önce Roblox'un açılmasını bekle.", "Hintayin munang matapos mag-launch ang Roblox." },
            new[] { "That doesn't look like a game link or place ID.", "Ça ne ressemble pas à un lien de jeu ou un ID de place.", "Das sieht nicht wie ein Spiellink oder eine Place-ID aus.", "Bu bir oyun bağlantısına veya place ID'ye benzemiyor.", "Mukhang hindi iyan link ng laro o place ID." },
            new[] { "Added {0}.", "{0} ajouté.", "{0} hinzugefügt.", "{0} eklendi.", "Naidagdag ang {0}." },
            new[] { "Font reset to Roblox's default.", "Police remise par défaut de Roblox.", "Schrift auf Roblox' Standard zurückgesetzt.", "Yazı tipi Roblox'un varsayılanına döndü.", "Ibinalik ang font sa default ng Roblox." },
            new[] { "Font set to {0}.", "Police réglée sur {0}.", "Schrift auf {0} gesetzt.", "Yazı tipi {0} olarak ayarlandı.", "Naka-set ang font sa {0}." },
            new[] { "Death sound set. It applies next time you launch Roblox.", "Son de mort réglé. Il s'applique au prochain lancement de Roblox.", "Todessound gesetzt. Er gilt beim nächsten Start von Roblox.", "Ölüm sesi ayarlandı. Roblox'u bir sonraki açışında uygulanır.", "Naka-set na ang death sound. Gagana ito sa susunod mong pag-launch ng Roblox." },
            new[] { "Cleared {0} MB of old Roblox cache and logs.", "{0} Mo de vieux cache et journaux Roblox effacés.", "{0} MB alter Roblox-Cache und Logs gelöscht.", "{0} MB eski Roblox önbelleği ve kaydı temizlendi.", "Nabura ang {0} MB ng lumang cache at log ng Roblox." },
            new[] { "Roblox will re-download next time you launch.", "Roblox sera retéléchargé au prochain lancement.", "Roblox wird beim nächsten Start neu heruntergeladen.", "Roblox bir sonraki başlatışta yeniden indirilecek.", "Magda-download ulit ang Roblox sa susunod mong pag-launch." },
            new[] { "GuideStrap is up to date.", "GuideStrap est à jour.", "GuideStrap ist aktuell.", "GuideStrap güncel.", "Updated na ang GuideStrap." },
            new[] { "Downloading the update...", "Téléchargement de la mise à jour...", "Update wird heruntergeladen...", "Güncelleme indiriliyor...", "Dina-download ang update..." },
            new[] { "Profile saved.", "Profil enregistré.", "Profil gespeichert.", "Profil kaydedildi.", "Na-save ang profile." },
            new[] { "Profile loaded.", "Profil chargé.", "Profil geladen.", "Profil yüklendi.", "Na-load ang profile." },
            new[] { "Nothing to copy yet.", "Rien à copier pour l'instant.", "Noch nichts zum Kopieren.", "Henüz kopyalanacak bir şey yok.", "Wala pang makokopya." },
            new[] { "Copied {0} flags to your clipboard.", "{0} flags copiés dans ton presse-papiers.", "{0} Flags in die Zwischenablage kopiert.", "{0} flag panoya kopyalandı.", "Nakopya ang {0} flag sa clipboard mo." },
            new[] { "All flags cleared.", "Tous les flags ont été effacés.", "Alle Flags gelöscht.", "Tüm flag'ler temizlendi.", "Nabura lahat ng flag." },
            new[] { "Type a value first.", "Écris d'abord une valeur.", "Gib zuerst einen Wert ein.", "Önce bir değer yaz.", "Mag-type muna ng value." },
            new[] { "Paste some JSON first.", "Colle d'abord du JSON.", "Füge zuerst JSON ein.", "Önce JSON yapıştır.", "Mag-paste muna ng JSON." },
            new[] { "Updated {0}.", "{0} mis à jour.", "{0} aktualisiert.", "{0} güncellendi.", "Na-update ang {0}." },
            new[] { "Removed {0}.", "{0} supprimé.", "{0} entfernt.", "{0} kaldırıldı.", "Natanggal ang {0}." },
            new[] { "Play Roblox", "Jouer à Roblox", "Roblox spielen", "Roblox oyna", "Maglaro ng Roblox" },
            new[] { "No account selected", "Aucun compte choisi", "Kein Konto gewählt", "Hesap seçilmedi", "Walang napiling account" },
            new[] { "Roblox will use whoever is logged in on the website.", "Roblox utilisera la personne connectée sur le site.", "Roblox nutzt, wer auf der Website angemeldet ist.", "Roblox, sitede kim giriş yaptıysa onu kullanır.", "Gagamitin ng Roblox kung sino ang naka-log in sa website." },
            new[] { "Roblox login", "Connexion Roblox", "Roblox-Anmeldung", "Roblox girişi", "Roblox login" },
            new[] { "Add accounts", "Ajouter des comptes", "Konten hinzufügen", "Hesap ekle", "Magdagdag ng account" },
            new[] { "{0} graphics  ·  {1}  ·  {2} FastFlag{3}", "Graphismes {0}  ·  {1}  ·  {2} FastFlag(s)", "{0} Grafik  ·  {1}  ·  {2} FastFlag(s)", "{0} grafik  ·  {1}  ·  {2} FastFlag", "{0} graphics  ·  {1}  ·  {2} FastFlag" },
            new[] { "custom font", "police perso", "eigene Schrift", "özel yazı tipi", "custom font" },
            new[] { "default font", "police par défaut", "Standardschrift", "varsayılan yazı tipi", "default na font" },
            new[] { "Custom font", "Police perso", "Eigene Schrift", "Özel yazı tipi", "Custom font" },
            new[] { "  (no preview, still works in-game)", "  (pas d'aperçu, marche quand même en jeu)", "  (keine Vorschau, funktioniert trotzdem im Spiel)", "  (önizleme yok, oyunda yine de çalışır)", "  (walang preview, gagana pa rin sa laro)" },
            new[] { "Balanced", "Équilibré", "Ausgewogen", "Dengeli", "Balanced" },
            new[] { "Max graphics", "Graphismes max", "Max. Grafik", "Maks grafik", "Max graphics" },
            new[] { "No flags yet. Pick a graphics preset or open the editor.", "Aucun flag pour l'instant. Choisis un préréglage graphique ou ouvre l'éditeur.", "Noch keine Flags. Wähle eine Grafik-Voreinstellung oder öffne den Editor.", "Henüz flag yok. Bir grafik ön ayarı seç veya düzenleyiciyi aç.", "Wala pang flag. Pumili ng graphics preset o buksan ang editor." },
            new[] { "The quick brown fox jumps over the lazy dog\n0123456789  !?  Hello from GuideStrap", "Portez ce vieux whisky au juge blond qui fume\n0123456789  !?  Bonjour de GuideStrap", "Zwölf Boxkämpfer jagen Viktor quer über den großen Sylter Deich\n0123456789  !?  Hallo von GuideStrap", "Pijamalı hasta yağız şoföre çabucak güvendi\n0123456789  !?  GuideStrap'ten merhaba", "Ang mabilis na kayumangging soro ay tumalon sa tamad na aso\n0123456789  !?  Hello mula sa GuideStrap" },
            new[] { "Roblox default", "Par défaut de Roblox", "Roblox-Standard", "Roblox varsayılanı", "Default ng Roblox" },
            new[] { "{0} flag{1}", "{0} flag(s)", "{0} Flag(s)", "{0} flag", "{0} flag" },
            new[] { "+ {0} more", "+ {0} de plus", "+ {0} weitere", "+ {0} tane daha", "+ {0} pa" },
            new[] { "About GuideStrap", "À propos de GuideStrap", "Über GuideStrap", "GuideStrap hakkında", "Tungkol sa GuideStrap" },
            new[] { "Open GuideStrap folder", "Ouvrir le dossier de GuideStrap", "GuideStrap-Ordner öffnen", "GuideStrap klasörünü aç", "Buksan ang GuideStrap folder" },
            new[] { "Configure settings", "Configurer les réglages", "Einstellungen anpassen", "Ayarları yapılandır", "I-configure ang settings" },
            new[] { "Accounts  ·  {0}", "Comptes  ·  {0}", "Konten  ·  {0}", "Hesaplar  ·  {0}", "Mga account  ·  {0}" },
            new[] { "Starting...", "Démarrage...", "Startet...", "Başlıyor...", "Nagsisimula..." },
            new[] { "Cancelling...", "Annulation...", "Wird abgebrochen...", "İptal ediliyor...", "Kinakansela..." },
            new[] { "Opened the game page in your browser", "Page du jeu ouverte dans ton navigateur", "Spielseite im Browser geöffnet", "Oyun sayfası tarayıcında açıldı", "Nabuksan ang page ng laro sa browser mo" },
            new[] { "Roblox didn't return a version.", "Roblox n'a pas renvoyé de version.", "Roblox hat keine Version geliefert.", "Roblox bir sürüm döndürmedi.", "Walang ibinigay na bersyon ang Roblox." },
            new[] { "Roblox said no (HTTP {0}).", "Roblox a refusé (HTTP {0}).", "Roblox hat abgelehnt (HTTP {0}).", "Roblox reddetti (HTTP {0}).", "Tumanggi ang Roblox (HTTP {0})." },
            new[] { "This account's saved login expired. Remove it and add it again.", "La connexion enregistrée de ce compte a expiré. Supprime-le et ajoute-le à nouveau.", "Die gespeicherte Anmeldung dieses Kontos ist abgelaufen. Entferne es und füge es neu hinzu.", "Bu hesabın kayıtlı girişinin süresi doldu. Kaldırıp tekrar ekle.", "Nag-expire na ang naka-save na login ng account na ito. Tanggalin ito at idagdag ulit." },
            new[] { "Couldn't log in this account (HTTP {0}).", "Impossible de connecter ce compte (HTTP {0}).", "Dieses Konto konnte nicht angemeldet werden (HTTP {0}).", "Bu hesapla giriş yapılamadı (HTTP {0}).", "Hindi ma-log in ang account na ito (HTTP {0})." },
            new[] { "Couldn't log in this account. Try again.", "Impossible de connecter ce compte. Réessaie.", "Dieses Konto konnte nicht angemeldet werden. Versuch es noch mal.", "Bu hesapla giriş yapılamadı. Tekrar dene.", "Hindi ma-log in ang account na ito. Subukan ulit." },
            new[] { "{0} downloaded corrupted. Try again.", "{0} est arrivé corrompu. Réessaie.", "{0} wurde beschädigt heruntergeladen. Versuch es noch mal.", "{0} bozuk indirildi. Tekrar dene.", "Sira ang na-download na {0}. Subukan ulit." },
            new[] { "Getting the latest Roblox...", "Récupération du dernier Roblox...", "Neuestes Roblox wird geholt...", "En son Roblox alınıyor...", "Kinukuha ang pinakabagong Roblox..." },
            new[] { "Installing Roblox... {0}%", "Installation de Roblox... {0}%", "Roblox wird installiert... {0}%", "Roblox kuruluyor... %{0}", "Ini-install ang Roblox... {0}%" },
            new[] { "Connecting to Roblox...", "Connexion à Roblox...", "Verbinde mit Roblox...", "Roblox'a bağlanılıyor...", "Kumokonekta sa Roblox..." },
            new[] { "Applying your settings...", "Application de tes réglages...", "Deine Einstellungen werden angewendet...", "Ayarların uygulanıyor...", "Inilalapat ang settings mo..." },
            new[] { "Signing in as {0}...", "Connexion en tant que {0}...", "Anmeldung als {0}...", "{0} olarak giriş yapılıyor...", "Nagla-log in bilang {0}..." },
            new[] { "Starting Roblox...", "Démarrage de Roblox...", "Roblox startet...", "Roblox başlatılıyor...", "Sinisimulan ang Roblox..." },
            new[] { "Death sound", "Son de mort", "Todessound", "Ölüm sesi", "Death sound" },
            new[] { "Mouse cursor", "Curseur de souris", "Mauszeiger", "Fare imleci", "Mouse cursor" },
            new[] { "Sky", "Ciel", "Himmel", "Gökyüzü", "Langit" },
            new[] { "Custom sound", "Son perso", "Eigener Sound", "Özel ses", "Custom na tunog" },
            new[] { "Roblox's default sound", "Son par défaut de Roblox", "Roblox' Standardsound", "Roblox'un varsayılan sesi", "Default na tunog ng Roblox" },
            new[] { "Roblox's default cursor", "Curseur par défaut de Roblox", "Roblox' Standardzeiger", "Roblox'un varsayılan imleci", "Default na cursor ng Roblox" },
            new[] { "Purple arrow made for GuideStrap", "Flèche violette faite pour GuideStrap", "Lila Pfeil, gemacht für GuideStrap", "GuideStrap için yapılmış mor ok", "Purple na arrow na ginawa para sa GuideStrap" },
            new[] { "A small dot, easy to aim with", "Un petit point, facile pour viser", "Ein kleiner Punkt, gut zum Zielen", "Küçük bir nokta, nişan almak kolay", "Maliit na tuldok, madaling pang-asinta" },
            new[] { "Your own image", "Ta propre image", "Dein eigenes Bild", "Kendi resmin", "Sarili mong larawan" },
            new[] { "Custom sky pack ({0} files). Games with their own sky keep it.", "Pack de ciel perso ({0} fichiers). Les jeux avec leur propre ciel le gardent.", "Eigenes Himmel-Paket ({0} Dateien). Spiele mit eigenem Himmel behalten ihn.", "Özel gökyüzü paketi ({0} dosya). Kendi gökyüzü olan oyunlar onu korur.", "Custom na sky pack ({0} file). Ang mga larong may sariling langit ay mananatili sa kanila." },
            new[] { "Roblox's default sky. Gray sky is under Graphics.", "Ciel par défaut de Roblox. Le ciel gris est dans Graphismes.", "Roblox' Standardhimmel. Grauer Himmel ist unter Grafik.", "Roblox'un varsayılan gökyüzü. Gri gökyüzü Grafikler'de.", "Default na langit ng Roblox. Nasa Graphics ang gray na langit." },
            new[] { "Lowest quality, flat lighting, no grass. For weak PCs or pure frames.", "Qualité minimale, éclairage plat, pas d'herbe. Pour les PC faibles ou les FPS purs.", "Niedrigste Qualität, flaches Licht, kein Gras. Für schwache PCs oder reine FPS.", "En düşük kalite, düz ışık, çim yok. Zayıf bilgisayarlar veya saf FPS için.", "Pinakamababang quality, flat na ilaw, walang damo. Para sa mahihinang PC o purong FPS." },
            new[] { "Performance", "Performance", "Leistung", "Performans", "Performance" },
            new[] { "Low settings that still look okay. Great for laptops.", "Réglages bas qui restent jolis. Parfait pour les portables.", "Niedrige Einstellungen, die trotzdem okay aussehen. Super für Laptops.", "Yine de iyi görünen düşük ayarlar. Laptoplar için harika.", "Mababang settings na maayos pa rin tingnan. Maganda para sa laptop." },
            new[] { "Roblox's normal automatic settings. Nothing changed.", "Les réglages automatiques normaux de Roblox. Rien n'est changé.", "Roblox' normale automatische Einstellungen. Nichts geändert.", "Roblox'un normal otomatik ayarları. Hiçbir şey değişmedi.", "Ang normal na awtomatikong settings ng Roblox. Walang binago." },
            new[] { "Quality", "Qualité", "Qualität", "Kalite", "Quality" },
            new[] { "Sharper textures and smoother edges. Needs a decent PC.", "Textures plus nettes et bords plus lisses. Demande un bon PC.", "Schärfere Texturen und glattere Kanten. Braucht einen ordentlichen PC.", "Daha keskin dokular ve daha düzgün kenarlar. İyi bir bilgisayar ister.", "Mas malinaw na texture at mas makinis na gilid. Kailangan ng maayos na PC." },
            new[] { "Max Graphics", "Graphismes max", "Max. Grafik", "Maks Grafik", "Max Graphics" },
            new[] { "Everything maxed out. Best looks, lowest FPS.", "Tout au maximum. Meilleur rendu, FPS les plus bas.", "Alles auf Maximum. Bestes Aussehen, niedrigste FPS.", "Her şey en üstte. En iyi görünüm, en düşük FPS.", "Lahat nasa max. Pinakamagandang itsura, pinakamababang FPS." },
            new[] { "Custom", "Perso", "Benutzerdefiniert", "Özel", "Custom" },
            new[] { "You changed settings yourself. Move the slider to pick a preset again.", "Tu as changé les réglages toi-même. Bouge le curseur pour rechoisir un préréglage.", "Du hast Einstellungen selbst geändert. Beweg den Regler, um wieder eine Voreinstellung zu wählen.", "Ayarları kendin değiştirdin. Tekrar ön ayar seçmek için kaydırıcıyı oynat.", "Ikaw mismo ang nagbago ng settings. Igalaw ang slider para pumili ulit ng preset." },
            new[] { "Pick a font", "Choisis une police", "Wähle eine Schrift", "Bir yazı tipi seç", "Pumili ng font" },
            new[] { "Pick a death sound", "Choisis un son de mort", "Wähle einen Todessound", "Bir ölüm sesi seç", "Pumili ng death sound" },
            new[] { "Pick a cursor image", "Choisis une image de curseur", "Wähle ein Zeigerbild", "Bir imleç resmi seç", "Pumili ng larawan ng cursor" },
            new[] { "Pick a folder with sky .tex files (from a Roblox sky mod pack)", "Choisis un dossier avec des fichiers de ciel .tex (d'un pack de ciel Roblox)", "Wähle einen Ordner mit Himmel-.tex-Dateien (aus einem Roblox-Himmel-Paket)", "Gökyüzü .tex dosyaları olan bir klasör seç (bir Roblox gökyüzü paketinden)", "Pumili ng folder na may sky .tex na file (mula sa Roblox sky pack)" },
            new[] { "Overwrite", "Remplacer", "Überschreiben", "Üzerine yaz", "Palitan" },
            new[] { "Keep original", "Garder l'original", "Original behalten", "Orijinali koru", "Panatilihin ang orihinal" },
            new[] { "Auto", "Auto", "Auto", "Otomatik", "Auto" },
            new[] { "Lowest", "Minimale", "Niedrigste", "En düşük", "Pinakamababa" },
            new[] { "Low", "Basse", "Niedrig", "Düşük", "Mababa" },
            new[] { "Medium", "Moyenne", "Mittel", "Orta", "Katamtaman" },
            new[] { "High", "Haute", "Hoch", "Yüksek", "Mataas" },
            new[] { "Off (1x)", "Désactivé (1x)", "Aus (1x)", "Kapalı (1x)", "Off (1x)" },
            new[] { "Default", "Par défaut", "Standard", "Varsayılan", "Default" },
            new[] { "GuideStrap arrow", "Flèche GuideStrap", "GuideStrap-Pfeil", "GuideStrap oku", "GuideStrap arrow" },
            new[] { "Small dot", "Petit point", "Kleiner Punkt", "Küçük nokta", "Maliit na tuldok" },
            new[] { "Custom image...", "Image perso...", "Eigenes Bild...", "Özel resim...", "Custom na larawan..." },
            new[] { "Couldn't add that account", "Impossible d'ajouter ce compte", "Konto konnte nicht hinzugefügt werden", "Hesap eklenemedi", "Hindi maidagdag ang account na iyan" },
            new[] { "Couldn't use that font", "Impossible d'utiliser cette police", "Schrift konnte nicht verwendet werden", "Yazı tipi kullanılamadı", "Hindi magamit ang font na iyan" },
            new[] { "Couldn't use that sound", "Impossible d'utiliser ce son", "Sound konnte nicht verwendet werden", "Ses kullanılamadı", "Hindi magamit ang tunog na iyan" },
            new[] { "Couldn't use that sky", "Impossible d'utiliser ce ciel", "Himmel konnte nicht verwendet werden", "Gökyüzü kullanılamadı", "Hindi magamit ang langit na iyan" },
            new[] { "Couldn't change that", "Impossible de changer ça", "Konnte das nicht ändern", "Bu değiştirilemedi", "Hindi iyan mabago" },
            new[] { "Restart now", "Redémarrer maintenant", "Jetzt neu starten", "Şimdi yeniden başlat", "I-restart ngayon" },
            new[] { "Later", "Plus tard", "Später", "Sonra", "Mamaya" },
            new[] { "Close Roblox first", "Ferme Roblox d'abord", "Schließ zuerst Roblox", "Önce Roblox'u kapat", "Isara muna ang Roblox" },
            new[] { "Update", "Mettre à jour", "Aktualisieren", "Güncelle", "I-update" },
            new[] { "Update failed", "Échec de la mise à jour", "Update fehlgeschlagen", "Güncelleme başarısız", "Pumalya ang update" },
            new[] { "Couldn't launch Roblox", "Impossible de lancer Roblox", "Roblox konnte nicht gestartet werden", "Roblox başlatılamadı", "Hindi ma-launch ang Roblox" },
            new[] { "Something went wrong", "Un problème est survenu", "Etwas ist schiefgelaufen", "Bir şeyler ters gitti", "May nangyaring mali" },
            new[] { "Graphics quality level, 1 (lowest) to 21 (highest)", "Niveau de qualité graphique, de 1 (minimum) à 21 (maximum)", "Grafikqualitätsstufe, 1 (niedrigste) bis 21 (höchste)", "Grafik kalite seviyesi, 1 (en düşük) ile 21 (en yüksek) arası", "Antas ng graphics quality, 1 (pinakamababa) hanggang 21 (pinakamataas)" },
            new[] { "True = use the texture quality flag", "True = utiliser le flag de qualité des textures", "True = Texturqualitäts-Flag nutzen", "True = doku kalitesi flag'ini kullan", "True = gamitin ang flag ng texture quality" },
            new[] { "Texture quality: 0 lowest, 1 low, 2 medium, 3 high", "Qualité des textures : 0 minimale, 1 basse, 2 moyenne, 3 haute", "Texturqualität: 0 niedrigste, 1 niedrig, 2 mittel, 3 hoch", "Doku kalitesi: 0 en düşük, 1 düşük, 2 orta, 3 yüksek", "Texture quality: 0 pinakamababa, 1 mababa, 2 katamtaman, 3 mataas" },
            new[] { "Anti-aliasing samples: 1, 2, 4 or 8", "Échantillons d'anticrénelage : 1, 2, 4 ou 8", "Kantenglättungs-Samples: 1, 2, 4 oder 8", "Kenar yumuşatma örnekleri: 1, 2, 4 veya 8", "Mga sample ng anti-aliasing: 1, 2, 4 o 8" },
            new[] { "True = render with Direct3D 11", "True = rendu avec Direct3D 11", "True = mit Direct3D 11 rendern", "True = Direct3D 11 ile çiz", "True = mag-render gamit ang Direct3D 11" },
            new[] { "True = render with Vulkan", "True = rendu avec Vulkan", "True = mit Vulkan rendern", "True = Vulkan ile çiz", "True = mag-render gamit ang Vulkan" },
            new[] { "True = render with OpenGL", "True = rendu avec OpenGL", "True = mit OpenGL rendern", "True = OpenGL ile çiz", "True = mag-render gamit ang OpenGL" },
            new[] { "Grass draw distance (min). 0 with max = no grass", "Distance d'affichage de l'herbe (min). 0 avec max = pas d'herbe", "Gras-Sichtweite (min). 0 mit max = kein Gras", "Çim çizim mesafesi (min). Max ile birlikte 0 = çim yok", "Layo ng pagguhit ng damo (min). 0 kasama ang max = walang damo" },
            new[] { "Grass draw distance (max). 0 = no grass", "Distance d'affichage de l'herbe (max). 0 = pas d'herbe", "Gras-Sichtweite (max). 0 = kein Gras", "Çim çizim mesafesi (max). 0 = çim yok", "Layo ng pagguhit ng damo (max). 0 = walang damo" },
            new[] { "True = gray sky instead of the skybox", "True = ciel gris à la place de la skybox", "True = grauer Himmel statt Skybox", "True = gökyüzü kutusu yerine gri gökyüzü", "True = gray na langit sa halip na skybox" },
            new[] { "True = freeze lighting updates (faster)", "True = figer les mises à jour d'éclairage (plus rapide)", "True = Lichtupdates einfrieren (schneller)", "True = ışık güncellemelerini dondur (daha hızlı)", "True = i-freeze ang pag-update ng ilaw (mas mabilis)" },
            new[] { "Grass movement. 0 = still grass", "Mouvement de l'herbe. 0 = herbe immobile", "Grasbewegung. 0 = stilles Gras", "Çim hareketi. 0 = hareketsiz çim", "Galaw ng damo. 0 = hindi gumagalaw na damo" },
            new[] { "True = turn off DPI scaling", "True = désactiver la mise à l'échelle DPI", "True = DPI-Skalierung ausschalten", "True = DPI ölçeklemeyi kapat", "True = i-off ang DPI scaling" },
            new[] { "False = Alt+Enter uses exclusive fullscreen", "False = Alt+Entrée utilise le plein écran exclusif", "False = Alt+Enter nutzt exklusiven Vollbildmodus", "False = Alt+Enter özel tam ekran kullanır", "False = exclusive fullscreen ang Alt+Enter" },
            new[] { "Mesh detail switching distance", "Distance de changement de détail des maillages", "Wechseldistanz für Mesh-Details", "Model detayı geçiş mesafesi", "Layo ng pagpapalit ng detalye ng mesh" },
            new[] { "Mesh detail distance, level 1 to 2", "Distance de détail des maillages, niveau 1 à 2", "Mesh-Detail-Distanz, Stufe 1 bis 2", "Model detayı mesafesi, seviye 1'den 2'ye", "Layo ng detalye ng mesh, level 1 hanggang 2" },
            new[] { "Mesh detail distance, level 2 to 3", "Distance de détail des maillages, niveau 2 à 3", "Mesh-Detail-Distanz, Stufe 2 bis 3", "Model detayı mesafesi, seviye 2'den 3'e", "Layo ng detalye ng mesh, level 2 hanggang 3" },
            new[] { "Mesh detail distance, level 3 to 4", "Distance de détail des maillages, niveau 3 à 4", "Mesh-Detail-Distanz, Stufe 3 bis 4", "Model detayı mesafesi, seviye 3'ten 4'e", "Layo ng detalye ng mesh, level 3 hanggang 4" },
            new[] { "Connected to a server in {0}", "Connecté à un serveur à {0}", "Mit einem Server in {0} verbunden", "{0} konumundaki bir sunucuya bağlandı", "Nakakonekta sa isang server sa {0}" },
            new[] { "Account", "Compte", "Konto", "Hesap", "Account" },
            new[] { "Open GuideStrap", "Ouvrir GuideStrap", "GuideStrap öffnen", "GuideStrap'i aç", "Buksan ang GuideStrap" },
            new[] { "Exit", "Quitter", "Beenden", "Çıkış", "Lumabas" },
            new[] { "Search", "Rechercher", "Suchen", "Ara", "Maghanap" },
            new[] { "Filter", "Filtrer", "Filtern", "Filtrele", "I-filter" },
            new[] { "Value", "Valeur", "Wert", "Değer", "Value" },
            new[] { "e.g. https://www.roblox.com/games/920587237", "ex. https://www.roblox.com/games/920587237", "z. B. https://www.roblox.com/games/920587237", "örn. https://www.roblox.com/games/920587237", "hal. https://www.roblox.com/games/920587237" },
            new[] { "Saved accounts", "Comptes enregistrés", "Gespeicherte Konten", "Kayıtlı hesaplar", "Mga naka-save na account" },
            new[] { "Default (Roblox's own login)", "Par défaut (connexion propre à Roblox)", "Standard (Roblox' eigene Anmeldung)", "Varsayılan (Roblox'un kendi girişi)", "Default (sariling login ng Roblox)" },
            new[] { "Uses whoever is logged in on the Roblox website", "Utilise la personne connectée sur le site Roblox", "Nutzt, wer auf der Roblox-Website angemeldet ist", "Roblox sitesinde kim giriş yaptıysa onu kullanır", "Ginagamit kung sino ang naka-log in sa Roblox website" },
            new[] { "Using Roblox's own login.", "Utilisation de la connexion propre à Roblox.", "Roblox' eigene Anmeldung wird genutzt.", "Roblox'un kendi girişi kullanılıyor.", "Gamit ang sariling login ng Roblox." },
            new[] { "Switched to {0}.", "Passé à {0}.", "Zu {0} gewechselt.", "{0} hesabına geçildi.", "Lumipat sa {0}." },
            new[] { "Advanced settings", "Réglages avancés", "Erweiterte Einstellungen", "Gelişmiş ayarlar", "Advanced na settings" },
            new[] { "Change every graphics setting yourself.", "Change chaque réglage graphique toi-même.", "Ändere jede Grafikeinstellung selbst.", "Her grafik ayarını kendin değiştir.", "Ikaw mismo ang magbago ng bawat graphics setting." },
            new[] { "Graphics quality", "Qualité graphique", "Grafikqualität", "Grafik kalitesi", "Graphics quality" },
            new[] { "Roblox's quality level (1-21). Auto = Roblox decides.", "Le niveau de qualité de Roblox (1-21). Auto = Roblox décide.", "Roblox' Qualitätsstufe (1-21). Auto = Roblox entscheidet.", "Roblox'un kalite seviyesi (1-21). Otomatik = Roblox karar verir.", "Antas ng quality ng Roblox (1-21). Auto = ang Roblox ang bahala." },
            new[] { "Texture quality", "Qualité des textures", "Texturqualität", "Doku kalitesi", "Texture quality" },
            new[] { "How sharp surfaces and decals look.", "La netteté des surfaces et des décalques.", "Wie scharf Oberflächen und Decals aussehen.", "Yüzeylerin ve çıkartmaların ne kadar keskin göründüğü.", "Gaano kalinaw ang mga surface at decal." },
            new[] { "Anti-aliasing", "Anticrénelage", "Kantenglättung", "Kenar yumuşatma", "Anti-aliasing" },
            new[] { "Smooths jagged edges. Costs FPS.", "Lisse les bords en escalier. Coûte des FPS.", "Glättet gezackte Kanten. Kostet FPS.", "Tırtıklı kenarları yumuşatır. FPS'e mal olur.", "Pinapakinis ang mga tulis-tulis na gilid. May bawas sa FPS." },
            new[] { "Rendering API", "API de rendu", "Rendering-API", "Çizim API'si", "Rendering API" },
            new[] { "Try Vulkan or D3D11 if you crash or stutter.", "Essaie Vulkan ou D3D11 si ça plante ou saccade.", "Probier Vulkan oder D3D11, wenn es abstürzt oder ruckelt.", "Çökme veya takılma olursa Vulkan ya da D3D11'i dene.", "Subukan ang Vulkan o D3D11 kung nagka-crash o nag-i-stutter." },
            new[] { "Remove grass", "Retirer l'herbe", "Gras entfernen", "Çimi kaldır", "Tanggalin ang damo" },
            new[] { "Hides terrain grass. Big FPS boost on grassy maps.", "Cache l'herbe du terrain. Gros gain de FPS sur les cartes avec de l'herbe.", "Versteckt Geländegras. Großer FPS-Gewinn auf Karten mit viel Gras.", "Arazi çimini gizler. Çimli haritalarda büyük FPS artışı.", "Itinatago ang damo sa terrain. Malaking dagdag na FPS sa mga map na madamo." },
            new[] { "Gray sky", "Ciel gris", "Grauer Himmel", "Gri gökyüzü", "Gray na langit" },
            new[] { "Replaces the skybox with flat gray.", "Remplace la skybox par un gris uni.", "Ersetzt die Skybox durch flaches Grau.", "Gökyüzü kutusunu düz griyle değiştirir.", "Pinapalitan ang skybox ng plain na gray." },
            new[] { "Low-detail meshes", "Maillages peu détaillés", "Detailarme Meshes", "Az detaylı modeller", "Low-detail na mesh" },
            new[] { "Always uses the simplest version of models.", "Utilise toujours la version la plus simple des modèles.", "Nutzt immer die einfachste Version von Modellen.", "Her zaman modellerin en basit sürümünü kullanır.", "Laging ginagamit ang pinakasimpleng bersyon ng mga model." },
            new[] { "Freeze lighting updates", "Figer l'éclairage", "Lichtupdates einfrieren", "Işık güncellemelerini dondur", "I-freeze ang pag-update ng ilaw" },
            new[] { "Faster, but lighting can look flat or wrong.", "Plus rapide, mais l'éclairage peut paraître plat ou faux.", "Schneller, aber das Licht kann flach oder falsch aussehen.", "Daha hızlı, ama ışık düz veya yanlış görünebilir.", "Mas mabilis, pero puwedeng magmukhang flat o mali ang ilaw." },
            new[] { "Still grass", "Herbe immobile", "Stilles Gras", "Hareketsiz çim", "Hindi gumagalaw na damo" },
            new[] { "Stops grass from waving around.", "Empêche l'herbe de bouger.", "Verhindert, dass das Gras sich bewegt.", "Çimin sallanmasını durdurur.", "Pinapahinto ang paggalaw ng damo." },
            new[] { "Disable DPI scaling", "Désactiver la mise à l'échelle DPI", "DPI-Skalierung ausschalten", "DPI ölçeklemeyi kapat", "I-off ang DPI scaling" },
            new[] { "Sharper on high-res screens, UI gets smaller.", "Plus net sur les écrans haute résolution, l'interface devient plus petite.", "Schärfer auf hochauflösenden Bildschirmen, die Oberfläche wird kleiner.", "Yüksek çözünürlüklü ekranlarda daha keskin, arayüz küçülür.", "Mas malinaw sa high-res na screen, lumiliit ang UI." },
            new[] { "Alt+Enter real fullscreen", "Vrai plein écran avec Alt+Entrée", "Echtes Vollbild mit Alt+Enter", "Alt+Enter ile gerçek tam ekran", "Totoong fullscreen sa Alt+Enter" },
            new[] { "Exclusive fullscreen, can lower input lag.", "Plein écran exclusif, peut réduire la latence.", "Exklusiver Vollbildmodus, kann die Eingabeverzögerung senken.", "Özel tam ekran, giriş gecikmesini azaltabilir.", "Exclusive fullscreen, puwedeng bumaba ang input lag." },
            new[] { "Play buttons on roblox.com now open GuideStrap.", "Les boutons Jouer de roblox.com ouvrent maintenant GuideStrap.", "Spielen-Buttons auf roblox.com öffnen jetzt GuideStrap.", "roblox.com'daki Oyna düğmeleri artık GuideStrap'i açıyor.", "Binubuksan na ngayon ng mga Play button sa roblox.com ang GuideStrap." },
            new[] { "Play buttons on roblox.com open normal Roblox again.", "Les boutons Jouer de roblox.com ouvrent à nouveau Roblox normal.", "Spielen-Buttons auf roblox.com öffnen wieder normales Roblox.", "roblox.com'daki Oyna düğmeleri tekrar normal Roblox'u açıyor.", "Normal na Roblox na ulit ang binubuksan ng mga Play button sa roblox.com." },
            new[] { "Language", "Langue", "Sprache", "Dil", "Wika" },
            new[] { "English, Español or Português.", "English, Español ou Português.", "English, Español oder Português.", "English, Español veya Português.", "English, Español o Português." },
            new[] { "Added {0}", "{0} ajouté(s)", "{0} hinzugefügt", "{0} eklendi", "Naidagdag ang {0}" },
            new[] { "Overwrote {0}", "{0} remplacé(s)", "{0} überschrieben", "{0} üzerine yazıldı", "Napalitan ang {0}" },
            new[] { "Kept {0} original", "{0} original(aux) gardé(s)", "{0} Original(e) behalten", "{0} orijinal korundu", "Itinabi ang {0} orihinal" },
            new[] { "{0} already the same", "{0} déjà identique(s)", "{0} bereits gleich", "{0} zaten aynı", "{0} pareho na" },
            new[] { "Skipped {0} not on the allowlist", "{0} ignoré(s), pas sur la liste autorisée", "{0} übersprungen, nicht auf der Erlaubnisliste", "{0} atlandı, izin listesinde değil", "Nilaktawan ang {0}, wala sa allowlist" },
            new[] { "Nothing changed.", "Rien n'a changé.", "Nichts geändert.", "Hiçbir şey değişmedi.", "Walang nagbago." },
            new[] { "This flag is already in your list", "Ce flag est déjà dans ta liste", "Dieses Flag ist schon in deiner Liste", "Bu flag zaten listende var", "Nasa listahan mo na ang flag na ito" },
            new[] { "{0}\n\nCurrent value:   {1}\nNew value:   {2}\n\nDo you want to overwrite it with the new value, or keep the original?", "{0}\n\nValeur actuelle :   {1}\nNouvelle valeur :   {2}\n\nTu veux le remplacer par la nouvelle valeur, ou garder l'original ?", "{0}\n\nAktueller Wert:   {1}\nNeuer Wert:   {2}\n\nMöchtest du es mit dem neuen Wert überschreiben oder das Original behalten?", "{0}\n\nMevcut değer:   {1}\nYeni değer:   {2}\n\nYeni değerle üzerine yazmak mı, yoksa orijinali korumak mı istiyorsun?", "{0}\n\nKasalukuyang value:   {1}\nBagong value:   {2}\n\nGusto mo bang palitan ito ng bagong value, o panatilihin ang orihinal?" },
            new[] { "Do the same for the other {0} duplicate{1}", "Faire pareil pour les {0} autres doublons", "Dasselbe für die anderen {0} Duplikate tun", "Diğer {0} kopya için de aynısını yap", "Gawin din ito sa iba pang {0} na doble" },
            new[] { "No flags yet. Press \"Add flag\" or \"Import JSON\" to get started.", "Aucun flag pour l'instant. Appuie sur \"Ajouter un flag\" ou \"Importer du JSON\" pour commencer.", "Noch keine Flags. Drück \"Flag hinzufügen\" oder \"JSON importieren\", um loszulegen.", "Henüz flag yok. Başlamak için \"Flag ekle\" veya \"JSON içe aktar\"a bas.", "Wala pang flag. Pindutin ang \"Magdagdag ng flag\" o \"I-import ang JSON\" para magsimula." },
            new[] { "No flags match your filter.", "Aucun flag ne correspond à ton filtre.", "Keine Flags passen zu deinem Filter.", "Filtrene uyan flag yok.", "Walang flag na tugma sa filter mo." },
            new[] { "GuideStrap Setup", "Installation de GuideStrap", "GuideStrap-Setup", "GuideStrap Kurulumu", "GuideStrap Setup" },
            new[] { "Welcome to GuideStrap", "Bienvenue dans GuideStrap", "Willkommen bei GuideStrap", "GuideStrap'e hoş geldin", "Welcome sa GuideStrap" },
            new[] { "Pick your language, then install. It only takes a few seconds.", "Choisis ta langue, puis installe. Ça ne prend que quelques secondes.", "Wähle deine Sprache und installiere dann. Das dauert nur ein paar Sekunden.", "Dilini seç, sonra kur. Sadece birkaç saniye sürer.", "Piliin ang wika mo, tapos i-install. Ilang segundo lang ito." },
            new[] { "Desktop shortcut", "Raccourci sur le bureau", "Desktop-Verknüpfung", "Masaüstü kısayolu", "Shortcut sa desktop" },
            new[] { "Add a GuideStrap icon to your desktop.", "Ajoute une icône GuideStrap sur ton bureau.", "Fügt ein GuideStrap-Symbol auf deinem Desktop hinzu.", "Masaüstüne bir GuideStrap simgesi ekler.", "Magdagdag ng GuideStrap icon sa desktop mo." },
            new[] { "Play buttons on roblox.com open GuideStrap.", "Les boutons Jouer de roblox.com ouvrent GuideStrap.", "Spielen-Buttons auf roblox.com öffnen GuideStrap.", "roblox.com'daki Oyna düğmeleri GuideStrap'i açar.", "Binubuksan ng mga Play button sa roblox.com ang GuideStrap." },
            new[] { "Install", "Installer", "Installieren", "Kur", "I-install" },
            new[] { "Installing...", "Installation...", "Wird installiert...", "Kuruluyor...", "Ini-install..." },
            new[] { "Setup failed", "L'installation a échoué", "Setup fehlgeschlagen", "Kurulum başarısız", "Pumalya ang setup" },
            new[] { "Installs for your Windows user only. Remove it any time from Settings > Apps.", "S'installe seulement pour ton utilisateur Windows. Tu peux le retirer quand tu veux dans Paramètres > Applications.", "Wird nur für deinen Windows-Benutzer installiert. Entferne es jederzeit unter Einstellungen > Apps.", "Sadece Windows kullanıcın için kurulur. İstediğin zaman Ayarlar > Uygulamalar'dan kaldırabilirsin.", "Para lang sa Windows user mo ang install. Tanggalin anumang oras sa Settings > Apps." },
            new[] { "Uninstall GuideStrap?", "Désinstaller GuideStrap ?", "GuideStrap deinstallieren?", "GuideStrap kaldırılsın mı?", "I-uninstall ang GuideStrap?" },
            new[] { "This removes GuideStrap, its copy of Roblox, your settings, mods and saved logins. Normal Roblox stays installed.", "Ceci supprime GuideStrap, sa copie de Roblox, tes réglages, mods et connexions enregistrées. Roblox normal reste installé.", "Das entfernt GuideStrap, seine Kopie von Roblox, deine Einstellungen, Mods und gespeicherten Anmeldungen. Normales Roblox bleibt installiert.", "Bu, GuideStrap'i, Roblox kopyasını, ayarlarını, modlarını ve kayıtlı girişlerini kaldırır. Normal Roblox kurulu kalır.", "Tatanggalin nito ang GuideStrap, ang kopya nito ng Roblox, ang settings, mods at mga naka-save na login mo. Mananatiling naka-install ang normal na Roblox." },
            new[] { "Uninstall", "Désinstaller", "Deinstallieren", "Kaldır", "I-uninstall" },
            new[] { "Close GuideStrap first", "Ferme GuideStrap d'abord", "Schließ zuerst GuideStrap", "Önce GuideStrap'i kapat", "Isara muna ang GuideStrap" },
            new[] { "GuideStrap is still running (check the tray near the clock). Close it, then try again.", "GuideStrap tourne encore (regarde dans la zone près de l'horloge). Ferme-le, puis réessaie.", "GuideStrap läuft noch (schau im Infobereich neben der Uhr). Schließ es und versuch es dann noch mal.", "GuideStrap hâlâ çalışıyor (saatin yanındaki tepsiye bak). Kapat, sonra tekrar dene.", "Tumatakbo pa ang GuideStrap (tingnan ang tray malapit sa orasan). Isara ito, tapos subukan ulit." },
            new[] { "GuideStrap was uninstalled.", "GuideStrap a été désinstallé.", "GuideStrap wurde deinstalliert.", "GuideStrap kaldırıldı.", "Na-uninstall na ang GuideStrap." },
            new[] { "GuideStrap is a Roblox launcher made by Guide. It downloads Roblox from Roblox's own servers, keeps it updated, and starts it with your settings applied.\n\nBesides launching, it handles graphics presets, FastFlags (only the ones Roblox allows), a custom in-game font, and switching between saved accounts.\n\nIt never touches the game while it's running. It only changes launch files and settings that Roblox reads on its own, so it isn't an exploit. Saved logins are encrypted for your Windows user.\n\nEverything happens in GuideStrap's own copy of Roblox. Your normal Roblox install is never changed, so launching Roblox without GuideStrap always gives you the original game, sounds and settings.\n\nGuideStrap is a fan project and isn't affiliated with Roblox Corporation.", "GuideStrap est un lanceur Roblox créé par Guide. Il télécharge Roblox depuis les serveurs de Roblox, le garde à jour et le lance avec tes réglages.\n\nEn plus du lancement, il gère les préréglages graphiques, les FastFlags (seulement ceux que Roblox autorise), une police perso en jeu et le changement entre comptes enregistrés.\n\nIl ne touche jamais au jeu pendant qu'il tourne. Il change seulement des fichiers de lancement et des réglages que Roblox lit lui-même, donc ce n'est pas un exploit. Les connexions enregistrées sont chiffrées pour ton utilisateur Windows.\n\nTout se passe dans la propre copie de Roblox de GuideStrap. Ton Roblox normal n'est jamais modifié, donc lancer Roblox sans GuideStrap te donne toujours le jeu, les sons et les réglages d'origine.\n\nGuideStrap est un projet de fan et n'est pas affilié à Roblox Corporation.", "GuideStrap ist ein Roblox-Launcher von Guide. Er lädt Roblox von Roblox' eigenen Servern herunter, hält es aktuell und startet es mit deinen Einstellungen.\n\nNeben dem Starten kümmert er sich um Grafik-Voreinstellungen, FastFlags (nur die, die Roblox erlaubt), eine eigene Schrift im Spiel und das Wechseln zwischen gespeicherten Konten.\n\nEr fasst das Spiel nie an, während es läuft. Er ändert nur Startdateien und Einstellungen, die Roblox selbst liest, also ist er kein Exploit. Gespeicherte Anmeldungen sind für deinen Windows-Benutzer verschlüsselt.\n\nAlles passiert in GuideStraps eigener Kopie von Roblox. Deine normale Roblox-Installation wird nie verändert, also bekommst du ohne GuideStrap immer das Originalspiel mit Originalsounds und -einstellungen.\n\nGuideStrap ist ein Fanprojekt und gehört nicht zur Roblox Corporation.", "GuideStrap, Guide tarafından yapılmış bir Roblox başlatıcısıdır. Roblox'u Roblox'un kendi sunucularından indirir, güncel tutar ve ayarlarınla başlatır.\n\nBaşlatmanın yanında grafik ön ayarlarını, FastFlags'i (sadece Roblox'un izin verdiklerini), oyun içi özel yazı tipini ve kayıtlı hesaplar arasında geçişi yönetir.\n\nOyun çalışırken ona asla dokunmaz. Sadece Roblox'un kendisinin okuduğu başlatma dosyalarını ve ayarları değiştirir, yani bir exploit değildir. Kayıtlı girişler Windows kullanıcın için şifrelenir.\n\nHer şey GuideStrap'in kendi Roblox kopyasında olur. Normal Roblox kurulumun asla değişmez, bu yüzden Roblox'u GuideStrap olmadan açmak her zaman orijinal oyunu, sesleri ve ayarları verir.\n\nGuideStrap bir hayran projesidir ve Roblox Corporation ile bağlantılı değildir.", "Ang GuideStrap ay Roblox launcher na gawa ni Guide. Dina-download nito ang Roblox mula sa sariling server ng Roblox, pinapanatiling updated, at sinisimulan gamit ang settings mo.\n\nBukod sa pag-launch, hawak din nito ang graphics presets, FastFlags (yung mga pinapayagan lang ng Roblox), custom na font sa laro, at paglipat sa pagitan ng mga naka-save na account.\n\nHindi nito ginagalaw ang laro habang tumatakbo. Binabago lang nito ang launch files at settings na kusang binabasa ng Roblox, kaya hindi ito exploit. Naka-encrypt ang mga naka-save na login para sa Windows user mo.\n\nLahat ay nangyayari sa sariling kopya ng Roblox ng GuideStrap. Hindi kailanman binabago ang normal mong Roblox install, kaya ang pag-launch ng Roblox nang walang GuideStrap ay laging orihinal na laro, tunog at settings.\n\nAng GuideStrap ay fan project at hindi konektado sa Roblox Corporation." },
            new[] { "Reinstall GuideStrap", "Réinstaller GuideStrap", "GuideStrap neu installieren", "GuideStrap'i yeniden kur", "I-reinstall ang GuideStrap" },
            new[] { "GuideStrap is already installed. Reinstalling replaces it with this copy and keeps your settings, accounts and mods.", "GuideStrap est déjà installé. La réinstallation le remplace par cette copie et garde tes réglages, comptes et mods.", "GuideStrap ist schon installiert. Neu installieren ersetzt es durch diese Kopie und behält deine Einstellungen, Konten und Mods.", "GuideStrap zaten kurulu. Yeniden kurmak onu bu kopyayla değiştirir ve ayarlarını, hesaplarını ve modlarını korur.", "Naka-install na ang GuideStrap. Papalitan ito ng kopyang ito at itatabi ang settings, account at mods mo." },
            new[] { "Installed: {0}  ·  This file: {1}", "Installé : {0}  ·  Ce fichier : {1}", "Installiert: {0}  ·  Diese Datei: {1}", "Kurulu: {0}  ·  Bu dosya: {1}", "Naka-install: {0}  ·  Ang file na ito: {1}" },
            new[] { "Also repair Roblox", "Réparer aussi Roblox", "Roblox auch reparieren", "Roblox'u da onar", "Ayusin din ang Roblox" },
            new[] { "Downloads a fresh copy of Roblox next time you launch.", "Télécharge une nouvelle copie de Roblox au prochain lancement.", "Lädt beim nächsten Start eine frische Kopie von Roblox herunter.", "Bir sonraki başlatışta Roblox'un yeni bir kopyasını indirir.", "Magda-download ng bagong kopya ng Roblox sa susunod mong pag-launch." },
            new[] { "Reinstall", "Réinstaller", "Neu installieren", "Yeniden kur", "I-reinstall" },
            new[] { "Reinstalling...", "Réinstallation...", "Wird neu installiert...", "Yeniden kuruluyor...", "Nire-reinstall..." },
            new[] { "GuideStrap is open", "GuideStrap est ouvert", "GuideStrap ist geöffnet", "GuideStrap açık", "Bukas ang GuideStrap" },
            new[] { "GuideStrap is open right now. Close it and continue?", "GuideStrap est ouvert en ce moment. Le fermer et continuer ?", "GuideStrap ist gerade geöffnet. Schließen und fortfahren?", "GuideStrap şu an açık. Kapatıp devam edilsin mi?", "Bukas ngayon ang GuideStrap. Isara ito at magpatuloy?" },
            new[] { "Close and continue", "Fermer et continuer", "Schließen und fortfahren", "Kapat ve devam et", "Isara at magpatuloy" },
            new[] { "Theme", "Thème", "Design", "Tema", "Tema" },
            new[] { "Purple", "Violet", "Lila", "Mor", "Purple" },
            new[] { "Black", "Noir", "Schwarz", "Siyah", "Itim" },
            new[] { "Purple, or plain black with no color.", "Violet, ou noir uni sans couleur.", "Lila oder schlichtes Schwarz ohne Farbe.", "Mor veya renksiz düz siyah.", "Purple, o plain na itim na walang kulay." },
            new[] { "The new theme shows after GuideStrap restarts.", "Le nouveau thème s'affiche après le redémarrage de GuideStrap.", "Das neue Design erscheint nach einem Neustart von GuideStrap.", "Yeni tema GuideStrap yeniden başladıktan sonra görünür.", "Lalabas ang bagong tema pagka-restart ng GuideStrap." },
            new[] { "Used by the Purple theme for buttons, switches and highlights.", "Utilisée par le thème Violet pour les boutons, interrupteurs et surlignages.", "Wird vom lila Design für Buttons, Schalter und Hervorhebungen genutzt.", "Mor tema tarafından düğmeler, anahtarlar ve vurgular için kullanılır.", "Ginagamit ng Purple na tema para sa mga button, switch at highlight." },
            new[] { "Potato mode", "Mode patate", "Kartoffelmodus", "Patates modu", "Potato mode" },
            new[] { "Everything low for the most FPS, or turn on parts one by one.", "Tout au minimum pour le plus de FPS, ou active les éléments un par un.", "Alles auf niedrig für die meisten FPS, oder schalte Teile einzeln ein.", "En çok FPS için her şey düşük, ya da parçaları tek tek aç.", "Lahat nasa mababa para sa pinakamaraming FPS, o i-on ang mga bahagi isa-isa." },
            new[] { "Turn on everything", "Tout activer", "Alles einschalten", "Hepsini aç", "I-on lahat" },
            new[] { "All of the switches below at once.", "Tous les interrupteurs ci-dessous d'un coup.", "Alle Schalter unten auf einmal.", "Aşağıdaki tüm anahtarlar bir kerede.", "Lahat ng switch sa ibaba nang sabay." },
            new[] { "Blurry textures", "Textures floues", "Verschwommene Texturen", "Bulanık dokular", "Malabong texture" },
            new[] { "Lowest texture quality. Works on every game's textures.", "Qualité de texture minimale. Marche sur les textures de n'importe quel jeu.", "Niedrigste Texturqualität. Wirkt auf die Texturen jedes Spiels.", "En düşük doku kalitesi. Her oyunun dokularında çalışır.", "Pinakamababang texture quality. Gumagana sa texture ng kahit anong laro." },
            new[] { "No grass", "Pas d'herbe", "Kein Gras", "Çim yok", "Walang damo" },
            new[] { "Hides terrain grass.", "Cache l'herbe du terrain.", "Versteckt Geländegras.", "Arazi çimini gizler.", "Itinatago ang damo sa terrain." },
            new[] { "Anti-aliasing off", "Anticrénelage désactivé", "Kantenglättung aus", "Kenar yumuşatma kapalı", "Naka-off ang anti-aliasing" },
            new[] { "No edge smoothing. Edges look a bit jagged.", "Pas de lissage des bords. Les bords sont un peu en escalier.", "Keine Kantenglättung. Kanten sehen etwas gezackt aus.", "Kenar yumuşatma yok. Kenarlar biraz tırtıklı görünür.", "Walang pagpapakinis ng gilid. Medyo tulis-tulis ang mga gilid." },
            new[] { "Preview", "Aperçu", "Vorschau", "Önizleme", "Preview" },
            new[] { "See what blurry textures look like.", "Vois à quoi ressemblent les textures floues.", "Sieh dir an, wie verschwommene Texturen aussehen.", "Bulanık dokuların nasıl göründüğüne bak.", "Tingnan kung ano ang itsura ng malabong texture." },
            new[] { "Texture preview", "Aperçu des textures", "Texturvorschau", "Doku önizlemesi", "Preview ng texture" },
            new[] { "Normal", "Normal", "Normal", "Normal", "Normal" },
            new[] { "Made-up example. In real games the blur looks the same, the textures just differ.", "Exemple inventé. Dans les vrais jeux le flou est le même, seules les textures changent.", "Erfundenes Beispiel. In echten Spielen sieht die Unschärfe gleich aus, nur die Texturen sind anders.", "Uydurma örnek. Gerçek oyunlarda bulanıklık aynı görünür, sadece dokular farklıdır.", "Gawa-gawang halimbawa. Sa totoong laro pareho ang labo, iba lang ang texture." },
            new[] { "FPS cap", "Limite de FPS", "FPS-Limit", "FPS sınırı", "FPS cap" },
            new[] { "Sets Roblox's own max frame rate before it opens. Normal Roblox keeps it too.", "Règle la limite d'images propre à Roblox avant qu'il s'ouvre. Roblox normal la garde aussi.", "Setzt Roblox' eigenes Bildratenlimit, bevor es startet. Normales Roblox behält es auch.", "Roblox açılmadan önce Roblox'un kendi kare hızı sınırını ayarlar. Normal Roblox da bunu korur.", "Sine-set ang sariling max frame rate ng Roblox bago ito bumukas. Mananatili rin ito sa normal na Roblox." },
            new[] { "Above normal", "Au-dessus de la normale", "Höher als normal", "Normalin üstünde", "Higit sa normal" },
            new[] { "Roblox priority", "Priorité de Roblox", "Roblox-Priorität", "Roblox önceliği", "Priority ng Roblox" },
            new[] { "Gives Roblox more CPU time. High can slow OBS while streaming.", "Donne plus de temps CPU à Roblox. Haute peut ralentir OBS pendant un stream.", "Gibt Roblox mehr CPU-Zeit. Hoch kann OBS beim Streamen verlangsamen.", "Roblox'a daha çok işlemci zamanı verir. Yüksek, yayın yaparken OBS'i yavaşlatabilir.", "Binibigyan ang Roblox ng mas maraming oras sa CPU. Puwedeng bumagal ang OBS sa High habang nag-i-stream." },
            new[] { "Muted", "Muet", "Stumm", "Sessiz", "Naka-mute" },
            new[] { "Custom .mp3...", ".mp3 perso...", "Eigene .mp3...", "Özel .mp3...", "Custom na .mp3..." },
            new[] { "Footsteps", "Pas", "Schritte", "Ayak sesleri", "Yabag" },
            new[] { "Jump sound", "Son de saut", "Sprungsound", "Zıplama sesi", "Tunog ng pagtalon" },
            new[] { "Sound pack", "Pack de sons", "Soundpaket", "Ses paketi", "Sound pack" },
            new[] { "None", "Aucun", "Keins", "Yok", "Wala" },
            new[] { "Using {0}. Its files replace Roblox's sounds.", "Utilise {0}. Ses fichiers remplacent les sons de Roblox.", "Nutzt {0}. Seine Dateien ersetzen Roblox' Sounds.", "{0} kullanılıyor. Dosyaları Roblox'un seslerinin yerine geçer.", "Gamit ang {0}. Pinapalitan ng mga file nito ang mga tunog ng Roblox." },
            new[] { "Open the folder and add a pack: one folder per pack, with .ogg or .mp3 files.", "Ouvre le dossier et ajoute un pack : un dossier par pack, avec des fichiers .ogg ou .mp3.", "Öffne den Ordner und füge ein Paket hinzu: ein Ordner pro Paket, mit .ogg- oder .mp3-Dateien.", "Klasörü aç ve bir paket ekle: her paket için bir klasör, içinde .ogg veya .mp3 dosyaları.", "Buksan ang folder at magdagdag ng pack: isang folder bawat pack, na may .ogg o .mp3 na file." },
            new[] { "No pack. Pick one, or open the folder to add more.", "Pas de pack. Choisis-en un, ou ouvre le dossier pour en ajouter.", "Kein Paket. Wähle eins oder öffne den Ordner, um mehr hinzuzufügen.", "Paket yok. Birini seç veya daha fazlasını eklemek için klasörü aç.", "Walang pack. Pumili ng isa, o buksan ang folder para magdagdag pa." },
            new[] { "No footstep sound", "Pas de son de pas", "Kein Schrittsound", "Ayak sesi yok", "Walang tunog ng yabag" },
            new[] { "Your own sound", "Ton propre son", "Dein eigener Sound", "Kendi sesin", "Sarili mong tunog" },
            new[] { "Roblox's default footsteps", "Pas par défaut de Roblox", "Roblox' Standardschritte", "Roblox'un varsayılan ayak sesleri", "Default na yabag ng Roblox" },
            new[] { "No jump or landing sound", "Pas de son de saut ni d'atterrissage", "Kein Sprung- oder Landesound", "Zıplama veya iniş sesi yok", "Walang tunog ng pagtalon o paglapag" },
            new[] { "Roblox's default jump sound", "Son de saut par défaut de Roblox", "Roblox' Standard-Sprungsound", "Roblox'un varsayılan zıplama sesi", "Default na tunog ng pagtalon ng Roblox" },
            new[] { "Pick a sound", "Choisis un son", "Wähle einen Sound", "Bir ses seç", "Pumili ng tunog" },
            new[] { "That isn't an MP3 file", "Ce n'est pas un fichier MP3", "Das ist keine MP3-Datei", "Bu bir MP3 dosyası değil", "Hindi iyan MP3 file" },
            new[] { "These Roblox sounds are .mp3 files. Convert your sound to .mp3 first (any free online converter works).", "Ces sons de Roblox sont des fichiers .mp3. Convertis ton son en .mp3 d'abord (n'importe quel convertisseur gratuit en ligne marche).", "Diese Roblox-Sounds sind .mp3-Dateien. Wandle deinen Sound zuerst in .mp3 um (jeder kostenlose Online-Konverter geht).", "Bu Roblox sesleri .mp3 dosyasıdır. Önce sesini .mp3'e çevir (herhangi bir ücretsiz çevrimiçi dönüştürücü olur).", "Mga .mp3 na file ang mga tunog na ito ng Roblox. I-convert muna sa .mp3 ang tunog mo (kahit anong libreng online converter ay pwede)." },
            new[] { "Sound set. It applies next time you launch Roblox.", "Son réglé. Il s'applique au prochain lancement de Roblox.", "Sound gesetzt. Er gilt beim nächsten Start von Roblox.", "Ses ayarlandı. Roblox'u bir sonraki açışında uygulanır.", "Naka-set na ang tunog. Gagana ito sa susunod mong pag-launch ng Roblox." },
            new[] { "Small cross", "Petite croix", "Kleines Kreuz", "Küçük artı", "Maliit na krus" },
            new[] { "Circle", "Cercle", "Kreis", "Daire", "Bilog" },
            new[] { "Classic arrow", "Flèche classique", "Klassischer Pfeil", "Klasik ok", "Klasikong arrow" },
            new[] { "Dot", "Point", "Punkt", "Nokta", "Tuldok" },
            new[] { "A small cross, easy to aim with", "Une petite croix, facile pour viser", "Ein kleines Kreuz, gut zum Zielen", "Küçük bir artı, nişan almak kolay", "Maliit na krus, madaling pang-asinta" },
            new[] { "A thin circle", "Un cercle fin", "Ein dünner Kreis", "İnce bir daire", "Manipis na bilog" },
            new[] { "White arrow with a black outline", "Flèche blanche avec un contour noir", "Weißer Pfeil mit schwarzem Rand", "Siyah kenarlı beyaz ok", "Puting arrow na may itim na gilid" },
            new[] { "Shift lock crosshair", "Viseur du shift lock", "Shift-Lock-Fadenkreuz", "Shift lock nişangahı", "Shift lock crosshair" },
            new[] { "A small dot in the middle of the screen", "Un petit point au milieu de l'écran", "Ein kleiner Punkt in der Bildschirmmitte", "Ekranın ortasında küçük bir nokta", "Maliit na tuldok sa gitna ng screen" },
            new[] { "A small cross in the middle of the screen", "Une petite croix au milieu de l'écran", "Ein kleines Kreuz in der Bildschirmmitte", "Ekranın ortasında küçük bir artı", "Maliit na krus sa gitna ng screen" },
            new[] { "A thin circle in the middle of the screen", "Un cercle fin au milieu de l'écran", "Ein dünner Kreis in der Bildschirmmitte", "Ekranın ortasında ince bir daire", "Manipis na bilog sa gitna ng screen" },
            new[] { "Roblox's default shift lock icon", "Icône de shift lock par défaut de Roblox", "Roblox' Standard-Shift-Lock-Symbol", "Roblox'un varsayılan shift lock simgesi", "Default na shift lock icon ng Roblox" },
            new[] { "Pick a crosshair image", "Choisis une image de viseur", "Wähle ein Fadenkreuz-Bild", "Bir nişangah resmi seç", "Pumili ng larawan ng crosshair" },
            new[] { "Setups", "Configurations", "Setups", "Kurulumlar", "Mga setup" },
            new[] { "Saved setups", "Configurations enregistrées", "Gespeicherte Setups", "Kayıtlı kurulumlar", "Mga naka-save na setup" },
            new[] { "Save", "Enregistrer", "Speichern", "Kaydet", "I-save" },
            new[] { "Pick a setup", "Choisis une configuration", "Wähle ein Setup", "Bir kurulum seç", "Pumili ng setup" },
            new[] { "Save your mods and graphics as a setup, like BedWars or Recording.", "Enregistre tes mods et graphismes comme configuration, par exemple BedWars ou Enregistrement.", "Speichere deine Mods und Grafik als Setup, z. B. BedWars oder Aufnahme.", "Modlarını ve grafiklerini bir kurulum olarak kaydet, örneğin BedWars veya Kayıt.", "I-save ang mods at graphics mo bilang setup, gaya ng BedWars o Recording." },
            new[] { "{0} saved. Pick one to switch to it.", "{0} enregistrée(s). Choisis-en une pour y passer.", "{0} gespeichert. Wähle eins, um dazu zu wechseln.", "{0} kayıtlı. Geçmek için birini seç.", "{0} ang naka-save. Pumili ng isa para lumipat dito." },
            new[] { "Save setup", "Enregistrer la configuration", "Setup speichern", "Kurulumu kaydet", "I-save ang setup" },
            new[] { "Name this setup. Saving with an existing name replaces it.", "Nomme cette configuration. Utiliser un nom existant la remplace.", "Gib diesem Setup einen Namen. Ein vorhandener Name wird ersetzt.", "Bu kuruluma bir isim ver. Var olan bir isimle kaydetmek onu değiştirir.", "Pangalanan ang setup na ito. Papalitan ito kung pareho ang pangalan." },
            new[] { "Saved {0}.", "{0} enregistrée.", "{0} gespeichert.", "{0} kaydedildi.", "Na-save ang {0}." },
            new[] { "Switched to {0}. It applies next time you launch Roblox.", "Passé à {0}. S'applique au prochain lancement de Roblox.", "Zu {0} gewechselt. Gilt beim nächsten Start von Roblox.", "{0} kurulumuna geçildi. Roblox'u bir sonraki açışında uygulanır.", "Lumipat sa {0}. Gagana ito sa susunod mong pag-launch ng Roblox." },
            new[] { "Pick a setup first", "Choisis d'abord une configuration", "Wähle zuerst ein Setup", "Önce bir kurulum seç", "Pumili muna ng setup" },
            new[] { "Choose the setup you want to delete from the list.", "Choisis dans la liste la configuration à supprimer.", "Wähle aus der Liste das Setup, das du löschen willst.", "Listeden silmek istediğin kurulumu seç.", "Piliin sa listahan ang setup na gusto mong burahin." },
            new[] { "Delete setup?", "Supprimer la configuration ?", "Setup löschen?", "Kurulum silinsin mi?", "Burahin ang setup?" },
            new[] { "Delete {0}? Your current settings stay the same.", "Supprimer {0} ? Tes réglages actuels ne changent pas.", "{0} löschen? Deine aktuellen Einstellungen bleiben gleich.", "{0} silinsin mi? Mevcut ayarların aynı kalır.", "Burahin ang {0}? Hindi magbabago ang kasalukuyang settings mo." },
            new[] { "Delete", "Supprimer", "Löschen", "Sil", "Burahin" },
            new[] { "Crash helper", "Aide aux plantages", "Absturzhilfe", "Çökme yardımcısı", "Crash helper" },
            new[] { "If Roblox crashes, shows what went wrong and how to fix it.", "Si Roblox plante, montre ce qui s'est passé et comment le réparer.", "Wenn Roblox abstürzt, zeigt sie, was schiefging und wie man es behebt.", "Roblox çökerse neyin ters gittiğini ve nasıl düzeltileceğini gösterir.", "Kapag nag-crash ang Roblox, ipinapakita kung ano ang nangyari at paano ito ayusin." },
            new[] { "Roblox crashed", "Roblox a planté", "Roblox ist abgestürzt", "Roblox çöktü", "Nag-crash ang Roblox" },
            new[] { "Roblox crashed while starting", "Roblox a planté au démarrage", "Roblox ist beim Starten abgestürzt", "Roblox açılırken çöktü", "Nag-crash ang Roblox habang nagsisimula" },
            new[] { "Error {0}: {1}", "Erreur {0} : {1}", "Fehler {0}: {1}", "Hata {0}: {1}", "Error {0}: {1}" },
            new[] { "Try these fixes, starting with the first one.", "Essaie ces solutions, en commençant par la première.", "Probier diese Lösungen, angefangen mit der ersten.", "Bu çözümleri ilkinden başlayarak dene.", "Subukan ang mga ayos na ito, simula sa una." },
            new[] { "Open Roblox logs", "Ouvrir les journaux de Roblox", "Roblox-Logs öffnen", "Roblox kayıtlarını aç", "Buksan ang mga log ng Roblox" },
            new[] { "memory access error", "erreur d'accès mémoire", "Speicherzugriffsfehler", "bellek erişim hatası", "error sa pag-access ng memory" },
            new[] { "Roblox stopped itself (security check)", "Roblox s'est arrêté tout seul (contrôle de sécurité)", "Roblox hat sich selbst beendet (Sicherheitsprüfung)", "Roblox kendini durdurdu (güvenlik kontrolü)", "kusang huminto ang Roblox (security check)" },
            new[] { "stack overflow", "dépassement de pile", "Stapelüberlauf", "yığın taşması", "stack overflow" },
            new[] { "memory corruption", "mémoire corrompue", "beschädigter Speicher", "bozuk bellek", "sirang memory" },
            new[] { "out of memory", "mémoire insuffisante", "kein Speicher mehr frei", "bellek yetersiz", "kulang ang memory" },
            new[] { "unhandled error", "erreur non gérée", "unbehandelter Fehler", "işlenmemiş hata", "hindi nahawakang error" },
            new[] { "the graphics driver stopped responding", "le pilote graphique a cessé de répondre", "der Grafiktreiber reagiert nicht mehr", "ekran kartı sürücüsü yanıt vermeyi durdurdu", "huminto sa pagsagot ang graphics driver" },
            new[] { "internal error", "erreur interne", "interner Fehler", "dahili hata", "internal error" },
            new[] { "unknown error", "erreur inconnue", "unbekannter Fehler", "bilinmeyen hata", "hindi kilalang error" },
            new[] { "Close overlays and macro tools", "Ferme les overlays et les outils de macro", "Schließ Overlays und Makro-Tools", "Arayüz kaplamalarını ve makro araçlarını kapat", "Isara ang mga overlay at macro tool" },
            new[] { "Roblox closes itself when another program hooks into it, like FPS unlockers, macros, cheats or some recording overlays.", "Roblox se ferme tout seul quand un autre programme s'y accroche, comme les débloqueurs de FPS, les macros, les triches ou certains overlays d'enregistrement.", "Roblox beendet sich selbst, wenn sich ein anderes Programm einklinkt, z. B. FPS-Unlocker, Makros, Cheats oder manche Aufnahme-Overlays.", "Başka bir program Roblox'a bağlandığında Roblox kendini kapatır, örneğin FPS kilit açıcılar, makrolar, hileler veya bazı kayıt kaplamaları.", "Kusang nagsasara ang Roblox kapag may ibang program na kumakabit dito, gaya ng FPS unlocker, macro, cheat o ilang recording overlay." },
            new[] { "Switch the rendering API", "Change l'API de rendu", "Wechsle die Rendering-API", "Çizim API'sini değiştir", "Palitan ang rendering API" },
            new[] { "Your graphics driver may not like Direct3D 11. Try Vulkan next time.", "Ton pilote graphique n'aime peut-être pas Direct3D 11. Essaie Vulkan la prochaine fois.", "Dein Grafiktreiber mag vielleicht Direct3D 11 nicht. Probier nächstes Mal Vulkan.", "Ekran kartı sürücün Direct3D 11'i sevmiyor olabilir. Bir dahaki sefere Vulkan'ı dene.", "Baka hindi gusto ng graphics driver mo ang Direct3D 11. Subukan ang Vulkan sa susunod." },
            new[] { "Your graphics driver may not like the current one. Try Direct3D 11 next time.", "Ton pilote graphique n'aime peut-être pas l'API actuelle. Essaie Direct3D 11 la prochaine fois.", "Dein Grafiktreiber mag vielleicht die aktuelle nicht. Probier nächstes Mal Direct3D 11.", "Ekran kartı sürücün mevcut olanı sevmiyor olabilir. Bir dahaki sefere Direct3D 11'i dene.", "Baka hindi gusto ng graphics driver mo ang kasalukuyan. Subukan ang Direct3D 11 sa susunod." },
            new[] { "Use Vulkan", "Utiliser Vulkan", "Vulkan nutzen", "Vulkan kullan", "Gamitin ang Vulkan" },
            new[] { "Use D3D11", "Utiliser D3D11", "D3D11 nutzen", "D3D11 kullan", "Gamitin ang D3D11" },
            new[] { "Free up memory", "Libère de la mémoire", "Gib Speicher frei", "Bellek boşalt", "Magbakante ng memory" },
            new[] { "Close browser tabs and other games, and turn on Potato mode in Graphics.", "Ferme les onglets du navigateur et les autres jeux, et active le Mode patate dans Graphismes.", "Schließ Browser-Tabs und andere Spiele und schalte den Kartoffelmodus unter Grafik ein.", "Tarayıcı sekmelerini ve diğer oyunları kapat, Grafikler'de Patates modunu aç.", "Isara ang mga tab ng browser at ibang laro, at i-on ang Potato mode sa Graphics." },
            new[] { "Launch once without mods", "Lancer une fois sans mods", "Einmal ohne Mods starten", "Bir kez modsuz başlat", "Mag-launch nang isang beses nang walang mods" },
            new[] { "A mod or custom font can break after a Roblox update. Your mods stay saved and come back the launch after.", "Un mod ou une police perso peut casser après une mise à jour de Roblox. Tes mods restent enregistrés et reviennent au lancement suivant.", "Ein Mod oder eine eigene Schrift kann nach einem Roblox-Update kaputtgehen. Deine Mods bleiben gespeichert und sind beim Start danach zurück.", "Bir mod veya özel yazı tipi bir Roblox güncellemesinden sonra bozulabilir. Modların kayıtlı kalır ve sonraki başlatışta geri gelir.", "Puwedeng masira ang mod o custom font pagkatapos ng update ng Roblox. Naka-save pa rin ang mods mo at babalik sa kasunod na launch." },
            new[] { "Skip mods", "Sauter les mods", "Mods überspringen", "Modları atla", "Laktawan ang mods" },
            new[] { "Reset FastFlags", "Réinitialiser les FastFlags", "FastFlags zurücksetzen", "FastFlags'i sıfırla", "I-reset ang FastFlags" },
            new[] { "A flag can stop working after a Roblox update. This clears your flags and sets Graphics back to Balanced.", "Un flag peut arrêter de marcher après une mise à jour de Roblox. Ceci efface tes flags et remet les Graphismes sur Équilibré.", "Ein Flag kann nach einem Roblox-Update aufhören zu funktionieren. Das löscht deine Flags und setzt Grafik auf Ausgewogen zurück.", "Bir flag bir Roblox güncellemesinden sonra çalışmayı bırakabilir. Bu, flag'lerini temizler ve Grafikleri Dengeli'ye döndürür.", "Puwedeng tumigil gumana ang isang flag pagkatapos ng update ng Roblox. Buburahin nito ang mga flag mo at ibabalik ang Graphics sa Balanced." },
            new[] { "Reset flags", "Réinitialiser les flags", "Flags zurücksetzen", "Flag'leri sıfırla", "I-reset ang mga flag" },
            new[] { "Turn off multiple Roblox windows", "Désactiver les fenêtres Roblox multiples", "Mehrere Roblox-Fenster ausschalten", "Birden fazla Roblox penceresini kapat", "I-off ang maraming Roblox window" },
            new[] { "Running more than one Roblox at once can make it crash.", "Lancer plusieurs Roblox en même temps peut le faire planter.", "Mehr als ein Roblox gleichzeitig kann es abstürzen lassen.", "Aynı anda birden fazla Roblox açmak çökmesine yol açabilir.", "Puwedeng mag-crash ang Roblox kapag higit sa isa ang bukas nang sabay." },
            new[] { "Turn off", "Désactiver", "Ausschalten", "Kapat", "I-off" },
            new[] { "Update your graphics driver", "Mets à jour ton pilote graphique", "Aktualisiere deinen Grafiktreiber", "Ekran kartı sürücünü güncelle", "I-update ang graphics driver mo" },
            new[] { "Get the newest driver from NVIDIA, AMD or Intel. Old drivers are the most common cause of crashes.", "Prends le dernier pilote chez NVIDIA, AMD ou Intel. Les vieux pilotes sont la cause la plus fréquente des plantages.", "Hol dir den neuesten Treiber von NVIDIA, AMD oder Intel. Alte Treiber sind die häufigste Ursache für Abstürze.", "NVIDIA, AMD veya Intel'den en yeni sürücüyü al. Eski sürücüler çökmelerin en yaygın sebebidir.", "Kunin ang pinakabagong driver mula sa NVIDIA, AMD o Intel. Ang lumang driver ang pinakakaraniwang sanhi ng crash." },
            new[] { "Downloads a fresh copy of Roblox next time you launch. Fixes broken or half-updated files.", "Télécharge une nouvelle copie de Roblox au prochain lancement. Répare les fichiers cassés ou à moitié mis à jour.", "Lädt beim nächsten Start eine frische Kopie von Roblox herunter. Behebt kaputte oder halb aktualisierte Dateien.", "Bir sonraki başlatışta Roblox'un yeni bir kopyasını indirir. Bozuk veya yarım güncellenmiş dosyaları düzeltir.", "Magda-download ng bagong kopya ng Roblox sa susunod mong pag-launch. Inaayos ang sira o kalahating na-update na file." },
            new[] { "Roblox is still using its files. Close every Roblox window, then press Repair again.", "Roblox utilise encore ses fichiers. Ferme toutes les fenêtres Roblox, puis appuie à nouveau sur Réparer.", "Roblox nutzt seine Dateien noch. Schließ alle Roblox-Fenster und drück dann noch mal auf Reparieren.", "Roblox hâlâ dosyalarını kullanıyor. Tüm Roblox pencerelerini kapat, sonra tekrar Onar'a bas.", "Ginagamit pa ng Roblox ang mga file nito. Isara lahat ng Roblox window, tapos pindutin ulit ang Ayusin." },
            new[] { "Friends", "Amis", "Freunde", "Arkadaşlar", "Mga kaibigan" },
            new[] { "Rejoin last server", "Rejoindre le dernier serveur", "Letztem Server erneut beitreten", "Son sunucuya tekrar katıl", "Bumalik sa huling server" },
            new[] { "Server hop", "Changer de serveur", "Server wechseln", "Sunucu değiştir", "Lumipat ng server" },
            new[] { "Small server", "Petit serveur", "Kleiner Server", "Küçük sunucu", "Maliit na server" },
            new[] { "Join a game first. GuideStrap remembers the last server you were in.", "Rejoins d'abord un jeu. GuideStrap se souvient du dernier serveur où tu étais.", "Tritt zuerst einem Spiel bei. GuideStrap merkt sich den letzten Server, auf dem du warst.", "Önce bir oyuna katıl. GuideStrap en son bulunduğun sunucuyu hatırlar.", "Sumali muna sa isang laro. Tinatandaan ng GuideStrap ang huling server na pinuntahan mo." },
            new[] { "Paste a game link first, or join a game once.", "Colle d'abord un lien de jeu, ou rejoins un jeu une fois.", "Füge zuerst einen Spiellink ein oder tritt einmal einem Spiel bei.", "Önce bir oyun bağlantısı yapıştır veya bir kez bir oyuna katıl.", "Mag-paste muna ng link ng laro, o sumali sa isang laro kahit isang beses." },
            new[] { "Finding a small server...", "Recherche d'un petit serveur...", "Suche einen kleinen Server...", "Küçük bir sunucu aranıyor...", "Naghahanap ng maliit na server..." },
            new[] { "Finding another server...", "Recherche d'un autre serveur...", "Suche einen anderen Server...", "Başka bir sunucu aranıyor...", "Naghahanap ng ibang server..." },
            new[] { "Couldn't find an open server for that game. Try again in a moment.", "Aucun serveur libre trouvé pour ce jeu. Réessaie dans un moment.", "Kein freier Server für dieses Spiel gefunden. Versuch es gleich noch mal.", "Bu oyun için boş bir sunucu bulunamadı. Birazdan tekrar dene.", "Walang nakitang bakanteng server para sa larong iyan. Subukan ulit maya-maya." },
            new[] { "Applying your {0} setup...", "Application de ta configuration {0}...", "Dein Setup {0} wird angewendet...", "{0} kurulumun uygulanıyor...", "Inilalapat ang setup mong {0}..." },
            new[] { "Signing in...", "Connexion...", "Anmeldung...", "Giriş yapılıyor...", "Nagla-log in..." },
            new[] { "Streamer mode is on", "Le mode streamer est activé", "Streamer-Modus ist an", "Yayıncı modu açık", "Naka-on ang streamer mode" },
            new[] { "Account names are hidden.", "Les noms de compte sont cachés.", "Kontonamen sind versteckt.", "Hesap adları gizli.", "Nakatago ang mga pangalan ng account." },
            new[] { "Streamer mode", "Mode streamer", "Streamer-Modus", "Yayıncı modu", "Streamer mode" },
            new[] { "Names hidden", "Noms cachés", "Namen versteckt", "İsimler gizli", "Nakatago ang pangalan" },
            new[] { "hidden", "caché", "versteckt", "gizli", "nakatago" },
            new[] { "Hides account names and server location in GuideStrap and on Discord.", "Cache les noms de compte et la position du serveur dans GuideStrap et sur Discord.", "Versteckt Kontonamen und Serverstandort in GuideStrap und auf Discord.", "GuideStrap'te ve Discord'da hesap adlarını ve sunucu konumunu gizler.", "Itinatago ang mga pangalan ng account at lokasyon ng server sa GuideStrap at sa Discord." },
            new[] { "Session summary", "Résumé de session", "Sitzungsübersicht", "Oturum özeti", "Buod ng session" },
            new[] { "After you close Roblox, a small popup shows how long you played.", "Quand tu fermes Roblox, une petite fenêtre montre combien de temps tu as joué.", "Wenn du Roblox schließt, zeigt ein kleines Fenster, wie lange du gespielt hast.", "Roblox'u kapattığında, küçük bir pencere ne kadar oynadığını gösterir.", "Pagkasara mo ng Roblox, may maliit na popup na nagpapakita kung gaano ka katagal naglaro." },
            new[] { "Session over", "Session terminée", "Sitzung beendet", "Oturum bitti", "Tapos na ang session" },
            new[] { "You played for {0}.", "Tu as joué {0}.", "Du hast {0} gespielt.", "{0} oynadın.", "Naglaro ka nang {0}." },
            new[] { "Games: {0}", "Jeux : {0}", "Spiele: {0}", "Oyunlar: {0}", "Mga laro: {0}" },
            new[] { "Last server: {0}", "Dernier serveur : {0}", "Letzter Server: {0}", "Son sunucu: {0}", "Huling server: {0}" },
            new[] { "Friend started playing", "Un ami a commencé à jouer", "Ein Freund hat angefangen zu spielen", "Bir arkadaşın oynamaya başladı", "Nagsimulang maglaro ang isang kaibigan" },
            new[] { "{0} is playing {1}.", "{0} joue à {1}.", "{0} spielt {1}.", "{0}, {1} oynuyor.", "Naglalaro si {0} ng {1}." },
            new[] { "A friend", "Un ami", "Ein Freund", "Bir arkadaş", "Isang kaibigan" },
            new[] { "a game", "un jeu", "ein Spiel", "bir oyun", "isang laro" },
            new[] { "Join", "Rejoindre", "Beitreten", "Katıl", "Sumali" },
            new[] { "Join game", "Rejoindre le jeu", "Spiel beitreten", "Oyuna katıl", "Sumali sa laro" },
            new[] { "See who's online and join them.", "Vois qui est en ligne et rejoins-les.", "Sieh, wer online ist, und tritt ihnen bei.", "Kimin çevrimiçi olduğunu gör ve onlara katıl.", "Tingnan kung sino ang online at sumali sa kanila." },
            new[] { "Refresh", "Actualiser", "Aktualisieren", "Yenile", "I-refresh" },
            new[] { "Online friends", "Amis en ligne", "Freunde online", "Çevrimiçi arkadaşlar", "Mga kaibigang online" },
            new[] { "Friend alerts", "Alertes d'amis", "Freunde-Hinweise", "Arkadaş bildirimleri", "Alerto ng kaibigan" },
            new[] { "A small popup when a friend starts playing a game.", "Une petite fenêtre quand un ami commence à jouer.", "Ein kleines Fenster, wenn ein Freund ein Spiel startet.", "Bir arkadaşın oyuna başladığında küçük bir pencere.", "Maliit na popup kapag nagsimulang maglaro ang isang kaibigan." },
            new[] { "Open this page to load your friends.", "Ouvre cette page pour charger tes amis.", "Öffne diese Seite, um deine Freunde zu laden.", "Arkadaşlarını yüklemek için bu sayfayı aç.", "Buksan ang page na ito para i-load ang mga kaibigan mo." },
            new[] { "Pick a saved account in Accounts to see your friends.", "Choisis un compte enregistré dans Comptes pour voir tes amis.", "Wähle unter Konten ein gespeichertes Konto, um deine Freunde zu sehen.", "Arkadaşlarını görmek için Hesaplar'dan kayıtlı bir hesap seç.", "Pumili ng naka-save na account sa Mga account para makita ang mga kaibigan mo." },
            new[] { "Loading...", "Chargement...", "Lädt...", "Yükleniyor...", "Naglo-load..." },
            new[] { "None of your {0} friends are online right now.", "Aucun de tes {0} amis n'est en ligne pour l'instant.", "Keiner deiner {0} Freunde ist gerade online.", "{0} arkadaşından hiçbiri şu an çevrimiçi değil.", "Wala sa {0} mong kaibigan ang online ngayon." },
            new[] { "{0} of {1} friends online.", "{0} amis en ligne sur {1}.", "{0} von {1} Freunden online.", "{1} arkadaştan {0} tanesi çevrimiçi.", "{0} sa {1} kaibigan ang online." },
            new[] { "Couldn't load friends. {0}", "Impossible de charger les amis. {0}", "Freunde konnten nicht geladen werden. {0}", "Arkadaşlar yüklenemedi. {0}", "Hindi ma-load ang mga kaibigan. {0}" },
            new[] { "Playing {0}", "Joue à {0}", "Spielt {0}", "{0} oynuyor", "Naglalaro ng {0}" },
            new[] { "In Roblox Studio", "Dans Roblox Studio", "In Roblox Studio", "Roblox Studio'da", "Nasa Roblox Studio" },
            new[] { "Online", "En ligne", "Online", "Çevrimiçi", "Online" },
            new[] { "What your Discord profile shows while you use GuideStrap.", "Ce que ton profil Discord affiche pendant que tu utilises GuideStrap.", "Was dein Discord-Profil zeigt, während du GuideStrap nutzt.", "GuideStrap kullanırken Discord profilinin ne gösterdiği.", "Ang ipinapakita ng Discord profile mo habang gamit mo ang GuideStrap." },
            new[] { "Shows GuideStrap and what you're doing on your Discord profile.", "Affiche GuideStrap et ce que tu fais sur ton profil Discord.", "Zeigt GuideStrap und was du machst auf deinem Discord-Profil.", "Discord profilinde GuideStrap'i ve ne yaptığını gösterir.", "Ipinapakita ang GuideStrap at ang ginagawa mo sa Discord profile mo." },
            new[] { "What to show", "Quoi afficher", "Was angezeigt wird", "Ne gösterilsin", "Ano ang ipapakita" },
            new[] { "Show the game you're in", "Afficher le jeu où tu es", "Das Spiel zeigen, in dem du bist", "Bulunduğun oyunu göster", "Ipakita ang larong nilalaro mo" },
            new[] { "The game's name, picture and a View game button.", "Le nom du jeu, son image et un bouton Voir le jeu.", "Der Name des Spiels, sein Bild und ein Spiel-ansehen-Button.", "Oyunun adı, resmi ve bir Oyunu gör düğmesi.", "Ang pangalan ng laro, larawan at View game button." },
            new[] { "Show the account you're using", "Afficher le compte que tu utilises", "Das Konto zeigen, das du nutzt", "Kullandığın hesabı göster", "Ipakita ang account na gamit mo" },
            new[] { "Adds \"Playing as\" with your display name.", "Ajoute \"Joue en tant que\" avec ton nom d'affichage.", "Fügt \"Spielt als\" mit deinem Anzeigenamen hinzu.", "Görünen adınla birlikte \"Oynayan:\" ekler.", "Nagdadagdag ng \"Naglalaro bilang\" kasama ang display name mo." },
            new[] { "Let friends join you", "Laisser tes amis te rejoindre", "Freunde dir beitreten lassen", "Arkadaşlarının sana katılmasına izin ver", "Payagan ang mga kaibigan na sumali sa iyo" },
            new[] { "Adds a Join server button that puts friends in your server.", "Ajoute un bouton Rejoindre le serveur qui amène tes amis dans ton serveur.", "Fügt einen Server-beitreten-Button hinzu, der Freunde auf deinen Server bringt.", "Arkadaşlarını sunucuna getiren bir Sunucuya katıl düğmesi ekler.", "Nagdadagdag ng Join server button na magdadala sa mga kaibigan mo sa server mo." },
            new[] { "Discord has to be open on this PC. Streamer mode (in Settings) hides your account, server location and the Join button.", "Discord doit être ouvert sur ce PC. Le mode streamer (dans Réglages) cache ton compte, la position du serveur et le bouton Rejoindre.", "Discord muss auf diesem PC geöffnet sein. Der Streamer-Modus (in Einstellungen) versteckt dein Konto, den Serverstandort und den Beitreten-Button.", "Discord bu bilgisayarda açık olmalı. Yayıncı modu (Ayarlar'da) hesabını, sunucu konumunu ve Katıl düğmesini gizler.", "Kailangang bukas ang Discord sa PC na ito. Itinatago ng streamer mode (sa Settings) ang account mo, lokasyon ng server at ang Join button." },
            new[] { "Mod gallery", "Galerie de mods", "Mod-Galerie", "Mod galerisi", "Mod gallery" },
            new[] { "Skies, cursors, crosshairs and sound packs you can install in one click.", "Ciels, curseurs, viseurs et packs de sons à installer en un clic.", "Himmel, Zeiger, Fadenkreuze und Soundpakete, die du mit einem Klick installierst.", "Tek tıkla kurabileceğin gökyüzleri, imleçler, nişangahlar ve ses paketleri.", "Mga langit, cursor, crosshair at sound pack na mai-install sa isang click." },
            new[] { "Browse", "Parcourir", "Durchsuchen", "Göz at", "Mag-browse" },
            new[] { "Loading the gallery...", "Chargement de la galerie...", "Galerie wird geladen...", "Galeri yükleniyor...", "Nilo-load ang gallery..." },
            new[] { "Only cosmetic files: images, skies and sounds. Everything installs into GuideStrap's copy of Roblox.", "Seulement des fichiers cosmétiques : images, ciels et sons. Tout s'installe dans la copie de Roblox de GuideStrap.", "Nur kosmetische Dateien: Bilder, Himmel und Sounds. Alles wird in GuideStraps Kopie von Roblox installiert.", "Sadece görsel dosyalar: resimler, gökyüzleri ve sesler. Her şey GuideStrap'in Roblox kopyasına kurulur.", "Cosmetic na file lang: larawan, langit at tunog. Lahat ay naka-install sa kopya ng Roblox ng GuideStrap." },
            new[] { "The gallery is empty right now.", "La galerie est vide pour l'instant.", "Die Galerie ist gerade leer.", "Galeri şu an boş.", "Walang laman ang gallery sa ngayon." },
            new[] { "{0} mods", "{0} mods", "{0} Mods", "{0} mod", "{0} mod" },
            new[] { "Couldn't load the gallery. Check your internet and try again.", "Impossible de charger la galerie. Vérifie ta connexion internet et réessaie.", "Galerie konnte nicht geladen werden. Prüf dein Internet und versuch es noch mal.", "Galeri yüklenemedi. İnternetini kontrol edip tekrar dene.", "Hindi ma-load ang gallery. Tingnan ang internet mo at subukan ulit." },
            new[] { "Cursor", "Curseur", "Zeiger", "İmleç", "Cursor" },
            new[] { "Crosshair", "Viseur", "Fadenkreuz", "Nişangah", "Crosshair" },
            new[] { "by {0}", "par {0}", "von {0}", "yapan: {0}", "gawa ni {0}" },
            new[] { "Installed", "Installé", "Installiert", "Kuruldu", "Naka-install na" },
            new[] { "Couldn't install that", "Impossible d'installer ça", "Konnte das nicht installieren", "Bu kurulamadı", "Hindi ma-install iyan" },
            new[] { "That mod is too big.", "Ce mod est trop gros.", "Dieser Mod ist zu groß.", "Bu mod çok büyük.", "Masyadong malaki ang mod na iyan." },
            new[] { "That image couldn't be read.", "Cette image n'a pas pu être lue.", "Dieses Bild konnte nicht gelesen werden.", "Bu resim okunamadı.", "Hindi mabasa ang larawang iyan." },
            new[] { "That sky pack has no .tex files.", "Ce pack de ciel n'a pas de fichiers .tex.", "Dieses Himmel-Paket hat keine .tex-Dateien.", "Bu gökyüzü paketinde .tex dosyası yok.", "Walang .tex na file ang sky pack na iyan." },
            new[] { "That sound pack has no .ogg or .mp3 files.", "Ce pack de sons n'a pas de fichiers .ogg ou .mp3.", "Dieses Soundpaket hat keine .ogg- oder .mp3-Dateien.", "Bu ses paketinde .ogg veya .mp3 dosyası yok.", "Walang .ogg o .mp3 na file ang sound pack na iyan." },
            new[] { "Link game", "Lier un jeu", "Spiel verknüpfen", "Oyun bağla", "I-link ang laro" },
            new[] { "Auto setups", "Configurations auto", "Auto-Setups", "Otomatik kurulumlar", "Auto setup" },
            new[] { "Remove all links?", "Retirer tous les liens ?", "Alle Verknüpfungen entfernen?", "Tüm bağlantılar kaldırılsın mı?", "Tanggalin lahat ng link?" },
            new[] { "Every game goes back to your normal settings.", "Tous les jeux reviennent à tes réglages normaux.", "Alle Spiele nutzen wieder deine normalen Einstellungen.", "Tüm oyunlar normal ayarlarına döner.", "Babalik ang lahat ng laro sa normal mong settings." },
            new[] { "Link a setup to a game and it turns on by itself when you launch that game.", "Lie une configuration à un jeu et elle s'active toute seule quand tu lances ce jeu.", "Verknüpfe ein Setup mit einem Spiel, dann schaltet es sich beim Start dieses Spiels von selbst ein.", "Bir kurulumu bir oyuna bağla, o oyunu başlattığında kendiliğinden açılır.", "I-link ang setup sa isang laro at kusa itong mao-on kapag nilaunch mo ang larong iyon." },
            new[] { "  +{0} more", "  +{0} de plus", "  +{0} weitere", "  +{0} tane daha", "  +{0} pa" },
            new[] { "Choose a saved setup in the list above, then link it to a game.", "Choisis une configuration enregistrée dans la liste au-dessus, puis lie-la à un jeu.", "Wähle oben in der Liste ein gespeichertes Setup und verknüpfe es dann mit einem Spiel.", "Yukarıdaki listeden kayıtlı bir kurulum seç, sonra onu bir oyuna bağla.", "Pumili ng naka-save na setup sa listahan sa itaas, tapos i-link ito sa isang laro." },
            new[] { "Link {0} to a game", "Lier {0} à un jeu", "{0} mit einem Spiel verknüpfen", "{0} kurulumunu bir oyuna bağla", "I-link ang {0} sa isang laro" },
            new[] { "Paste the game link or place ID. GuideStrap switches to this setup whenever you launch that game.", "Colle le lien du jeu ou l'ID de place. GuideStrap passe à cette configuration chaque fois que tu lances ce jeu.", "Füge den Spiellink oder die Place-ID ein. GuideStrap wechselt zu diesem Setup, wann immer du dieses Spiel startest.", "Oyun bağlantısını veya place ID'yi yapıştır. GuideStrap o oyunu her başlattığında bu kuruluma geçer.", "I-paste ang link ng laro o place ID. Lilipat ang GuideStrap sa setup na ito tuwing ilalaunch mo ang larong iyon." },
            new[] { "Link", "Lier", "Verknüpfen", "Bağla", "I-link" },
            new[] { "That doesn't look like a game link", "Ça ne ressemble pas à un lien de jeu", "Das sieht nicht wie ein Spiellink aus", "Bu bir oyun bağlantısına benzemiyor", "Mukhang hindi iyan link ng laro" },
            new[] { "Paste a link like roblox.com/games/123456, or just the number.", "Colle un lien comme roblox.com/games/123456, ou juste le numéro.", "Füge einen Link wie roblox.com/games/123456 ein, oder nur die Zahl.", "roblox.com/games/123456 gibi bir bağlantı ya da sadece numarayı yapıştır.", "Mag-paste ng link gaya ng roblox.com/games/123456, o ang numero lang." },
            new[] { "Linked {0}.", "{0} liée.", "{0} verknüpft.", "{0} bağlandı.", "Na-link ang {0}." },
            new[] { "Rejoin your server", "Rejoins ton serveur", "Tritt deinem Server erneut bei", "Sunucuna tekrar katıl", "Bumalik sa server mo" },
            new[] { "Jump straight back into the server you were in, if it's still running.", "Retourne directement dans le serveur où tu étais, s'il tourne encore.", "Spring direkt zurück auf den Server, auf dem du warst, falls er noch läuft.", "Hâlâ açıksa bulunduğun sunucuya hemen geri dön.", "Bumalik agad sa server na pinuntahan mo, kung tumatakbo pa ito." },
            new[] { "Rejoin", "Rejoindre", "Erneut beitreten", "Tekrar katıl", "Bumalik" },
            new[] { "Checking Roblox's servers...", "Vérification des serveurs de Roblox...", "Prüfe Roblox' Server...", "Roblox sunucuları kontrol ediliyor...", "Tinitingnan ang mga server ng Roblox..." },
            new[] { "You're offline", "Tu es hors ligne", "Du bist offline", "Çevrimdışısın", "Offline ka" },
            new[] { "GuideStrap can't reach the internet. Check your Wi-Fi or cable, then try again.", "GuideStrap n'arrive pas à accéder à internet. Vérifie ton Wi-Fi ou ton câble, puis réessaie.", "GuideStrap erreicht das Internet nicht. Prüf dein WLAN oder Kabel und versuch es dann noch mal.", "GuideStrap internete ulaşamıyor. Wi-Fi'ını veya kablonu kontrol et, sonra tekrar dene.", "Hindi maabot ng GuideStrap ang internet. Tingnan ang Wi-Fi o cable mo, tapos subukan ulit." },
            new[] { "Try again", "Réessayer", "Noch mal versuchen", "Tekrar dene", "Subukan ulit" },
            new[] { "Roblox looks down", "Roblox semble en panne", "Roblox scheint down zu sein", "Roblox çökmüş görünüyor", "Mukhang down ang Roblox" },
            new[] { "Roblox's servers aren't answering right now, so games might not load. This is on Roblox's side, not your PC.", "Les serveurs de Roblox ne répondent pas en ce moment, donc les jeux risquent de ne pas charger. Le problème vient de Roblox, pas de ton PC.", "Roblox' Server antworten gerade nicht, daher laden Spiele vielleicht nicht. Das liegt an Roblox, nicht an deinem PC.", "Roblox sunucuları şu an yanıt vermiyor, bu yüzden oyunlar yüklenmeyebilir. Sorun Roblox'ta, bilgisayarında değil.", "Hindi sumasagot ang mga server ng Roblox ngayon, kaya baka hindi mag-load ang mga laro. Problema ito ng Roblox, hindi ng PC mo." },
            new[] { "Try anyway", "Essayer quand même", "Trotzdem versuchen", "Yine de dene", "Subukan pa rin" },
            new[] { "Status page", "Page de statut", "Statusseite", "Durum sayfası", "Status page" },
            new[] { "Start screen background", "Fond de l'écran d'accueil", "Hintergrund des Startbildschirms", "Başlangıç ekranı arka planı", "Background ng start screen" },
            new[] { "Your own picture behind the start screen. It's darkened so text stays readable.", "Ta propre image derrière l'écran d'accueil. Elle est assombrie pour que le texte reste lisible.", "Dein eigenes Bild hinter dem Startbildschirm. Es wird abgedunkelt, damit Text lesbar bleibt.", "Başlangıç ekranının arkasında kendi resmin. Yazılar okunabilsin diye karartılır.", "Sarili mong larawan sa likod ng start screen. Pinadilim ito para mabasa pa rin ang text." },
            new[] { "Choose image", "Choisir une image", "Bild wählen", "Resim seç", "Pumili ng larawan" },
            new[] { "The start screen is back to plain.", "L'écran d'accueil est redevenu uni.", "Der Startbildschirm ist wieder schlicht.", "Başlangıç ekranı tekrar sade.", "Plain na ulit ang start screen." },
            new[] { "Pick a picture", "Choisis une image", "Wähle ein Bild", "Bir resim seç", "Pumili ng larawan" },
            new[] { "That picture is too small.", "Cette image est trop petite.", "Dieses Bild ist zu klein.", "Bu resim çok küçük.", "Masyadong maliit ang larawang iyan." },
            new[] { "Background set. It shows on the start screen.", "Fond réglé. Il s'affiche sur l'écran d'accueil.", "Hintergrund gesetzt. Er erscheint auf dem Startbildschirm.", "Arka plan ayarlandı. Başlangıç ekranında görünür.", "Naka-set na ang background. Lalabas ito sa start screen." },
            new[] { "Couldn't use that picture", "Impossible d'utiliser cette image", "Bild konnte nicht verwendet werden", "Bu resim kullanılamadı", "Hindi magamit ang larawang iyan" },
            new[] { "Start with Windows", "Démarrer avec Windows", "Mit Windows starten", "Windows ile başlat", "Magsimula kasabay ng Windows" },
            new[] { "Opens quietly in the tray when your PC starts, so friend alerts work.", "S'ouvre discrètement dans la zone de notification au démarrage du PC, pour que les alertes d'amis marchent.", "Öffnet sich leise im Infobereich, wenn dein PC startet, damit Freunde-Hinweise funktionieren.", "Bilgisayarın açılınca tepside sessizce açılır, böylece arkadaş bildirimleri çalışır.", "Tahimik na bumubukas sa tray pagbukas ng PC mo, para gumana ang alerto ng kaibigan." },
            new[] { "Opens quietly in the tray when your PC starts.", "S'ouvre discrètement dans la zone de notification au démarrage du PC.", "Öffnet sich leise im Infobereich, wenn dein PC startet.", "Bilgisayarın açılınca tepside sessizce açılır.", "Tahimik na bumubukas sa tray pagbukas ng PC mo." },
            new[] { "Install GuideStrap first", "Installe GuideStrap d'abord", "Installiere zuerst GuideStrap", "Önce GuideStrap'i kur", "I-install muna ang GuideStrap" },
            new[] { "This only works for the installed GuideStrap, not a copy you're running from somewhere else.", "Ça ne marche qu'avec le GuideStrap installé, pas avec une copie lancée depuis un autre endroit.", "Das funktioniert nur mit dem installierten GuideStrap, nicht mit einer Kopie, die du von woanders startest.", "Bu sadece kurulu GuideStrap ile çalışır, başka bir yerden açtığın bir kopyayla değil.", "Gumagana lang ito sa naka-install na GuideStrap, hindi sa kopyang binuksan mo mula sa ibang lugar." },
            new[] { "Uninstall GuideStrap", "Désinstaller GuideStrap", "GuideStrap deinstallieren", "GuideStrap'i kaldır", "I-uninstall ang GuideStrap" },
            new[] { "Removes GuideStrap, its settings and its copy of Roblox. Normal Roblox stays.", "Supprime GuideStrap, ses réglages et sa copie de Roblox. Roblox normal reste.", "Entfernt GuideStrap, seine Einstellungen und seine Kopie von Roblox. Normales Roblox bleibt.", "GuideStrap'i, ayarlarını ve Roblox kopyasını kaldırır. Normal Roblox kalır.", "Tinatanggal ang GuideStrap, ang settings nito at ang kopya nito ng Roblox. Mananatili ang normal na Roblox." },
            new[] { "GuideStrap isn't installed", "GuideStrap n'est pas installé", "GuideStrap ist nicht installiert", "GuideStrap kurulu değil", "Hindi naka-install ang GuideStrap" },
            new[] { "This copy isn't installed, so there's nothing to remove.", "Cette copie n'est pas installée, donc il n'y a rien à supprimer.", "Diese Kopie ist nicht installiert, also gibt es nichts zu entfernen.", "Bu kopya kurulu değil, yani kaldırılacak bir şey yok.", "Hindi naka-install ang kopyang ito, kaya walang tatanggalin." },
            new[] { "The language GuideStrap uses.", "La langue utilisée par GuideStrap.", "Die Sprache, die GuideStrap nutzt.", "GuideStrap'in kullandığı dil.", "Ang wikang gamit ng GuideStrap." },
            new[] { "What's new in GuideStrap {0}", "Nouveautés de GuideStrap {0}", "Neu in GuideStrap {0}", "GuideStrap {0} yenilikleri", "Ano ang bago sa GuideStrap {0}" },
            new[] { "Nice", "Super", "Super", "Süper", "Ayos" },
            new[] { "• GuideStrap checks if Roblox is down before launching\n• Pick your own picture for the start screen (Settings > Appearance)\n• Start GuideStrap with Windows, quietly in the tray (Settings)\n• Uninstall GuideStrap from Settings > Tools\n• New languages: Français, Deutsch, Türkçe and Filipino\n• This window, after every update", "• GuideStrap vérifie si Roblox est en panne avant de le lancer\n• Choisis ta propre image pour l'écran d'accueil (Réglages > Apparence)\n• Démarre GuideStrap avec Windows, discrètement dans la zone de notification (Réglages)\n• Désinstalle GuideStrap depuis Réglages > Outils\n• Nouvelles langues : Français, Deutsch, Türkçe et Filipino\n• Cette fenêtre, après chaque mise à jour", "• GuideStrap prüft vor dem Start, ob Roblox down ist\n• Wähle dein eigenes Bild für den Startbildschirm (Einstellungen > Aussehen)\n• Starte GuideStrap mit Windows, leise im Infobereich (Einstellungen)\n• Deinstalliere GuideStrap unter Einstellungen > Werkzeuge\n• Neue Sprachen: Français, Deutsch, Türkçe und Filipino\n• Dieses Fenster, nach jedem Update", "• GuideStrap başlatmadan önce Roblox'un çöküp çökmediğini kontrol eder\n• Başlangıç ekranı için kendi resmini seç (Ayarlar > Görünüm)\n• GuideStrap'i Windows ile, tepside sessizce başlat (Ayarlar)\n• GuideStrap'i Ayarlar > Araçlar'dan kaldır\n• Yeni diller: Français, Deutsch, Türkçe ve Filipino\n• Her güncellemeden sonra bu pencere", "• Tinitingnan ng GuideStrap kung down ang Roblox bago mag-launch\n• Pumili ng sarili mong larawan para sa start screen (Settings > Itsura)\n• Simulan ang GuideStrap kasabay ng Windows, tahimik sa tray (Settings)\n• I-uninstall ang GuideStrap sa Settings > Mga tool\n• Mga bagong wika: Français, Deutsch, Türkçe at Filipino\n• Ang window na ito, pagkatapos ng bawat update" },
            new[] { "This removes GuideStrap and its copy of Roblox. Normal Roblox stays installed.", "Ceci supprime GuideStrap et sa copie de Roblox. Roblox normal reste installé.", "Das entfernt GuideStrap und seine Kopie von Roblox. Normales Roblox bleibt installiert.", "Bu, GuideStrap'i ve Roblox kopyasını kaldırır. Normal Roblox kurulu kalır.", "Tatanggalin nito ang GuideStrap at ang kopya nito ng Roblox. Mananatiling naka-install ang normal na Roblox." },
            new[] { "Keep my settings, accounts and mods in case I reinstall", "Garder mes réglages, comptes et mods au cas où je réinstalle", "Meine Einstellungen, Konten und Mods behalten, falls ich neu installiere", "Yeniden kurarsam diye ayarlarımı, hesaplarımı ve modlarımı sakla", "Itabi ang settings, account at mods ko kung sakaling mag-reinstall ako" },
            new[] { "GuideStrap was uninstalled. Your settings, accounts and mods are kept for next time.", "GuideStrap a été désinstallé. Tes réglages, comptes et mods sont gardés pour la prochaine fois.", "GuideStrap wurde deinstalliert. Deine Einstellungen, Konten und Mods bleiben fürs nächste Mal erhalten.", "GuideStrap kaldırıldı. Ayarların, hesapların ve modların bir dahaki sefer için saklandı.", "Na-uninstall na ang GuideStrap. Naitabi ang settings, account at mods mo para sa susunod." },
            new[] { "• Black is now the default theme (Purple is still in Settings > Appearance)\n• New black logo for the app, the taskbar, Discord and the website\n• Uninstalling can now keep your settings, accounts and mods for a reinstall", "• Noir est maintenant le thème par défaut (Violet reste dans Réglages > Apparence)\n• Nouveau logo noir pour l'app, la barre des tâches, Discord et le site\n• La désinstallation peut maintenant garder tes réglages, comptes et mods pour une réinstallation", "• Schwarz ist jetzt das Standard-Design (Lila gibt es weiter unter Einstellungen > Aussehen)\n• Neues schwarzes Logo für die App, die Taskleiste, Discord und die Website\n• Beim Deinstallieren kannst du jetzt Einstellungen, Konten und Mods für eine Neuinstallation behalten", "• Siyah artık varsayılan tema (Mor hâlâ Ayarlar > Görünüm'de)\n• Uygulama, görev çubuğu, Discord ve web sitesi için yeni siyah logo\n• Kaldırırken artık ayarlarını, hesaplarını ve modlarını yeniden kurulum için saklayabilirsin", "• Itim na ang default na tema (nasa Settings > Itsura pa rin ang Purple)\n• Bagong itim na logo para sa app, taskbar, Discord at website\n• Puwede mo nang itabi ang settings, account at mods mo pag nag-uninstall, para sa reinstall" },
            new[] { "• The logo is now black in every theme, Purple included\n• Fixed desktop and Start menu icons keeping the old look after an update\n• Black is now the default theme (Purple is still in Settings > Appearance)\n• New black logo for the app, the taskbar, Discord and the website\n• Uninstalling can now keep your settings, accounts and mods for a reinstall", "• Le logo est maintenant noir dans tous les thèmes, Violet compris\n• Corrigé : les icônes du bureau et du menu Démarrer ne gardent plus l'ancien look après une mise à jour\n• Noir est maintenant le thème par défaut (Violet reste dans Réglages > Apparence)\n• Nouveau logo noir pour l'app, la barre des tâches, Discord et le site\n• La désinstallation peut maintenant garder tes réglages, comptes et mods pour une réinstallation", "• Das Logo ist jetzt in jedem Design schwarz, auch in Lila\n• Behoben: Desktop- und Startmenü-Symbole behalten nach einem Update nicht mehr das alte Aussehen\n• Schwarz ist jetzt das Standard-Design (Lila gibt es weiter unter Einstellungen > Aussehen)\n• Neues schwarzes Logo für die App, die Taskleiste, Discord und die Website\n• Beim Deinstallieren kannst du jetzt Einstellungen, Konten und Mods für eine Neuinstallation behalten", "• Logo artık Mor dahil her temada siyah\n• Düzeltildi: masaüstü ve Başlat menüsü simgeleri güncellemeden sonra eski görünümde kalmıyor\n• Siyah artık varsayılan tema (Mor hâlâ Ayarlar > Görünüm'de)\n• Uygulama, görev çubuğu, Discord ve web sitesi için yeni siyah logo\n• Kaldırırken artık ayarlarını, hesaplarını ve modlarını yeniden kurulum için saklayabilirsin", "• Itim na ang logo sa bawat tema, pati sa Purple\n• Naayos: hindi na naiiwan sa lumang itsura ang mga icon sa desktop at Start menu pagkatapos ng update\n• Itim na ang default na tema (nasa Settings > Itsura pa rin ang Purple)\n• Bagong itim na logo para sa app, taskbar, Discord at website\n• Puwede mo nang itabi ang settings, account at mods mo pag nag-uninstall, para sa reinstall" }
        };
    }

    // Install / reinstall / uninstall.
    // The installed copy (Start menu, desktop shortcut, Roblox links) never shows setup.
    // Running any other copy (like the downloaded .exe) shows setup, or Reinstall/Repair if already installed.
    static class Setup
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GuideStrap";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool StartsWithWindows
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue("GuideStrap") != null;
                }
                catch { return false; }
            }
        }

        // Starts the installed GuideStrap in the tray when Windows starts
        public static void SetStartup(bool on)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (on) key.SetValue("GuideStrap", $"\"{InstallExe}\" --tray");
                else key.DeleteValue("GuideStrap", false);
            }
            catch { }
        }
        public static string InstallExe => Path.Combine(Paths.Base, "GuideStrap.exe");
        static string StartMenuLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "GuideStrap.lnk");
        static string DesktopLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GuideStrap.lnk");

        static bool RunningInstalled =>
            string.Equals(Path.GetFullPath(Environment.ProcessPath ?? ""), Path.GetFullPath(InstallExe), StringComparison.OrdinalIgnoreCase);

        public static string? InstalledVersion()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
                return key?.GetValue("DisplayVersion") as string;
            }
            catch { return null; }
        }

        public static bool IsInstalled => File.Exists(InstallExe) && InstalledVersion() != null;
        public static bool HasDesktopShortcut => File.Exists(DesktopLink);

        public static bool NeedsSetup()
        {
#if DEBUG
            return false;   // never while developing in Visual Studio
#else
            return !RunningInstalled;
#endif
        }

        // A Roblox link opened a copy that isn't the installed one: pass the link to the installed copy.
        public static bool HandOffIfInstalled(string[] args)
        {
#if DEBUG
            return false;
#else
            bool hasLink = args.Any(a => a.StartsWith("roblox", StringComparison.OrdinalIgnoreCase));
            if (!hasLink || RunningInstalled || !IsInstalled) return false;
            var psi = new ProcessStartInfo(InstallExe) { UseShellExecute = false, WorkingDirectory = Paths.Base };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi);
            return true;
#endif
        }

        public static bool OtherCopiesRunning()
        {
            int me = Environment.ProcessId;
            return Process.GetProcessesByName("GuideStrap").Any(p => { using (p) return p.Id != me; });
        }

        public static void CloseOtherCopies()
        {
            int me = Environment.ProcessId;
            foreach (var p in Process.GetProcessesByName("GuideStrap"))
            {
                using (p)
                {
                    if (p.Id == me) continue;
                    try { p.Kill(); p.WaitForExit(4000); } catch { }
                }
            }
        }

        public static void Install(string language, bool desktopShortcut, bool openLinks, bool repairRoblox, bool startWithWindows)
        {
            Directory.CreateDirectory(Paths.Base);
            if (!RunningInstalled)
            {
                // the old copy can stay locked for a moment after closing, so retry briefly
                for (int attempt = 0; ; attempt++)
                {
                    try { File.Copy(Environment.ProcessPath!, InstallExe, true); break; }
                    catch (IOException) when (attempt < 10) { Thread.Sleep(300); }
                }
            }

            CreateShortcut(StartMenuLink);
            SetStartup(startWithWindows);
            if (desktopShortcut) CreateShortcut(DesktopLink);
            else try { if (File.Exists(DesktopLink)) File.Delete(DesktopLink); } catch { }
            WriteEntry();

            var settings = AppSettings.Load();
            settings.Language = language;
            if (openLinks) LinkHandler.Register(settings, InstallExe);
            else if (LinkHandler.PointsTo(InstallExe)) LinkHandler.Restore(settings);
            settings.Save();

            if (repairRoblox)
                try { if (Directory.Exists(Paths.Versions)) Directory.Delete(Paths.Versions, true); } catch { }
        }

        public static void StartInstalled() =>
            Process.Start(new ProcessStartInfo(InstallExe) { UseShellExecute = true, WorkingDirectory = Paths.Base });

        // keeps the version shown in Windows' app list right after self-updates
        public static void RefreshEntry()
        {
            try
            {
                if (!RunningInstalled || InstalledVersion() == AppInfo.Version) return;
                WriteEntry();
                // after an update, remake the shortcuts and tell Windows to reload icons,
                // otherwise the desktop and Start menu keep showing the old icon
                if (File.Exists(DesktopLink)) CreateShortcut(DesktopLink);
                if (File.Exists(StartMenuLink)) CreateShortcut(StartMenuLink);
                // a GuideStrap pinned to the taskbar is a shortcut too
                string pinned = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar", "GuideStrap.lnk");
                if (File.Exists(pinned)) CreateShortcut(pinned);
                NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);   // SHCNE_ASSOCCHANGED
            }
            catch { }
        }

        static void WriteEntry()
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
            key.SetValue("DisplayName", "GuideStrap");
            key.SetValue("DisplayVersion", AppInfo.Version);
            key.SetValue("Publisher", "Guide");
            key.SetValue("DisplayIcon", InstallExe);
            key.SetValue("InstallLocation", Paths.Base);
            key.SetValue("UninstallString", $"\"{InstallExe}\" --uninstall");
            key.SetValue("URLInfoAbout", "https://github.com/guideships/GuideStrap");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            try { key.SetValue("EstimatedSize", (int)(new FileInfo(InstallExe).Length / 1024), RegistryValueKind.DWord); } catch { }
        }

        static void CreateShortcut(string path)
        {
            try
            {
                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return;
                dynamic shell = Activator.CreateInstance(type)!;
                dynamic link = shell.CreateShortcut(path);
                link.TargetPath = InstallExe;
                link.WorkingDirectory = Paths.Base;
                link.IconLocation = InstallExe + ",0";
                link.Description = "GuideStrap";
                link.Save();
                Marshal.FinalReleaseComObject(link);
                Marshal.FinalReleaseComObject(shell);
            }
            catch { }
        }

        // Returns true if GuideStrap was removed (the caller should then exit)
        public static bool Uninstall(IWin32Window? owner = null)
        {
            var settings = AppSettings.Load();
            L.Lang = settings.Language.Length > 0 ? settings.Language : L.SystemLanguage();
            Theme.Apply(settings.ThemeName, settings.Accent);

            if (OtherCopiesRunning())
            {
                ThemedDialog.Prompt(owner, "Close GuideStrap first", "GuideStrap is still running (check the tray near the clock). Close it, then try again.", "OK");
                return false;
            }
            bool keep;
            using (var ask = new ThemedDialog("Uninstall GuideStrap?",
                       "This removes GuideStrap and its copy of Roblox. Normal Roblox stays installed.",
                       new[] { "Uninstall", "Cancel" },
                       "Keep my settings, accounts and mods in case I reinstall"))
            {
                if (owner == null) ask.StartPosition = FormStartPosition.CenterScreen;
                ask.ShowDialog(owner);
                if (ask.Choice != 0) return false;
                keep = ask.Checked;
            }

            SetStartup(false);
            try { if (LinkHandler.PointsTo(InstallExe)) LinkHandler.Restore(settings); } catch { }
            foreach (var link in new[] { StartMenuLink, DesktopLink })
                try { if (File.Exists(link)) File.Delete(link); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }

            ThemedDialog.Prompt(owner, "GuideStrap", keep
                ? "GuideStrap was uninstalled. Your settings, accounts and mods are kept for next time."
                : "GuideStrap was uninstalled.", "OK");
            // deleted a moment after we exit (this .exe can't delete itself while running).
            // Keeping settings: only the app, its copy of Roblox and downloaded caches go.
            string q(string path) => "\"" + path + "\"";
            string remove = keep
                ? $"del /f /q {q(InstallExe)} & del /f /q {q(InstallExe + ".old")} & rmdir /s /q {q(Paths.Versions)} & rmdir /s /q {q(Paths.Downloads)}" +
                  $" & rmdir /s /q {q(Paths.Avatars)} & rmdir /s /q {q(Paths.Icons)}"
                : $"rmdir /s /q {q(Paths.Base)}";
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & {remove}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            });
            return true;
        }
    }

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
