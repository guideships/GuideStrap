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
        void BuildSettingsPage()
        {
            var page = NewPage("Settings", "Settings", "Launcher options.");

            ToggleSwitch T(ToggleSwitch t, bool value, Action<bool> changed)
            {
                t.Size = new Size(D(40), D(20));
                t.Checked = value;
                t.CheckedChanged += (_, _) => changed(t.Checked);
                return t;
            }

            Section(page, "Behavior");
            Add(page, SettingCard("Use my selected account for website joins", "Play buttons on roblox.com join as the account picked here.",
                T(websiteAccountToggle, settings.UseAccountForWebsite, v => { settings.UseAccountForWebsite = v; settings.Save(); })));
            Add(page, SettingCard("Close this window when launching", "Only the loading screen stays open while Roblox starts.",
                T(closeAfterToggle, settings.CloseAfterLaunch, v => { settings.CloseAfterLaunch = v; settings.Save(); })));
            Add(page, SettingCard("Stay in the tray while Roblox is open", "Needed for Discord status, server location and recent games.",
                T(backgroundToggle, settings.RunInBackground, v => { settings.RunInBackground = v; settings.Save(); })));
            var crashTest = Secondary("Preview", "\uE890", D(100), D(30));
            crashTest.Click += (_, _) =>
            {
                using var f = new CrashForm(new CrashInfo { Code = 0xC0000005 });
                f.ShowDialog(this);
            };
            var crashRight = new Panel { Size = new Size(D(100) + D(12) + D(40), D(30)), BackColor = Theme.Card };
            crashTest.Location = new Point(0, 0);
            T(crashToggle, settings.CrashHelper, v => { settings.CrashHelper = v; settings.Save(); });
            crashToggle.Location = new Point(D(112), (D(30) - crashToggle.Height) / 2);
            crashRight.Controls.Add(crashTest);
            crashRight.Controls.Add(crashToggle);
            Add(page, SettingCard("Crash helper", "If Roblox crashes, shows what went wrong and how to fix it.", crashRight));
            Add(page, SettingCard("Start with Windows", "Opens quietly in the tray when your PC starts, so friend alerts work.",
                T(startupToggle, Setup.StartsWithWindows, v =>
                {
                    if (v && !File.Exists(Setup.InstallExe))
                    {
                        Ask("Install GuideStrap first", "This only works for the installed GuideStrap, not a copy you're running from somewhere else.", "OK");
                        startupToggle.Checked = false;
                        return;
                    }
                    Setup.SetStartup(v);
                })));
            Add(page, SettingCard("Streamer mode", "Hides account names and server location in GuideStrap and on Discord.",
                T(streamerToggle, settings.StreamerMode, v =>
                {
                    settings.StreamerMode = v;
                    settings.Save();
                    RefreshHome();
                    Presence.Configure(settings);
                })));
            Add(page, SettingCard("Session summary", "After you close Roblox, a small popup shows how long you played.",
                T(summaryToggle, settings.SessionSummary, v => { settings.SessionSummary = v; settings.Save(); })));
            Add(page, SettingCard("Show server location", "Pops up where the server you joined is, like \"Dallas, Texas, US\".",
                T(locationToggle, settings.ShowServerLocation, v => { settings.ShowServerLocation = v; settings.Save(); })));
            Add(page, SettingCard("Allow multiple Roblox windows", "Run more than one account at once. Roblox sometimes blocks this.",
                T(multiToggle, settings.MultiInstance, v => { settings.MultiInstance = v; settings.Save(); })));
            Add(page, SettingCard("Open Roblox links with GuideStrap", "Off gives Play buttons on roblox.com back to normal Roblox.",
                T(linksToggle, LinkHandler.IsOurs(), v =>
                {
                    try
                    {
                        if (v) LinkHandler.Register(settings); else LinkHandler.Restore(settings);
                        settings.Save();
                        SetStatus(v ? "Play buttons on roblox.com now open GuideStrap." : "Play buttons on roblox.com open normal Roblox again.", null, StatusKind.Idle);
                    }
                    catch (Exception ex) { Ask("Couldn't change that", ex.Message, "OK"); }
                })));

            Section(page, "Appearance");
            var picker = new AccentPicker { Selected = settings.Accent };
            picker.Size = new Size(D(30) * Theme.Accents.Length, D(30));
            picker.Picked += name =>
            {
                settings.Accent = name;
                settings.Save();
                if (Ask("Restart to apply", "The new color shows after GuideStrap restarts.", "Restart now", "Later") == 0)
                    Application.Restart();
            };
            var themeCombo = new DarkCombo { ItemHeight = D(22), Width = D(170) };
            foreach (var t in Theme.Themes) themeCombo.Items.Add(L.T(t));
            themeCombo.SelectedIndex = Math.Max(0, Array.IndexOf(Theme.Themes, settings.ThemeName));
            themeCombo.SelectedIndexChanged += (_, _) =>
            {
                string pick = Theme.Themes[themeCombo.SelectedIndex];
                if (pick == settings.ThemeName) return;
                settings.ThemeName = pick;
                settings.Save();
                if (Ask("Restart to apply", "The new theme shows after GuideStrap restarts.", "Restart now", "Later") == 0)
                    Application.Restart();
            };
            Add(page, SettingCard("Theme", "Purple, or plain black with no color.", themeCombo));
            Add(page, SettingCard("Accent color", "Used by the Purple theme for buttons, switches and highlights.", picker));

            var bgButtons = ButtonPair(
                Secondary("Choose image", "\uE8B9", D(130), D(30)), ChooseLauncherBackground,
                Secondary("Reset", "\uE72C", D(80), D(30)), () =>
                {
                    settings.LauncherBackground = null;
                    settings.Save();
                    SetStatus("The start screen is back to plain.", null, StatusKind.Idle);
                });
            Add(page, SettingCard("Start screen background", "Your own picture behind the start screen. It's darkened so text stays readable.", bgButtons));

            var langCombo = new DarkCombo { ItemHeight = D(22), Width = D(170) };
            foreach (var (_, native) in L.Languages) langCombo.Items.Add(native);
            langCombo.SelectedIndex = Math.Max(0, Array.FindIndex(L.Languages, x => x.Code == L.Lang));
            langCombo.SelectedIndexChanged += (_, _) =>
            {
                string code = L.Languages[langCombo.SelectedIndex].Code;
                settings.Language = code;
                settings.Save();
                if (code != L.Lang && Ask("Restart to apply", "The new language shows after GuideStrap restarts.", "Restart now", "Later") == 0)
                    Application.Restart();
            };
            Add(page, SettingCard("Language", "The language GuideStrap uses.", langCombo));

            Section(page, "Profiles");
            var profileButtons = ButtonPair(
                Secondary("Export", "\uE74E", D(96), D(30)), ExportProfile,
                Secondary("Import", "\uE8E5", D(96), D(30)), ImportProfile);
            Add(page, SettingCard("Settings profile", "Share your graphics, FastFlags, cursor and color as one file.", profileButtons));

            Section(page, "Tools");
            var folder = Secondary("Open folder", "\uE8B7", D(130), D(30));
            folder.Click += (_, _) =>
            {
                Directory.CreateDirectory(Paths.Base);
                Process.Start(new ProcessStartInfo(Paths.Base) { UseShellExecute = true });
            };
            Add(page, SettingCard("GuideStrap folder", "Settings, mods and the downloaded Roblox live here.", folder));

            var clean = Secondary("Clear cache", "\uE74D", D(130), D(30));
            clean.Click += async (_, _) =>
            {
                clean.Enabled = false;
                long freed = await Task.Run(() => Cleaner.ClearCache());
                clean.Enabled = true;
                SetStatus($"Cleared {freed / 1024.0 / 1024.0:0.#} MB of old Roblox cache and logs.", null, StatusKind.Idle);
            };
            Add(page, SettingCard("Clear cache", "Deletes Roblox's temporary files, old logs and cached pictures.", clean));

            var reinstall = Secondary("Repair", "\uE90F", D(120), D(30));
            reinstall.HoverColor = Theme.Danger;
            reinstall.Click += (_, _) =>
            {
                if (Ask("Repair Roblox?", "This deletes GuideStrap's copy of Roblox. It downloads a fresh one next time you launch.", "Repair", "Cancel") != 0) return;
                try
                {
                    if (Directory.Exists(Paths.Versions)) Directory.Delete(Paths.Versions, true);
                    SetStatus("Roblox will re-download next time you launch.", 0, StatusKind.Idle);
                }
                catch (Exception ex) { Ask("Close Roblox first", ex.Message, "OK"); }
            };
            Add(page, SettingCard("Repair Roblox", "Fixes crashes or a broken install by downloading Roblox again.", reinstall));

            var uninstall = Secondary("Uninstall", "\uE74D", D(120), D(30));
            uninstall.HoverColor = Theme.Danger;
            uninstall.Click += (_, _) =>
            {
                if (!File.Exists(Setup.InstallExe))
                {
                    Ask("GuideStrap isn't installed", "This copy isn't installed, so there's nothing to remove.", "OK");
                    return;
                }
                if (Setup.Uninstall(this))
                {
                    Presence.Shutdown();
                    Application.Exit();
                }
            };
            Add(page, SettingCard("Uninstall GuideStrap", "Removes GuideStrap, its settings and its copy of Roblox. Normal Roblox stays.", uninstall));

            var update = Secondary("Check now", "\uE895", D(120), D(30));
            update.Click += async (_, _) => await CheckForUpdatesAsync(manual: true);
            Add(page, SettingCard("Updates", $"You're on GuideStrap {AppInfo.Version}.", update), 0);
        }

        async Task CheckForUpdatesAsync(bool manual)
        {
            if (!Updater.Configured)
            {
                if (manual) Ask("Updates aren't set up yet", "Once GuideStrap is published on GitHub, set UpdateRepo in the code and it will update itself.", "OK");
                return;
            }
            var found = await Updater.CheckAsync();
            if (found == null)
            {
                if (manual) SetStatus("GuideStrap is up to date.", null, StatusKind.Idle);
                return;
            }
            if (Ask("Update available", $"GuideStrap {found.Value.Version} is out. Update now?", "Update", "Later") == 0)
            {
                SetStatus("Downloading the update...", null, StatusKind.Busy);
                try { await Updater.InstallAsync(found.Value.Url); }
                catch (Exception ex) { Ask("Update failed", ex.Message, "OK"); }
            }
        }

        void ExportProfile()
        {
            using var dlg = new SaveFileDialog { Filter = "GuideStrap profile (*.gsprofile)|*.gsprofile", FileName = "GuideStrap profile.gsprofile" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var p = new Profile
            {
                Preset = settings.Preset,
                Flags = new Dictionary<string, string>(settings.Flags),
                CursorPreset = settings.CursorPreset == "custom" ? "default" : settings.CursorPreset,
                Accent = settings.Accent,
            };
            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));
            SetStatus("Profile saved.", null, StatusKind.Idle);
        }

        void ImportProfile()
        {
            using var dlg = new OpenFileDialog { Filter = "GuideStrap profile (*.gsprofile)|*.gsprofile" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            Profile? p;
            try { p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(dlg.FileName)); }
            catch { p = null; }
            if (p == null) { Ask("That profile can't be read", "The file is damaged or isn't a GuideStrap profile.", "OK"); return; }
            if (Ask("Use this profile?", "Your graphics, FastFlags, cursor and accent color will be replaced.", "Use profile", "Cancel") != 0) return;

            settings.Preset = p.Preset;
            settings.Flags = p.Flags.Where(kv => FF.IsAllowed(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            settings.CursorPreset = p.CursorPreset is "guidestrap" or "dot" or "cross" or "circle" or "classic" ? p.CursorPreset : "default";
            bool colorChanged = p.Accent != settings.Accent && Theme.Accents.Any(a => a.Name == p.Accent);
            if (colorChanged) settings.Accent = p.Accent;
            settings.Save();
            RefreshGraphicsControls();
            RefreshFlagSummary();
            RefreshMods();
            RefreshHome();
            SetStatus("Profile loaded.", null, StatusKind.Idle);
            if (colorChanged && Ask("Restart to apply", "The profile's color shows after GuideStrap restarts.", "Restart now", "Later") == 0)
                Application.Restart();
        }

        void BuildAboutPage()
        {
            var page = NewPage("About", "About", "What GuideStrap is.");

            var hero = new Card { Size = new Size(CW, D(96)) };
            var logo = new LogoMark { Size = new Size(D(64), D(64)), Location = new Point(D(16), D(16)) };
            var name = At(Lbl("GuideStrap", 16f, semibold: true), D(92), D(18));
            var ver = At(Lbl($"Version {AppInfo.Version}  ·  by Guide", 9f, Theme.SubText), D(94), D(54));
            hero.Controls.AddRange(new Control[] { logo, name, ver });
            Add(page, hero, 8);

            var body = new Card { Size = new Size(CW, D(10)) };
            var text = Lbl(AppInfo.AboutText, 9.25f, Theme.Text);
            text.MaximumSize = new Size(CW - D(32), 0);
            text.Location = new Point(D(16), D(14));
            body.Controls.Add(text);
            body.Height = text.GetPreferredSize(new Size(CW - D(32), 0)).Height + D(28);
            Add(page, body, 0);
        }
    }
}
