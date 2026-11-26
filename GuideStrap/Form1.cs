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
        readonly AuroraPanel sidebar = new();
        readonly Panel pageHost = new(), bottomBar = new();
        // old tab names that now live inside another tab, and the section to scroll to
        readonly Dictionary<string, (string Page, Control Anchor)> merged = new();
        readonly NavList nav = new();
        readonly TextBox searchBox = new();
        readonly Dictionary<string, PagePanel> pages = new();
        readonly Card chip = new();
        readonly AvatarBox chipAvatar = new();
        readonly Label chipName = new(), chipSub = new();
        readonly Label statusIcon = new(), statusLabel = new();
        readonly ProgressLine progress = new();
        readonly PageTransition transition = new();
        readonly InAppToast toast = new();
        bool shownOnce;
        bool backdropOn;   // your picture is behind the whole window
        Bitmap? sidebarBackdrop, pagesBackdrop;
        Color PageBack => backdropOn ? Color.Transparent : Theme.Bg;

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

        // ---------- performance ----------
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

        // ---------- recent games, favorites, playtime ----------
        readonly FlowLayoutPanel recentPanel = new(), favPanel = new();
        Control favSection = null!;
        int recentVersion = -1, favVersion = -1, playVersion = -1;
        readonly Label playWeek = new(), playToday = new(), playTotal = new(), playNote = new();
        readonly Label[] playTop = { new(), new(), new() };
        readonly WeekBars weekBars = new();
        readonly ToggleSwitch friendJoinToggle = new(), sortShotsToggle = new();

        int CW => D(690);   // content width

        public Form1() : this("Home") { }

        public Form1(string startPage)
        {
            InitializeComponent();

            BuildShell();
            ApplySettingsBackdrop();
            BuildHomePage();
            BuildAccountsPage();
            BuildGraphicsPage();
            BuildFlagsPage();
            BuildModsPage();
            BuildFontPage();       // goes inside Mods
            BuildFriendsPage();
            BuildDiscordPage();
            BuildSettingsPage();
            BuildAboutPage();      // goes inside Settings
            BuildHelpPage();

            RefreshAccounts();
            RefreshGraphicsControls();
            RefreshFlagSummary();
            RefreshFontPreview();
            RefreshMods();
            RefreshHome();
            RefreshRecent();
            RefreshFavorites();
            RefreshPlaytime();
            ShowPage(startPage, animate: false);

            Load += async (_, _) => await LoadAvatarsAsync();
            Shown += (_, _) => shownOnce = true;
            // any click or key press means the page may look different: smooth scrolling makes a fresh page picture
            var input = new InputWatcher();
            Application.AddMessageFilter(input);
            FormClosed += (_, _) => Application.RemoveMessageFilter(input);
            Disposed += (_, _) => Application.RemoveMessageFilter(input);   // also when a ready-made window is thrown away unused
            FormClosed += (_, _) =>
            {
                sidebar.BackgroundImage = null;
                pageHost.BackgroundImage = null;
                sidebarBackdrop?.Dispose();
                pagesBackdrop?.Dispose();
                sidebarBackdrop = pagesBackdrop = null;
            };
            Activated += (_, _) =>
            {
                Presence.Screen("Editing settings", nav.TextOf(currentPage));
                if (recentVersion != RecentGames.Version) RefreshRecent();
                if (favVersion != Favorites.Version) RefreshFavorites();
                if (playVersion != Playtime.Version) RefreshPlaytime();
            };
        }

        // ---- opening fast from the main menu ----
        // Building this window (every page at once) takes a moment. The main menu builds it quietly once it's
        // sitting idle, so "Configure settings" and "Accounts" open right away. If your settings or accounts
        // were saved in between (tray menu, update check), the ready-made one is thrown away and a fresh one is built.
        static Form1? ready;
        static (DateTime, DateTime) readyStamp;

        static (DateTime, DateTime) SavedStamp()
        {
            try
            {
                return (File.GetLastWriteTimeUtc(Path.Combine(Paths.Base, "settings.json")),
                        File.GetLastWriteTimeUtc(Path.Combine(Paths.Base, "accounts.dat")));
            }
            catch { return (DateTime.MinValue, DateTime.MinValue); }
        }

        public static void Prepare()
        {
            if (ready != null && !ready.IsDisposed) return;
            try
            {
                ready = new Form1("Home");
                readyStamp = SavedStamp();   // after building, in case building it saved something itself
            }
            catch { ready = null; }
        }

        public static Form1 Take(string page)
        {
            var f = ready;
            ready = null;
            if (f != null && !f.IsDisposed && SavedStamp() == readyStamp)
            {
                f.ShowPage(page, animate: false);
                return f;
            }
            try { f?.Dispose(); } catch { }
            return new Form1(page);
        }

        public static void Discard()
        {
            var f = ready;
            ready = null;
            try { f?.Dispose(); } catch { }
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
            search.Location = new Point(D(12), D(8));   // even 8 px above and below the search box
            searchBox.TextChanged += (_, _) => nav.SetFilter(searchBox.Text);
            searchBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (nav.FirstVisibleKey is string k) ShowPage(k);
            };

            nav.ItemHeight = D(34);
            nav.Gap = D(2);
            nav.HeaderHeight = D(26);
            nav.Add("Home", "\uE768", "Play", "launch join game home start favorites favourite pin server list ping playtime stats hours shortcut");
            nav.AddHeader("Game");
            nav.Add("Graphics", "\uE7F4", "Graphics", "fps quality performance texture grass sky preset lag blurry cap priority");
            nav.Add("Mods", "\uE790", "Mods", "mods death sound oof cursor mouse sky skybox crosshair shift lock footsteps jump pack setup gallery font fonts text letters typeface");
            nav.Add("Flags", "\uE7C1", "FastFlags", "fflag flag json fastflag fflags");
            nav.AddHeader("Social");
            nav.Add("Accounts", "\uE716", "Accounts", "account login switch alt user profile join my server");
            nav.Add("Friends", "\uE716", "Friends", "friends online join playing alerts");
            nav.Add("Discord", "\uE8F2", "Discord", "discord rich presence status join activity");
            nav.AddHeader("App");
            nav.Add("Settings", "\uE713", "Settings", "launcher close website folder reinstall crash helper streamer summary theme glass about info version backup export import screenshots pictures");
            nav.Add("Help", "\uE897", "Help", "help discord server bug report support question faq ban website guide");
            // same left and right edges as the search box, Main menu and account card above and below it
            nav.Location = new Point(D(12), D(46));
            nav.Size = new Size(sw - D(24), nav.ContentHeight);
            nav.Navigate += key => ShowPage(key);

            chip.Size = new Size(sw - D(24), D(50));
            chip.Location = new Point(D(12), sh - D(62));
            chip.Cursor = Cursors.Hand;
            chipAvatar.Size = new Size(D(30), D(30));
            chipAvatar.Location = new Point(D(10), D(10));
            chipName.AutoSize = false;
            chipName.Size = new Size(chip.Width - D(60), D(18));
            chipName.Location = new Point(D(48), D(7));
            chipName.Font = Theme.F("Segoe UI Semibold", 9f);
            chipName.ForeColor = Theme.Text;
            chipName.BackColor = Color.Transparent;
            chipName.AutoEllipsis = true;
            chipSub.AutoSize = false;
            chipSub.Size = new Size(chip.Width - D(60), D(16));
            chipSub.Location = new Point(D(48), D(25));
            chipSub.Font = Theme.F("Segoe UI", 8f);
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
            menuBtn.BaseColor = Color.Transparent;
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

            // icon and message share one 22 px row, centered on the same line as the buttons on the right
            statusIcon.AutoSize = false;
            statusIcon.Size = new Size(D(22), D(22));
            statusIcon.TextAlign = ContentAlignment.MiddleCenter;
            statusIcon.Font = new Font(Theme.IconFont, 10f);
            statusIcon.BackColor = Color.Transparent;
            statusIcon.Location = new Point(D(18), D(17));
            statusLabel.AutoSize = false;
            statusLabel.Size = new Size(W - D(380), D(22));
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Location = new Point(D(44), D(17));
            statusLabel.Font = Theme.F("Segoe UI", 9f);
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

            Controls.Add(toast);
        }

        // Your custom picture behind the whole window: top bar, sidebar and pages (Settings > Appearance)
        void ApplySettingsBackdrop()
        {
            if (!settings.BackgroundInSettings) return;
            var full = ApplyBackdrop(settings.LauncherBackground, 190);
            if (full == null) return;
            // the sidebar and the pages start below the top bar, so each gets its own cut-out of the same picture
            int top = D(36), h = ClientSize.Height - D(36) - D(56), sw = sidebar.Width;
            if (h <= 0 || ClientSize.Width - sw <= 0) return;
            sidebarBackdrop = Cut(full, 0, top, sw, h);
            pagesBackdrop = Cut(full, sw, top, ClientSize.Width - sw, h);
            sidebar.BackgroundImageLayout = ImageLayout.None;
            sidebar.BackgroundImage = sidebarBackdrop;
            pageHost.BackgroundImageLayout = ImageLayout.None;
            pageHost.BackgroundImage = pagesBackdrop;
            backdropOn = true;

            static Bitmap Cut(Bitmap src, int cx, int cy, int cw, int ch)
            {
                var b = new Bitmap(cw, ch, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var cg = Graphics.FromImage(b);
                cg.DrawImageUnscaled(src, -cx, -cy);
                return b;
            }
        }

        // Rebuilds this window so a new theme or color shows right away (no restart)
        void ReloadWindow(string section)
        {
            var ctx = GuideStrapContext.Current;
            if (busy || ctx == null)
            {
                if (Ask("Restart to apply", "The change shows after GuideStrap restarts.", "Restart now", "Later") == 0)
                    Application.Restart();
                return;
            }
            Theme.Apply(settings.ThemeName, settings.Accent, settings.SeeThrough);
            var fresh = new Form1(section) { StartPosition = FormStartPosition.Manual, Location = Location };
            ctx.Open(fresh);
            Close();
        }

        FlowLayoutPanel NewPage(string key, string title, string subtitle)
        {
            // The page sits in a "view" panel and is made wider than it, so Windows' own (solid, dark)
            // scrollbar is pushed out of sight. A thin see-through scrollbar is drawn on top instead.
            int viewW = ClientSize.Width - sidebar.Width - pageHost.Padding.Horizontal;
            int viewH = ClientSize.Height - D(36) - D(56);
            var view = new Panel { Size = new Size(viewW, viewH), BackColor = PageBack, Visible = false };
            var page = new PagePanel
            {
                Backdrop = backdropOn,
                Location = Point.Empty,
                Size = new Size(viewW + SystemInformation.VerticalScrollBarWidth + D(4), viewH),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = PageBack,   // see-through when your picture is behind the window
                Padding = new Padding(0, 0, 0, D(28)),
            };
            var bar = new OverlayScrollBar { Dock = DockStyle.Right, Width = D(12) };
            bar.Attach(page);
            // the picture that glides while you scroll: above the page, under the scrollbar
            var proxy = new ScrollProxy { Dock = DockStyle.Fill, Page = page };
            page.Proxy = proxy;
            page.RenderContent = () => RenderPageContent(page);
            view.Controls.Add(page);
            view.Controls.Add(proxy);
            view.Controls.Add(bar);
            proxy.BringToFront();
            bar.BringToFront();
            var t = Lbl(title, 16f, semibold: true);
            t.Margin = new Padding(0, D(2), 0, 0);
            var s = Lbl(subtitle, 9.25f, Theme.SubText);
            s.Margin = new Padding(D(2), D(1), 0, D(14));
            page.Controls.Add(t);
            page.Controls.Add(s);
            Theme.DarkScroll(page);
            view.Dock = DockStyle.Fill;
            // Keep the page exactly as tall as what's visible (and a bit wider, to hide Windows' scrollbar).
            // Done by hand: pinning it to the edges goes wrong while the window is still being built,
            // which left some pages taller than the window so their bottom part couldn't be scrolled to.
            void Fit()
            {
                if (view.ClientSize.Width <= 0 || view.ClientSize.Height <= 0) return;
                var want = new Rectangle(0, 0, view.ClientSize.Width + SystemInformation.VerticalScrollBarWidth + D(4), view.ClientSize.Height);
                if (page.Bounds != want) page.Bounds = want;
            }
            view.Resize += (_, _) => Fit();
            pageHost.Controls.Add(view);
            Fit();
            pages[key] = page;
            return page;
        }

        // a wrapping note exactly as tall as its text, so longer languages get another line instead of being cut off
        void FitNote(Label l, int width, int minHeight)
        {
            l.AutoSize = false;
            var size = TextRenderer.MeasureText(l.Text, l.Font, new Size(width - D(6), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            l.Size = new Size(width, Math.Max(minHeight, size.Height + D(4)));
        }

        T Add<T>(FlowLayoutPanel page, T c, int bottom = 6) where T : Control
        {
            c.Margin = new Padding(0, 0, 0, D(bottom));
            page.Controls.Add(c);
            return c;
        }

        static readonly Dictionary<string, string> SectionIcons = new()
        {
            ["Recent games"] = "\uE81C",
            ["Favorites"] = "\uE735",
            ["Playtime"] = "\uE916",
            ["Backup"] = "\uE777",
            ["Performance"] = "\uE9D9",
            ["Recommended"] = "\uE734",
            ["Setups"] = "\uE74E",
            ["Text fonts"] = "\uE8D2",
            ["Your own mods"] = "\uE8B7",
            ["Active flags"] = "\uE7C1",
            ["What to show"] = "\uE7B3",
            ["Behavior"] = "\uE713",
            ["Appearance"] = "\uE790",
            ["Profiles"] = "\uE77B",
            ["Tools"] = "\uE90F",
            ["About"] = "\uE946",
            ["Community"] = "\uE8F2",
            ["Common questions"] = "\uE897",
        };

        Control Section(FlowLayoutPanel page, string text)
        {
            var title = new SectionTitle
            {
                Text = L.T(text),
                Glyph = SectionIcons.TryGetValue(text, out var icon) ? icon : "",
                Size = new Size(CW, D(24)),
                Margin = new Padding(D(2), D(12), 0, D(6)),
            };
            page.Controls.Add(title);
            return title;
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
            // the text stops before the control on the right (longer languages end with "..." instead of running under it)
            int textW = width - D(16) - control.Width - D(32);
            var t = At(Lbl(title, 9.75f), D(16), ry + D(9));
            var d = At(Lbl(desc, 8.5f, Theme.SubText), D(16), ry + D(29));
            foreach (var l in new[] { t, d })
            {
                l.AutoSize = false;
                l.AutoEllipsis = true;
                l.Size = new Size(textW, l.PreferredHeight);
            }
            parent.Controls.Add(t);
            parent.Controls.Add(d);
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
            // "Font" and "About" are sections inside Mods and Settings now
            if (merged.TryGetValue(key, out var m))
            {
                ShowPage(m.Page, animate: false);
                if (pages.TryGetValue(m.Page, out var host)) ScrollToSection(host, m.Anchor);
                return;
            }
            if (key == currentPage || !pages.TryGetValue(key, out var next)) return;
            pages.TryGetValue(currentPage, out var prev);
            int dir = nav.IndexOf(key) >= nav.IndexOf(currentPage) ? 1 : -1;   // down the list = slide up
            currentPage = key;
            nav.Select(key, animate);
            if (key == "Home") { RefreshHome(); RefreshPlaytime(); }
            if (key == "Friends" && DateTime.Now - friendsLoaded > TimeSpan.FromSeconds(30)) _ = RefreshFriendsAsync();
            if (key == "Graphics" && !settings.RecommendAsked) BeginInvoke(new Action(() => OfferRecommended(firstVisit: true)));
            if (ContainsFocus) Presence.Screen("Editing settings", nav.TextOf(key));

            bool canAnimate = animate && prev != null && prev.Visible && IsHandleCreated && pageHost.Width > 0 && pageHost.Height > 0;
            if (!canAnimate)
            {
                foreach (var (k, p) in pages) p.Parent!.Visible = k == key;
                return;
            }

            // Snapshot old page, cover with it, swap pages underneath, snapshot new page, then slide both.
            // Moving two bitmaps is far cheaper than moving every control.
            var from = SnapPage(prev!);
            transition.Cover(from);
            foreach (var (k, p) in pages) p.Parent!.Visible = k == key;
            next.ScrollTo(0, smooth: false);
            var to = SnapPage(next);
            transition.Slide(to, dir);
        }

        // A picture of a page (what's visible of it) for the slide between tabs
        Bitmap SnapPage(PagePanel p)
        {
            var view = p.Parent!;
            if (!backdropOn) return PageTransition.Snapshot(p);

            // With your picture behind the window the page is see-through, and Windows' snapshot can't
            // include the picture. So: picture first, then each piece of the page drawn on top.
            var bmp = new Bitmap(Math.Max(1, view.Width), Math.Max(1, view.Height), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            if (pagesBackdrop != null) g.DrawImageUnscaled(pagesBackdrop, -view.Left, -view.Top);
            else g.Clear(Theme.Bg);
            DrawChildren(g, p, Point.Empty, new Rectangle(Point.Empty, view.Size));
            return bmp;
        }

        static void DrawChildren(Graphics g, Control parent, Point origin, Rectangle view)
        {
            for (int i = parent.Controls.Count - 1; i >= 0; i--)   // bottom-most first
            {
                var c = parent.Controls[i];
                if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                var r = new Rectangle(origin.X + c.Left, origin.Y + c.Top, c.Width, c.Height);
                if (!r.IntersectsWith(view)) continue;
                if (c is Label l && l.BackColor.A < 255) { DrawLabel(g, l, r); continue; }
                if (c is Panel && c is not Card && c.BackColor.A < 255) { DrawChildren(g, c, r.Location, view); continue; }
                try
                {
                    using var b = new Bitmap(c.Width, c.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    c.DrawToBitmap(b, new Rectangle(Point.Empty, c.Size));
                    g.DrawImageUnscaled(b, r.X, r.Y);
                }
                catch { }
            }
        }

        // One picture of a whole page (top to bottom) for smooth scrolling. With your background picture behind
        // the window it's see-through, so the background can stay still while the page glides over it.
        Bitmap? RenderPageContent(PagePanel p)
        {
            var view = p.Parent;
            if (view == null || !p.Visible || view.Width <= 0) return null;
            int h = Math.Min(Math.Max(view.Height, p.DisplayRectangle.Height), 12000);
            var bmp = new Bitmap(view.Width, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                if (!backdropOn) g.Clear(Theme.Bg);
                // children's positions are where they are on screen now; shift them to page-top coordinates
                DrawContent(g, p, new Point(0, p.Offset), backdropOn);
            }
            return bmp;
        }

        static void DrawContent(Graphics g, Control parent, Point origin, bool seeThrough)
        {
            for (int i = parent.Controls.Count - 1; i >= 0; i--)   // bottom-most first
            {
                var c = parent.Controls[i];
                if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                var r = new Rectangle(origin.X + c.Left, origin.Y + c.Top, c.Width, c.Height);
                if (c is Label l && l.BackColor.A < 255) { if (seeThrough) DrawLabelPlus(g, l, r); else DrawLabel(g, l, r); continue; }
                if (c is Panel && c is not Card && c.BackColor.A < 255) { DrawContent(g, c, r.Location, seeThrough); continue; }
                if (seeThrough && c is SectionTitle st) { DrawSectionPlus(g, st, r); continue; }
                if (seeThrough && c is GameTile gt) { DrawTilePlus(g, gt, r); continue; }
                try
                {
                    using var b = new Bitmap(c.Width, c.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    c.DrawToBitmap(b, new Rectangle(Point.Empty, c.Size));
                    g.DrawImageUnscaled(b, r.X, r.Y);
                }
                catch { }
            }
        }

        // Text drawn so it works on a see-through picture (Windows' normal text drawing can't)
        static StringFormat FormatFor(ContentAlignment a, bool ellipsis)
        {
            var f = new StringFormat(StringFormat.GenericTypographic);
            f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            f.Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None;
            f.Alignment = a switch
            {
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => StringAlignment.Far,
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => StringAlignment.Center,
                _ => StringAlignment.Near,
            };
            f.LineAlignment = a switch
            {
                ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => StringAlignment.Center,
                ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => StringAlignment.Far,
                _ => StringAlignment.Near,
            };
            return f;
        }

        static void DrawLabelPlus(Graphics g, Label l, Rectangle r)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var f = FormatFor(l.TextAlign, l.AutoEllipsis);
            using var b = new SolidBrush(l.ForeColor);
            // labels draw their text a pixel in from the edge
            var rr = new RectangleF(r.X + 1, r.Y, r.Width + 2, r.Height + 2);
            g.DrawString(l.Text, l.Font, b, rr, f);
        }

        static void DrawSectionPlus(Graphics g, SectionTitle st, Rectangle r)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var f = FormatFor(ContentAlignment.MiddleLeft, true);
            int x = r.X;
            if (st.Glyph.Length > 0)
            {
                using var ib = new SolidBrush(Theme.AccentLight);
                g.DrawString(st.Glyph, Theme.F(Theme.IconFont, 10f), ib, new RectangleF(x, r.Y, st.LogicalToDeviceUnits(22), r.Height), f);
                x += st.LogicalToDeviceUnits(24);
            }
            using var tb = new SolidBrush(Theme.Text);
            g.DrawString(st.Text, st.Font, tb, new RectangleF(x, r.Y, r.Right - x, r.Height), f);
        }

        static void DrawTilePlus(Graphics g, GameTile t, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            int pad = t.LogicalToDeviceUnits(6);
            float rad = t.LogicalToDeviceUnits(5);
            var icon = new RectangleF(r.X + pad, r.Y + pad, r.Width - pad * 2, r.Width - pad * 2);
            using (var path = Theme.Round(icon, rad))
            {
                if (t.Image != null)
                {
                    var state = g.Save();
                    g.SetClip(path);
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(t.Image, icon);
                    g.Restore(state);
                }
                else
                {
                    using var pb = new SolidBrush(Theme.ButtonBg);
                    g.FillPath(pb, path);
                }
            }
            var text = new RectangleF(r.X + pad, icon.Bottom + t.LogicalToDeviceUnits(4), r.Width - pad * 2, r.Bottom - icon.Bottom - t.LogicalToDeviceUnits(6));
            using var f = FormatFor(ContentAlignment.TopCenter, true);
            using var b = new SolidBrush(Theme.Text);
            g.DrawString(t.Text, t.Font, b, text, f);
        }


        static void DrawLabel(Graphics g, Label l, Rectangle r)
        {
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            if (l.AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
            flags |= l.TextAlign switch
            {
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => TextFormatFlags.HorizontalCenter,
                _ => TextFormatFlags.Left,
            };
            flags |= l.TextAlign switch
            {
                ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter,
                ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => TextFormatFlags.Bottom,
                _ => TextFormatFlags.Top,
            };
            TextRenderer.DrawText(g, l.Text, l.Font, r, l.ForeColor, flags);
        }

        // puts a section title at the top of its page (waits until the window is on screen if needed)
        void ScrollToSection(FlowLayoutPanel host, Control anchor)
        {
            void DoScroll()
            {
                if (host.IsDisposed || anchor.IsDisposed) return;
                int y = Math.Max(0, anchor.Top - host.AutoScrollPosition.Y - D(4));
                if (host is PagePanel pp) pp.ScrollTo(y, smooth: false);
                else host.AutoScrollPosition = new Point(0, y);
            }
            if (IsHandleCreated) { BeginInvoke(new Action(DoScroll)); return; }
            EventHandler? once = null;
            once = (_, _) => { Shown -= once; DoScroll(); };
            Shown += once;
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
            // a little pop-up for things you just did (not for "working on it" messages)
            if (shownOnce && kind != StatusKind.Busy && !IsDisposed)
                toast.ShowMessage(statusLabel.Text, kind == StatusKind.Error,
                    new Point(ClientSize.Width - D(18), ClientSize.Height - D(56) - D(14)), D(440));
        }

        Account? ActiveAccount => accounts.FirstOrDefault(a => a.UserId == settings.ActiveAccountId);
        static string Initial(string s) => s.Length > 0 ? s[..1].ToUpperInvariant() : "?";
    }

    sealed class InputWatcher : IMessageFilter
    {
        public bool PreFilterMessage(ref Message m)
        {
            // mouse button down (left, right, middle) or key down
            if (m.Msg is 0x0201 or 0x0204 or 0x0207 or 0x0100 or 0x0104) PagePanel.InputEpoch++;
            return false;
        }
    }
}
