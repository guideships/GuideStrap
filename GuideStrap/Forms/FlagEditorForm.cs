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
    // FASTFLAG EDITOR (pop-up window)
    public class FlagEditorForm : ThemedForm
    {
        readonly Dictionary<string, string> flags;
        readonly Action changed;
        readonly FlowLayoutPanel list = new();
        readonly DarkCombo picker = new();
        readonly TextBox valueBox = new(), importBox = new(), filterBox = new();
        readonly Label pickDesc = new(), countLabel = new(), resultLabel = new();
        readonly CollapsePanel addPanel, importPanel;
        readonly int rowWidth;

        public FlagEditorForm(Dictionary<string, string> flags, Action changed)
        {
            this.flags = flags;
            this.changed = changed;
            Text = "FastFlag editor";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CloseOnEscape = true;
            ClientSize = new Size(D(760), D(580));
            int W = ClientSize.Width;
            int innerW = W - D(48);
            rowWidth = innerW - D(22);

            var bar = BuildTitleBar("FastFlag editor", minimize: false);
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(D(24), D(2), D(24), D(6)) };
            var footer = new Panel { Dock = DockStyle.Bottom, Height = D(54), BackColor = Theme.Bar };
            footer.Paint += (_, e) => { using var p = new Pen(Theme.Border); e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(bar);

            // --- header + toolbar ---
            var header = new Panel { Dock = DockStyle.Top, Height = D(102), BackColor = Theme.Bg };
            header.Controls.Add(At(Lbl("FastFlag editor", 14f, semibold: true), 0, 0));
            header.Controls.Add(At(Lbl("Only flags on Roblox's allowlist are kept. Adding one you already have asks before replacing it.", 8.75f, Theme.SubText), D(2), D(30)));

            var addBtn = Primary("Add flag", "\uE710", D(112), D(30));
            addBtn.Location = new Point(0, D(62));
            var importBtn = Secondary("Import JSON", "\uE896", D(132), D(30));
            importBtn.Location = new Point(D(120), D(62));
            var copyBtn = Secondary("Copy JSON", "\uE8C8", D(120), D(30));
            copyBtn.Location = new Point(D(260), D(62));
            var clearBtn = Secondary("Clear all", "\uE74D", D(104), D(30));
            clearBtn.HoverColor = Theme.Danger;
            clearBtn.Location = new Point(D(388), D(62));
            var filter = InputBox(filterBox, "Filter", D(170), "\uE721");
            filter.Location = new Point(innerW - D(170), D(62));
            header.Controls.AddRange(new Control[] { addBtn, importBtn, copyBtn, clearBtn, filter });

            // --- add panel ---
            addPanel = new CollapsePanel(innerW, D(100), D(8));
            var ac = addPanel.Inner;
            foreach (var a in FF.Allowed) picker.Items.Add(a.Name);
            picker.ItemHeight = D(22);
            picker.Width = D(360);
            picker.Location = new Point(D(16), D(16));
            picker.SelectedIndex = 0;
            var valueWrap = InputBox(valueBox, "Value", D(160));
            valueWrap.Location = new Point(D(388), D(14));
            var addConfirm = Primary("Add", "\uE710", D(96), D(30));
            addConfirm.Location = new Point(innerW - D(112), D(14));
            pickDesc.AutoSize = true;
            pickDesc.ForeColor = Theme.SubText;
            pickDesc.BackColor = Color.Transparent;
            pickDesc.Font = Theme.F("Segoe UI", 8.75f);
            pickDesc.Location = new Point(D(16), D(52));
            pickDesc.Text = L.T(FF.Describe(picker.SelectedItem as string));
            var addHint = At(Lbl("Tip: press Enter in the value box to add.", 8f, Theme.SubText), D(16), D(73));
            ac.Controls.AddRange(new Control[] { picker, valueWrap, addConfirm, pickDesc, addHint });

            // --- import panel ---
            importPanel = new CollapsePanel(innerW, D(200), D(8));
            var ic = importPanel.Inner;
            ic.Controls.Add(At(Lbl("Paste FastFlags JSON", 9.75f), D(16), D(10)));
            var importWrap = new Card
            {
                BackColor = Theme.Input, Radius = 6, Padding = new Padding(D(12), D(8), D(4), D(8)),
                Location = new Point(D(16), D(34)), Size = new Size(innerW - D(32), D(114)),
            };
            importBox.Multiline = true;
            importBox.AcceptsReturn = true;
            importBox.ScrollBars = ScrollBars.Vertical;
            importBox.BorderStyle = BorderStyle.None;
            importBox.BackColor = Theme.Input;
            importBox.ForeColor = Theme.Text;
            importBox.Font = new Font("Consolas", 9f);
            importBox.Dock = DockStyle.Fill;
            importBox.PlaceholderText = "{\n  \"DFIntTextureQualityOverride\": \"0\",\n  \"FFlagDebugSkyGray\": \"True\"\n}";
            importWrap.Controls.Add(importBox);
            var importConfirm = Primary("Import", "\uE896", D(104), D(30));
            importConfirm.Location = new Point(innerW - D(120), D(158));
            ic.Controls.Add(importWrap);
            ic.Controls.Add(At(Lbl("Flags that aren't on Roblox's allowlist are skipped.", 8f, Theme.SubText), D(16), D(164)));
            ic.Controls.Add(importConfirm);

            // --- list ---
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.BackColor = Theme.Bg;
            Theme.DarkScroll(list);

            body.Controls.Add(list);
            body.Controls.Add(importPanel);
            body.Controls.Add(addPanel);
            body.Controls.Add(header);

            // --- footer ---
            countLabel.AutoSize = true;
            countLabel.ForeColor = Theme.Text;
            countLabel.BackColor = Color.Transparent;
            countLabel.Font = Theme.F("Segoe UI Semibold", 9.5f);
            countLabel.Location = new Point(D(24), D(17));
            resultLabel.AutoSize = false;
            resultLabel.Size = new Size(W - D(280), D(20));
            resultLabel.ForeColor = Theme.Gold;
            resultLabel.BackColor = Color.Transparent;
            resultLabel.Font = Theme.F("Segoe UI", 9f);
            resultLabel.Location = new Point(D(110), D(18));
            resultLabel.AutoEllipsis = true;
            var done = Primary("Done", "", D(96), D(30));
            done.Location = new Point(W - D(120), D(12));
            done.Click += (_, _) => Close();
            footer.Controls.AddRange(new Control[] { countLabel, resultLabel, done });

            // --- events ---
            addBtn.Click += (_, _) =>
            {
                importPanel.Collapse();
                addPanel.Toggle();
                if (addPanel.IsOpen) valueBox.Focus();
            };
            importBtn.Click += (_, _) =>
            {
                addPanel.Collapse();
                importPanel.Toggle();
                if (importPanel.IsOpen) importBox.Focus();
            };
            copyBtn.Click += (_, _) =>
            {
                if (flags.Count == 0) { ShowResult("Nothing to copy yet."); return; }
                Clipboard.SetText(JsonSerializer.Serialize(flags.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value),
                    new JsonSerializerOptions { WriteIndented = true }));
                ShowResult($"Copied {flags.Count} flags to your clipboard.");
            };
            clearBtn.Click += (_, _) =>
            {
                if (flags.Count == 0) return;
                if (Ask("Clear every flag?", $"This removes all {flags.Count} flags (and resets Graphics to Balanced).", "Clear all", "Cancel") != 0) return;
                flags.Clear();
                changed();
                RefreshList();
                ShowResult("All flags cleared.");
            };
            filterBox.TextChanged += (_, _) => RefreshList();
            picker.SelectedIndexChanged += (_, _) => pickDesc.Text = L.T(FF.Describe(picker.SelectedItem as string));
            addConfirm.Click += (_, _) => AddFromPicker();
            valueBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddFromPicker(); } };
            importConfirm.Click += (_, _) => ImportJson();

            RefreshList();
            Activated += (_, _) => Presence.Screen("Editing settings", "FastFlag editor");
        }

        void ShowResult(string text)
        {
            resultLabel.Text = L.T(text);
            Anim.Run((resultLabel, "flash"), 0f, 1f, 500, v => resultLabel.ForeColor = Theme.Mix(Theme.Bar, Theme.Gold, v));
        }

        void AddFromPicker()
        {
            string? name = picker.SelectedItem as string;
            string value = valueBox.Text.Trim();
            if (name == null) return;
            if (value.Length == 0) { valueBox.Focus(); ShowResult("Type a value first."); return; }
            var r = Merge(new List<(string, string)> { (name, value) });
            valueBox.Clear();
            ShowResult(r.Summary(0));
        }

        void ImportJson()
        {
            string text = importBox.Text.Trim();
            if (text.Length == 0) { ShowResult("Paste some JSON first."); return; }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
            }
            catch (JsonException)
            {
                Ask("That isn't valid JSON", "Make sure it looks like { \"FlagName\": \"Value\", ... } with quotes around names.", "OK");
                return;
            }
            if (root is not JsonObject obj)
            {
                Ask("That isn't a flag list", "The JSON should be an object like { \"FlagName\": \"Value\" }.", "OK");
                return;
            }

            var incoming = new List<(string, string)>();
            int skipped = 0;
            foreach (var (key, node) in obj)
            {
                if (!FF.IsAllowed(key)) { skipped++; continue; }
                string val = node switch
                {
                    JsonValue jv when jv.TryGetValue<bool>(out var b) => b ? "True" : "False",
                    JsonValue jv when jv.TryGetValue<string>(out var s) => s,
                    null => "",
                    _ => node.ToJsonString(),
                };
                if (val.Trim().Length == 0) { skipped++; continue; }
                incoming.Add((key, val.Trim()));
            }

            var r = Merge(incoming);
            ShowResult(r.Summary(skipped));
            if (r.Added + r.Updated > 0 || skipped > 0) importBox.Clear();
            if (r.Added + r.Updated > 0) importPanel.Collapse();
        }

        record MergeResult(int Added, int Updated, int Kept, int Same)
        {
            public string Summary(int skipped)
            {
                var parts = new List<string>();
                if (Added > 0) parts.Add(L.F("Added {0}", Added));
                if (Updated > 0) parts.Add(L.F("Overwrote {0}", Updated));
                if (Kept > 0) parts.Add(L.F("Kept {0} original", Kept));
                if (Same > 0) parts.Add(L.F("{0} already the same", Same));
                if (skipped > 0) parts.Add(L.F("Skipped {0} not on the allowlist", skipped));
                return parts.Count == 0 ? L.T("Nothing changed.") : string.Join("  •  ", parts);
            }
        }

        // Adds flags; if one already exists with a different value, asks: overwrite or keep original.
        MergeResult Merge(List<(string Name, string Value)> incoming)
        {
            int added = 0, updated = 0, kept = 0, same = 0;
            int conflictsLeft = incoming.Count(i => flags.TryGetValue(i.Name, out var cur) && cur != i.Value);
            bool? applyAll = null;
            var touched = new HashSet<string>();

            foreach (var (name, value) in incoming)
            {
                if (!flags.TryGetValue(name, out var current))
                {
                    flags[name] = value;
                    added++;
                    touched.Add(name);
                    continue;
                }
                if (current == value) { same++; continue; }

                bool overwrite;
                if (applyAll.HasValue) overwrite = applyAll.Value;
                else
                {
                    using var dlg = new ThemedDialog(
                        "This flag is already in your list",
                        $"{name}\n\nCurrent value:   {current}\nNew value:   {value}\n\nDo you want to overwrite it with the new value, or keep the original?",
                        new[] { "Overwrite", "Keep original" },
                        conflictsLeft > 1 ? $"Do the same for the other {conflictsLeft - 1} duplicate{(conflictsLeft - 1 == 1 ? "" : "s")}" : null);
                    dlg.ShowDialog(this);
                    overwrite = dlg.Choice == 0;
                    if (dlg.Checked) applyAll = overwrite;
                }
                conflictsLeft--;

                if (overwrite) { flags[name] = value; updated++; touched.Add(name); }
                else kept++;
            }

            if (added + updated > 0) changed();
            RefreshList(touched);
            return new MergeResult(added, updated, kept, same);
        }

        void RefreshList(HashSet<string>? highlight = null)
        {
            list.SuspendLayout();
            foreach (var c in list.Controls.Cast<Control>().ToList()) c.Dispose();

            string f = filterBox.Text.Trim();
            var items = flags.Where(kv => f.Length == 0 || kv.Key.Contains(f, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(kv => kv.Key).ToList();

            if (items.Count == 0)
            {
                var empty = Lbl(flags.Count == 0 ? "No flags yet. Press \"Add flag\" or \"Import JSON\" to get started." : "No flags match your filter.", 10f, Theme.SubText);
                empty.Margin = new Padding(D(2), D(12), 0, 0);
                list.Controls.Add(empty);
            }

            foreach (var (name, value) in items)
            {
                string key = name;
                var row = new Card { Size = new Size(rowWidth, D(50)), Margin = new Padding(0, 0, 0, D(6)) };
                row.Controls.Add(At(Lbl(name, 9.25f, semibold: true), D(14), D(7)));
                row.Controls.Add(At(Lbl(FF.Describe(name), 8f, Theme.SubText), D(14), D(27)));

                var tb = new TextBox();
                var wrap = InputBox(tb, "", D(140));
                tb.Text = value;
                wrap.Location = new Point(rowWidth - D(196), D(10));
                void Commit()
                {
                    string v = tb.Text.Trim();
                    if (v.Length == 0 || !flags.ContainsKey(key) || flags[key] == v) return;
                    flags[key] = v;
                    changed();
                    ShowResult($"Updated {key}.");
                }
                tb.Leave += (_, _) => Commit();
                tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Commit(); } };

                var del = Secondary("", "\uE74D", D(36), D(30));
                del.HoverColor = Theme.Danger;
                del.Location = new Point(rowWidth - D(48), D(10));
                del.Click += (_, _) => Later(() =>
                {
                    flags.Remove(key);
                    changed();
                    RefreshList();
                    ShowResult($"Removed {key}.");
                });

                row.Controls.Add(wrap);
                row.Controls.Add(del);
                list.Controls.Add(row);

                if (highlight != null && highlight.Contains(key))
                    Anim.Run((row, "glow"), 1f, 0f, 1600, v => { row.BorderColor = Theme.Mix(Theme.Border, Theme.Gold, v); row.Invalidate(); });
            }

            countLabel.Text = L.T($"{flags.Count} flag{(flags.Count == 1 ? "" : "s")}");
            list.ResumeLayout();
        }
    }
}
