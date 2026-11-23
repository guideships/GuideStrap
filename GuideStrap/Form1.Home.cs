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
    }
}
