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
        bool fpsSyncing;
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
            Theme.DarkScroll(page);
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
            // the description wraps before the control on the right instead of running under it
            var d = Lbl(desc, 8.5f, Theme.SubText);
            d.AutoSize = false;
            int textW = CW - D(32) - (control != null ? control.Width + D(16) : 0);
            var size = TextRenderer.MeasureText(d.Text, d.Font, new Size(textW, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            d.Size = new Size(textW, size.Height + D(2));
            d.Location = new Point(D(16), D(31));
            card.Controls.Add(d);
            card.Height = Math.Max(D(height), d.Bottom + D(10));
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
            if (key == "Graphics" && !settings.RecommendAsked) BeginInvoke(new Action(() => OfferRecommended(firstVisit: true)));
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
    }
}
