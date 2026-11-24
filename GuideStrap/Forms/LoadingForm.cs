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
    // Loading screen shown while Roblox is updated and started
    class LoadingForm : ThemedForm
    {
        readonly GuideStrapContext context;
        readonly AppSettings settings;
        readonly List<Account> accounts;
        readonly string? launchUri;
        readonly string placeText;
        readonly string? jobId;
        readonly ServerPick pick;
        readonly SpinningLogo logo = new();
        readonly Label status = new();
        readonly LoadingBar bar = new();
        readonly RoundButton cancel;
        readonly CancellationTokenSource cts = new();
        bool finished;

        public LoadingForm(GuideStrapContext context, AppSettings settings, List<Account> accounts, string? launchUri, string placeText,
            string? jobId = null, ServerPick pick = ServerPick.None)
        {
            this.context = context;
            this.jobId = jobId;
            this.pick = pick;
            this.settings = settings;
            this.accounts = accounts;
            this.launchUri = launchUri;
            this.placeText = placeText;
            Text = "GuideStrap";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(D(540), D(284));
            int W = ClientSize.Width;

            var acc = accounts.FirstOrDefault(a => a.UserId == settings.ActiveAccountId);
            string who = settings.StreamerMode ? L.T("hidden") : acc != null ? acc.Username : L.T("Roblox login");
            var left = At(Lbl(L.F("Account: {0}", who), 8.25f, Theme.SubText), D(12), D(10));
            var right = new Label
            {
                Text = $"GuideStrap {AppInfo.Version}", AutoSize = false, TextAlign = ContentAlignment.TopRight,
                Size = new Size(D(200), D(18)), Location = new Point(W - D(212), D(10)),
                Font = Theme.F("Segoe UI", 8.25f), ForeColor = Theme.SubText, BackColor = Theme.Bg,
            };

            logo.Size = new Size(D(112), D(112));
            logo.Location = new Point((W - logo.Width) / 2, D(30));

            status.AutoSize = false;
            status.TextAlign = ContentAlignment.MiddleCenter;
            status.Size = new Size(W - D(40), D(30));
            status.Location = new Point(D(20), D(148));
            status.Font = Theme.F("Segoe UI", 13f);
            status.ForeColor = Theme.Text;
            status.BackColor = Theme.Bg;
            status.Text = L.T("Starting...");

            bar.Location = new Point(D(36), D(194));
            bar.Size = new Size(W - D(72), D(8));

            cancel = Secondary("Cancel", "", D(120), D(30));
            cancel.Location = new Point((W - cancel.Width) / 2, D(226));
            cancel.Click += (_, _) => Cancel();

            Controls.AddRange(new Control[] { left, right, logo, status, bar, cancel });
            // your custom picture behind the loading screen (Settings > Appearance)
            if (settings.BackgroundOnLoader && ApplyBackdrop(settings.LauncherBackground) != null)
            {
                right.BackColor = Color.Transparent;
                status.BackColor = Color.Transparent;
            }
            foreach (Control c in new Control[] { left, right, logo, status }) c.MouseDown += DragWindow;
            CloseOnEscape = true;

            Shown += async (_, _) => { Presence.Screen("Launching Roblox"); await RunAsync(); };
            FormClosing += (_, _) => { if (!finished) cts.Cancel(); };
        }

        void Cancel()
        {
            if (finished) return;
            cancel.Enabled = false;
            status.Text = L.T("Cancelling...");
            cts.Cancel();
        }

        void Report(string text, float progress)
        {
            if (finished || IsDisposed) return;
            status.Text = L.T(text);
            if (progress < 0) bar.Indeterminate = true;
            else
            {
                bar.Indeterminate = false;
                bar.Value = progress;
            }
        }

        async Task RunAsync()
        {
            logo.Spinning = true;
            bar.Indeterminate = true;
            try
            {
                Report("Checking Roblox's servers...", -1f);
                while (true)
                {
                    var health = await RobloxStatus.CheckAsync();
                    if (finished || IsDisposed) return;
                    if (health == RobloxStatus.Result.Offline)
                    {
                        if (Ask("You're offline", "GuideStrap can't reach the internet. Check your Wi-Fi or cable, then try again.", "Try again", "Cancel") == 0) continue;
                        Finish(false);
                        return;
                    }
                    if (health == RobloxStatus.Result.Down)
                    {
                        int choice = Ask("Roblox looks down", "Roblox's servers aren't answering right now, so games might not load. This is on Roblox's side, not your PC.",
                            "Try anyway", "Status page", "Cancel");
                        if (choice == 1) Process.Start(new ProcessStartInfo("https://status.roblox.com") { UseShellExecute = true });
                        if (choice != 0) { Finish(false); return; }
                    }
                    break;
                }

                // run the heavy work (download, unzip, file writes) off the UI thread so the animation stays smooth
                void SafeReport(string t, float p)
                {
                    if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(() => Report(t, p)));
                }
                var token = cts.Token;
                if (settings.MultiInstance) MultiInstance.Hold();
                var proc = await Task.Run(() => RobloxLauncher.LaunchAsync(settings, accounts, launchUri, placeText, SafeReport, token, jobId, pick));
                cancel.Enabled = false;
                if (proc == null)
                {
                    status.Text = L.T("Opened the game page in your browser");
                    await Task.Delay(1500);
                }
                else
                {
                    // keep the loading screen up until Roblox's window shows (max ~15s)
                    for (int i = 0; i < 60; i++)
                    {
                        await Task.Delay(250);
                        try
                        {
                            proc.Refresh();
                            if (proc.HasExited || proc.MainWindowHandle != IntPtr.Zero) break;
                        }
                        catch { break; }
                    }
                    CrashInfo? early = null;
                    try { if (proc.HasExited) early = CrashWatch.FromExitCode(unchecked((uint)proc.ExitCode)); }
                    catch { }
                    if (early != null)
                    {
                        if (settings.CrashHelper) context.ShowCrash(early);
                    }
                    else if (settings.RunInBackground || settings.MultiInstance)
                        context.WatchRoblox();
                }
                Finish(proc != null);
            }
            catch (OperationCanceledException)
            {
                Finish(false);
            }
            catch (Exception ex)
            {
                logo.Spinning = false;
                bar.Indeterminate = false;
                Ask("Couldn't launch Roblox", ex.Message, "OK");
                Finish(false);
            }
        }

        void Finish(bool launched)
        {
            if (finished) return;
            finished = true;
            logo.Spinning = false;
            bar.Indeterminate = false;
            // nothing else open and Roblox didn't start: go back to the start screen
            if (!launched && context.OpenCount <= 1) context.Open(new LauncherForm(context));
            Close();
        }
    }
}
