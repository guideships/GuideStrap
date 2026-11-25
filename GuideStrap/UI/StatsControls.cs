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
    // Seven small bars: how long you played on each of the last 7 days (today on the right)
    public class WeekBars : Control
    {
        double[] values = new double[7];
        string[] labels = new string[7];

        public WeekBars()
        {
            SetStyle(Theme.PaintStyles, true);
            Font = Theme.F("Segoe UI", 7.5f);
        }

        public void SetData(double[] v, string[] l)
        {
            values = v;
            labels = l;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color bg = Theme.SolidBack(this, Theme.Card);
            Theme.ClearToParent(this, g, bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int n = values.Length;
            if (n == 0) return;

            int labelH = LogicalToDeviceUnits(16);
            float slot = Width / (float)n;
            float barW = Math.Min(slot * 0.55f, LogicalToDeviceUnits(22));
            float maxH = Height - labelH - LogicalToDeviceUnits(4);
            if (maxH <= 2) return;
            double max = Math.Max(values.Max(), 1);
            for (int i = 0; i < n; i++)
            {
                float cx = slot * i + slot / 2;
                float h = Math.Max((float)(values[i] / max) * maxH, LogicalToDeviceUnits(3));
                var r = new RectangleF(cx - barW / 2, maxH - h, barW, h);
                using (var path = Theme.Round(r, Math.Min(barW / 2, LogicalToDeviceUnits(4))))
                using (var b = new SolidBrush(i == n - 1 ? Theme.AccentLight : Theme.Track))
                    g.FillPath(b, path);
                if (i < labels.Length && labels[i] != null)
                    TextRenderer.DrawText(g, labels[i], Font, new Rectangle((int)(slot * i), Height - labelH, (int)slot, labelH), Theme.SubText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
        }
    }
}
