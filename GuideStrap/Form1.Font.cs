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
        // TEXT & FONT
        void BuildFontPage()
        {
            // lives inside the Mods tab, above "Your own mods"
            var page = pages["Mods"];
            int insertAt = page.Controls.GetChildIndex(ownModsSection);
            int before = page.Controls.Count;
            var section = Section(page, "Text fonts");
            merged["Font"] = ("Mods", section);

            var card = new Card { Size = new Size(CW, D(206)) };
            card.Controls.Add(At(Lbl("Current font", 8.5f, Theme.SubText), D(16), D(12)));
            fontName.AutoSize = true;
            fontName.Font = Theme.F("Segoe UI Semibold", 12.5f);
            fontName.ForeColor = Theme.Text;
            fontName.BackColor = Color.Transparent;
            fontName.Location = new Point(D(14), D(27));

            var previewWrap = new Card
            {
                BackColor = Theme.Input, Radius = 6,
                Location = new Point(D(16), D(60)), Size = new Size(CW - D(32), D(88)),
            };
            fontPreview.AutoSize = false;
            fontPreview.Location = new Point(D(14), D(8));
            fontPreview.Size = new Size(previewWrap.Width - D(28), D(72));
            fontPreview.ForeColor = Theme.Text;
            fontPreview.BackColor = Color.Transparent;
            fontPreview.Text = "The quick brown fox jumps over the lazy dog\n0123456789  !?  Hello from GuideStrap";
            previewWrap.Controls.Add(fontPreview);

            var choose = Primary("Choose font file", "\uE8E5", D(168), D(30));
            choose.Location = new Point(D(16), D(162));
            choose.Click += (_, _) => ChooseFont();
            var reset = Secondary("Use default font", "\uE72C", D(160), D(30));
            reset.Location = new Point(D(192), D(162));
            reset.Click += (_, _) =>
            {
                settings.FontFile = null;
                settings.FontDisplayName = null;
                settings.Save();
                RefreshFontPreview();
                RefreshHome();
                SetStatus("Font reset to Roblox's default.", null, StatusKind.Idle);
            };
            card.Controls.AddRange(new Control[] { fontName, previewWrap, choose, reset });
            Add(page, card, 8);

            var tip = Lbl("Works with .ttf and .otf files (free fonts: fonts.google.com). Emoji keep their normal look. Applies next time you press Play.", 8.5f, Theme.SubText);
            tip.AutoSize = false;
            tip.Size = new Size(CW, D(36));
            Add(page, tip, 0);

            // move the new controls (section, card, tip) above "Your own mods"
            var added = page.Controls.Cast<Control>().Skip(before).ToList();
            foreach (var c in added) page.Controls.SetChildIndex(c, insertAt++);
        }

        void ChooseFont()
        {
            using var dlg = new OpenFileDialog { Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf", Title = L.T("Pick a font") };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                fontPreview.Font = Theme.F("Segoe UI", 14f);
                previewFonts?.Dispose();
                previewFonts = null;

                Directory.CreateDirectory(Paths.Base);
                foreach (var old in Directory.GetFiles(Paths.Base, "CustomFont.*"))
                    try { File.Delete(old); } catch { }

                string dest = Path.Combine(Paths.Base, "CustomFont" + Path.GetExtension(dlg.FileName).ToLowerInvariant());
                File.Copy(dlg.FileName, dest, true);
                settings.FontFile = dest;
                settings.FontDisplayName = Path.GetFileNameWithoutExtension(dlg.FileName);
                settings.Save();
                SetStatus($"Font set to {settings.FontDisplayName}.", null, StatusKind.Idle);
            }
            catch (Exception ex)
            {
                Ask("Couldn't use that font", ex.Message, "OK");
            }
            RefreshFontPreview();
            RefreshHome();
        }

        void RefreshFontPreview()
        {
            fontPreview.Font = Theme.F("Segoe UI", 14f);
            previewFonts?.Dispose();
            previewFonts = null;

            if (settings.FontFile == null || !File.Exists(settings.FontFile))
            {
                fontName.Text = L.T("Roblox default");
                return;
            }
            fontName.Text = L.T(settings.FontDisplayName ?? "Custom font");
            try
            {
                previewFonts = new PrivateFontCollection();
                previewFonts.AddFontFile(settings.FontFile);
                var fam = previewFonts.Families[0];
                var style = fam.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
                fontPreview.Font = new Font(fam, 14f, style);
            }
            catch
            {
                fontName.Text += L.T("  (no preview, still works in-game)");
            }
        }
    }
}
