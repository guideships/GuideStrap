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
    // "Roblox crashed" window: the error, then fixes in order, with one-click buttons where possible
    class CrashForm : ThemedForm
    {
        sealed record Fix(string Title, string Desc, string? Button, Func<string?>? Run);

        public CrashForm(CrashInfo info)
        {
            StartPosition = FormStartPosition.CenterScreen;
            CloseOnEscape = true;
            Text = L.T("Roblox crashed");

            int w = D(480), pad = D(20);
            var bar = BuildTitleBar("Roblox crashed", false);
            int y = D(36) + D(8);
            Controls.Add(At(Lbl(info.WhileStarting ? "Roblox crashed while starting" : "Roblox crashed", 13f, semibold: true), pad, y));
            y += D(32);
            Controls.Add(At(Lbl(L.F("Error {0}: {1}", $"0x{info.Code:X8}", L.T(CodeName(info.Code))), 9f, Theme.SubText), pad, y));
            y += D(22);
            Controls.Add(At(Lbl("Try these fixes, starting with the first one.", 9f, Theme.SubText), pad, y));
            y += D(30);

            var fixes = BuildFixes(info);
            var list = Theme.DarkScroll(new Panel { Location = new Point(pad, y), Width = w - pad * 2, AutoScroll = true, BackColor = Theme.Bg });
            int total = fixes.Sum(f => { using var probe = FixCard(f, list.Width); return probe.Height + D(8); });
            int cardW = total > D(400) ? list.Width - SystemInformation.VerticalScrollBarWidth - D(4) : list.Width;
            int ly = 0;
            foreach (var fix in fixes)
            {
                var card = FixCard(fix, cardW);
                card.Location = new Point(0, ly);
                list.Controls.Add(card);
                ly += card.Height + D(8);
            }
            list.Height = Math.Min(ly, D(400));
            Controls.Add(list);
            y += list.Height + D(8);

            var logs = Secondary("Open Roblox logs", "\uE8B7", D(160), D(30));
            logs.Location = new Point(pad, y);
            logs.Click += (_, _) =>
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
                if (Directory.Exists(dir)) Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
            };
            var close = Primary("Close", "", D(96), D(30));
            close.Location = new Point(w - pad - D(96), y);
            close.Click += (_, _) => Close();
            Controls.Add(logs);
            Controls.Add(close);
            y += D(30) + pad;

            Controls.Add(bar);
            ClientSize = new Size(w, y);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        static string CodeName(uint code) => code switch
        {
            0xC0000005 => "memory access error",
            0xC0000409 => "Roblox stopped itself (security check)",
            0xC00000FD => "stack overflow",
            0xC0000374 => "memory corruption",
            0xC0000017 or 0xC000012D => "out of memory",
            0xE06D7363 => "unhandled error",
            0x887A0005 or 0x887A0006 or 0x887A0007 => "the graphics driver stopped responding",
            0x80000003 => "internal error",
            _ => "unknown error",
        };

        Card FixCard(Fix fix, int width)
        {
            var card = new Card { Size = new Size(width, D(60)) };
            int btnW = 0;
            if (fix.Button != null)
                btnW = Math.Max(D(96), TextRenderer.MeasureText(L.T(fix.Button), Theme.F("Segoe UI Semibold", 9f)).Width + D(30));
            int textW = width - D(32) - (btnW > 0 ? btnW + D(12) : 0);

            var title = Lbl(fix.Title, 9.75f, semibold: true);
            title.Location = new Point(D(16), D(10));
            var desc = Lbl(fix.Desc, 8.75f, Theme.SubText);
            desc.AutoSize = false;
            var size = TextRenderer.MeasureText(desc.Text, desc.Font, new Size(textW, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            desc.Size = new Size(textW, size.Height + D(2));
            desc.Location = new Point(D(16), D(32));
            card.Height = Math.Max(D(60), desc.Bottom + D(12));
            card.Controls.Add(title);
            card.Controls.Add(desc);

            if (fix.Button != null && fix.Run != null)
            {
                var b = Secondary(fix.Button, "", btnW, D(30));
                b.Location = new Point(width - D(16) - btnW, (card.Height - D(30)) / 2);
                b.Click += (_, _) =>
                {
                    string? result = fix.Run();
                    if (result == null) return;
                    b.Text = L.T(result);
                    b.Enabled = false;
                };
                card.Controls.Add(b);
            }
            return card;
        }

        static void ChangeSettings(Action<AppSettings> change)
        {
            var open = Application.OpenForms.OfType<Form1>().FirstOrDefault();
            if (open != null) { open.ChangeSettings(change); return; }
            var s = AppSettings.Load();
            change(s);
            s.Save();
        }

        static bool HasMods(AppSettings s)
        {
            if (s.DeathSoundFile != null || s.FontFile != null || s.CustomSky || s.SoundPack != null ||
                s.CursorPreset != "default" || s.CrosshairPreset != "default" || s.FootstepSound != "default" || s.JumpSound != "default")
                return true;
            try { return Directory.Exists(Paths.Mods) && Directory.EnumerateFiles(Paths.Mods, "*", SearchOption.AllDirectories).Any(); }
            catch { return false; }
        }

        List<Fix> BuildFixes(CrashInfo info)
        {
            var s = AppSettings.Load();
            var fixes = new List<Fix>();
            uint c = info.Code;
            bool gpu = info.Clue == "gpu" || c is 0x887A0005 or 0x887A0006 or 0x887A0007;
            bool memory = info.Clue == "memory" || c is 0xC0000017 or 0xC000012D;
            bool security = c == 0xC0000409;
            var last = LastServer.Load();
            if (last != null && DateTime.Now - last.Time < TimeSpan.FromMinutes(20) && GuideStrapContext.Current is GuideStrapContext ctx)
                fixes.Add(new Fix("Rejoin your server", "Jump straight back into the server you were in, if it's still running.", "Rejoin", () =>
                {
                    ctx.Open(new LoadingForm(ctx, AppSettings.Load(), AccountStore.Load(), null, last.PlaceId.ToString(), last.JobId));
                    Close();
                    return null;
                }));
            var overlays = new Fix("Close overlays and macro tools",
                "Roblox closes itself when another program hooks into it, like FPS unlockers, macros, cheats or some recording overlays.", null, null);

            if (gpu || c == 0xC0000005)
            {
                bool onD3D = s.Flags.TryGetValue(FF.D3D11, out var v) && v.Equals("True", StringComparison.OrdinalIgnoreCase);
                fixes.Add(new Fix("Switch the rendering API",
                    onD3D ? "Your graphics driver may not like Direct3D 11. Try Vulkan next time." : "Your graphics driver may not like the current one. Try Direct3D 11 next time.",
                    onD3D ? "Use Vulkan" : "Use D3D11", () =>
                    {
                        ChangeSettings(x =>
                        {
                            x.Flags.Remove(FF.D3D11);
                            x.Flags.Remove(FF.Vulkan);
                            x.Flags.Remove(FF.OpenGL);
                            x.Flags[onD3D ? FF.Vulkan : FF.D3D11] = "True";
                            x.Preset = -1;
                        });
                        return "Done";
                    }));
            }
            if (memory)
                fixes.Add(new Fix("Free up memory", "Close browser tabs and other games, and pick a lower preset in Graphics.", null, null));
            if (security)
                fixes.Add(overlays);
            if (HasMods(s))
                fixes.Add(new Fix("Launch once without mods",
                    "A mod or custom font can break after a Roblox update. Your mods stay saved and come back the launch after.", "Skip mods", () =>
                    {
                        try
                        {
                            Directory.CreateDirectory(Paths.Base);
                            File.WriteAllText(Paths.SkipModsMarker, "");
                            return "Done";
                        }
                        catch { return null; }
                    }));
            if (s.Flags.Count > 0)
                fixes.Add(new Fix("Reset FastFlags",
                    "A flag can stop working after a Roblox update. This clears your flags and sets Graphics back to Balanced.", "Reset flags", () =>
                    {
                        ChangeSettings(x => { x.Flags.Clear(); x.Preset = 2; });
                        return "Done";
                    }));
            if (s.MultiInstance)
                fixes.Add(new Fix("Turn off multiple Roblox windows", "Running more than one Roblox at once can make it crash.", "Turn off", () =>
                {
                    ChangeSettings(x => x.MultiInstance = false);
                    return "Done";
                }));
            fixes.Add(new Fix("Update your graphics driver",
                "Get the newest driver from NVIDIA, AMD or Intel. Old drivers are the most common cause of crashes.", null, null));
            fixes.Add(new Fix("Repair Roblox",
                "Downloads a fresh copy of Roblox next time you launch. Fixes broken or half-updated files.", "Repair", () =>
                {
                    try
                    {
                        if (Directory.Exists(Paths.Versions)) Directory.Delete(Paths.Versions, true);
                        return "Done";
                    }
                    catch
                    {
                        Ask("Close Roblox first", "Roblox is still using its files. Close every Roblox window, then press Repair again.", "OK");
                        return null;
                    }
                }));
            if (!security)
                fixes.Add(overlays);
            return fixes;
        }
    }
}
