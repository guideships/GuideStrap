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
        // MODS
        void BuildModsPage()
        {
            var page = NewPage("Mods", "Mods", "Cosmetic file swaps. Only GuideStrap's copy of Roblox is changed.");

            var browse = Primary("Browse", "\uE71E", D(110), D(30));
            browse.Click += (_, _) =>
            {
                using var g = new GalleryForm(ChangeSettings);
                g.ShowDialog(this);
            };
            Add(page, SettingCard("Mod gallery", "Skies, cursors, crosshairs and sound packs you can install in one click.", browse), 8);

            // death sound
            var soundButtons = ButtonPair(
                Secondary("Choose .ogg", "\uE8E5", D(120), D(30)), ChooseDeathSound,
                Secondary("Reset", "\uE72C", D(80), D(30)), () => { settings.DeathSoundFile = null; settings.Save(); RefreshMods(); });
            Add(page, ModCard("Death sound", deathStatus, soundButtons));

            // footsteps + jump
            SetupCombo(footstepCombo, D(170), "Default", "Muted", "Custom .mp3...");
            footstepCombo.SelectedIndexChanged += (_, _) => { if (!modsSyncing) OnSoundPicked(footstepCombo, "footsteps"); };
            Add(page, ModCard("Footsteps", footstepStatus, footstepCombo));
            SetupCombo(jumpCombo, D(170), "Default", "Muted", "Custom .mp3...");
            jumpCombo.SelectedIndexChanged += (_, _) => { if (!modsSyncing) OnSoundPicked(jumpCombo, "jump"); };
            Add(page, ModCard("Jump sound", jumpStatus, jumpCombo));

            // sound packs
            packCombo.ItemHeight = D(22);
            packCombo.Width = D(150);
            var openPacks = Secondary("", "\uE8B7", D(36), D(30));
            openPacks.Click += (_, _) =>
            {
                Directory.CreateDirectory(Paths.SoundPacks);
                Process.Start(new ProcessStartInfo(Paths.SoundPacks) { UseShellExecute = true });
            };
            var packRight = new Panel { Size = new Size(D(150) + D(6) + D(36), D(30)), BackColor = Theme.Card };
            packCombo.Location = new Point(0, (D(30) - packCombo.Height) / 2);
            openPacks.Location = new Point(D(156), 0);
            packRight.Controls.Add(packCombo);
            packRight.Controls.Add(openPacks);
            packCombo.SelectedIndexChanged += (_, _) =>
            {
                if (modsSyncing) return;
                settings.SoundPack = packCombo.SelectedIndex <= 0 ? null : packCombo.SelectedItem?.ToString();
                settings.Save();
                RefreshMods();
            };
            Add(page, ModCard("Sound pack", packStatus, packRight));

            // cursor
            cursorPreview.Size = new Size(D(32), D(32));
            cursorPreview.SizeMode = PictureBoxSizeMode.Zoom;
            cursorPreview.BackColor = Theme.Input;
            SetupCombo(cursorCombo, D(170), "Default", "GuideStrap arrow", "Small dot", "Small cross", "Circle", "Classic arrow", "Custom image...");
            var cursorRight = new Panel { Size = new Size(D(212), D(32)), BackColor = Theme.Card };
            cursorPreview.Location = new Point(0, 0);
            cursorCombo.Location = new Point(D(42), (D(32) - cursorCombo.Height) / 2);
            cursorRight.Controls.Add(cursorPreview);
            cursorRight.Controls.Add(cursorCombo);
            cursorCombo.SelectedIndexChanged += (_, _) => { if (!modsSyncing) OnCursorPicked(); };
            Add(page, ModCard("Mouse cursor", cursorStatus, cursorRight));

            // shift lock crosshair
            crosshairPreview.Size = new Size(D(32), D(32));
            crosshairPreview.SizeMode = PictureBoxSizeMode.Zoom;
            crosshairPreview.BackColor = Theme.Input;
            SetupCombo(crosshairCombo, D(170), "Default", "Dot", "Small cross", "Circle", "Custom image...");
            var crosshairRight = new Panel { Size = new Size(D(212), D(32)), BackColor = Theme.Card };
            crosshairPreview.Location = new Point(0, 0);
            crosshairCombo.Location = new Point(D(42), (D(32) - crosshairCombo.Height) / 2);
            crosshairRight.Controls.Add(crosshairPreview);
            crosshairRight.Controls.Add(crosshairCombo);
            crosshairCombo.SelectedIndexChanged += (_, _) => { if (!modsSyncing) OnCrosshairPicked(); };
            Add(page, ModCard("Shift lock crosshair", crosshairStatus, crosshairRight));

            // sky
            var skyButtons = ButtonPair(
                Secondary("Choose sky folder", "\uE8B7", D(150), D(30)), ChooseSky,
                Secondary("Reset", "\uE72C", D(80), D(30)), () =>
                {
                    settings.CustomSky = false;
                    settings.Save();
                    try { if (Directory.Exists(Paths.Sky)) Directory.Delete(Paths.Sky, true); } catch { }
                    RefreshMods();
                });
            Add(page, ModCard("Sky", skyStatus, skyButtons));

            Section(page, "Setups");
            setupCombo.ItemHeight = D(22);
            setupCombo.Width = D(150);
            var saveSetup = Secondary("Save", "\uE74E", D(80), D(30));
            var deleteSetup = Secondary("", "\uE74D", D(36), D(30));
            deleteSetup.HoverColor = Theme.Danger;
            var setupRight = new Panel { Size = new Size(D(150) + D(6) + D(80) + D(6) + D(36), D(30)), BackColor = Theme.Card };
            setupCombo.Location = new Point(0, (D(30) - setupCombo.Height) / 2);
            saveSetup.Location = new Point(D(156), 0);
            deleteSetup.Location = new Point(D(242), 0);
            setupRight.Controls.Add(setupCombo);
            setupRight.Controls.Add(saveSetup);
            setupRight.Controls.Add(deleteSetup);
            setupCombo.SelectedIndexChanged += (_, _) =>
            {
                if (modsSyncing || setupCombo.SelectedIndex <= 0) return;
                if (setupCombo.SelectedItem?.ToString() is string name) UseSetup(name);
            };
            saveSetup.Click += (_, _) => SaveSetup();
            deleteSetup.Click += (_, _) => DeleteSetup();
            Add(page, ModCard("Saved setups", setupStatus, setupRight));

            var linkGame = Secondary("Link game", "\uE71B", D(110), D(30));
            var clearLinks = Secondary("", "\uE74D", D(36), D(30));
            clearLinks.HoverColor = Theme.Danger;
            var autoRight = new Panel { Size = new Size(D(110) + D(6) + D(36), D(30)), BackColor = Theme.Card };
            linkGame.Location = new Point(0, 0);
            clearLinks.Location = new Point(D(116), 0);
            autoRight.Controls.Add(linkGame);
            autoRight.Controls.Add(clearLinks);
            linkGame.Click += (_, _) => LinkGameToSetup();
            clearLinks.Click += (_, _) =>
            {
                if (settings.GameSetups.Count == 0) return;
                if (Ask("Remove all links?", "Every game goes back to your normal settings.", "Remove", "Cancel") != 0) return;
                settings.GameSetups.Clear();
                settings.Save();
                RefreshMods();
            };
            Add(page, ModCard("Auto setups", autoStatus, autoRight));

            ownModsSection = Section(page, "Your own mods");
            var openMods = Secondary("Open mods folder", "\uE8B7", D(160), D(30));
            openMods.Click += (_, _) =>
            {
                Directory.CreateDirectory(Paths.Mods);
                Process.Start(new ProcessStartInfo(Paths.Mods) { UseShellExecute = true });
            };
            Add(page, SettingCard("Mods folder", "Files here replace the matching files in Roblox (same folder layout).", openMods));

            var note = Lbl("Normal Roblox is never touched. Launching Roblox without GuideStrap always uses the original files, sounds and settings.", 8.5f, Theme.SubText);
            note.AutoSize = false;
            note.Size = new Size(CW, D(34));
            note.Margin = new Padding(D(2), D(6), 0, 0);
            page.Controls.Add(note);
        }

        Control ownModsSection = null!;   // the text font section goes right above this

        Card ModCard(string title, Label status, Control right)
        {
            var card = new Card { Size = new Size(CW, D(58)) };
            card.Controls.Add(At(Lbl(title, 9.75f), D(16), D(10)));
            status.AutoSize = false;
            status.Size = new Size(CW - right.Width - D(48), D(18));
            status.Location = new Point(D(16), D(31));
            status.Font = Theme.F("Segoe UI", 8.5f);
            status.ForeColor = Theme.SubText;
            status.AutoEllipsis = true;
            card.Controls.Add(status);
            right.Location = new Point(card.Width - D(16) - right.Width, (card.Height - right.Height) / 2);
            card.Controls.Add(right);
            return card;
        }

        Panel ButtonPair(RoundButton a, Action onA, RoundButton b, Action onB)
        {
            var p = new Panel { Size = new Size(a.Width + D(6) + b.Width, a.Height), BackColor = Theme.Card };
            a.Location = new Point(0, 0);
            b.Location = new Point(a.Width + D(6), 0);
            a.Click += (_, _) => onA();
            b.Click += (_, _) => onB();
            p.Controls.Add(a);
            p.Controls.Add(b);
            return p;
        }

        // Used by the crash helper so its fixes land in this window's settings too
        internal void ChangeSettings(Action<AppSettings> change)
        {
            change(settings);
            settings.Save();
            multiToggle.Checked = settings.MultiInstance;
            RefreshGraphicsControls();
            RefreshFlagSummary();
            RefreshMods();
            RefreshHome();
        }

        void RefreshMods()
        {
            modsSyncing = true;
            deathStatus.Text = L.T(settings.DeathSoundFile != null && File.Exists(settings.DeathSoundFile)
                ? "Custom sound" : "Roblox's default sound");
            cursorCombo.SelectedIndex = Math.Max(0, Array.IndexOf(CursorKeys, settings.CursorPreset));
            cursorStatus.Text = L.T(settings.CursorPreset switch
            {
                "guidestrap" => "Purple arrow made for GuideStrap",
                "dot" => "A small dot, easy to aim with",
                "cross" => "A small cross, easy to aim with",
                "circle" => "A thin circle",
                "classic" => "White arrow with a black outline",
                "custom" => "Your own image",
                _ => "Roblox's default cursor",
            });
            string? dir = ModAssets.CursorDir(settings);
            var old = cursorPreview.Image;
            cursorPreview.Image = null;
            old?.Dispose();
            if (dir != null)
            {
                try
                {
                    using var fs = File.OpenRead(Path.Combine(dir, "ArrowCursor.png"));
                    using var img = Image.FromStream(fs);
                    cursorPreview.Image = new Bitmap(img);
                }
                catch { }
            }
            crosshairCombo.SelectedIndex = Math.Max(0, Array.IndexOf(CrosshairKeys, settings.CrosshairPreset));
            crosshairStatus.Text = L.T(settings.CrosshairPreset switch
            {
                "dot" => "A small dot in the middle of the screen",
                "cross" => "A small cross in the middle of the screen",
                "circle" => "A thin circle in the middle of the screen",
                "custom" => "Your own image",
                _ => "Roblox's default shift lock icon",
            });
            ShowPreview(crosshairPreview, ModAssets.CrosshairFile(settings));

            footstepCombo.SelectedIndex = Math.Max(0, Array.IndexOf(SoundModes, settings.FootstepSound));
            footstepStatus.Text = L.T(settings.FootstepSound switch { "mute" => "No footstep sound", "custom" => "Your own sound", _ => "Roblox's default footsteps" });
            jumpCombo.SelectedIndex = Math.Max(0, Array.IndexOf(SoundModes, settings.JumpSound));
            jumpStatus.Text = L.T(settings.JumpSound switch { "mute" => "No jump or landing sound", "custom" => "Your own sound", _ => "Roblox's default jump sound" });

            var packs = ModAssets.SoundPacks();
            packCombo.Items.Clear();
            packCombo.Items.Add(L.T("None"));
            foreach (var pk in packs) packCombo.Items.Add(pk);
            int packIdx = settings.SoundPack == null ? 0 : packs.IndexOf(settings.SoundPack) + 1;
            packCombo.SelectedIndex = Math.Max(0, packIdx);
            packStatus.Text = packIdx > 0
                ? L.F("Using {0}. Its files replace Roblox's sounds.", settings.SoundPack!)
                : L.T(packs.Count == 0 ? "Open the folder and add a pack: one folder per pack, with .ogg or .mp3 files." : "No pack. Pick one, or open the folder to add more.");

            setupCombo.Items.Clear();
            setupCombo.Items.Add(L.T("Pick a setup"));
            var setupNames = settings.Setups.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var n in setupNames) setupCombo.Items.Add(n);
            int setupIdx = settings.LastSetup == null ? 0 : setupNames.IndexOf(settings.LastSetup) + 1;
            setupCombo.SelectedIndex = Math.Max(0, setupIdx);
            setupStatus.Text = setupNames.Count == 0
                ? L.T("Save your mods and graphics as a setup, like BedWars or Recording.")
                : L.F("{0} saved. Pick one to switch to it.", setupNames.Count);

            var links = settings.GameSetups.Where(kv => settings.Setups.ContainsKey(kv.Value)).ToList();
            var recentGames = RecentGames.Load();
            string GameName(string key) =>
                long.TryParse(key, out var gid) ? recentGames.FirstOrDefault(r => r.PlaceId == gid)?.Name ?? key : key;
            autoStatus.Text = links.Count == 0
                ? L.T("Link a setup to a game and it turns on by itself when you launch that game.")
                : string.Join(",  ", links.Take(3).Select(kv => $"{GameName(kv.Key)} \u2192 {kv.Value}"))
                  + (links.Count > 3 ? L.F("  +{0} more", links.Count - 3) : "");

            int skyFiles = Directory.Exists(Paths.Sky) ? Directory.GetFiles(Paths.Sky, "*.tex").Length : 0;
            skyStatus.Text = L.T(settings.CustomSky && skyFiles > 0
                ? $"Custom sky pack ({skyFiles} files). Games with their own sky keep it."
                : "Roblox's default sky. Gray sky is under Graphics.");
            modsSyncing = false;
        }

        void ChooseDeathSound()
        {
            using var dlg = new OpenFileDialog { Filter = "Ogg sound (*.ogg)|*.ogg", Title = L.T("Pick a death sound") };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var head = new byte[4];
                using (var fs = File.OpenRead(dlg.FileName)) fs.ReadExactly(head);
                if (Encoding.ASCII.GetString(head) != "OggS")
                {
                    Ask("That isn't an Ogg file", "Roblox only plays .ogg sounds here. Convert your sound to .ogg first (any free online converter works).", "OK");
                    return;
                }
                Directory.CreateDirectory(Paths.Managed);
                string dest = Path.Combine(Paths.Managed, "ouch.ogg");
                File.Copy(dlg.FileName, dest, true);
                settings.DeathSoundFile = dest;
                settings.Save();
                SetStatus("Death sound set. It applies next time you launch Roblox.", null, StatusKind.Idle);
            }
            catch (Exception ex) { Ask("Couldn't use that sound", ex.Message, "OK"); }
            RefreshMods();
        }

        void OnCursorPicked()
        {
            string pick = CursorKeys[Math.Clamp(cursorCombo.SelectedIndex, 0, CursorKeys.Length - 1)];
            if (pick == "custom")
            {
                using var dlg = new OpenFileDialog { Filter = "Images (*.png)|*.png", Title = L.T("Pick a cursor image") };
                if (dlg.ShowDialog(this) != DialogResult.OK || !ModAssets.ImportCustomCursor(dlg.FileName))
                {
                    RefreshMods();
                    return;
                }
            }
            settings.CursorPreset = pick;
            settings.Save();
            RefreshMods();
        }

        void OnCrosshairPicked()
        {
            string pick = CrosshairKeys[Math.Clamp(crosshairCombo.SelectedIndex, 0, CrosshairKeys.Length - 1)];
            if (pick == "custom")
            {
                using var dlg = new OpenFileDialog { Filter = "Images (*.png)|*.png", Title = L.T("Pick a crosshair image") };
                if (dlg.ShowDialog(this) != DialogResult.OK || !ModAssets.ImportCustomCrosshair(dlg.FileName))
                {
                    RefreshMods();
                    return;
                }
            }
            settings.CrosshairPreset = pick;
            settings.Save();
            RefreshMods();
        }

        void OnSoundPicked(DarkCombo combo, string kind)
        {
            string mode = SoundModes[Math.Clamp(combo.SelectedIndex, 0, SoundModes.Length - 1)];
            if (mode == "custom")
            {
                using var dlg = new OpenFileDialog { Filter = "MP3 sound (*.mp3)|*.mp3", Title = L.T("Pick a sound") };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    RefreshMods();
                    return;
                }
                try
                {
                    var head = new byte[3];
                    using (var fs = File.OpenRead(dlg.FileName)) fs.ReadExactly(head);
                    bool mp3 = (head[0] == 'I' && head[1] == 'D' && head[2] == '3') || (head[0] == 0xFF && (head[1] & 0xE0) == 0xE0);
                    if (!mp3)
                    {
                        Ask("That isn't an MP3 file", "These Roblox sounds are .mp3 files. Convert your sound to .mp3 first (any free online converter works).", "OK");
                        RefreshMods();
                        return;
                    }
                    Directory.CreateDirectory(ModAssets.SoundsDir);
                    File.Copy(dlg.FileName, Path.Combine(ModAssets.SoundsDir, kind + ".mp3"), true);
                }
                catch (Exception ex)
                {
                    Ask("Couldn't use that sound", ex.Message, "OK");
                    RefreshMods();
                    return;
                }
            }
            if (kind == "footsteps") settings.FootstepSound = mode; else settings.JumpSound = mode;
            settings.Save();
            SetStatus("Sound set. It applies next time you launch Roblox.", null, StatusKind.Idle);
            RefreshMods();
        }

        static void ShowPreview(PictureBox box, string? file)
        {
            var old = box.Image;
            box.Image = null;
            old?.Dispose();
            if (file == null || !File.Exists(file)) return;
            try
            {
                using var fs = File.OpenRead(file);
                using var img = Image.FromStream(fs);
                box.Image = new Bitmap(img);
            }
            catch { }
        }

        // SETUPS: saved combos of mods + graphics, switched in one click
        SavedSetup CaptureSetup() => new()
        {
            Preset = settings.Preset,
            Flags = new Dictionary<string, string>(settings.Flags),
            CursorPreset = settings.CursorPreset,
            CrosshairPreset = settings.CrosshairPreset,
            DeathSoundFile = settings.DeathSoundFile,
            FootstepSound = settings.FootstepSound,
            JumpSound = settings.JumpSound,
            SoundPack = settings.SoundPack,
            CustomSky = settings.CustomSky,
            FpsCap = settings.FpsCap,
            Priority = settings.Priority,
        };

        void SaveSetup()
        {
            string? name = AskText("Save setup", "Name this setup. Saving with an existing name replaces it.", "BedWars", "Save");
            if (name == null) return;
            if (name.Length > 40) name = name[..40];
            settings.Setups[name] = CaptureSetup();
            settings.LastSetup = name;
            settings.Save();
            RefreshMods();
            SetStatus(L.F("Saved {0}.", name), null, StatusKind.Idle);
        }

        void UseSetup(string name)
        {
            if (!settings.Setups.TryGetValue(name, out var s)) return;
            s.ApplyTo(settings);
            settings.LastSetup = name;
            settings.Save();
            SyncFpsCombo();
            priorityCombo.SelectedIndex = Math.Clamp(settings.Priority, 0, 2);
            RefreshGraphicsControls();
            RefreshFlagSummary();
            RefreshMods();
            RefreshHome();
            SetStatus(L.F("Switched to {0}. It applies next time you launch Roblox.", name), null, StatusKind.Idle);
        }

        void DeleteSetup()
        {
            if (setupCombo.SelectedIndex <= 0 || setupCombo.SelectedItem?.ToString() is not string name)
            {
                Ask("Pick a setup first", "Choose the setup you want to delete from the list.", "OK");
                return;
            }
            if (Ask("Delete setup?", L.F("Delete {0}? Your current settings stay the same.", name), "Delete", "Cancel") != 0) return;
            settings.Setups.Remove(name);
            foreach (var k in settings.GameSetups.Where(kv => kv.Value == name).Select(kv => kv.Key).ToList())
                settings.GameSetups.Remove(k);
            if (settings.LastSetup == name) settings.LastSetup = null;
            settings.Save();
            RefreshMods();
        }

        void LinkGameToSetup()
        {
            if (setupCombo.SelectedIndex <= 0 || setupCombo.SelectedItem?.ToString() is not string name)
            {
                Ask("Pick a setup first", "Choose a saved setup in the list above, then link it to a game.", "OK");
                return;
            }
            string? text = AskText(L.F("Link {0} to a game", name),
                "Paste the game link or place ID. GuideStrap switches to this setup whenever you launch that game.",
                "https://www.roblox.com/games/...", "Link");
            if (text == null) return;
            long? id = RobloxLauncher.ParsePlaceId(text);
            if (id == null)
            {
                Ask("That doesn't look like a game link", "Paste a link like roblox.com/games/123456, or just the number.", "OK");
                return;
            }
            settings.GameSetups[id.Value.ToString()] = name;
            settings.Save();
            RefreshMods();
            SetStatus(L.F("Linked {0}.", name), null, StatusKind.Idle);
        }

        string? AskText(string title, string message, string placeholder, string ok)
        {
            using var d = new ThemedDialog(title, message, new[] { ok, "Cancel" }, null, placeholder);
            d.ShowDialog(this);
            return d.Choice == 0 && d.Input.Length > 0 ? d.Input : null;
        }

        void ChooseLauncherBackground()
        {
            using var dlg = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp", Title = L.T("Pick a picture") };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                using (var fs = File.OpenRead(dlg.FileName))
                using (var test = Image.FromStream(fs))
                {
                    if (test.Width < 16 || test.Height < 16) throw new Exception(L.T("That picture is too small."));
                }
                Directory.CreateDirectory(Paths.Base);
                foreach (var old in Directory.GetFiles(Paths.Base, "launcher-background.*"))
                    try { File.Delete(old); } catch { }
                string dest = Path.Combine(Paths.Base, "launcher-background" + Path.GetExtension(dlg.FileName).ToLowerInvariant());
                File.Copy(dlg.FileName, dest, true);
                settings.LauncherBackground = dest;
                settings.Save();
                SetStatus("Background set. It shows on the start screen.", null, StatusKind.Idle);
                if (settings.BackgroundInSettings) BeginInvoke(new Action(() => ReloadWindow("Appearance")));
            }
            catch (Exception ex) { Ask("Couldn't use that picture", ex.Message, "OK"); }
        }

        void ChooseSky()
        {
            using var dlg = new FolderBrowserDialog { Description = L.T("Pick a folder with sky .tex files (from a Roblox sky mod pack)"), UseDescriptionForTitle = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var files = Directory.GetFiles(dlg.SelectedPath, "*.tex", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                Ask("No sky files found", "Sky packs are .tex files like sky512_up.tex. Pick the folder that contains them.", "OK");
                return;
            }
            try
            {
                if (Directory.Exists(Paths.Sky)) Directory.Delete(Paths.Sky, true);
                Directory.CreateDirectory(Paths.Sky);
                foreach (var f in files) File.Copy(f, Path.Combine(Paths.Sky, Path.GetFileName(f)), true);
                settings.CustomSky = true;
                settings.Save();
            }
            catch (Exception ex) { Ask("Couldn't use that sky", ex.Message, "OK"); }
            RefreshMods();
        }

        ToggleSwitch Tg(ToggleSwitch t, bool value, Action<bool> changed)
        {
            t.Size = new Size(D(40), D(20));
            t.Checked = value;
            t.CheckedChanged += (_, _) => changed(t.Checked);
            return t;
        }
    }
}
