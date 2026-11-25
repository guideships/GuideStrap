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
    class FriendPresence
    {
        public long UserId { get; init; }
        public int Type { get; init; }          // 0 offline, 1 online, 2 in a game, 3 in Studio
        public string? Location { get; init; }
        public long? PlaceId { get; init; }
        public long? RootPlaceId { get; init; }
        public string? GameId { get; init; }    // the server, when the friend's privacy allows joining
    }

    // Checks friends every 90 seconds (one small request) and tells the app when one starts playing
    static class FriendWatcher
    {
        static System.Windows.Forms.Timer? timer;
        static List<long> friendIds = new();
        static DateTime idsLoaded;
        static long idsFor;
        static readonly Dictionary<long, string> names = new();
        static Dictionary<long, (int Type, long Place)> last = new();
        static bool first = true, busy;
        public static event Action<FriendPresence, string>? FriendStartedPlaying;
        public static event Action<string>? FriendJoinedYou;
        static string? togetherJob;                       // your server when we last checked
        static readonly HashSet<long> together = new();   // friends already in your server (alerted once)

        static Mutex? owner;

        public static void Start()
        {
            if (timer != null) return;
            try
            {
                owner = new Mutex(true, @"Local\GuideStrapFriendWatcher", out bool mine);
                if (!mine) { owner.Dispose(); owner = null; return; }
            }
            catch { owner = null; }
            // first check after 15 seconds, then every 90 (ticks run on the UI thread)
            timer = new System.Windows.Forms.Timer { Interval = 15_000 };
            timer.Tick += async (_, _) =>
            {
                // a bit more often while you're in a game, so "joined your server" pops up soon after
                if (timer != null) timer.Interval = Presence.CurrentJobId != null ? 45_000 : 90_000;
                await PollAsync();
            };
            timer.Start();
        }

        public static void Stop()
        {
            timer?.Stop();
            timer?.Dispose();
            timer = null;
            try { owner?.ReleaseMutex(); } catch { }
            owner?.Dispose();
            owner = null;
        }

        static async Task PollAsync()
        {
            if (busy) return;
            busy = true;
            try
            {
                var s = AppSettings.Load();
                var acc = (s.FriendAlerts || s.FriendJoinAlerts) && s.ActiveAccountId != null
                    ? AccountStore.Load().FirstOrDefault(a => a.UserId == s.ActiveAccountId) : null;
                if (acc == null)
                {
                    last.Clear();
                    first = true;
                    return;
                }
                if (idsFor != acc.UserId)
                {
                    idsFor = acc.UserId;
                    friendIds.Clear();
                    last.Clear();
                    first = true;
                }
                if (friendIds.Count == 0 || DateTime.Now - idsLoaded > TimeSpan.FromMinutes(10))
                {
                    friendIds = await RobloxApi.GetFriendIdsAsync(acc.UserId, acc.Cookie);
                    idsLoaded = DateTime.Now;
                }
                if (friendIds.Count == 0) return;

                var presence = await RobloxApi.GetPresenceAsync(acc.Cookie, friendIds);
                var now = new Dictionary<long, (int Type, long Place)>();
                var started = new List<FriendPresence>();
                foreach (var f in presence)
                {
                    long place = f.RootPlaceId ?? f.PlaceId ?? 0;
                    now[f.UserId] = (f.Type, place);
                    if (first || f.Type != 2) continue;
                    bool same = last.TryGetValue(f.UserId, out var before) && before.Type == 2 && before.Place == place;
                    if (!same) started.Add(f);
                }
                last = now;
                bool wasFirst = first;
                first = false;

                // friends who are now in the same server as you
                string? myJob = Presence.CurrentJobId;
                // friends already there when you joined a new server count as "already together", no popup
                bool newServer = myJob != togetherJob;
                if (newServer) { together.Clear(); togetherJob = myJob; }
                var joined = new List<long>();
                if (myJob != null)
                    foreach (var f in presence)
                        if (f.Type == 2 && f.GameId != null && string.Equals(f.GameId, myJob, StringComparison.OrdinalIgnoreCase)
                            && together.Add(f.UserId) && !wasFirst && !newServer)
                            joined.Add(f.UserId);
                if (joined.Count > 0 && s.FriendJoinAlerts)
                {
                    await RobloxApi.FillNamesAsync(joined, names);
                    string who = s.StreamerMode ? L.T("A friend") : names.GetValueOrDefault(joined[0], L.T("A friend"));
                    FriendJoinedYou?.Invoke(who);
                    started.RemoveAll(f => joined.Contains(f.UserId));   // one popup per friend is enough
                }

                if (started.Count == 0 || !s.FriendAlerts) return;
                await RobloxApi.FillNamesAsync(started.Select(f => f.UserId), names);
                var pick = started[0];
                FriendStartedPlaying?.Invoke(pick, names.GetValueOrDefault(pick.UserId, L.T("A friend")));
            }
            catch { }
            finally { busy = false; }
        }
    }
}
