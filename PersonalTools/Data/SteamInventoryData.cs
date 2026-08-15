using System.Text.Json;
using PersonalTools.Entities;

namespace PersonalTools.Data;

public interface ISteamInventoryData
{
    Task<string> ResolveSteamId(string profileReference, CancellationToken cancellationToken = default);
    Task<SteamProfileLookupResult> ResolveProfile(string profileReference, CancellationToken cancellationToken = default);
    Task<SteamInventoryResult> LoadCs2Inventory(string steamId, CancellationToken cancellationToken = default);
}

public sealed class SteamInventoryData : ISteamInventoryData
{
    private const string SteamCommunity = "https://steamcommunity.com/";
    private const string ImageBase = "https://community.akamai.steamstatic.com/economy/image/";
    private static readonly SemaphoreSlim RequestGate = new(1, 1);
    private static DateTime LastRequestUtc = DateTime.MinValue;
    private readonly HttpClient _httpClient;

    public SteamInventoryData(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string> ResolveSteamId(string profileReference, CancellationToken cancellationToken = default)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(profileReference, "^7656\\d{13}$")) return profileReference;
        string reference = profileReference.Trim('/');
        string address = profileReference.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? profileReference
            : SteamCommunity + (reference.Contains('/') ? reference : $"id/{reference}");
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
            || !(uri.Host.Equals("steamcommunity.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".steamcommunity.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Enter a Steam profile URL, custom profile URL, or 64-bit Steam ID.");

        string path = uri.AbsolutePath.Trim('/');
        if (path.StartsWith("profiles/", StringComparison.OrdinalIgnoreCase))
        {
            string steamId = path[9..];
            if (!System.Text.RegularExpressions.Regex.IsMatch(steamId, "^7656\\d{13}$")) throw new InvalidOperationException("The SteamID64 was invalid.");
            return steamId;
        }
        if (!path.StartsWith("id/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use a Steam community profile or custom profile URL.");

        string xml = await GetStringWithPacing(new Uri(uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/?xml=1"), cancellationToken);
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(xml, "<steamID64>(\\d{17})</steamID64>");
        if (!match.Success) throw new InvalidOperationException("Steam could not resolve that public profile.");
        return match.Groups[1].Value;
    }

    public async Task<SteamProfileLookupResult> ResolveProfile(string profileReference, CancellationToken cancellationToken = default)
    {
        string steamId = await ResolveSteamId(profileReference, cancellationToken);
        string xml = await GetStringWithPacing(new Uri(_httpClient.BaseAddress!, $"profiles/{steamId}/?xml=1"), cancellationToken);

        if (!System.Text.RegularExpressions.Regex.IsMatch(xml, "<steamID64>\\d{17}</steamID64>"))
            throw new InvalidOperationException("Steam could not resolve that profile. It may be private.");

        return new SteamProfileLookupResult
        {
            SteamId64 = steamId,
            DisplayName = ExtractXmlField(xml, "steamID") ?? steamId,
            AvatarUrl = ExtractXmlField(xml, "avatarMedium") ?? ExtractXmlField(xml, "avatarIcon") ?? string.Empty,
            ProfileUrl = $"{SteamCommunity}profiles/{steamId}"
        };
    }

    private static string? ExtractXmlField(string xml, string tag)
    {
        System.Text.RegularExpressions.Match cdata = System.Text.RegularExpressions.Regex.Match(xml, $"<{tag}><!\\[CDATA\\[(.*?)\\]\\]></{tag}>", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (cdata.Success) return cdata.Groups[1].Value;
        System.Text.RegularExpressions.Match plain = System.Text.RegularExpressions.Regex.Match(xml, $"<{tag}>(.*?)</{tag}>", System.Text.RegularExpressions.RegexOptions.Singleline);
        return plain.Success ? plain.Groups[1].Value : null;
    }

    public async Task<SteamInventoryResult> LoadCs2Inventory(string steamId, CancellationToken cancellationToken = default)
    {
        List<SteamInventoryItem> items = [];
        string? startAssetId = null;
        do
        {
            string address = $"inventory/{steamId}/730/2?l=english&count=2000" + (startAssetId is null ? string.Empty : $"&start_assetid={Uri.EscapeDataString(startAssetId)}");
            using JsonDocument document = JsonDocument.Parse(await GetStringWithPacing(new Uri(_httpClient.BaseAddress!, address), cancellationToken));
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("success", out JsonElement success) || success.GetInt32() != 1)
                throw new InvalidOperationException("Steam could not load this inventory. It may be private or temporarily unavailable.");

            Dictionary<string, JsonElement> descriptions = (root.TryGetProperty("descriptions", out JsonElement descriptionArray) ? descriptionArray.EnumerateArray() : Enumerable.Empty<JsonElement>())
                .ToDictionary(description => $"{description.GetProperty("classid").GetString()}_{description.GetProperty("instanceid").GetString()}");
            foreach (JsonElement asset in root.TryGetProperty("assets", out JsonElement assets) ? assets.EnumerateArray() : Enumerable.Empty<JsonElement>())
            {
                string key = $"{asset.GetProperty("classid").GetString()}_{asset.GetProperty("instanceid").GetString()}";
                if (descriptions.TryGetValue(key, out JsonElement description)) items.Add(MapItem(asset, description));
            }
            startAssetId = root.TryGetProperty("more_items", out JsonElement more) && more.GetBoolean() && root.TryGetProperty("last_assetid", out JsonElement last) ? last.GetString() : null;
        } while (!string.IsNullOrWhiteSpace(startAssetId));

        return new SteamInventoryResult
        {
            SteamId = steamId,
            ProfileUrl = $"{SteamCommunity}profiles/{steamId}",
            Items = items.OrderBy(item => item.Name).ToList()
        };
    }

    private static SteamInventoryItem MapItem(JsonElement asset, JsonElement description)
    {
        List<string> details = [];
        if (description.TryGetProperty("descriptions", out JsonElement lines))
            details.AddRange(lines.EnumerateArray().Select(line => line.TryGetProperty("value", out JsonElement value) ? value.GetString() : null).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().Take(8));

        string quality = string.Empty;
        if (description.TryGetProperty("tags", out JsonElement tags))
        {
            JsonElement tag = tags.EnumerateArray().FirstOrDefault(item => item.TryGetProperty("category", out JsonElement category) && category.GetString() == "Quality");
            if (tag.ValueKind != JsonValueKind.Undefined && tag.TryGetProperty("localized_tag_name", out JsonElement value)) quality = value.GetString() ?? string.Empty;
        }
        string inspect = description.TryGetProperty("actions", out JsonElement actions)
            ? actions.EnumerateArray().Select(action => action.TryGetProperty("link", out JsonElement link) ? link.GetString() : null).FirstOrDefault(link => link?.Contains("steam://rungame", StringComparison.OrdinalIgnoreCase) == true) ?? string.Empty
            : string.Empty;
        inspect = inspect.Replace("%assetid%", asset.GetProperty("assetid").GetString());

        return new SteamInventoryItem
        {
            AssetId = asset.GetProperty("assetid").GetString() ?? string.Empty,
            Name = description.GetProperty("name").GetString() ?? "Unknown item",
            MarketHashName = description.TryGetProperty("market_hash_name", out JsonElement market) ? market.GetString() ?? string.Empty : string.Empty,
            Type = description.TryGetProperty("type", out JsonElement type) ? type.GetString() ?? string.Empty : string.Empty,
            Quality = quality,
            IconUrl = description.TryGetProperty("icon_url_large", out JsonElement large) ? ImageBase + large.GetString() : ImageBase + description.GetProperty("icon_url").GetString(),
            InspectLink = inspect,
            Amount = int.TryParse(asset.GetProperty("amount").GetString(), out int amount) ? amount : 1,
            Tradable = description.TryGetProperty("tradable", out JsonElement tradable) && tradable.GetInt32() == 1,
            Marketable = description.TryGetProperty("marketable", out JsonElement marketable) && marketable.GetInt32() == 1,
            Details = details
        };
    }

    private async Task<string> GetStringWithPacing(Uri uri, CancellationToken cancellationToken)
    {
        await RequestGate.WaitAsync(cancellationToken);
        try
        {
            TimeSpan wait = TimeSpan.FromSeconds(1.2) - (DateTime.UtcNow - LastRequestUtc);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
            LastRequestUtc = DateTime.UtcNow;
            try { return await _httpClient.GetStringAsync(uri, cancellationToken); }
            catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new InvalidOperationException("Steam is temporarily rate limiting inventory requests. Please wait a few minutes and try again.");
            }
            catch (HttpRequestException)
            {
                throw new InvalidOperationException("Steam could not be reached. Please try again shortly.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("Steam took too long to respond. Please try again shortly.");
            }
        }
        finally { RequestGate.Release(); }
    }
}
