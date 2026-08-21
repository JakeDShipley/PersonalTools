using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Entities.CSStats;

namespace PersonalTools.Data.CSStats;

public interface IAccountStandingData
{
    Task<CSStatsAccountStandingObj> GetStanding(string steam64Id, string? configuredKey = null, CancellationToken cancellationToken = default);
}

public sealed class AccountStandingData : IAccountStandingData
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string _steamKey;

    public AccountStandingData(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _cache = cache;
        _steamKey = configuration["Steam:WebApiKey"] ?? string.Empty;
    }

    public async Task<CSStatsAccountStandingObj> GetStanding(string steam64Id, string? configuredKey = null, CancellationToken cancellationToken = default)
    {
        string key = string.IsNullOrWhiteSpace(configuredKey) ? _steamKey : configuredKey;
        if (string.IsNullOrWhiteSpace(key)) return NotConfigured();
        return await _cache.GetOrCreateAsync($"steam-account-standing:{steam64Id}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync($"ISteamUser/GetPlayerBans/v1/?key={Uri.EscapeDataString(key)}&steamids={Uri.EscapeDataString(steam64Id)}", cancellationToken);
                if (!response.IsSuccessStatusCode) return Unavailable();
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
                JsonElement players = document.RootElement.TryGetProperty("players", out JsonElement direct) ? direct : document.RootElement.GetProperty("response").GetProperty("players");
                return players.GetArrayLength() == 0 ? Unavailable() : Map(players[0]);
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException) { return Unavailable(); }
        }) ?? Unavailable();
    }

    private static CSStatsAccountStandingObj Map(JsonElement player)
    {
        bool vac = Flag(player, "VACBanned"), community = Flag(player, "CommunityBanned");
        int games = Number(player, "NumberOfGameBans"), vacCount = Number(player, "NumberOfVACBans"), days = Number(player, "DaysSinceLastBan");
        string economy = Text(player, "EconomyBan");
        DateTime? when = DetailDate(player);
        List<CSStatsBanRecordObj> records = [];
        if (vac) records.Add(Record("Steam", "VAC ban", vacCount > 1 ? $"Steam reports {vacCount} VAC bans." : "Steam reports a VAC ban.", when, days));
        if (games > 0) records.Add(Record("Steam", "Game ban", games > 1 ? $"Steam reports {games} game bans." : "Steam reports a game ban.", when, days));
        if (community) records.Add(Record("Steam", "Community ban", "Steam reports a community ban.", null, null));
        if (!string.IsNullOrWhiteSpace(economy) && !economy.Equals("none", StringComparison.OrdinalIgnoreCase)) records.Add(Record("Steam", "Economy restriction", $"Steam reports an economy restriction: {economy}.", null, null));
        return new CSStatsAccountStandingObj { Records = records, Sources = Sources(records.Count == 0 ? "No public bans reported" : "Public enforcement reported", "Checked through Steam's GetPlayerBans Web API.") };
    }

    private static List<CSStatsBanSourceObj> Sources(string steamStatus, string steamDetail) => [new() { Platform = "Steam", Status = steamStatus, Detail = steamDetail }, new() { Platform = "FACEIT", Status = "Not publicly available", Detail = "FACEIT public profile data does not provide a reliable ban-history feed." }, new() { Platform = "Leetify", Status = "Not a ban authority", Detail = "Leetify performance data is not enforcement evidence." }];
    private static CSStatsBanRecordObj Record(string platform, string type, string reason, DateTime? bannedUtc, int? days) => new() { Platform = platform, Type = type, Reason = reason, BannedUtc = bannedUtc, DaysSinceBan = days > 0 ? days : null };
    private static bool Flag(JsonElement element, string property) => element.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
    private static int Number(JsonElement element, string property) => element.TryGetProperty(property, out JsonElement value) && value.TryGetInt32(out int result) ? result : 0;
    private static string Text(JsonElement element, string property) => element.TryGetProperty(property, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
    private static DateTime? DetailDate(JsonElement player) => player.TryGetProperty("bans", out JsonElement bans) && bans.ValueKind == JsonValueKind.Array && bans.GetArrayLength() > 0 && bans[0].TryGetProperty("BanStartTime", out JsonElement time) && time.TryGetInt64(out long seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : null;
    private static CSStatsAccountStandingObj NotConfigured() => new() { Sources = Sources("Not configured", "Add a server-side Steam Web API key to enable public ban checks.") };
    private static CSStatsAccountStandingObj Unavailable() => new() { Sources = Sources("Unavailable", "Steam ban data could not be verified right now.") };
}
