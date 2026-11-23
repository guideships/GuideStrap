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
    }
}
