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
    // A game's public servers with their ping, so you can pick a fast one
    class ServerListForm : ThemedForm
    {
        readonly long placeId;
        readonly Panel list;
        readonly Label status;
        readonly RoundButton refresh;
        CancellationTokenSource? cts;
        public string? ChosenServer { get; private set; }

        public ServerListForm(long placeId, string? gameName)
        {
            this.placeId = placeId;
            StartPosition = FormStartPosition.CenterParent;
            CloseOnEscape = true;
            Text = L.T("Server list");

            int w = D(560), pad = D(20);
            var bar = BuildTitleBar("Server list", false);
            int y = D(36) + D(8);

            var title = Lbl(string.IsNullOrWhiteSpace(gameName) ? $"Place {placeId}" : gameName, 13f, semibold: true);
            title.AutoSize = false;
            title.AutoEllipsis = true;
            title.Size = new Size(w - pad * 2, D(28));
            title.Location = new Point(pad, y);
            Controls.Add(title);
            y += D(32);

            status = Lbl("Loading servers...", 9f, Theme.SubText);
            status.AutoSize = false;
            status.AutoEllipsis = true;
            status.Size = new Size(w - pad * 2, D(20));
            status.Location = new Point(pad, y);
            Controls.Add(status);
            y += D(28);

            list = Theme.DarkScroll(new Panel { Location = new Point(pad, y), Size = new Size(w - pad * 2, D(380)), AutoScroll = true, BackColor = Theme.Bg });
            Controls.Add(list);
            y += list.Height + D(12);

            refresh = Secondary("Refresh", "", D(110), D(30));
            refresh.Location = new Point(pad, y);
            refresh.Click += async (_, _) => await LoadAsync();
            var close = Secondary("Close", "", D(96), D(30));
            close.Location = new Point(w - pad - close.Width, y);
            close.Click += (_, _) => Close();
            Controls.Add(refresh);
            Controls.Add(close);
            y += D(30) + pad;

            Controls.Add(bar);
            ClientSize = new Size(w, y);

            Shown += async (_, _) => await LoadAsync();
            FormClosed += (_, _) => cts?.Cancel();
        }

        async Task LoadAsync()
        {
            cts?.Cancel();
            var mine = new CancellationTokenSource();
            cts = mine;
            refresh.Enabled = false;
            status.Text = L.T("Loading servers...");
            List<ServerEntry> servers;
            try { servers = await RobloxApi.GetServersAsync(placeId, mine.Token); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                if (IsDisposed || mine.IsCancellationRequested) return;
                status.Text = L.F("Couldn't load servers. {0}", ex.Message);
                refresh.Enabled = true;
                return;
            }
            if (IsDisposed || mine.IsCancellationRequested) return;
            refresh.Enabled = true;

            // lowest ping first, then the fullest (more people to play with)
            var sorted = servers.OrderBy(sv => sv.Ping ?? int.MaxValue).ThenByDescending(sv => sv.Playing).Take(60).ToList();
            status.Text = sorted.Count == 0
                ? L.T("No open servers right now. Try again in a moment.")
                : L.F("{0} open servers, fastest first.", sorted.Count);

            list.SuspendLayout();
            foreach (var c in list.Controls.Cast<Control>().ToList()) c.Dispose();
            list.AutoScrollPosition = Point.Empty;
            int rowH = D(52), gap = D(6);
            bool scrolls = sorted.Count * (rowH + gap) > list.Height;
            int rowW = list.Width - (scrolls ? SystemInformation.VerticalScrollBarWidth + D(4) : 0);
            int ry = 0;
            foreach (var sv in sorted)
            {
                var row = Row(sv, rowW, rowH);
                row.Location = new Point(0, ry);
                list.Controls.Add(row);
                ry += rowH + gap;
            }
            list.ResumeLayout();
        }

        Card Row(ServerEntry sv, int width, int height)
        {
            var card = new Card { Size = new Size(width, height) };
            string players = sv.MaxPlayers > 0 ? L.F("{0} / {1} players", sv.Playing, sv.MaxPlayers) : L.F("{0} players", sv.Playing);
            var top = Lbl(players, 9.75f, semibold: true);
            top.Location = new Point(D(14), D(8));
            string ping = sv.Ping != null ? L.F("{0} ms ping", sv.Ping.Value) : L.T("Ping unknown");
            string fps = sv.Fps != null ? L.F("{0} FPS", Math.Round(sv.Fps.Value)) : "";
            var sub = Lbl(fps.Length > 0 ? ping + "  ·  " + fps : ping, 8.5f, PingColor(sv.Ping));
            sub.Location = new Point(D(15), D(29));
            var join = Primary("Join", "", D(96), D(30));
            join.Location = new Point(width - D(12) - join.Width, (height - join.Height) / 2);
            join.Click += (_, _) =>
            {
                ChosenServer = sv.Id;
                DialogResult = DialogResult.OK;
            };
            card.Controls.AddRange(new Control[] { top, sub, join });
            return card;
        }

        static Color PingColor(int? ping) =>
            ping == null ? Theme.SubText : ping < 90 ? Theme.Success : ping < 160 ? Theme.Gold : Theme.Danger;
    }
}
