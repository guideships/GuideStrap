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
    }
}
