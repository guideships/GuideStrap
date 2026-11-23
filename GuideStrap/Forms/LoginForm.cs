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
    // LOGIN WINDOW (the real Roblox login page inside WebView2)
    class LoginForm : Form
    {
        readonly WebView2 web = new();
        readonly System.Windows.Forms.Timer poll = new() { Interval = 1000 };
        readonly string dataDir = Path.Combine(Path.GetTempPath(), "GuideStrapLogin_" + Guid.NewGuid().ToString("N"));
        bool done;
        public string? Cookie { get; private set; }

        public LoginForm()
        {
            Text = "Log in to Roblox - GuideStrap";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(LogicalToDeviceUnits(520), LogicalToDeviceUnits(760));
            BackColor = Theme.Bg;
            web.Dock = DockStyle.Fill;
            Controls.Add(web);

            Load += async (_, _) => await InitAsync();
            poll.Tick += async (_, _) => await CheckCookieAsync();
            FormClosed += (_, _) =>
            {
                poll.Stop();
                web.Dispose();
                try { Directory.Delete(dataDir, true); } catch { }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int dark = 1; NativeMethods.DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); } catch { }
        }

        async Task InitAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
                await web.EnsureCoreWebView2Async(env);
                web.CoreWebView2.Navigate("https://www.roblox.com/login");
                poll.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The login window needs Microsoft Edge WebView2.\n\n" + ex.Message, "GuideStrap");
                Close();
            }
        }

        async Task CheckCookieAsync()
        {
            if (done || web.CoreWebView2 == null) return;
            var cookies = await web.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
            var c = cookies.FirstOrDefault(x => x.Name == ".ROBLOSECURITY");
            if (c == null || string.IsNullOrEmpty(c.Value)) return;
            done = true;
            Cookie = c.Value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
