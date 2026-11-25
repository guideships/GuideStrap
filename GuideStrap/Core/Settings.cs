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
    // DATA: settings, accounts, paths, encryption
    static class Paths
    {
        public static readonly string Base = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuideStrap");
        public static readonly string Versions = Path.Combine(Base, "Versions");
        public static readonly string Avatars = Path.Combine(Base, "Avatars");
        public static readonly string Icons = Path.Combine(Base, "GameIcons");
        public static readonly string Mods = Path.Combine(Base, "Mods");
        public static readonly string Managed = Path.Combine(Base, "ManagedMods");
        public static readonly string Sky = Path.Combine(Managed, "sky");
        public static readonly string SoundPacks = Path.Combine(Base, "SoundPacks");
        public static readonly string SkipModsMarker = Path.Combine(Base, "skip-mods-once");
        public static readonly string Downloads = Path.Combine(Base, "Downloads");
    }

    class AppSettings
    {
        public int Preset { get; set; } = 2;     // 0-6 = preset (see Form1.PresetOrder), -1 = custom
        public Dictionary<string, string> Flags { get; set; } = new();
        public string? FontFile { get; set; }
        public string? FontDisplayName { get; set; }
        public long? ActiveAccountId { get; set; }
        public bool UseAccountForWebsite { get; set; } = true;
        public bool CloseAfterLaunch { get; set; } = true;
        public bool DiscordPresence { get; set; }
        public bool DiscordShowGame { get; set; } = true;
        public bool DiscordShowAccount { get; set; }
        public bool DiscordAllowJoin { get; set; }
        public bool StreamerMode { get; set; }
        public bool FriendAlerts { get; set; } = true;
        public bool FriendJoinAlerts { get; set; } = true;      // popup when a friend joins the server you're in
        public bool SortScreenshots { get; set; }                // move Roblox screenshots into a folder per game
        public bool SessionSummary { get; set; } = true;
        public Dictionary<string, string> GameSetups { get; set; } = new();
        public int SettingsVersion { get; set; }
        public bool RunInBackground { get; set; } = true;
        public bool ShowServerLocation { get; set; } = true;
        public bool MultiInstance { get; set; }
        public bool CrashHelper { get; set; } = true;
        public bool RecommendAsked { get; set; }
        public string? DeathSoundFile { get; set; }
        public string CursorPreset { get; set; } = "default";
        public string CrosshairPreset { get; set; } = "default";
        public string FootstepSound { get; set; } = "default";
        public string JumpSound { get; set; } = "default";
        public string? SoundPack { get; set; }
        public int FpsCap { get; set; }
        public int Priority { get; set; }
        public Dictionary<string, SavedSetup> Setups { get; set; } = new();
        public string? LastSetup { get; set; }
        public bool CustomSky { get; set; }
        public string Accent { get; set; } = "Grey";
        public string ThemeName { get; set; } = "Black";
        public bool SeeThrough { get; set; } = true;            // Glass: slightly see-through windows
        public bool BackgroundOnLoader { get; set; } = true;    // your picture also on the Roblox loading screen
        public bool BackgroundInSettings { get; set; } = true;  // ...and behind the whole settings window
        public string? LauncherBackground { get; set; }
        public string LastSeenVersion { get; set; } = "";
        public string Language { get; set; } = "";
        public Dictionary<string, string> PreviousLinkHandlers { get; set; } = new();
        public DateTime LastUpdateCheck { get; set; }

        static string FilePath => Path.Combine(Paths.Base, "settings.json");

        public static AppSettings Load()
        {
            AppSettings s;
            try { s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
            catch { s = new(); }
            if (s.SettingsVersion < 2)
            {
                // 4.5: Discord status is now off until you turn it on in the Discord tab
                s.DiscordPresence = false;
                s.SettingsVersion = 2;
                try { s.Save(); } catch { }
            }
            if (s.SettingsVersion < 3)
            {
                // 4.8: accent colors now work on Black too. Black used to always be grey,
                // so keep it grey for people who never picked a color for it.
                if (s.ThemeName != "Purple" && s.Accent == "Purple") s.Accent = "Grey";
                s.SettingsVersion = 3;
                try { s.Save(); } catch { }
            }
            if (s.SettingsVersion < 4)
            {
                // 4.8: Glass is see-through and your background picture shows everywhere. Turn those on once.
                s.SeeThrough = true;
                s.BackgroundOnLoader = true;
                s.BackgroundInSettings = true;
                s.SettingsVersion = 4;
                try { s.Save(); } catch { }
            }
            return s;
        }

        public AppSettings Clone() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this)) ?? new();

        public void Save()
        {
            Directory.CreateDirectory(Paths.Base);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    class SavedSetup
    {
        public int Preset { get; set; } = 2;
        public Dictionary<string, string> Flags { get; set; } = new();
        public string CursorPreset { get; set; } = "default";
        public string CrosshairPreset { get; set; } = "default";
        public string? DeathSoundFile { get; set; }
        public string FootstepSound { get; set; } = "default";
        public string JumpSound { get; set; } = "default";
        public string? SoundPack { get; set; }
        public bool CustomSky { get; set; }
        public int FpsCap { get; set; }
        public int Priority { get; set; }

        public void ApplyTo(AppSettings s)
        {
            s.Preset = Preset;
            s.Flags = Flags.Where(kv => FF.IsAllowed(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            s.CursorPreset = CursorPreset;
            s.CrosshairPreset = CrosshairPreset;
            s.DeathSoundFile = DeathSoundFile;
            s.FootstepSound = FootstepSound;
            s.JumpSound = JumpSound;
            s.SoundPack = SoundPack;
            s.CustomSky = CustomSky;
            s.FpsCap = FpsCap;
            s.Priority = Priority;
        }
    }

    class Profile
    {
        public int Preset { get; set; } = 2;
        public Dictionary<string, string> Flags { get; set; } = new();
        public string CursorPreset { get; set; } = "default";
        public string Accent { get; set; } = "Grey";
    }
}
