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
        public const string Version = "5.1";
        // GitHub repo for self-updates, e.g. "yourname/GuideStrap". Leave empty until you publish releases.
        public const string UpdateRepo = "guideships/GuideStrap";
        public const string DiscordAppId = "1574254036398375062";
        public const string SiteUrl = "https://guideships.github.io/GuideStrap";
        public const string DiscordUrl = "https://discord.gg/ErEXCXvNf7";
        // Discord loads the status picture from this link, so it works without uploading art in the Discord portal
        // Shown once after GuideStrap updates itself
        public const string WhatsNew =
            "\u2022 A cleaner look: Launch Roblox and Play Roblox stand out, and buttons, icons and text line up better\n" +
            "\u2022 Play Roblox no longer looks greyed out with the grey accent color or the Glass theme\n" +
            "\u2022 Settings opens faster from the main menu\n" +
            "\u2022 Rounded dropdowns that match the rest of GuideStrap\n" +
            "\u2022 The Glass theme is more solid, so your background doesn't distract\n" +
            "\u2022 Clearer hover and selected looks on switches, sliders, dropdowns and the sidebar\n" +
            "\u2022 Keyboard support: Tab, arrow keys, Enter and Space work on buttons, switches, sliders and the sidebar\n" +
            "\u2022 Recent game names and longer translations no longer get cut off\n" +
            "\u2022 Importing a profile with a new accent color applies it right away, no restart";

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
