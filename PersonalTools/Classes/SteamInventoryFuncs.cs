using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Entities;

namespace PersonalTools.Classes
{
    public interface ISteamInventoryFuncs { Task<SteamInventoryResult> GetCs2Inventory(string profileReference); }

    public class SteamInventoryFuncs : ISteamInventoryFuncs
    {
        private const string SteamCommunity = "https://steamcommunity.com/";
        private const string ImageBase = "https://community.akamai.steamstatic.com/economy/image/";
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private static readonly SemaphoreSlim RequestGate = new(1, 1);
        private static DateTime LastRequestUtc = DateTime.MinValue;
        public SteamInventoryFuncs(HttpClient httpClient, IMemoryCache cache) { _httpClient = httpClient; _cache = cache; }

        public async Task<SteamInventoryResult> GetCs2Inventory(string profileReference)
        {
            string steamId = await ResolveSteamId(profileReference);
            return await _cache.GetOrCreateAsync($"steam-cs2-{steamId}", async entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5); return await LoadInventory(steamId); }) ?? new SteamInventoryResult();
        }

        private async Task<string> ResolveSteamId(string value)
        {
            value = value?.Trim() ?? string.Empty;
            if (System.Text.RegularExpressions.Regex.IsMatch(value, "^7656\\d{13}$")) return value;
            if (!Uri.TryCreate(value.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? value : SteamCommunity + value.Trim('/'), UriKind.Absolute, out Uri? uri) || !uri.Host.EndsWith("steamcommunity.com", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Enter a Steam profile URL, custom profile URL, or 64-bit Steam ID.");
            string path = uri.AbsolutePath.Trim('/');
            if (path.StartsWith("profiles/", StringComparison.OrdinalIgnoreCase)) return path[9..];
            if (!path.StartsWith("id/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use a Steam community profile or custom profile URL.");
            string xml = await GetStringWithPacing(new Uri(uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/?xml=1"));
            var match = System.Text.RegularExpressions.Regex.Match(xml, "<steamID64>(\\d{17})</steamID64>");
            if (!match.Success) throw new InvalidOperationException("Steam could not resolve that public profile.");
            return match.Groups[1].Value;
        }

        private async Task<SteamInventoryResult> LoadInventory(string steamId)
        {
            List<SteamInventoryItem> items = new(); string? startAssetId = null;
            do
            {
                string address = $"{SteamCommunity}inventory/{steamId}/730/2?l=english&count=2000" + (startAssetId is null ? string.Empty : $"&start_assetid={Uri.EscapeDataString(startAssetId)}");
                using JsonDocument document = JsonDocument.Parse(await GetStringWithPacing(new Uri(address)));
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("success", out JsonElement success) || success.GetInt32() != 1) throw new InvalidOperationException("Steam could not load this inventory. It may be private or temporarily unavailable.");
                Dictionary<string, JsonElement> descriptions = (root.TryGetProperty("descriptions", out JsonElement descriptionArray) ? descriptionArray.EnumerateArray() : Enumerable.Empty<JsonElement>()).ToDictionary(d => $"{d.GetProperty("classid").GetString()}_{d.GetProperty("instanceid").GetString()}");
                foreach (JsonElement asset in root.TryGetProperty("assets", out JsonElement assets) ? assets.EnumerateArray() : Enumerable.Empty<JsonElement>()) { string key = $"{asset.GetProperty("classid").GetString()}_{asset.GetProperty("instanceid").GetString()}"; if (descriptions.TryGetValue(key, out JsonElement description)) items.Add(MapItem(asset, description)); }
                startAssetId = root.TryGetProperty("more_items", out JsonElement more) && more.GetBoolean() && root.TryGetProperty("last_assetid", out JsonElement last) ? last.GetString() : null;
            } while (!string.IsNullOrWhiteSpace(startAssetId));
            return new SteamInventoryResult { SteamId = steamId, ProfileUrl = $"{SteamCommunity}profiles/{steamId}", Items = items.OrderBy(i => i.Name).ToList() };
        }

        private static SteamInventoryItem MapItem(JsonElement asset, JsonElement description)
        {
            List<string> details = new(); if (description.TryGetProperty("descriptions", out JsonElement lines)) details.AddRange(lines.EnumerateArray().Select(x => x.TryGetProperty("value", out JsonElement value) ? value.GetString() : null).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Take(8));
            string quality = string.Empty; if (description.TryGetProperty("tags", out JsonElement tags)) { JsonElement tag = tags.EnumerateArray().FirstOrDefault(t => t.TryGetProperty("category", out JsonElement c) && c.GetString() == "Quality"); if (tag.ValueKind != JsonValueKind.Undefined && tag.TryGetProperty("localized_tag_name", out JsonElement q)) quality = q.GetString() ?? string.Empty; }
            string inspect = description.TryGetProperty("actions", out JsonElement actions) ? actions.EnumerateArray().Select(a => a.TryGetProperty("link", out JsonElement l) ? l.GetString() : null).FirstOrDefault(l => l?.Contains("steam://rungame", StringComparison.OrdinalIgnoreCase) == true) ?? string.Empty : string.Empty;
            inspect = inspect.Replace("%assetid%", asset.GetProperty("assetid").GetString());
            return new SteamInventoryItem { AssetId = asset.GetProperty("assetid").GetString() ?? string.Empty, Name = description.GetProperty("name").GetString() ?? "Unknown item", MarketHashName = description.TryGetProperty("market_hash_name", out JsonElement market) ? market.GetString() ?? string.Empty : string.Empty, Type = description.TryGetProperty("type", out JsonElement type) ? type.GetString() ?? string.Empty : string.Empty, Quality = quality, IconUrl = description.TryGetProperty("icon_url_large", out JsonElement large) ? ImageBase + large.GetString() : ImageBase + description.GetProperty("icon_url").GetString(), InspectLink = inspect, Amount = int.TryParse(asset.GetProperty("amount").GetString(), out int amount) ? amount : 1, Tradable = description.TryGetProperty("tradable", out JsonElement tradable) && tradable.GetInt32() == 1, Marketable = description.TryGetProperty("marketable", out JsonElement marketable) && marketable.GetInt32() == 1, Details = details };
        }
        private async Task<string> GetStringWithPacing(Uri uri)
        {
            await RequestGate.WaitAsync();
            try
            {
                TimeSpan wait = TimeSpan.FromSeconds(1.2) - (DateTime.UtcNow - LastRequestUtc);
                if (wait > TimeSpan.Zero) await Task.Delay(wait);
                LastRequestUtc = DateTime.UtcNow;
                try { return await _httpClient.GetStringAsync(uri); }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new InvalidOperationException("Steam is temporarily rate limiting inventory requests. Please wait a few minutes and try again.");
                }
            }
            finally { RequestGate.Release(); }
        }
    }
}
