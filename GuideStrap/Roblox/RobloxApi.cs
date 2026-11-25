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
    // ROBLOX WEB API
    static class RobloxApi
    {
        public static readonly HttpClient Http = new();
        static readonly HttpClient AuthHttp = new(new HttpClientHandler { UseCookies = false });

        public static async Task<string> GetLatestVersionAsync(CancellationToken ct = default)
        {
            string json = await Http.GetStringAsync("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer", ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("clientVersionUpload").GetString()
                   ?? throw new Exception("Roblox didn't return a version.");
        }

        public static async Task<Account> GetAccountAsync(string cookie)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
            req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
            using var resp = await AuthHttp.SendAsync(req);
            if (!resp.IsSuccessStatusCode) throw new Exception($"Roblox said no (HTTP {(int)resp.StatusCode}).");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var r = doc.RootElement;
            return new Account
            {
                UserId = r.GetProperty("id").GetInt64(),
                Username = r.GetProperty("name").GetString() ?? "",
                DisplayName = r.GetProperty("displayName").GetString() ?? "",
                Cookie = cookie,
            };
        }

        public static async Task<Image?> GetAvatarAsync(long userId)
        {
            string path = Path.Combine(Paths.Avatars, userId + ".png");
            try
            {
                if (!File.Exists(path))
                {
                    string json = await Http.GetStringAsync(
                        $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=false");
                    using var doc = JsonDocument.Parse(json);
                    string? url = doc.RootElement.GetProperty("data")[0].GetProperty("imageUrl").GetString();
                    if (string.IsNullOrEmpty(url)) return null;
                    Directory.CreateDirectory(Paths.Avatars);
                    await File.WriteAllBytesAsync(path, await Http.GetByteArrayAsync(url));
                }
                using var fs = File.OpenRead(path);
                using var img = Image.FromStream(fs);
                return new Bitmap(img);
            }
            catch { return null; }
        }

        // Public server list. smallest = fewest players first, otherwise a random open server.
        public static async Task<string?> FindServerAsync(long placeId, bool smallest, string? exclude, CancellationToken ct)
        {
            string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?sortOrder={(smallest ? "Asc" : "Desc")}&limit=100&excludeFullGames=true";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));
            var open = new List<(string Id, int Playing)>();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var sv in data.EnumerateArray())
                {
                    string? id = sv.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
                    int playing = sv.TryGetProperty("playing", out var pl) && pl.ValueKind == JsonValueKind.Number ? pl.GetInt32() : 0;
                    int max = sv.TryGetProperty("maxPlayers", out var mx) && mx.ValueKind == JsonValueKind.Number ? mx.GetInt32() : 0;
                    if (id == null || id == exclude || (max > 0 && playing >= max)) continue;
                    open.Add((id, playing));
                }
            }
            if (open.Count == 0) return null;
            return smallest ? open.OrderBy(x => x.Playing).First().Id : open[Random.Shared.Next(open.Count)].Id;
        }

        // Public servers for the server list: players, ping and FPS (Roblox reports these itself)
        public static async Task<List<ServerEntry>> GetServersAsync(long placeId, CancellationToken ct)
        {
            var list = new List<ServerEntry>();
            string? cursor = null;
            for (int page = 0; page < 2; page++)
            {
                string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?sortOrder=Desc&limit=100&excludeFullGames=true" +
                             (cursor != null ? "&cursor=" + Uri.EscapeDataString(cursor) : "");
                using var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct));
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sv in data.EnumerateArray())
                    {
                        string? id = sv.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
                        if (id == null || !Guid.TryParse(id, out _)) continue;
                        int playing = sv.TryGetProperty("playing", out var pl) && pl.ValueKind == JsonValueKind.Number ? pl.GetInt32() : 0;
                        int max = sv.TryGetProperty("maxPlayers", out var mx) && mx.ValueKind == JsonValueKind.Number ? mx.GetInt32() : 0;
                        int? ping = sv.TryGetProperty("ping", out var pg) && pg.ValueKind == JsonValueKind.Number ? (int)Math.Round(pg.GetDouble()) : null;
                        double? fps = sv.TryGetProperty("fps", out var fp) && fp.ValueKind == JsonValueKind.Number ? fp.GetDouble() : null;
                        if (max > 0 && playing >= max) continue;
                        list.Add(new ServerEntry(id, playing, max, ping, fps));
                    }
                }
                cursor = doc.RootElement.TryGetProperty("nextPageCursor", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                if (string.IsNullOrEmpty(cursor)) break;
            }
            return list;
        }

        static bool TryProp(JsonElement e, string name, out JsonElement value)
        {
            value = default;
            if (e.ValueKind != JsonValueKind.Object) return false;
            foreach (var prop in e.EnumerateObject())
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) { value = prop.Value; return true; }
            return false;
        }

        static long? Num(JsonElement e, string name) =>
            TryProp(e, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;

        static string? Str(JsonElement e, string name) =>
            TryProp(e, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        // Signed-in POST that handles Roblox's CSRF token handshake
        static async Task<string> PostAuthedAsync(string url, string cookie, string json)
        {
            string? csrf = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                if (csrf != null) req.Headers.Add("X-CSRF-TOKEN", csrf);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await AuthHttp.SendAsync(req);
                if (csrf == null && resp.Headers.TryGetValues("x-csrf-token", out var token) && !resp.IsSuccessStatusCode)
                {
                    csrf = token.First();
                    continue;
                }
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new Exception("This account's saved login expired. Remove it and add it again.");
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
            throw new Exception("Roblox didn't accept the request. Try again.");
        }

        public static async Task<List<long>> GetFriendIdsAsync(long userId, string cookie)
        {
            var ids = new List<long>();
            string? cursor = null;
            for (int page = 0; page < 4; page++)
            {
                string url = $"https://friends.roblox.com/v1/users/{userId}/friends/find?limit=50" +
                             (cursor != null ? "&cursor=" + Uri.EscapeDataString(cursor) : "");
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                using var resp = await AuthHttp.SendAsync(req);
                if (!resp.IsSuccessStatusCode) break;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                if (TryProp(doc.RootElement, "PageItems", out var items) && items.ValueKind == JsonValueKind.Array)
                    foreach (var it in items.EnumerateArray())
                        if (Num(it, "id") is long id) ids.Add(id);
                cursor = Str(doc.RootElement, "NextCursor");
                if (string.IsNullOrEmpty(cursor)) break;
            }
            if (ids.Count == 0)
            {
                // older endpoint, kept as a fallback
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://friends.roblox.com/v1/users/{userId}/friends");
                req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                using var resp = await AuthHttp.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    if (TryProp(doc.RootElement, "data", out var data) && data.ValueKind == JsonValueKind.Array)
                        foreach (var it in data.EnumerateArray())
                            if (Num(it, "id") is long id) ids.Add(id);
                }
            }
            return ids.Distinct().ToList();
        }

        public static async Task<List<FriendPresence>> GetPresenceAsync(string cookie, List<long> userIds)
        {
            var list = new List<FriendPresence>();
            foreach (var chunk in userIds.Chunk(50))
            {
                string json = JsonSerializer.Serialize(new { userIds = chunk });
                using var doc = JsonDocument.Parse(await PostAuthedAsync("https://presence.roblox.com/v1/presence/users", cookie, json));
                if (!TryProp(doc.RootElement, "userPresences", out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var e in arr.EnumerateArray())
                {
                    if (Num(e, "userId") is not long uid) continue;
                    list.Add(new FriendPresence
                    {
                        UserId = uid,
                        Type = (int)(Num(e, "userPresenceType") ?? 0),
                        Location = Str(e, "lastLocation"),
                        PlaceId = Num(e, "placeId"),
                        RootPlaceId = Num(e, "rootPlaceId"),
                        GameId = Str(e, "gameId") is string g && Guid.TryParse(g, out _) ? g : null,
                    });
                }
            }
            return list;
        }

        // Display names for user IDs (cached in the given dictionary)
        public static async Task FillNamesAsync(IEnumerable<long> userIds, Dictionary<long, string> cache)
        {
            var missing = userIds.Where(id => !cache.ContainsKey(id)).Distinct().ToList();
            foreach (var chunk in missing.Chunk(100))
            {
                try
                {
                    string json = JsonSerializer.Serialize(new { userIds = chunk, excludeBannedUsers = false });
                    using var resp = await Http.PostAsync("https://users.roblox.com/v1/users", new StringContent(json, Encoding.UTF8, "application/json"));
                    if (!resp.IsSuccessStatusCode) continue;
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    if (!TryProp(doc.RootElement, "data", out var data) || data.ValueKind != JsonValueKind.Array) continue;
                    foreach (var u in data.EnumerateArray())
                        if (Num(u, "id") is long id) cache[id] = Str(u, "displayName") ?? Str(u, "name") ?? id.ToString();
                }
                catch { }
            }
        }

        // A one-time ticket that lets the Roblox client log in as a saved account
        public static async Task<string> GetAuthTicketAsync(string cookie)
        {
            string? csrf = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket");
                req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                req.Headers.Referrer = new Uri("https://www.roblox.com/");
                req.Headers.Add("Origin", "https://www.roblox.com");
                req.Headers.Add("RBXAuthenticationNegotiation", "1");
                if (csrf != null) req.Headers.Add("X-CSRF-TOKEN", csrf);
                req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                using var resp = await AuthHttp.SendAsync(req);
                if (resp.Headers.TryGetValues("rbx-authentication-ticket", out var ticket)) return ticket.First();
                if (resp.Headers.TryGetValues("x-csrf-token", out var token)) { csrf = token.First(); continue; }
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new Exception("This account's saved login expired. Remove it and add it again.");
                throw new Exception($"Couldn't log in this account (HTTP {(int)resp.StatusCode}).");
            }
            throw new Exception("Couldn't log in this account. Try again.");
        }

        public static string BuildLaunchUri(string ticket, long? placeId, string? jobId = null)
        {
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string tracker = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
            if (placeId == null)
                return $"roblox-player:1+launchmode:app+gameinfo:{ticket}+launchtime:{time}+browsertrackerid:{tracker}" +
                       "+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";

            string placeUrl = Uri.EscapeDataString(jobId != null
                ? $"https://www.roblox.com/Game/PlaceLauncher.ashx?request=RequestGameJob&browserTrackerId={tracker}" +
                  $"&placeId={placeId}&gameId={jobId}&isPlayTogetherGame=false&joinAttemptId={Guid.NewGuid()}&joinAttemptOrigin=publicServerListJoin"
                : $"https://www.roblox.com/Game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId={tracker}" +
                  $"&placeId={placeId}&isPlayTogetherGame=false&joinAttemptId={Guid.NewGuid()}&joinAttemptOrigin=PlayButton");
            return $"roblox-player:1+launchmode:play+gameinfo:{ticket}+launchtime:{time}+placelauncherurl:{placeUrl}" +
                   $"+browsertrackerid:{tracker}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
        }
    }

    record ServerEntry(string Id, int Playing, int MaxPlayers, int? Ping, double? Fps);
}
