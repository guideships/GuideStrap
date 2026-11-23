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
                              backgroundToggle = new(), locationToggle = new(), multiToggle = new(), linksToggle = new();

        // ---------- mods ----------
        readonly Label deathStatus = new(), cursorStatus = new(), skyStatus = new();
        readonly DarkCombo cursorCombo = new();
        readonly PictureBox cursorPreview = new();
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
            nav.Add("Graphics", "\uE7F4", "Graphics", "fps quality performance texture grass sky preset lag");
            nav.Add("Flags", "\uE7C1", "FastFlags", "fflag flag json fastflag fflags");
            nav.Add("Font", "\uE8D2", "Text & Font", "font text letters typeface");
            nav.Add("Mods", "\uE790", "Mods", "mods death sound oof cursor mouse sky skybox");
            nav.Add("Settings", "\uE713", "Settings", "launcher close website folder reinstall");
            nav.Add("About", "\uE946", "About", "about info version what help guide");
            nav.Location = new Point(D(10), D(46));
            nav.Size = new Size(sw - D(20), 8 * (nav.ItemHeight + nav.Gap));
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
            if (acc != null)
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
            var page = NewPage("Font", "Text & Font", "The font Roblox uses in-game.");

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

            // death sound
            var soundButtons = ButtonPair(
                Secondary("Choose .ogg", "\uE8E5", D(120), D(30)), ChooseDeathSound,
                Secondary("Reset", "\uE72C", D(80), D(30)), () => { settings.DeathSoundFile = null; settings.Save(); RefreshMods(); });
            Add(page, ModCard("Death sound", deathStatus, soundButtons));

            // cursor
            cursorPreview.Size = new Size(D(32), D(32));
            cursorPreview.SizeMode = PictureBoxSizeMode.Zoom;
            cursorPreview.BackColor = Theme.Input;
            SetupCombo(cursorCombo, D(170), "Default", "GuideStrap arrow", "Small dot", "Custom image...");
            var cursorRight = new Panel { Size = new Size(D(212), D(32)), BackColor = Theme.Card };
            cursorPreview.Location = new Point(0, 0);
            cursorCombo.Location = new Point(D(42), (D(32) - cursorCombo.Height) / 2);
            cursorRight.Controls.Add(cursorPreview);
            cursorRight.Controls.Add(cursorCombo);
            cursorCombo.SelectedIndexChanged += (_, _) => { if (!modsSyncing) OnCursorPicked(); };
            Add(page, ModCard("Mouse cursor", cursorStatus, cursorRight));

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

        void RefreshMods()
        {
            modsSyncing = true;
            deathStatus.Text = L.T(settings.DeathSoundFile != null && File.Exists(settings.DeathSoundFile)
                ? "Custom sound" : "Roblox's default sound");
            cursorCombo.SelectedIndex = settings.CursorPreset switch { "guidestrap" => 1, "dot" => 2, "custom" => 3, _ => 0 };
            cursorStatus.Text = L.T(settings.CursorPreset switch
            {
                "guidestrap" => "Purple arrow made for GuideStrap",
                "dot" => "A small dot, easy to aim with",
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
            string pick = cursorCombo.SelectedIndex switch { 1 => "guidestrap", 2 => "dot", 3 => "custom", _ => "default" };
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

            Section(page, "Discord");
            Add(page, SettingCard("Discord status", "Shows on your profile what you're doing in GuideStrap and which game you're in.",
                T(discordToggle, settings.DiscordPresence, v =>
                {
                    settings.DiscordPresence = v;
                    settings.Save();
                    Presence.Enable(v);
                    if (v) Presence.Screen("Editing settings", "Settings");
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
            Add(page, SettingCard("Language", "English, Español or Português.", langCombo));

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
            settings.CursorPreset = p.CursorPreset is "guidestrap" or "dot" ? p.CursorPreset : "default";
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
        public int Choice { get; private set; } = -1;
        public bool Checked => check?.Checked ?? false;

        public ThemedDialog(string title, string message, string[] buttons, string? checkText = null)
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

        public static Color LogoDark = Color.FromArgb(22, 14, 40);
        public static Color LogoLight = Color.FromArgb(52, 32, 96);

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
                LogoDark = Color.FromArgb(24, 24, 24);
                LogoLight = Color.FromArgb(50, 50, 50);
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
        public static void Solidify(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is Label l && l.BackColor == Color.Transparent && root.BackColor.A == 255) l.BackColor = root.BackColor;
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
            g.Clear(Parent?.BackColor ?? Theme.Bg);
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
            g.Clear(Parent?.BackColor ?? Theme.Bg);
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
            g.Clear(Parent?.BackColor ?? Theme.Bg);
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
            g.Clear(Parent?.BackColor ?? Theme.Bar);
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
            g.Clear(Parent?.BackColor ?? Theme.Card);
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
            g.Clear(Parent?.BackColor ?? Theme.Bg);
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
        public bool DiscordPresence { get; set; } = true;
        public bool RunInBackground { get; set; } = true;
        public bool ShowServerLocation { get; set; } = true;
        public bool MultiInstance { get; set; }
        public string? DeathSoundFile { get; set; }
        public string CursorPreset { get; set; } = "default";
        public bool CustomSky { get; set; }
        public string Accent { get; set; } = "Purple";
        public string ThemeName { get; set; } = "Purple";
        public string Language { get; set; } = "";
        public Dictionary<string, string> PreviousLinkHandlers { get; set; } = new();
        public DateTime LastUpdateCheck { get; set; }

        static string FilePath => Path.Combine(Paths.Base, "settings.json");

        public static AppSettings Load()
        {
            try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
            catch { return new(); }
        }

        public void Save()
        {
            Directory.CreateDirectory(Paths.Base);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
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
            if (settings.DeathSoundFile != null && File.Exists(settings.DeathSoundFile))
                overlay[@"content\sounds\ouch.ogg"] = settings.DeathSoundFile;
            string? cursorDir = ModAssets.CursorDir(settings);
            if (cursorDir != null)
                foreach (var name in new[] { "ArrowCursor.png", "ArrowFarCursor.png" })
                    overlay[$@"content\textures\Cursors\KeyboardMouse\{name}"] = Path.Combine(cursorDir, name);
            if (settings.CustomSky && Directory.Exists(Paths.Sky))
                foreach (var f in Directory.GetFiles(Paths.Sky, "*.tex"))
                    overlay[$@"PlatformContent\pc\textures\sky\{Path.GetFileName(f)}"] = f;
            ApplyOverlay(versionDir, overlay);

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

            if (settings.FontFile == null || !File.Exists(settings.FontFile)) return;

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

        public static string BuildLaunchUri(string ticket, long? placeId)
        {
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string tracker = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
            if (placeId == null)
                return $"roblox-player:1+launchmode:app+gameinfo:{ticket}+launchtime:{time}+browsertrackerid:{tracker}" +
                       "+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";

            string placeUrl = Uri.EscapeDataString(
                $"https://www.roblox.com/Game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId={tracker}" +
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

            foreach (var pkg in packages)
            {
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

                string target = PackageMap.TryGetValue(pkg.Name, out var sub) ? Path.Combine(tempDir, sub) : tempDir;
                await Task.Run(() => ExtractZip(data, target), ct);
            }

            File.WriteAllText(Path.Combine(tempDir, "AppSettings.xml"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Settings>\r\n" +
                "\t<ContentFolder>content</ContentFolder>\r\n" +
                "\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n</Settings>\r\n");

            if (Directory.Exists(versionDir)) Directory.Delete(versionDir, true);
            Directory.Move(tempDir, versionDir);
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
        public const string Version = "4.2";
        // GitHub repo for self-updates, e.g. "yourname/GuideStrap". Leave empty until you publish releases.
        public const string UpdateRepo = "guideships/GuideStrap";
        public const string DiscordAppId = "1574254036398375062";

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
                L.Lang = repair && current.Language.Length > 0 ? current.Language : L.SystemLanguage();
                if (repair) Theme.Apply(current.ThemeName, current.Accent);
                Open(new SetupForm(this, repair));
                return;
            }

            var settings = AppSettings.Load();
            L.Lang = settings.Language.Length > 0 ? settings.Language : L.SystemLanguage();
            Theme.Apply(settings.ThemeName, settings.Accent);
            Setup.RefreshEntry();
            Updater.CleanupOld();
            Presence.Enable(settings.DiscordPresence);
            Presence.WatchEnded += OnWatchEnded;
            Presence.ServerLocated += location =>
            {
                if (tray != null && AppSettings.Load().ShowServerLocation)
                    tray.ShowBalloonTip(4000, "GuideStrap", L.F("Connected to a server in {0}", location), ToolTipIcon.None);
            };
            if (launchUri != null)
                Open(new LoadingForm(this, settings, AccountStore.Load(), launchUri, ""));
            else
                Open(new LauncherForm(this));
        }

        public void Open(Form form)
        {
            open++;
            form.FormClosed += (_, _) =>
            {
                open--;
                if (open <= 0 && !Presence.Watching) ExitThread();
            };
            form.Show();
        }

        // Keeps GuideStrap running quietly (tray icon) while Roblox is open, for the Discord status.
        public void WatchRoblox()
        {
            if (Presence.Watching) return;
            Presence.WatchRoblox();
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
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            if (open <= 0) ExitThread();
        }

        protected override void ExitThreadCore()
        {
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
                if (inRoblox && placeId != 0)
                {
                    string name = gameName ?? "a game";
                    p.Details = Cut("In " + name);
                    p.State = gameCreator != null ? Cut("by " + gameCreator) : null;
                    p.Timestamps = new Timestamps(gameStart);
                    p.Assets = new Assets
                    {
                        LargeImageKey = gameIcon is { Length: > 0 and <= 256 } ? gameIcon : "guidestrap",
                        LargeImageText = Cut(name),
                        SmallImageKey = "guidestrap",
                        SmallImageText = serverLocation != null ? Cut("Server: " + serverLocation) : "GuideStrap",
                    };
                    p.Buttons = new[] { new DiscordRPC.Button { Label = "View game", Url = $"https://www.roblox.com/games/{placeId}" } };
                }
                else if (inRoblox)
                {
                    p.Details = "In the Roblox app";
                    p.Timestamps = new Timestamps(robloxStart);
                    p.Assets = new Assets { LargeImageKey = "guidestrap", LargeImageText = "GuideStrap" };
                }
                else
                {
                    p.Details = screen;
                    p.State = screenDetail;
                    p.Timestamps = new Timestamps(appStart);
                    p.Assets = new Assets { LargeImageKey = "guidestrap", LargeImageText = "GuideStrap" };
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
                        var file = new DirectoryInfo(logDir).GetFiles("*.log")
                            .Where(f => f.CreationTime >= started.AddSeconds(-15))
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
                            await HandleLineAsync(line);
                    }

                    // Roblox can restart itself, so only stop after it's been gone for a few seconds
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
                gameName = gameCreator = gameIcon = serverLocation = lastServerIp = null;
                Log("Roblox closed");
                Push();
                WatchEnded?.Invoke();
            }
        }

        static async Task HandleLineAsync(string line)
        {
            if (line.Contains("! Joining game") || line.Contains("Joining game"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"place (\d+)");
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
            Action<string, float> report, CancellationToken ct)
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

            report("Applying your settings...", -1f);
            Mods.Apply(versionDir, settings);

            var acc = accounts.FirstOrDefault(a => a.UserId == settings.ActiveAccountId);
            string arg;
            if (launchUri != null)   // opened from the website's Play button
            {
                arg = launchUri;
                if (acc != null && settings.UseAccountForWebsite && launchUri.Contains("gameinfo:"))
                {
                    report($"Signing in as {acc.DisplayName}...", -1f);
                    string ticket = await RobloxApi.GetAuthTicketAsync(acc.Cookie);
                    arg = Regex.Replace(launchUri, @"gameinfo:[^+]*", _ => "gameinfo:" + ticket);
                }
            }
            else
            {
                long? placeId = ParsePlaceId(placeText);
                if (placeText.Trim().Length > 0 && placeId == null)
                    throw new Exception("That doesn't look like a game link or place ID.");

                if (acc == null)
                {
                    if (placeId != null)
                    {
                        Process.Start(new ProcessStartInfo($"https://www.roblox.com/games/{placeId}") { UseShellExecute = true });
                        return null;
                    }
                    arg = "--app";
                }
                else
                {
                    report($"Signing in as {acc.DisplayName}...", -1f);
                    string ticket = await RobloxApi.GetAuthTicketAsync(acc.Cookie);
                    arg = RobloxApi.BuildLaunchUri(ticket, placeId);
                }
            }
            ct.ThrowIfCancellationRequested();

            report("Starting Roblox...", -1f);
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = versionDir, UseShellExecute = false };
            psi.ArgumentList.Add(arg);
            return Process.Start(psi);
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
            Shown += async (_, _) => await AutoUpdateCheckAsync(settings);
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
        readonly SpinningLogo logo = new();
        readonly Label status = new();
        readonly LoadingBar bar = new();
        readonly RoundButton cancel;
        readonly CancellationTokenSource cts = new();
        bool finished;

        public LoadingForm(GuideStrapContext context, AppSettings settings, List<Account> accounts, string? launchUri, string placeText)
        {
            this.context = context;
            this.settings = settings;
            this.accounts = accounts;
            this.launchUri = launchUri;
            this.placeText = placeText;
            Text = "GuideStrap";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(D(540), D(284));
            int W = ClientSize.Width;

            var acc = accounts.FirstOrDefault(a => a.UserId == settings.ActiveAccountId);
            var left = At(Lbl(L.F("Account: {0}", acc != null ? acc.Username : L.T("Roblox login")), 8.25f, Theme.SubText), D(12), D(10));
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
                // run the heavy work (download, unzip, file writes) off the UI thread so the animation stays smooth
                void SafeReport(string t, float p)
                {
                    if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(() => Report(t, p)));
                }
                var token = cts.Token;
                if (settings.MultiInstance) MultiInstance.Hold();
                var proc = await Task.Run(() => RobloxLauncher.LaunchAsync(settings, accounts, launchUri, placeText, SafeReport, token));
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
                    if (settings.RunInBackground || settings.MultiInstance)
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
            g.Clear(Parent?.BackColor ?? Theme.Bg);
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
            g.Clear(Parent?.BackColor ?? Theme.Bg);
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
            g.Clear(Parent?.BackColor ?? Theme.Bg);
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
            g.Clear(Parent?.BackColor ?? Theme.Bg);
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
    static class ModAssets
    {
        public static string? CursorDir(AppSettings s) => s.CursorPreset switch
        {
            "guidestrap" or "dot" => EnsurePreset(s.CursorPreset),
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
                        if (name == "dot")
                        {
                            using var outline = new SolidBrush(Color.FromArgb(220, 0, 0, 0));
                            g.FillEllipse(outline, 32 - 5f, 32 - 5f, 10, 10);
                            using var fill = new SolidBrush(Color.White);
                            g.FillEllipse(fill, 32 - 3.5f, 32 - 3.5f, 7, 7);
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
            g.Clear(Parent?.BackColor ?? Theme.Card);
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
        public static readonly (string Code, string Native)[] Languages = { ("en", "English"), ("es", "Español"), ("pt", "Português") };
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
            return c is "es" or "pt" ? c : "en";
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
            int col = lang == "es" ? 1 : 2;
            var d = new Dictionary<string, string>(Data.Length);
            foreach (var row in Data) d[row[0]] = row[col];
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
            new[] { "Text & Font", "Texto y fuente", "Texto e fonte" },
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
            new[] { "Used by the Purple theme for buttons, switches and highlights.", "Lo usa el tema Morado para botones, interruptores y resaltados.", "Usada pelo tema Roxo em botões, chaves e destaques." }
        };
    }

    // Install / reinstall / uninstall.
    // The installed copy (Start menu, desktop shortcut, Roblox links) never shows setup.
    // Running any other copy (like the downloaded .exe) shows setup, or Reinstall/Repair if already installed.
    static class Setup
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GuideStrap";
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

        public static void Install(string language, bool desktopShortcut, bool openLinks, bool repairRoblox)
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
            try { if (RunningInstalled && InstalledVersion() != AppInfo.Version) WriteEntry(); } catch { }
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

        public static void Uninstall()
        {
            var settings = AppSettings.Load();
            L.Lang = settings.Language.Length > 0 ? settings.Language : L.SystemLanguage();
            Theme.Apply(settings.ThemeName, settings.Accent);

            if (OtherCopiesRunning())
            {
                ThemedDialog.Prompt(null, "Close GuideStrap first", "GuideStrap is still running (check the tray near the clock). Close it, then try again.", "OK");
                return;
            }
            if (ThemedDialog.Prompt(null, "Uninstall GuideStrap?",
                    "This removes GuideStrap, its copy of Roblox, your settings, mods and saved logins. Normal Roblox stays installed.",
                    "Uninstall", "Cancel") != 0) return;

            try { if (LinkHandler.PointsTo(InstallExe)) LinkHandler.Restore(settings); } catch { }
            foreach (var link in new[] { StartMenuLink, DesktopLink })
                try { if (File.Exists(link)) File.Delete(link); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }

            ThemedDialog.Prompt(null, "GuideStrap", "GuideStrap was uninstalled.", "OK");
            // the folder (including this .exe) is deleted a moment after we exit
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{Paths.Base}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            });
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
        readonly ToggleSwitch desktopToggle = new(), linksToggle = new(), robloxToggle = new();
        readonly Label status = new();
        readonly RoundButton install, cancel;

        public SetupForm(GuideStrapContext context, bool repair)
        {
            this.context = context;
            this.repair = repair;
            string titleText = repair ? "Reinstall GuideStrap" : "GuideStrap Setup";
            Text = "GuideStrap Setup";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(D(520), D(repair ? 510 : 430));
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
            desktopToggle.Size = linksToggle.Size = robloxToggle.Size = new Size(D(40), D(20));
            desktopToggle.Checked = !repair || Setup.HasDesktopShortcut;
            linksToggle.Checked = !repair || LinkHandler.PointsTo(Setup.InstallExe);
            robloxToggle.Checked = false;

            Controls.Add(Row(y, "Language", null, langCombo)); y += D(60);
            Controls.Add(Row(y, "Desktop shortcut", "Add a GuideStrap icon to your desktop.", desktopToggle)); y += D(60);
            Controls.Add(Row(y, "Open Roblox links with GuideStrap", "Play buttons on roblox.com open GuideStrap.", linksToggle)); y += D(60);
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
            bool desktop = desktopToggle.Checked, links = linksToggle.Checked, fixRoblox = repair && robloxToggle.Checked;
            try
            {
                await Task.Run(() => Setup.Install(language, desktop, links, fixRoblox));
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
