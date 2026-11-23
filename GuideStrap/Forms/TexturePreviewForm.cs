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
    // Shows a made-up brick wall and grass tile, normal vs blurry textures
    class TexturePreviewForm : ThemedForm
    {
        readonly List<Bitmap> images = new();

        public TexturePreviewForm()
        {
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CloseOnEscape = true;
            Text = L.T("Texture preview");

            int tile = D(150), pad = D(20), gap = D(12);
            var bar = BuildTitleBar("Texture preview", false);
            int y = D(36) + D(8);
            string[] cols = { "Normal", "Blurry textures" };
            for (int c = 0; c < 2; c++)
                Controls.Add(At(Lbl(cols[c], 9f, Theme.SubText), pad + c * (tile + gap), y));
            y += D(22);

            using var brick = MakeBrick(256);
            using var grass = MakeGrass(256);
            foreach (var src in new[] { brick, grass })
            {
                for (int c = 0; c < 2; c++)
                {
                    Bitmap img;
                    if (c == 0) img = ScaleTo(src, tile);
                    else
                    {
                        using var small = ScaleTo(src, 12);
                        img = ScaleTo(small, tile);
                    }
                    images.Add(img);
                    Controls.Add(new PictureBox
                    {
                        Image = img, Size = new Size(tile, tile), Location = new Point(pad + c * (tile + gap), y),
                        SizeMode = PictureBoxSizeMode.Normal, BackColor = Theme.Card,
                    });
                }
                y += tile + gap;
            }

            var note = Lbl("Made-up example. In real games the blur looks the same, the textures just differ.", 8.5f, Theme.SubText);
            note.AutoSize = false;
            note.Size = new Size(tile * 2 + gap, D(34));
            note.Location = new Point(pad, y);
            Controls.Add(note);
            y += D(34);

            Controls.Add(bar);
            ClientSize = new Size(pad * 2 + tile * 2 + gap, y + pad);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var b in images) b.Dispose();
            base.Dispose(disposing);
        }

        static Bitmap ScaleTo(Bitmap src, int size)
        {
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            using var attr = new System.Drawing.Imaging.ImageAttributes();
            attr.SetWrapMode(WrapMode.TileFlipXY);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, new Rectangle(0, 0, size, size), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
            return bmp;
        }

        static Bitmap MakeBrick(int s)
        {
            var bmp = new Bitmap(s, s);
            var r = new Random(7);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(185, 178, 166));
            int bh = s / 8, bw = s / 4;
            using var dot = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
            for (int y = 0; y < s; y += bh)
            {
                int off = (y / bh) % 2 == 1 ? bw / 2 : 0;
                for (int x = -bw; x < s + bw; x += bw)
                {
                    int v = r.Next(-20, 21);
                    using (var b = new SolidBrush(Color.FromArgb(150 + v, 68 + v / 2, 52 + v / 2)))
                        g.FillRectangle(b, x + off + 3, y + 3, bw - 6, bh - 6);
                    for (int i = 0; i < 40; i++)
                        g.FillRectangle(dot, x + off + 3 + r.Next(0, bw - 8), y + 3 + r.Next(0, bh - 8), 2, 2);
                }
            }
            return bmp;
        }

        static Bitmap MakeGrass(int s)
        {
            var bmp = new Bitmap(s, s);
            var r = new Random(3);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(79, 138, 46));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < 1800; i++)
            {
                float x = (float)r.NextDouble() * s, y = (float)r.NextDouble() * s, len = 4 + (float)r.NextDouble() * 9;
                int v = r.Next(-30, 31);
                using var p = new Pen(Color.FromArgb(70 + v, 140 + v, 45 + v / 2), 1.5f);
                g.DrawLine(p, x, y, x + ((float)r.NextDouble() - 0.5f) * 4, y - len);
            }
            return bmp;
        }
    }
}
