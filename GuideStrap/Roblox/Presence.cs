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
    // Discord status. Shows where you are in GuideStrap, and the game you're in while Roblox runs.
    // Roblox doesn't tell other programs what game you joined, so this follows Roblox's log file (like Bloxstrap does).
    static class Presence
    {
        static DiscordRpcClient? client;
        static readonly DateTime appStart = DateTime.UtcNow;
        static string screen = "In the main menu";
        static string? screenDetail;
        static bool inRoblox;
        static long placeId;
        static string? gameName, gameCreator, gameIcon, serverLocation, lastServerIp;
        public static event Action<string>? ServerLocated;
        static DateTime robloxStart, gameStart;
        static CancellationTokenSource? watchCts;

        public static bool Watching { get; private set; }
        public static event Action? WatchEnded;
        public static event Action<CrashInfo>? RobloxCrashed;
        public static event Action<SessionSummary>? SessionEnded;
        static bool showGame = true, showAccount, allowJoin, streamer;
        static volatile string? account;
        static string? jobId, sessionLocation;
        static readonly List<string> sessionGames = new();

        public static void Configure(AppSettings s)
        {
            showGame = s.DiscordShowGame;
            showAccount = s.DiscordShowAccount;
            allowJoin = s.DiscordAllowJoin;
            streamer = s.StreamerMode;
            Push();
        }

        // The saved account being launched (null = Roblox login). Only shown if the user turns it on.
        public static void SetAccount(string? name) => account = name;

        static string LogPath => Path.Combine(Paths.Base, "discord.log");

        static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(Paths.Base);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  {msg}{Environment.NewLine}");
            }
            catch { }
        }

        public static void Enable(bool on)
        {
            if (on && client == null)
            {
                try
                {
                    try { if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 200_000) File.Delete(LogPath); } catch { }
                    client = new DiscordRpcClient(AppInfo.DiscordAppId);
                    client.OnReady += (_, e) => Log($"connected to Discord as {e.User?.Username}");
                    client.OnError += (_, e) => Log($"Discord error: {e.Message}");
                    client.OnConnectionFailed += (_, _) => Log("couldn't reach Discord (is the Discord app open?)");
                    client.Initialize();
                    Push();
                }
                catch (Exception ex) { Log("start failed: " + ex.Message); client = null; }
            }
            else if (!on && client != null)
            {
                try { client.ClearPresence(); client.Dispose(); } catch { }
                client = null;
            }
        }

        public static void Shutdown()
        {
            watchCts?.Cancel();
            Enable(false);
        }

        // Called by the windows: "In the main menu", "Editing settings" + page, "Launching Roblox"...
        public static void Screen(string text, string? detail = null)
        {
            screen = text;
            screenDetail = detail;
            Push();
        }

        static string Cut(string s, int max = 120) => s.Length <= max ? s : s[..(max - 1)] + "…";

        static void Push()
        {
            if (client == null) return;
            try
            {
                var p = new DiscordRPC.RichPresence();
                string? who = account;
                string? playingAs = showAccount && !streamer && who != null ? Cut("Playing as " + who) : null;
                if (inRoblox && placeId != 0 && showGame)
                {
                    string name = gameName ?? "a game";
                    p.Details = Cut("In " + name);
                    p.State = playingAs ?? (gameCreator != null ? Cut("by " + gameCreator) : null);
                    p.Timestamps = new Timestamps(gameStart);
                    p.Assets = new Assets
                    {
                        LargeImageKey = gameIcon is { Length: > 0 and <= 256 } ? gameIcon : AppInfo.LogoUrl,
                        LargeImageText = Cut(name),
                        SmallImageKey = AppInfo.LogoUrl,
                        SmallImageText = serverLocation != null && !streamer ? Cut("Server: " + serverLocation) : "GuideStrap",
                    };
                    var buttons = new List<DiscordRPC.Button>();
                    if (allowJoin && !streamer && jobId != null)
                        buttons.Add(new DiscordRPC.Button { Label = "Join server", Url = $"{AppInfo.SiteUrl}/join.html?placeId={placeId}&gameInstanceId={jobId}" });
                    buttons.Add(new DiscordRPC.Button { Label = "View game", Url = $"https://www.roblox.com/games/{placeId}" });
                    p.Buttons = buttons.ToArray();
                }
                else if (inRoblox)
                {
                    p.Details = placeId != 0 ? "Playing Roblox" : "In the Roblox app";
                    p.State = playingAs;
                    p.Timestamps = new Timestamps(placeId != 0 ? gameStart : robloxStart);
                    p.Assets = new Assets { LargeImageKey = AppInfo.LogoUrl, LargeImageText = "GuideStrap" };
                }
                else
                {
                    p.Details = screen;
                    p.State = screenDetail;
                    p.Timestamps = new Timestamps(appStart);
                    p.Assets = new Assets { LargeImageKey = AppInfo.LogoUrl, LargeImageText = "GuideStrap" };
                }
                client.SetPresence(p);
            }
            catch (Exception ex) { Log("couldn't set status: " + ex.Message); }
        }

        public static void WatchRoblox()
        {
            if (Watching) return;
            Watching = true;
            inRoblox = true;
            placeId = 0;
            jobId = null;
            sessionLocation = null;
            sessionGames.Clear();
            robloxStart = DateTime.UtcNow;
            SetOwnPriority(ProcessPriorityClass.BelowNormal);   // never compete with Roblox for CPU
            Push();
            watchCts = new CancellationTokenSource();
            _ = WatchAsync(watchCts.Token);
        }

        public static void StopWatching() => watchCts?.Cancel();

        static void SetOwnPriority(ProcessPriorityClass level)
        {
            try
            {
                using var me = Process.GetCurrentProcess();
                me.PriorityClass = level;
            }
            catch { }
        }

        static async Task WatchAsync(CancellationToken ct)
        {
            var started = DateTime.Now;
            CrashWatch.Begin();
            StreamReader? reader = null;
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");
                int missed = 0;
                while (!ct.IsCancellationRequested)
                {
                    // find Roblox's new log file (it appears a moment after Roblox starts)
                    if (reader == null && Directory.Exists(logDir))
                    {
                        // Roblox writes several logs (crash handler, installer...). The game one has "Player" in its name.
                        var file = new DirectoryInfo(logDir).GetFiles("*.log")
                            .Where(f => f.Name.Contains("Player", StringComparison.OrdinalIgnoreCase)
                                        && !f.Name.Contains("Crash", StringComparison.OrdinalIgnoreCase)
                                        && f.CreationTime >= started.AddSeconds(-90))
                            .OrderByDescending(f => f.CreationTime)
                            .FirstOrDefault();
                        if (file != null)
                        {
                            Log("following " + file.Name);
                            reader = new StreamReader(new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
                        }
                    }

                    if (reader != null)
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync(ct)) != null)
                        {
                            CrashWatch.Line(line);
                            await HandleLineAsync(line);
                        }
                    }

                    // Roblox can restart itself, so only stop after it's been gone for a few seconds
                    if (CrashWatch.Poll()) missed = 0;
                    else if (++missed >= 4) break;

                    await Task.Delay(1000, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log("watch error: " + ex.Message); }
            finally
            {
                SetOwnPriority(ProcessPriorityClass.Normal);
                reader?.Dispose();
                Watching = false;
                inRoblox = false;
                placeId = 0;
                jobId = null;
                gameName = gameCreator = gameIcon = serverLocation = lastServerIp = null;
                Log("Roblox closed");
                Push();
                var crash = CrashWatch.End(report: !ct.IsCancellationRequested);
                if (crash != null)
                {
                    Log($"Roblox crashed: 0x{crash.Code:X8}");
                    try { RobloxCrashed?.Invoke(crash); }
                    catch (Exception ex) { Log("crash helper error: " + ex.Message); }
                }
                else if (!ct.IsCancellationRequested)
                {
                    try { SessionEnded?.Invoke(new SessionSummary(DateTime.UtcNow - robloxStart, sessionGames.ToList(), sessionLocation)); }
                    catch (Exception ex) { Log("summary error: " + ex.Message); }
                }
                WatchEnded?.Invoke();
            }
        }

        static async Task HandleLineAsync(string line)
        {
            if (line.Contains("! Joining game") || line.Contains("Joining game"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"place (\d+)");
                var jm = System.Text.RegularExpressions.Regex.Match(line, @"Joining game '([0-9a-fA-F-]{36})'");
                if (m.Success && jm.Success && long.TryParse(m.Groups[1].Value, out long joined))
                {
                    jobId = jm.Groups[1].Value;
                    LastServer.Save(joined, jobId);
                    if (joined == placeId) Push();
                }
                if (m.Success && long.TryParse(m.Groups[1].Value, out long id) && id != placeId)
                {
                    Log("joining place " + id);
                    placeId = id;
                    gameStart = DateTime.UtcNow;
                    gameName = null; gameCreator = null; gameIcon = null; serverLocation = null; lastServerIp = null;
                    Push();
                    await LoadGameInfoAsync(id);
                }
            }
            else if (line.Contains("UDMUX Address = ") || line.Contains("Connection accepted from"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"(?:UDMUX Address = |Connection accepted from )(\d+\.\d+\.\d+\.\d+)");
                if (m.Success && m.Groups[1].Value != lastServerIp)
                {
                    lastServerIp = m.Groups[1].Value;
                    await LocateServerAsync(lastServerIp);
                }
            }
            else if (line.Contains("Time to disconnect replication data") || line.Contains("leaveUGCGameInternal"))
            {
                if (placeId != 0) Log("left game");
                placeId = 0;
                jobId = null;
                gameName = gameCreator = gameIcon = serverLocation = lastServerIp = null;
                Push();
            }
        }

        static async Task LocateServerAsync(string ip)
        {
            try
            {
                using var doc = JsonDocument.Parse(await RobloxApi.Http.GetStringAsync($"https://ipinfo.io/{ip}/json"));
                string Get(string key) => doc.RootElement.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
                var parts = new[] { Get("city"), Get("region"), Get("country") }.Where(s => s.Length > 0).Distinct().ToList();
                if (parts.Count == 0 || ip != lastServerIp) return;
                serverLocation = string.Join(", ", parts);
                sessionLocation = serverLocation;
                Log("server in " + serverLocation);
                Push();
                ServerLocated?.Invoke(serverLocation);
            }
            catch (Exception ex) { Log("server location error: " + ex.Message); }
        }

        static async Task LoadGameInfoAsync(long id)
        {
            try
            {
                var http = RobloxApi.Http;
                using var u = JsonDocument.Parse(await http.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{id}/universe"));
                long universe = u.RootElement.GetProperty("universeId").GetInt64();
                using var g = JsonDocument.Parse(await http.GetStringAsync($"https://games.roblox.com/v1/games?universeIds={universe}"));
                var game = g.RootElement.GetProperty("data")[0];
                string? name = game.GetProperty("name").GetString();
                string? creator = game.GetProperty("creator").GetProperty("name").GetString();
                using var t = JsonDocument.Parse(await http.GetStringAsync($"https://thumbnails.roblox.com/v1/games/icons?universeIds={universe}&size=512x512&format=Png&isCircular=false"));
                string? icon = t.RootElement.GetProperty("data")[0].GetProperty("imageUrl").GetString();
                if (id != placeId) return;   // left or teleported meanwhile
                gameName = name; gameCreator = creator; gameIcon = icon;
                if (name != null && !sessionGames.Contains(name)) sessionGames.Add(name);
                Log($"in game: {name}");
                Push();
                RecentGames.Add(new RecentGame { PlaceId = id, Name = name ?? "Game", Creator = creator ?? "", IconUrl = icon ?? "", LastPlayed = DateTime.Now });
            }
            catch (Exception ex) { Log("game info error: " + ex.Message); }
        }
    }

    record SessionSummary(TimeSpan Length, List<string> Games, string? Location);
}
