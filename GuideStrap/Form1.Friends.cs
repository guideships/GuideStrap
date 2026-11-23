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
    }
}
