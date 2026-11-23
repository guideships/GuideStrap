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
    // ANIMATION ENGINE (one shared ~60fps timer, ease-out curves)
    public static class Anim
    {
        sealed class Tween
        {
            public object Key = null!;
            public float From, To;
            public double Start, Duration;
            public Action<float> Set = null!;
            public Action? Done;
        }

        static readonly List<Tween> tweens = new();
        static readonly List<(object Key, Action<double> Frame)> loops = new();
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static System.Windows.Forms.Timer? timer;
        static double lastTick = -1;
        static bool hiRes;

        public static void Run(object key, float from, float to, int ms, Action<float> set, Action? done = null)
        {
            tweens.RemoveAll(t => Equals(t.Key, key));
            tweens.Add(new Tween { Key = key, From = from, To = to, Start = clock.Elapsed.TotalMilliseconds, Duration = Math.Max(1, ms), Set = set, Done = done });
            Wake();
        }

        // Runs every frame until stopped; gets the milliseconds since the last frame.
        public static void Loop(object key, Action<double> frame)
        {
            loops.RemoveAll(l => Equals(l.Key, key));
            loops.Add((key, frame));
            Wake();
        }

        public static void StopLoop(object key) => loops.RemoveAll(l => Equals(l.Key, key));

        static void Wake()
        {
            if (timer == null)
            {
                timer = new System.Windows.Forms.Timer { Interval = 8 };
                timer.Tick += Tick;
            }
            if (timer.Enabled) return;
            // Windows timers normally tick every ~15.6ms, which looks choppy.
            // Ask for 1ms timer precision only while something is animating.
            if (!hiRes) { NativeMethods.timeBeginPeriod(1); hiRes = true; }
            lastTick = -1;
            timer.Start();
        }

        static void Tick(object? sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalMilliseconds;
            double dt = lastTick < 0 ? 16 : Math.Min(50, now - lastTick);
            lastTick = now;

            foreach (var t in tweens.ToArray())
            {
                if (!tweens.Contains(t)) continue;
                double p = Math.Clamp((now - t.Start) / t.Duration, 0, 1);
                float eased = 1f - (float)Math.Pow(1 - p, 4);   // ease-out quart
                bool ok = true;
                try { t.Set(t.From + (t.To - t.From) * eased); } catch { ok = false; }
                if (!ok || p >= 1)
                {
                    tweens.Remove(t);
                    if (ok) { try { t.Done?.Invoke(); } catch { } }
                }
            }
            foreach (var l in loops.ToArray())
            {
                try { l.Frame(dt); } catch { loops.Remove(l); }
            }

            if (tweens.Count == 0 && loops.Count == 0)
            {
                timer?.Stop();
                lastTick = -1;
                if (hiRes) { NativeMethods.timeEndPeriod(1); hiRes = false; }
            }
        }
    }
}
