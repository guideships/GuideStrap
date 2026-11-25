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
    // APP INFO + ABOUT TEXT
    static class AppInfo
    {
        public const string Version = "5.0";
        // GitHub repo for self-updates, e.g. "yourname/GuideStrap". Leave empty until you publish releases.
        public const string UpdateRepo = "guideships/GuideStrap";
        public const string DiscordAppId = "1574254036398375062";
        public const string SiteUrl = "https://guideships.github.io/GuideStrap";
        public const string DiscordUrl = "https://discord.gg/ErEXCXvNf7";
        // Discord loads the status picture from this link, so it works without uploading art in the Discord portal
        // Shown once after GuideStrap updates itself
        public const string WhatsNew =
            "\u2022 Server list: see a game's servers with their ping and join a fast one\n" +
            "\u2022 Favorites and desktop shortcuts for your games (right-click a game)\n" +
            "\u2022 Playtime on the Play tab: how long you played this week\n" +
            "\u2022 A popup when a friend joins your server\n" +
            "\u2022 Join my server: send another saved account into your server (Accounts)\n" +
            "\u2022 Full backup of your settings and mods (Settings > Backup)\n" +
            "\u2022 Sort Roblox screenshots into a folder for each game (Settings > Tools)\n" +
            "\u2022 Two new graphics presets: Smooth and Sharp\n" +
            "\u2022 The server location popup no longer repeats\n" +
            "\u2022 Right-click no longer presses buttons, and long button text fits in every language\n" +
            "\u2022 Dropdowns no longer flash white when you switch tabs or scroll\n" +
            "\u2022 Safer updates, custom fonts work with two Roblox windows open, and Launch Roblox always uses the account you picked";

        public const string LogoUrl = "https://raw.githubusercontent.com/guideships/GuideStrap/master/docs/logo-black.png";   // new name so Discord doesn't show its cached old picture
        public const string GalleryUrl = "https://raw.githubusercontent.com/guideships/GuideStrap/master/gallery/index.json";

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
            5 => "Smooth",
            6 => "Sharp",
            _ => "Custom",
        };
    }
}
