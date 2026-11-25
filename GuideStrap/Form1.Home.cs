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
    public partial class Form1
    {
        // HOME / PLAY
        void BuildHomePage()
        {
            var page = NewPage("Home", "Play", "Start Roblox with your settings.");

            var hero = new Card { Size = new Size(CW, D(100)) };
            homeAvatar.Size = new Size(D(60), D(60));
            homeAvatar.Location = new Point(D(18), D(20));
            homeAvatar.Ring = Theme.Gold;
            homeGreeting.Font = Theme.F("Segoe UI Semibold", 14f);
            homeGreeting.ForeColor = Theme.Text;
            homeGreeting.BackColor = Color.Transparent;
            homeGreeting.AutoSize = true;
            homeGreeting.Location = new Point(D(92), D(22));
            homeSub.Font = Theme.F("Segoe UI", 9f);
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
            playButton.Font = Theme.F("Segoe UI Semibold", 11f);
            playButton.Size = new Size(CW, D(42));
            playButton.Radius = 4;
            playButton.Click += (_, _) => StartLaunch();
            Add(page, playButton, 8);

            var serverRow = new Panel { Size = new Size(CW, D(32)), BackColor = PageBack };
            int quarter = (CW - D(18)) / 4;
            var rejoinBtn = Secondary("Rejoin last server", "\uE72C", quarter, D(32));
            var hopBtn = Secondary("Server hop", "\uE8AB", quarter, D(32));
            var smallBtn = Secondary("Small server", "\uE716", quarter, D(32));
            var listBtn = Secondary("Server list", "\uE8FD", quarter, D(32));
            rejoinBtn.Location = new Point(0, 0);
            hopBtn.Location = new Point(quarter + D(6), 0);
            smallBtn.Location = new Point((quarter + D(6)) * 2, 0);
            listBtn.Location = new Point((quarter + D(6)) * 3, 0);
            rejoinBtn.Click += (_, _) => StartServerLaunch(ServerPick.None, rejoin: true);
            hopBtn.Click += (_, _) => StartServerLaunch(ServerPick.Hop, rejoin: false);
            smallBtn.Click += (_, _) => StartServerLaunch(ServerPick.Small, rejoin: false);
            listBtn.Click += (_, _) => OpenServerListFromBox();
            serverRow.Controls.AddRange(new Control[] { rejoinBtn, hopBtn, smallBtn, listBtn });
            Add(page, serverRow, 8);

            statGraphics.AutoSize = false;
            statGraphics.Size = new Size(CW, D(20));
            statGraphics.Font = Theme.F("Segoe UI", 8.75f);
            statGraphics.ForeColor = Theme.SubText;
            statGraphics.Margin = new Padding(D(2), 0, 0, 0);
            page.Controls.Add(statGraphics);

            favSection = Section(page, "Favorites");
            favPanel.Size = new Size(CW, D(142));
            favPanel.FlowDirection = FlowDirection.LeftToRight;
            favPanel.WrapContents = true;
            favPanel.BackColor = PageBack;
            favPanel.Margin = new Padding(0);
            page.Controls.Add(favPanel);

            Section(page, "Recent games");
            recentPanel.Size = new Size(CW, D(140));
            recentPanel.FlowDirection = FlowDirection.LeftToRight;
            recentPanel.WrapContents = false;
            recentPanel.BackColor = PageBack;
            recentPanel.Margin = new Padding(0);
            page.Controls.Add(recentPanel);
            var tip = Lbl("Right-click a game to pin it, see its servers or make a desktop shortcut.", 8.5f, Theme.SubText);
            tip.AutoSize = false;
            tip.AutoEllipsis = true;
            tip.Size = new Size(CW, D(18));
            tip.Margin = new Padding(D(2), D(2), 0, 0);
            page.Controls.Add(tip);

            // --- playtime ---
            Section(page, "Playtime");
            var playCard = new Card { Size = new Size(CW, D(132)) };
            void Style(Label l, float size, Color color, int x, int y, int w, int h, bool semibold = false)
            {
                l.AutoSize = false;
                l.AutoEllipsis = true;
                l.UseMnemonic = false;
                l.Font = Theme.F(semibold ? "Segoe UI Semibold" : "Segoe UI", size);
                l.ForeColor = color;
                l.BackColor = Color.Transparent;
                l.Location = new Point(x, y);
                l.Size = new Size(w, h);
                playCard.Controls.Add(l);
            }
            playCard.Controls.Add(At(Lbl("This week", 8.5f, Theme.SubText), D(16), D(12)));
            Style(playWeek, 16f, Theme.Text, D(14), D(28), D(180), D(36), semibold: true);
            Style(playToday, 8.75f, Theme.SubText, D(16), D(72), D(180), D(20));
            Style(playTotal, 8.75f, Theme.SubText, D(16), D(94), D(180), D(20));
            weekBars.Location = new Point(D(206), D(12));
            weekBars.Size = new Size(D(220), D(108));
            playCard.Controls.Add(weekBars);
            int topX = D(446);
            playCard.Controls.Add(At(Lbl("Most played this week", 8.5f, Theme.SubText), topX, D(12)));
            for (int i = 0; i < playTop.Length; i++)
                Style(playTop[i], 9.25f, Theme.Text, topX, D(36) + i * D(26), CW - topX - D(16), D(22));
            Add(page, playCard, 4);
            playNote.Text = L.T("Playtime counts while GuideStrap stays in the tray (Settings > Stay in the tray while Roblox is open).");
            playNote.AutoSize = false;
            playNote.Size = new Size(CW, D(20));
            playNote.Font = Theme.F("Segoe UI", 8.5f);
            playNote.ForeColor = Theme.SubText;
            playNote.BackColor = Color.Transparent;
            playNote.AutoEllipsis = true;
            playNote.Margin = new Padding(D(2), 0, 0, 0);
            page.Controls.Add(playNote);
        }

        void RefreshPlaytime()
        {
            playVersion = Playtime.Version;
            var data = Playtime.Load();
            var days = Playtime.LastWeekByDay(data);
            System.Globalization.CultureInfo culture;
            try { culture = new System.Globalization.CultureInfo(L.Lang == "fil" ? "fil-PH" : L.Lang); }
            catch { culture = System.Globalization.CultureInfo.CurrentCulture; }
            var labels = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6).ToString("ddd", culture)).ToArray();
            weekBars.SetData(days, labels);
            playWeek.Text = Playtime.Format(days.Sum());
            playToday.Text = L.F("Today: {0}", Playtime.Format(days[6]));
            playTotal.Text = L.F("All time: {0}", Playtime.Format(data.Totals.Values.Sum()));
            var top = Playtime.TopThisWeek(data, playTop.Length);
            for (int i = 0; i < playTop.Length; i++)
                playTop[i].Text = i < top.Count ? $"{top[i].Name}  ·  {Playtime.Format(top[i].Seconds)}"
                    : i == 0 ? L.T("Nothing played this week yet.") : "";
            playNote.Visible = !settings.RunInBackground || data.Sessions.Count == 0;
        }

        void RefreshFavorites()
        {
            favVersion = Favorites.Version;
            var games = Favorites.Load();
            favPanel.SuspendLayout();
            foreach (var c in favPanel.Controls.Cast<Control>().ToList()) c.Dispose();
            foreach (var g in games) favPanel.Controls.Add(MakeTile(g));
            int rows = Math.Max(1, (games.Count + 5) / 6);
            favPanel.Size = new Size(CW, rows * D(142));
            favPanel.ResumeLayout();
            favSection.Visible = games.Count > 0;
            favPanel.Visible = games.Count > 0;
        }

        GameTile MakeTile(RecentGame g)
        {
            var tile = new GameTile { Text = g.Name, Size = new Size(D(108), D(136)), Margin = new Padding(0, 0, D(6), D(6)) };
            long id = g.PlaceId;
            tile.MouseClick += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                placeBox.Text = id.ToString();
                StartLaunch();
            };
            tile.ContextMenuStrip = TileMenu(tile, g);
            _ = LoadTileIconAsync(tile, g);
            return tile;
        }

        // right-click menu on a game: play, servers, favorite, desktop shortcut
        ContextMenuStrip TileMenu(GameTile tile, RecentGame g)
        {
            var menu = new ContextMenuStrip { ShowImageMargin = false, Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) };
            menu.ForeColor = Theme.Text;
            void Build()
            {
                menu.Items.Clear();
                void Item(string text, Action run)
                {
                    var item = new ToolStripMenuItem(L.T(text)) { ForeColor = Theme.Text };
                    item.Click += (_, _) => Later(run);
                    menu.Items.Add(item);
                }
                bool fav = Favorites.Contains(g.PlaceId);
                Item("Play", () => { placeBox.Text = g.PlaceId.ToString(); StartLaunch(); });
                Item("Server list", () => OpenServerList(g.PlaceId, g.Name));
                Item(fav ? "Remove from favorites" : "Add to favorites", () => ToggleFavorite(g, remove: fav));
                Item("Desktop shortcut", () => MakeShortcut(g, tile));
            }
            Build();
            menu.Opening += (_, e) => { Build(); e.Cancel = false; };
            tile.Disposed += (_, _) => menu.Dispose();
            return menu;
        }

        void ToggleFavorite(RecentGame g, bool remove)
        {
            if (remove)
            {
                Favorites.Remove(g.PlaceId);
                SetStatus(L.F("Removed {0} from favorites.", g.Name), null, StatusKind.Idle);
            }
            else if (Favorites.Add(g))
                SetStatus(L.F("Added {0} to favorites.", g.Name), null, StatusKind.Idle);
            else
            {
                SetStatus(L.F("You can pin up to {0} games. Remove one first.", Favorites.Max), null, StatusKind.Error);
                return;
            }
            RefreshFavorites();
        }

        void MakeShortcut(RecentGame g, GameTile tile)
        {
            try
            {
                Setup.CreateGameShortcut(g.Name, g.PlaceId, tile.IsDisposed ? null : tile.Image);
                SetStatus(L.F("Made a desktop shortcut for {0}.", g.Name), null, StatusKind.Idle);
            }
            catch (Exception ex) { Ask("Couldn't make the shortcut", ex.Message, "OK"); }
        }

        void OpenServerListFromBox()
        {
            string place = placeBox.Text.Trim();
            long? id = place.Length > 0 ? RobloxLauncher.ParsePlaceId(place) : LastServer.Load()?.PlaceId;
            if (id == null)
            {
                SetStatus(place.Length > 0 ? "That doesn't look like a game link or place ID." : "Paste a game link first, or join a game once.", null, StatusKind.Error);
                return;
            }
            long placeId = id.Value;
            string? name = Favorites.Load().Concat(RecentGames.Load()).FirstOrDefault(x => x.PlaceId == placeId)?.Name;
            OpenServerList(placeId, name);
        }

        void OpenServerList(long placeId, string? name)
        {
            string? server;
            using (var f = new ServerListForm(placeId, name))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                server = f.ChosenServer;
            }
            var ctx = GuideStrapContext.Current;
            if (server == null || ctx == null) return;
            ctx.Open(new LoadingForm(ctx, settings, accounts, null, placeId.ToString(), server));
            if (settings.CloseAfterLaunch) Close();
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
                var tile = MakeTile(g);
                tile.Margin = new Padding(0, 0, D(6), 0);
                recentPanel.Controls.Add(tile);
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
            value.Font = Theme.F("Segoe UI Semibold", 10.5f);
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
    }
}
