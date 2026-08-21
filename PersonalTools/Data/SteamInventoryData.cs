using System.Text.Json;
using System.Net;
using System.Text;
using System.Xml.Linq;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Entities;

namespace PersonalTools.Data;

public interface ISteamInventoryData
{
    Task<string> ResolveSteamId(string profileReference, CancellationToken cancellationToken = default);
    Task<SteamProfileLookupResult> ResolveProfile(string profileReference, CancellationToken cancellationToken = default);
    Task<SteamPublicProfile?> GetPublicProfile(string steamId, CancellationToken cancellationToken = default);
    Task<SteamInventoryResult> LoadCs2Inventory(string steamId, CancellationToken cancellationToken = default);
}

public sealed class SteamInventoryData : ISteamInventoryData
{
    private const string SteamCommunity = "https://steamcommunity.com/";
    private const string ImageBase = "https://community.akamai.steamstatic.com/economy/image/";
    private static readonly SemaphoreSlim RequestGate = new(1, 1);
    private static DateTime LastRequestUtc = DateTime.MinValue;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public SteamInventoryData(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

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

    public async Task<SteamPublicProfile?> GetPublicProfile(string steamId, CancellationToken cancellationToken = default)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(steamId, "^7656\\d{13}$")) return null;
        string cacheKey = $"steam-public-profile:{steamId}";
        if (_cache.TryGetValue(cacheKey, out SteamPublicProfile? cachedProfile)) return cachedProfile;

        try
        {
            string xml = await GetStringWithPacing(new Uri($"{SteamCommunity}profiles/{steamId}/?xml=1"), cancellationToken);
            XDocument document = XDocument.Parse(xml, LoadOptions.None);
            XElement? profile = document.Root;
            if (profile is null) return null;

            SteamPublicProfile result = new()
            {
                SteamId = profile.Element("steamID64")?.Value.Trim() ?? steamId,
                DisplayName = profile.Element("steamID")?.Value.Trim() ?? string.Empty,
                AvatarUrl = profile.Element("avatarFull")?.Value.Trim() ?? string.Empty
            };
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static SteamInventoryItem MapItem(JsonElement asset, JsonElement description)
    {
        List<string> detailsHtml = [];
        if (description.TryGetProperty("descriptions", out JsonElement lines))
        {
            detailsHtml.AddRange(lines.EnumerateArray()
                .Select(line => line.TryGetProperty("value", out JsonElement value) ? SanitiseDescriptionHtml(value.GetString()) : string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(8));
        }

        string rarity = string.Empty;
        if (description.TryGetProperty("tags", out JsonElement tags))
        {
            // Steam's Quality tag identifies the item variant (for example StatTrak™), while
            // players expect this field to be the actual CS rarity: Covert, Mil-Spec and so on.
            JsonElement tag = tags.EnumerateArray().FirstOrDefault(item => item.TryGetProperty("category", out JsonElement category) && category.GetString() == "Rarity");
            if (tag.ValueKind != JsonValueKind.Undefined && tag.TryGetProperty("localized_tag_name", out JsonElement value)) rarity = value.GetString() ?? string.Empty;
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
            Rarity = rarity,
            IconUrl = description.TryGetProperty("icon_url_large", out JsonElement large) ? ImageBase + large.GetString() : ImageBase + description.GetProperty("icon_url").GetString(),
            InspectLink = inspect,
            Amount = int.TryParse(asset.GetProperty("amount").GetString(), out int amount) ? amount : 1,
            Tradable = description.TryGetProperty("tradable", out JsonElement tradable) && tradable.GetInt32() == 1,
            Marketable = description.TryGetProperty("marketable", out JsonElement marketable) && marketable.GetInt32() == 1,
            DetailsHtml = detailsHtml
        };
    }

    /// <summary>
    /// Steam description values are HTML fragments. Preserve the useful presentation around
    /// stickers and line breaks, but deliberately discard links, images, attributes and every
    /// other tag so upstream content can never become executable markup in the application.
    /// </summary>
    private static string SanitiseDescriptionHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        HtmlDocument document = new();
        document.LoadHtml(value);
        StringBuilder output = new();
        AppendSafeDescriptionNodes(document.DocumentNode.ChildNodes, output);
        return output.ToString().Trim();
    }

    private static void AppendSafeDescriptionNodes(HtmlNodeCollection nodes, StringBuilder output)
    {
        foreach (HtmlNode node in nodes)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                output.Append(WebUtility.HtmlEncode(HtmlEntity.DeEntitize(node.InnerText)));
                continue;
            }

            string name = node.Name.ToLowerInvariant();
            if (name == "br")
            {
                output.Append("<br>");
                continue;
            }

            string? tag = name switch
            {
                "b" or "strong" => "strong",
                "i" or "em" => "em",
                _ => null
            };

            if (tag is not null) output.Append('<').Append(tag).Append('>');
            AppendSafeDescriptionNodes(node.ChildNodes, output);
            if (tag is not null) output.Append("</").Append(tag).Append('>');
        }
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
