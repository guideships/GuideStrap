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
        // HELP: Discord server, reporting bugs, website and common questions
        void BuildHelpPage()
        {
            var page = NewPage("Help", "Help", "Get help, report bugs and find answers.");

            Section(page, "Community");
            var join = Secondary("Join", "", D(110), D(30));
            join.Click += (_, _) => OpenLink(AppInfo.DiscordUrl);
            Add(page, SettingCard("GuideStrap Discord", "Ask questions, share ideas and hear about updates first.", join));

            var report = Secondary("Report", "", D(110), D(30));
            report.Click += (_, _) => OpenLink(AppInfo.DiscordUrl);
            Add(page, SettingCard("Report a bug", "Open a ticket in #bug-reports on our Discord. A screenshot helps a lot.", report));

            var site = Secondary("Open", "", D(110), D(30));
            site.Click += (_, _) => OpenLink(AppInfo.SiteUrl);
            Add(page, SettingCard("Website", "Every feature, downloads and news.", site), 8);

            Section(page, "Common questions");
            Add(page, SettingCard("Can I get banned for using GuideStrap?",
                "No. It only uses settings from Roblox's own allowlist, which Roblox reads when it starts. It never touches the game while it's running.", null));
            Add(page, SettingCard("Does it change my normal Roblox?",
                "No. GuideStrap uses its own copy of Roblox, so normal Roblox stays exactly as it is.", null));
            Add(page, SettingCard("My changes don't show in game",
                "Close Roblox fully, then launch it again from GuideStrap. Changes apply when Roblox starts.", null));
            Add(page, SettingCard("Roblox won't open or keeps crashing",
                "Try Settings > Tools > Repair Roblox. If it still crashes, the crash helper shows what went wrong and how to fix it.", null));
            Add(page, SettingCard("How do I uninstall?",
                "Settings > Tools > Uninstall GuideStrap, or from Windows' Installed apps list.", null), 0);
        }

        void OpenLink(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { SetStatus("Couldn't open your browser.", null, StatusKind.Error); }
        }
    }
}
