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
                T(multiToggle, settings.MultiInstance, v => { settings.MultiInstance = v; settings.Save(); if (v) MultiInstance.Hold(); })));
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

            merged["Appearance"] = ("Settings", Section(page, "Appearance"));
            var picker = new AccentPicker { Selected = settings.Accent, Enabled = settings.ThemeName != "Glass" };
            picker.Size = new Size(D(30) * Theme.Accents.Length, D(30));
            picker.Picked += name =>
            {
                settings.Accent = name;
                settings.Save();
                BeginInvoke(new Action(() => ReloadWindow("Appearance")));   // shows the new color right away
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
                BeginInvoke(new Action(() => ReloadWindow("Appearance")));   // switches theme right away
            };
            Add(page, SettingCard("Theme", "Purple, plain black, or Glass: dark grey with glowing card edges.", themeCombo));
            Add(page, SettingCard("Accent color", "Buttons, switches and highlights in the Purple and Black themes. Glass always uses grey.", picker));
            var seeToggle = T(new ToggleSwitch(), settings.SeeThrough, v =>
            {
                settings.SeeThrough = v;
                settings.Save();
                Theme.Apply(settings.ThemeName, settings.Accent, v);
                Opacity = Theme.WindowOpacity;
            });
            seeToggle.Enabled = settings.ThemeName == "Glass";
            Add(page, SettingCard("See-through windows", "Glass only. Lets your desktop show through a little. Text is a bit less sharp while it's on.", seeToggle));

            var bgButtons = ButtonPair(
                Secondary("Choose image", "\uE8B9", D(130), D(30)), ChooseLauncherBackground,
                Secondary("Reset", "\uE72C", D(80), D(30)), () =>
                {
                    settings.LauncherBackground = null;
                    settings.Save();
                    SetStatus("The start screen is back to plain.", null, StatusKind.Idle);
                    if (settings.BackgroundInSettings) BeginInvoke(new Action(() => ReloadWindow("Appearance")));
                });
            Add(page, SettingCard("Start screen background", "Your own picture behind the start screen. It's darkened so text stays readable.", bgButtons));
            Add(page, SettingCard("Show on the loading screen", "Your picture also shows behind the Roblox loading screen.",
                T(new ToggleSwitch(), settings.BackgroundOnLoader, v => { settings.BackgroundOnLoader = v; settings.Save(); })));
            Add(page, SettingCard("Show in settings", "Your picture also shows behind this whole window.",
                T(new ToggleSwitch(), settings.BackgroundInSettings, v =>
                {
                    settings.BackgroundInSettings = v;
                    settings.Save();
                    if (settings.LauncherBackground != null) BeginInvoke(new Action(() => ReloadWindow("Appearance")));
                })));

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

            Section(page, "Backup");
            var backupButtons = ButtonPair(
                Secondary("Back up", "\uE74E", D(96), D(30)), ExportBackup,
                Secondary("Restore", "\uE777", D(96), D(30)), ImportBackup);
            Add(page, SettingCard("Full backup", "Settings, mods, favorites and playtime in one file. Saved accounts aren't included, they only work on this PC.", backupButtons));

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

            var sortNow = Secondary("Sort now", "\uE8CB", D(110), D(30));
            sortNow.Click += async (_, _) =>
            {
                sortNow.Enabled = false;
                int moved = await Task.Run(() => ScreenshotSorter.Sort());
                if (IsDisposed) return;
                sortNow.Enabled = true;
                SetStatus(moved == 0 ? L.T("No new screenshots to sort.") : L.F("Moved {0} screenshots into game folders.", moved), null, StatusKind.Idle);
            };
            var sortRight = new Panel { Size = new Size(D(110) + D(12) + D(40), D(30)), BackColor = Theme.Card };
            sortNow.Location = new Point(0, 0);
            T(sortShotsToggle, settings.SortScreenshots, v => { settings.SortScreenshots = v; settings.Save(); });
            sortShotsToggle.Location = new Point(D(122), (D(30) - sortShotsToggle.Height) / 2);
            sortRight.Controls.Add(sortNow);
            sortRight.Controls.Add(sortShotsToggle);
            Add(page, SettingCard("Sort screenshots by game", "Moves Roblox screenshots into a folder for each game (Pictures > Roblox). Turn the switch on to do it after every session.", sortRight));

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

        void ExportBackup()
        {
            using var dlg = new SaveFileDialog { Filter = L.T("GuideStrap backup") + " (*.gsbackup)|*.gsbackup", FileName = $"GuideStrap backup {DateTime.Now:yyyy-MM-dd}.gsbackup" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                settings.Save();
                Backup.Export(dlg.FileName);
                SetStatus("Backup saved.", null, StatusKind.Idle);
            }
            catch (Exception ex) { Ask("Couldn't save the backup", ex.Message, "OK"); }
        }

        void ImportBackup()
        {
            using var dlg = new OpenFileDialog { Filter = L.T("GuideStrap backup") + " (*.gsbackup)|*.gsbackup" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (Ask("Restore this backup?", "Your settings, mods, favorites and playtime will be replaced with the ones in the backup. Saved accounts stay.",
                    "Restore", "Cancel") != 0) return;
            try { Backup.Import(dlg.FileName); }
            catch (Exception ex)
            {
                Ask("Couldn't restore the backup", ex.Message, "OK");
                return;
            }
            // this window still holds the old settings, so restart before anything saves over the backup
            Ask("Backup restored", "GuideStrap restarts now to load it.", "OK");
            Application.Restart();
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
            try { File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex) { Ask("Couldn't save the profile", ex.Message, "OK"); return; }
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
            settings.Flags = (p.Flags ?? new()).Where(kv => FF.IsAllowed(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            settings.CursorPreset = p.CursorPreset is "guidestrap" or "dot" or "cross" or "circle" or "classic" ? p.CursorPreset : "default";
            bool colorChanged = p.Accent != settings.Accent && Theme.Accents.Any(a => a.Name == p.Accent);
            if (colorChanged) settings.Accent = p.Accent;
            settings.Save();
            RefreshGraphicsControls();
            RefreshFlagSummary();
            RefreshMods();
            RefreshHome();
            SetStatus("Profile loaded.", null, StatusKind.Idle);
            // a new accent color: rebuild the window like the color picker does, instead of keeping the old color until a restart
            if (colorChanged) BeginInvoke(new Action(() => ReloadWindow("Appearance")));
        }

        void BuildAboutPage()
        {
            // lives at the bottom of the Settings tab
            var page = pages["Settings"];
            merged["About"] = ("Settings", Section(page, "About"));

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
