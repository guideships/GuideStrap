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
        // GRAPHICS
        void BuildGraphicsPage()
        {
            var page = NewPage("Graphics", "Graphics", "Pick a preset, or change settings one by one.");

            var presetCard = new Card { Size = new Size(CW, D(136)) };
            presetCard.Controls.Add(At(Lbl("Preset", 8.5f, Theme.SubText), D(16), D(12)));
            presetTitle.Font = new Font("Segoe UI Semibold", 12.5f);
            presetTitle.ForeColor = Theme.Text;
            presetTitle.BackColor = Color.Transparent;
            presetTitle.AutoSize = true;
            presetTitle.Location = new Point(D(14), D(27));
            presetDesc.ForeColor = Theme.SubText;
            presetDesc.BackColor = Color.Transparent;
            presetDesc.AutoSize = false;
            presetDesc.Size = new Size(CW - D(32), D(20));
            presetDesc.Location = new Point(D(16), D(52));
            presetDesc.Font = new Font("Segoe UI", 8.75f);
            presetSlider.Minimum = 0;
            presetSlider.Maximum = 4;
            presetSlider.ShowTicks = true;
            presetSlider.Size = new Size(CW - D(32), D(28));
            presetSlider.Location = new Point(D(16), D(76));
            var left = At(Lbl("Max FPS", 8.5f, Theme.SubText), D(16), D(108));
            var mid = new Label
            {
                Text = "Balanced", AutoSize = false, TextAlign = ContentAlignment.TopCenter,
                Size = new Size(D(160), D(18)), Location = new Point(CW / 2 - D(80), D(108)),
                ForeColor = Theme.SubText, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f),
            };
            var right = new Label
            {
                Text = "Max graphics", AutoSize = false, TextAlign = ContentAlignment.TopRight,
                Size = new Size(D(160), D(18)), Location = new Point(CW - D(176), D(108)),
                ForeColor = Theme.SubText, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f),
            };
            presetCard.Controls.AddRange(new Control[] { presetTitle, presetDesc, presetSlider, left, mid, right });
            Add(page, presetCard, 8);

            // --- potato mode: every low setting at once, or one by one ---
            var potato = new Expander("Potato mode", "Everything low for the most FPS, or turn on parts one by one.", CW, D(58), D(4) + 7 * D(54) + D(4));
            var pc = potato.Content;
            int pw = pc.Width, py = D(4);
            var potatoParts = new[] { potatoBlur, potatoGrass, potatoMesh, potatoSky, potatoMsaa };
            foreach (var tg in potatoParts.Append(potatoAll)) tg.Size = new Size(D(40), D(20));
            var previewBtn = Secondary("Preview", "\uE890", D(110), D(30));
            previewBtn.Click += (_, _) => { using var pf = new TexturePreviewForm(); pf.ShowDialog(this); };
            AddRow(pc, pw, ref py, "Turn on everything", "All of the switches below at once.", potatoAll);
            AddRow(pc, pw, ref py, "Blurry textures", "Lowest texture quality. Works on every game's textures.", potatoBlur);
            AddRow(pc, pw, ref py, "No grass", "Hides terrain grass.", potatoGrass);
            AddRow(pc, pw, ref py, "Low-detail meshes", "Always uses the simplest version of models.", potatoMesh);
            AddRow(pc, pw, ref py, "Gray sky", "Replaces the skybox with flat gray.", potatoSky);
            AddRow(pc, pw, ref py, "Anti-aliasing off", "No edge smoothing. Edges look a bit jagged.", potatoMsaa);
            AddRow(pc, pw, ref py, "Preview", "See what blurry textures look like.", previewBtn, last: true);
            Add(page, potato, 8);

            potatoAll.CheckedChanged += (_, _) =>
            {
                if (potatoSyncing) return;
                potatoSyncing = true;
                foreach (var tg in potatoParts) tg.Checked = potatoAll.Checked;
                potatoSyncing = false;
                ApplyPotatoToControls();
            };
            foreach (var tg in potatoParts)
                tg.CheckedChanged += (_, _) => { if (!potatoSyncing) ApplyPotatoToControls(); };

            // --- advanced (animated expander) ---
            int rows = 11;
            var adv = new Expander("Advanced settings", "Change every graphics setting yourself.", CW, D(58), D(4) + rows * D(54) + D(4));
            var content = adv.Content;
            int cw = content.Width;
            int ry = D(4);

            var qualityBox = new Panel { Size = new Size(D(260), D(28)), BackColor = Theme.Card };
            qualitySlider.Minimum = 0;
            qualitySlider.Maximum = 21;
            qualitySlider.Size = new Size(D(210), D(28));
            qualitySlider.Location = new Point(0, 0);
            qualityValue.AutoSize = false;
            qualityValue.Size = new Size(D(46), D(28));
            qualityValue.Location = new Point(D(214), 0);
            qualityValue.TextAlign = ContentAlignment.MiddleRight;
            qualityValue.ForeColor = Theme.AccentLight;
            qualityValue.BackColor = Theme.Card;
            qualityValue.Font = new Font("Segoe UI Semibold", 9.5f);
            qualityBox.Controls.Add(qualitySlider);
            qualityBox.Controls.Add(qualityValue);

            SetupCombo(textureCombo, D(170), "Auto", "Lowest", "Low", "Medium", "High");
            SetupCombo(msaaCombo, D(170), "Auto", "Off (1x)", "2x", "4x", "8x");
            SetupCombo(rendererCombo, D(170), "Auto", "Direct3D 11", "Vulkan", "OpenGL");
            var toggles = new[] { grassToggle, skyToggle, meshToggle, voxelToggle, grassMotionToggle, dpiToggle, fullscreenToggle };
            foreach (var tg in toggles) tg.Size = new Size(D(40), D(20));

            AddRow(content, cw, ref ry, "Graphics quality", "Roblox's quality level (1-21). Auto = Roblox decides.", qualityBox);
            AddRow(content, cw, ref ry, "Texture quality", "How sharp surfaces and decals look.", textureCombo);
            AddRow(content, cw, ref ry, "Anti-aliasing", "Smooths jagged edges. Costs FPS.", msaaCombo);
            AddRow(content, cw, ref ry, "Rendering API", "Try Vulkan or D3D11 if you crash or stutter.", rendererCombo);
            AddRow(content, cw, ref ry, "Remove grass", "Hides terrain grass. Big FPS boost on grassy maps.", grassToggle);
            AddRow(content, cw, ref ry, "Gray sky", "Replaces the skybox with flat gray.", skyToggle);
            AddRow(content, cw, ref ry, "Low-detail meshes", "Always uses the simplest version of models.", meshToggle);
            AddRow(content, cw, ref ry, "Freeze lighting updates", "Faster, but lighting can look flat or wrong.", voxelToggle);
            AddRow(content, cw, ref ry, "Still grass", "Stops grass from waving around.", grassMotionToggle);
            AddRow(content, cw, ref ry, "Disable DPI scaling", "Sharper on high-res screens, UI gets smaller.", dpiToggle);
            AddRow(content, cw, ref ry, "Alt+Enter real fullscreen", "Exclusive fullscreen, can lower input lag.", fullscreenToggle, last: true);
            Add(page, adv, 0);

            Section(page, "Performance");
            SetupCombo(fpsCombo, D(170), "Roblox default", "60 FPS", "120 FPS", "144 FPS", "165 FPS", "240 FPS", "Custom...");
            SyncFpsCombo();
            fpsCombo.SelectedIndexChanged += (_, _) =>
            {
                if (fpsSyncing) return;
                int idx = fpsCombo.SelectedIndex;
                if (idx == FpsCaps.Length)   // Custom...
                {
                    string? text = AskText("Custom FPS cap", "Type any frame rate, like 75, 200 or 360. Roblox saves it in its own settings file (GlobalBasicSettings_13.xml).",
                        settings.FpsCap > 0 ? settings.FpsCap.ToString() : "360", "Set");
                    if (text != null && int.TryParse(text.Trim(), out int fps) && fps >= 1 && fps <= 10000)
                    {
                        bool ok = fps <= 360 || Ask("That's a very high cap", "Very high caps make your graphics card run flat out, which can heat it up and make the rest of your PC lag. Most screens show 60 to 240 FPS.",
                            "Keep it", "Cancel") == 0;
                        if (ok)
                        {
                            settings.FpsCap = fps;
                            settings.Save();
                        }
                    }
                    else if (text != null)
                        Ask("That isn't a frame rate", "Type a whole number from 1 to 10000.", "OK");
                }
                else
                {
                    settings.FpsCap = FpsCaps[Math.Clamp(idx, 0, FpsCaps.Length - 1)];
                    settings.Save();
                }
                SyncFpsCombo();
            };
            Add(page, SettingCard("FPS cap", "Any frame rate you want, set in Roblox's own settings before it opens. Normal Roblox keeps it too.", fpsCombo));
            SetupCombo(priorityCombo, D(170), "Normal", "Above normal", "High");
            priorityCombo.SelectedIndex = Math.Clamp(settings.Priority, 0, 2);
            priorityCombo.SelectedIndexChanged += (_, _) =>
            {
                int level = Math.Clamp(priorityCombo.SelectedIndex, 0, 2);
                if (level == 2 && settings.Priority != 2 &&
                    Ask("High can slow your PC", "High puts Roblox ahead of everything else, so Discord, OBS and your browser can stutter. Above normal is usually enough.",
                        "Use Above normal", "Use High anyway") != 1)
                {
                    priorityCombo.SelectedIndex = 1;   // runs this handler again with Above normal
                    return;
                }
                settings.Priority = level;
                settings.Save();
            };
            Add(page, SettingCard("Roblox priority", "Gives Roblox more CPU time. High can slow OBS while streaming.", priorityCombo));

            Section(page, "Recommended");
            var recommend = Secondary("Load recommended", "\uE9D9", D(170), D(30));
            recommend.Click += (_, _) => OfferRecommended(firstVisit: false);
            Add(page, SettingCard("Recommended settings",
                "Checks your PC and picks graphics settings that fit it. Only changes graphics and the FPS cap, nothing else.", recommend), 0);

            presetSlider.ValueChanged += (_, _) => { if (!syncing) ApplyPreset(presetSlider.Value); };
            presetSlider.Released += (_, _) => { if (!syncing && settings.Preset != presetSlider.Value) ApplyPreset(presetSlider.Value); };
            qualitySlider.ValueChanged += (_, _) =>
            {
                qualityValue.Text = L.T(qualitySlider.Value == 0 ? "Auto" : qualitySlider.Value.ToString());
                OnGraphicsChanged();
            };
            foreach (var combo in new[] { textureCombo, msaaCombo, rendererCombo })
                combo.SelectedIndexChanged += (_, _) => OnGraphicsChanged();
            foreach (var tg in toggles)
                tg.CheckedChanged += (_, _) => OnGraphicsChanged();
        }

        DarkCombo SetupCombo(DarkCombo c, int width, params string[] items)
        {
            c.ItemHeight = D(22);
            c.Width = width;
            c.Items.AddRange(items.Select(L.T).ToArray<object>());
            c.SelectedIndex = 0;
            return c;
        }

        static (string Title, string Desc) PresetInfo(int p) => p switch
        {
            0 => ("Max FPS", "Lowest quality, flat lighting, no grass. For weak PCs or pure frames."),
            1 => ("Performance", "Low settings that still look okay. Great for laptops."),
            2 => ("Balanced", "Roblox's normal automatic settings. Nothing changed."),
            3 => ("Quality", "Sharper textures and smoother edges. Needs a decent PC."),
            4 => ("Max Graphics", "Everything maxed out. Best looks, lowest FPS."),
            _ => ("Custom", "You changed settings yourself. Move the slider to pick a preset again."),
        };

        void UpdatePresetLabels()
        {
            var (title, desc) = PresetInfo(settings.Preset);
            presetTitle.Text = L.T(title);
            presetDesc.Text = L.T(desc);
        }

        // PC CHECK: suggests a graphics preset and FPS cap for this PC
        void OfferRecommended(bool firstVisit)
        {
            if (firstVisit)
            {
                if (settings.RecommendAsked) return;
                settings.RecommendAsked = true;
                settings.Save();
            }
            PcInfo pc;
            try { pc = PcCheck.Read(); }
            catch { pc = new PcInfo(); }
            var (preset, cap) = PcCheck.Recommend(pc);
            string presetName = L.T(preset switch { 0 => "Max FPS", 1 => "Performance", 3 => "Quality", _ => "Balanced" });
            string capText = cap > 0 ? L.F("{0} FPS (your screen's refresh rate)", cap) : L.T("Roblox default");

            var lines = new List<string>();
            if (pc.Gpu.Length > 0)
                lines.Add(pc.VramGb >= 1 ? L.F("Graphics card: {0} ({1} GB)", pc.Gpu, Math.Round(pc.VramGb)) : L.F("Graphics card: {0}", pc.Gpu));
            if (pc.RamGb > 0) lines.Add(L.F("Memory: {0} GB", Math.Round(pc.RamGb)));
            lines.Add(L.F("Processor: {0} threads", pc.Threads));
            if (pc.RefreshHz > 0) lines.Add(L.F("Screen: {0} Hz", pc.RefreshHz));
            lines.Add("");
            lines.Add(L.F("Recommended graphics: {0}", presetName));
            lines.Add(L.F("Recommended FPS cap: {0}", capText));
            lines.Add("");
            lines.Add(L.T("This only changes your graphics settings and FPS cap. Your mods, accounts and other settings stay the same."));

            int choice = Ask(firstVisit ? "Use recommended settings for your PC?" : "Recommended settings for your PC",
                string.Join("\n", lines), "Use recommended", firstVisit ? "Keep mine" : "Cancel");
            if (choice != 0) return;

            syncing = true;
            presetSlider.Value = preset;
            syncing = false;
            ApplyPreset(preset);
            settings.FpsCap = cap;
            settings.Save();
            SyncFpsCombo();
            SetStatus(L.F("Recommended settings loaded: {0} graphics.", presetName), null, StatusKind.Idle);
        }

        void ApplyPreset(int p)
        {
            // (quality, texture index, msaa index, no grass, gray sky, low meshes, freeze lighting)
            var (q, tex, msaa, grass, sky, mesh, voxel) = p switch
            {
                0 => (1, 1, 1, true, true, true, true),
                1 => (5, 2, 0, true, false, true, false),
                3 => (16, 4, 3, false, false, false, false),
                4 => (21, 4, 3, false, false, false, false),
                _ => (0, 0, 0, false, false, false, false),
            };
            syncing = true;
            qualitySlider.Value = q;
            textureCombo.SelectedIndex = tex;
            msaaCombo.SelectedIndex = msaa;
            grassToggle.Checked = grass;
            skyToggle.Checked = sky;
            meshToggle.Checked = mesh;
            voxelToggle.Checked = voxel;
            syncing = false;

            ApplyControlsToFlags();
            settings.Preset = p;
            UpdatePresetLabels();
            settings.Save();
            RefreshFlagSummary();
            RefreshHome();
            SyncPotato();
        }

        void OnGraphicsChanged()
        {
            if (syncing) return;
            ApplyControlsToFlags();
            settings.Preset = -1;
            UpdatePresetLabels();
            settings.Save();
            RefreshFlagSummary();
            RefreshHome();
            SyncPotato();
        }

        // Potato switches drive the same controls as Advanced settings, so both always agree.
        // Shows the saved FPS cap: a preset, or "Custom (N FPS)" for any other number
        void SyncFpsCombo()
        {
            fpsSyncing = true;
            int preset = Array.IndexOf(FpsCaps, settings.FpsCap);
            int custom = FpsCaps.Length;
            fpsCombo.Items[custom] = preset < 0 && settings.FpsCap > 0 ? L.F("Custom ({0} FPS)", settings.FpsCap) : L.T("Custom...");
            fpsCombo.SelectedIndex = preset >= 0 ? preset : settings.FpsCap > 0 ? custom : 0;
            fpsSyncing = false;
        }

        void ApplyPotatoToControls()
        {
            syncing = true;
            if (potatoBlur.Checked) textureCombo.SelectedIndex = 1;
            else if (textureCombo.SelectedIndex == 1) textureCombo.SelectedIndex = 0;
            grassToggle.Checked = potatoGrass.Checked;
            meshToggle.Checked = potatoMesh.Checked;
            skyToggle.Checked = potatoSky.Checked;
            if (potatoMsaa.Checked) msaaCombo.SelectedIndex = 1;
            else if (msaaCombo.SelectedIndex == 1) msaaCombo.SelectedIndex = 0;
            syncing = false;
            OnGraphicsChanged();
        }

        void SyncPotato()
        {
            potatoSyncing = true;
            potatoBlur.Checked = textureCombo.SelectedIndex == 1;
            potatoGrass.Checked = grassToggle.Checked;
            potatoMesh.Checked = meshToggle.Checked;
            potatoSky.Checked = skyToggle.Checked;
            potatoMsaa.Checked = msaaCombo.SelectedIndex == 1;
            potatoAll.Checked = potatoBlur.Checked && potatoGrass.Checked && potatoMesh.Checked && potatoSky.Checked && potatoMsaa.Checked;
            potatoSyncing = false;
        }

        void ApplyControlsToFlags()
        {
            var f = settings.Flags;
            void Set(string name, string? value) { if (value == null) f.Remove(name); else f[name] = value; }

            Set(FF.Quality, qualitySlider.Value == 0 ? null : qualitySlider.Value.ToString());
            if (textureCombo.SelectedIndex <= 0) { f.Remove(FF.TexEnabled); f.Remove(FF.Texture); }
            else { f[FF.TexEnabled] = "True"; f[FF.Texture] = (textureCombo.SelectedIndex - 1).ToString(); }
            Set(FF.Msaa, msaaCombo.SelectedIndex <= 0 ? null : MsaaValues[msaaCombo.SelectedIndex]);
            Set(FF.D3D11, rendererCombo.SelectedIndex == 1 ? "True" : null);
            Set(FF.Vulkan, rendererCombo.SelectedIndex == 2 ? "True" : null);
            Set(FF.OpenGL, rendererCombo.SelectedIndex == 3 ? "True" : null);
            Set(FF.GrassMin, grassToggle.Checked ? "0" : null);
            Set(FF.GrassMax, grassToggle.Checked ? "0" : null);
            Set(FF.GraySky, skyToggle.Checked ? "True" : null);
            foreach (var lod in FF.Lod) Set(lod, meshToggle.Checked ? "0" : null);
            Set(FF.PauseVoxelizer, voxelToggle.Checked ? "True" : null);
            Set(FF.GrassMotion, grassMotionToggle.Checked ? "0" : null);
            Set(FF.DisableDpi, dpiToggle.Checked ? "True" : null);
            Set(FF.AltEnter, fullscreenToggle.Checked ? "False" : null);
        }

        void RefreshGraphicsControls()
        {
            var f = settings.Flags;
            bool Is(string name, string value) => f.TryGetValue(name, out var v) && string.Equals(v, value, StringComparison.OrdinalIgnoreCase);

            syncing = true;
            qualitySlider.Value = f.TryGetValue(FF.Quality, out var q) && int.TryParse(q, out var qi) ? Math.Clamp(qi, 1, 21) : 0;
            qualityValue.Text = L.T(qualitySlider.Value == 0 ? "Auto" : qualitySlider.Value.ToString());
            textureCombo.SelectedIndex = Is(FF.TexEnabled, "True") && f.TryGetValue(FF.Texture, out var tx) && int.TryParse(tx, out var ti)
                ? Math.Clamp(ti, 0, 3) + 1 : 0;
            int msaaIdx = f.TryGetValue(FF.Msaa, out var ms) ? Array.IndexOf(MsaaValues, ms) : 0;
            msaaCombo.SelectedIndex = msaaIdx < 0 ? 0 : msaaIdx;
            rendererCombo.SelectedIndex = Is(FF.D3D11, "True") ? 1 : Is(FF.Vulkan, "True") ? 2 : Is(FF.OpenGL, "True") ? 3 : 0;
            grassToggle.Checked = Is(FF.GrassMax, "0");
            skyToggle.Checked = Is(FF.GraySky, "True");
            meshToggle.Checked = Is(FF.Lod[0], "0");
            voxelToggle.Checked = Is(FF.PauseVoxelizer, "True");
            grassMotionToggle.Checked = Is(FF.GrassMotion, "0");
            dpiToggle.Checked = Is(FF.DisableDpi, "True");
            fullscreenToggle.Checked = Is(FF.AltEnter, "False");
            if (settings.Preset >= 0) presetSlider.Value = settings.Preset;
            UpdatePresetLabels();
            syncing = false;
            SyncPotato();
        }
    }
}
