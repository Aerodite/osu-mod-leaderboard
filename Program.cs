using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace osu_mod_leaderboard;

internal abstract class Program
{
    // rate limit scares me
    // https://osu.ppy.sh/docs/#terms-of-use
    private const int RequestsPerMinute = 240;
    private const int Delay = 60000 / RequestsPerMinute;

    // get your clientid and secret from https://osu.ppy.sh/home/account/edit#oauth
    // (im too lazy for .env config)
    private const string ClientId = "";
    private const string ClientSecret = "";
    private static readonly HttpClient Http = new();

    // change this with whatever mods you want to exclude
    private static readonly string[] ExcludedMods = { "DT", "NC" };
    private static readonly HashSet<string> ExcludedModsSet = new(ExcludedMods, StringComparer.OrdinalIgnoreCase);

    private static async Task Main()
    {
        string? token = await GetToken(ClientId, ClientSecret);
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var userList = await FetchTopUsers(1000);
        var usersWithoutCertainMods = new List<RankedUser>();
        string excludedModsText = string.Join(", ", ExcludedMods);

        const int batchSize = 20;
        for (int i = 0; i < userList.Count; i += batchSize)
        {
            var batch = userList.Skip(i).Take(batchSize).ToList();
            var tasks = batch.Select(async user =>
            {
                try
                {
                    bool containsCertainMods = await HasExcludedModPlays(user.Id, user.Username, user.Rank);

                    if (!containsCertainMods)
                    {
                        lock (usersWithoutCertainMods)
                        {
                            usersWithoutCertainMods.Add(user);
                        }

                        Console.WriteLine(
                            $"{user.Username} (#{user.Rank}) has no plays with excluded mods: ({excludedModsText}).");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"{user.Username} (#{user.Rank}) has plays with excluded mods: ({excludedModsText}).");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching {user.Username}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
            await Task.Delay(Delay);
        }

        Console.WriteLine($"\nUsers with no plays containing: {excludedModsText}");
        int order = 1;
        foreach (RankedUser u in usersWithoutCertainMods.OrderBy(u => u.Rank))
        {
            Console.WriteLine($"{order}: {u.Username} (#{u.Rank})");
            order++;
        }
    }

    private static async Task<List<RankedUser>> FetchTopUsers(int count)
    {
        var users = new List<RankedUser>();
        int page = 1;
        const int pageSize = 50;
        int rankCounter = 1;

        while (users.Count < count)
        {
            string url = $"https://osu.ppy.sh/api/v2/rankings/osu/performance?limit={pageSize}&page={page}";
            HttpResponseMessage res = await Http.GetAsync(url);
            res.EnsureSuccessStatusCode();
            string json = await res.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("ranking", out JsonElement rankingArray)) break;

            foreach (JsonElement u in rankingArray.EnumerateArray())
            {
                if (!u.TryGetProperty("user", out JsonElement userProp)) continue;

                string id = userProp.GetProperty("id").GetInt32().ToString();
                string username = userProp.GetProperty("username").GetString() ?? "Unknown";

                users.Add(new RankedUser { Id = id, Username = username, Rank = rankCounter });
                Console.WriteLine($"Fetched user: {username} (Rank {rankCounter})");
                rankCounter++;

                if (users.Count >= count) break;
            }

            page++;
            await Task.Delay(Delay);
        }

        return users;
    }

    private static async Task<string?> GetToken(string clientId, string clientSecret)
    {
        var form = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "grant_type", "client_credentials" },
            { "scope", "public" }
        };

        HttpResponseMessage res =
            await Http.PostAsync("https://osu.ppy.sh/oauth/token", new FormUrlEncodedContent(form));
        res.EnsureSuccessStatusCode();
        string json = await res.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString();
    }

    private static async Task<bool> HasExcludedModPlays(string userId, string username, int rank)
    {
        const int limit = 100;
        int offset = 0;

        while (true)
        {
            Console.WriteLine($"Fetching top plays for {username} (#{rank}) | offset {offset}...");
            string url = $"https://osu.ppy.sh/api/v2/users/{userId}/scores/best?limit={limit}&offset={offset}&mode=osu";
            HttpResponseMessage res = await Http.GetAsync(url);
            res.EnsureSuccessStatusCode();
            string json = await res.Content.ReadAsStringAsync();

            var plays = JsonSerializer.Deserialize<List<OsuScore>>(json) ?? new List<OsuScore>();
            if (plays.Count == 0) break;

            if (plays.Any(p => p.Mods.Any(m => ExcludedModsSet.Contains(m))))
            {
                Console.WriteLine($"Found excluded mod play for {username} (#{rank}) at offset {offset}");
                return true;
            }

            offset += plays.Count;
            await Task.Delay(Delay);
        }

        Console.WriteLine($"No excluded mod plays found for {username} (#{rank})");
        return false;
    }

    private class RankedUser
    {
        public required string Id { get; set; }
        public required string Username { get; set; }
        public int Rank { get; set; }
    }

    private class OsuScore
    {
        [JsonPropertyName("mods")]
        // ReSharper disable once CollectionNeverUpdated.Local
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public required List<string> Mods { get; set; }
    }
}