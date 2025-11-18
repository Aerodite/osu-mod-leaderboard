using System.Net.Http.Headers;
using System.Text.Json;

namespace osu_mod_leaderboard;

internal abstract class Program
{
    private static readonly HttpClient Http = new();
        
    // rate limit scares me
    // https://osu.ppy.sh/docs/#terms-of-use
    private const int RequestsPerMinute = 240;
    private const int Delay = 60000 / RequestsPerMinute;
    
    // get your clientid and secret from https://osu.ppy.sh/home/account/edit#oauth
    private const string ClientId = "";
    private const string ClientSecret = "";

    private class RankedUser
    {
        public required string Id { get; set; }
        public required string Username { get; set; }
        public int Rank { get; set; }
    }

    private abstract class OsuScore
    {
        // ReSharper disable once CollectionNeverUpdated.Local
        public required List<string> Mods { get; set; }
    }

    private static async Task Main()
    {
        string? token = await GetToken(ClientId, ClientSecret);
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
        var userList = await FetchTopUsers(1000);

        for (int i = 0; i < userList.Count; i++)
        {
            userList[i].Rank = i + 1;
        }

        var noDtUsers = new List<RankedUser>();
            
        foreach (RankedUser user in userList)
        {
            try
            {
                var plays = await GetAllTopPlays(user.Id, user.Username, user.Rank);

                bool hasDt = plays.Any(p =>
                    p.Mods.Any(m =>
                        string.Equals(m, "DT", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m, "NC", StringComparison.OrdinalIgnoreCase)
                    )
                );

                if (!hasDt)
                {
                    noDtUsers.Add(user);
                    Console.WriteLine($"User {user.Username} (Rank {user.Rank}) has no plays with DT");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching {user.Username}: {ex.Message}");
            }
        }

        Console.WriteLine("\n Users with no DT plays:");
        foreach (RankedUser u in noDtUsers.OrderBy(u => u.Rank))
        {
            Console.WriteLine($"Rank {u.Rank}: {u.Username}");
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
            if (!doc.RootElement.TryGetProperty("ranking", out JsonElement rankingArray))
                break;

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
            {"client_id", clientId},
            {"client_secret", clientSecret},
            {"grant_type", "client_credentials"},
            {"scope", "public"}
        };

        HttpResponseMessage res = await Http.PostAsync("https://osu.ppy.sh/oauth/token", new FormUrlEncodedContent(form));
        res.EnsureSuccessStatusCode();
        string json = await res.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString();
    }

    private static async Task<List<OsuScore>> GetAllTopPlays(string userId, string username, int rank)
    {
        var allPlays = new List<OsuScore>();
        const int limit = 100;
        int offset = 0;

        while (true)
        {
            Console.WriteLine($"Fetching top plays for {username} (#{rank}) | offset {offset}...");
            string url = $"https://osu.ppy.sh/api/v2/users/{userId}/scores/best?limit={limit}&offset={offset}&mode=osu";
            HttpResponseMessage res = await Http.GetAsync(url);
            res.EnsureSuccessStatusCode();
            string json = await res.Content.ReadAsStringAsync();

            var plays = JsonSerializer.Deserialize<List<OsuScore>>(json) ?? [];
            if (plays.Count == 0) break;

            allPlays.AddRange(plays);
            offset += plays.Count;
                
            await Task.Delay(Delay);
        }

        Console.WriteLine($"Finished fetching {allPlays.Count} plays for Rank {rank}: {username}.");
        return allPlays;
    }
}