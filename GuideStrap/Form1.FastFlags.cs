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
        // FASTFLAGS
        void BuildFlagsPage()
        {
            var page = NewPage("Flags", "FastFlags", "Flags from Roblox's allowlist.");

            var open = Primary("Open editor", "\uE70F", D(132), D(30));
            open.Click += (_, _) => OpenFlagEditor();
            Add(page, SettingCard("FastFlag editor", "Add, import or edit flags.", open), 8);

            Section(page, "Active flags");
            flagPreviewCard = new Card { Size = new Size(CW, D(70)), Cursor = Cursors.Hand };
            flagPreview.AutoSize = true;
            flagPreview.MaximumSize = new Size(CW - D(32), 0);
            flagPreview.Font = new Font("Consolas", 9f);
            flagPreview.BackColor = Color.Transparent;
            flagPreview.Location = new Point(D(16), D(12));
            flagPreview.Cursor = Cursors.Hand;
            flagPreviewCard.Controls.Add(flagPreview);
            flagPreviewCard.Click += (_, _) => OpenFlagEditor();
            flagPreview.Click += (_, _) => OpenFlagEditor();
            Add(page, flagPreviewCard, 8);

            var info = Lbl("Roblox only reads flags on its allowlist, so anything else gets skipped automatically.", 8.5f, Theme.SubText);
            Add(page, info, 0);
        }

        void RefreshFlagSummary()
        {
            int count = settings.Flags.Count;
            if (count == 0)
            {
                flagPreview.Text = "No flags yet. Pick a graphics preset or open the editor.";
                flagPreview.ForeColor = Theme.SubText;
            }
            else
            {
                var lines = settings.Flags.OrderBy(k => k.Key).Take(10).Select(kv => $"{kv.Key} = {kv.Value}").ToList();
                if (count > 10) lines.Add(L.F("+ {0} more", count - 10));
                flagPreview.Text = string.Join("\n", lines);
                flagPreview.ForeColor = Theme.Text;
            }
            flagPreviewCard.Height = flagPreview.GetPreferredSize(new Size(CW - D(32), 0)).Height + D(24);
        }

        void OpenFlagEditor()
        {
            using var editor = new FlagEditorForm(settings.Flags, () =>
            {
                settings.Preset = settings.Flags.Count == 0 ? 2 : -1;
                settings.Save();
                RefreshGraphicsControls();
                RefreshFlagSummary();
                RefreshHome();
            });
            editor.ShowDialog(this);
        }
    }
}
