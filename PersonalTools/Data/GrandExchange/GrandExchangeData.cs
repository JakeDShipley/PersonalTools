using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Entities.GrandExchange;

namespace PersonalTools.Data.GrandExchange
{
    public class GrandExchangeData : IGrandExchangeData
    {
        private const string MappingCacheKey = "OsrsWikiItemMapping";
        private const string MappingUrl = "api/v1/osrs/mapping";
        private const string LatestPricesUrl = "api/v1/osrs/latest";
        private const string WikiItemImageBaseUrl = "https://oldschool.runescape.wiki/images/";

        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;

        public GrandExchangeData(HttpClient httpClient, IMemoryCache memoryCache)
        {
            _httpClient = httpClient;
            _memoryCache = memoryCache;
        }

        public async Task<GrandExchangeLookupResultObj> SearchItems(string searchTerm)
        {
            try
            {
                List<OsrsWikiItemModel> itemMapping = await GetItemMapping();

                List<OsrsWikiItemModel> matches = itemMapping
                    .Where(x => x.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Name.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(x => x.Name)
                    .Take(24)
                    .ToList();

                if (!matches.Any())
                    return new GrandExchangeLookupResultObj();

                Dictionary<string, OsrsWikiLatestPriceModel> latestPrices = await GetLatestPrices();

                return new GrandExchangeLookupResultObj
                {
                    Items = matches.Select(x => new GrandExchangeItemObj
                    {
                        ItemId = x.Id,
                        Name = x.Name,
                        Description = x.Examine,
                        IconUrl = GetItemIconUrl(x.Icon),
                        MembersOnly = x.Members,
                        BuyLimit = x.Limit ?? 0,
                        HighPrice = GetPrice(latestPrices, x.Id, x => x.High),
                        HighPriceUpdated = GetPriceUpdated(latestPrices, x.Id, x => x.HighTime),
                        LowPrice = GetPrice(latestPrices, x.Id, x => x.Low),
                        LowPriceUpdated = GetPriceUpdated(latestPrices, x.Id, x => x.LowTime)
                    }).ToList()
                };
            }
            catch (HttpRequestException)
            {
                return new GrandExchangeLookupResultObj
                {
                    ErrorMessage = "The Grand Exchange price service could not be reached. Please try again shortly."
                };
            }
            catch (JsonException)
            {
                return new GrandExchangeLookupResultObj
                {
                    ErrorMessage = "The Grand Exchange price service returned an unexpected response. Please try again shortly."
                };
            }
        }

        /// <summary>
        /// Builds the direct OSRS Wiki image URL from the icon file name
        /// returned by the item mapping API.
        /// </summary>
        /// <param name="icon"></param>
        /// <returns></returns>
        private static string GetItemIconUrl(string icon)
        {
            if (string.IsNullOrWhiteSpace(icon))
                return string.Empty;

            string imageFileName = icon.Trim().Replace(" ", "_");

            return $"{WikiItemImageBaseUrl}{Uri.EscapeDataString(imageFileName)}";
        }

        private async Task<List<OsrsWikiItemModel>> GetItemMapping()
        {
            if (_memoryCache.TryGetValue(MappingCacheKey, out List<OsrsWikiItemModel>? cachedItemMapping) && cachedItemMapping != null)
                return cachedItemMapping;

            List<OsrsWikiItemModel>? itemMapping = await _httpClient.GetFromJsonAsync<List<OsrsWikiItemModel>>(MappingUrl);

            itemMapping ??= new List<OsrsWikiItemModel>();

            // The mapping is large and changes infrequently, so retain it locally between searches.
            _memoryCache.Set(MappingCacheKey, itemMapping, TimeSpan.FromHours(12));

            return itemMapping;
        }

        private async Task<Dictionary<string, OsrsWikiLatestPriceModel>> GetLatestPrices()
        {
            OsrsWikiLatestPricesResponse? response = await _httpClient.GetFromJsonAsync<OsrsWikiLatestPricesResponse>(LatestPricesUrl);

            return response?.Data ?? new Dictionary<string, OsrsWikiLatestPriceModel>();
        }

        private static int GetPrice(Dictionary<string, OsrsWikiLatestPriceModel> latestPrices, int itemId, Func<OsrsWikiLatestPriceModel, int?> getPrice)
        {
            return latestPrices.TryGetValue(itemId.ToString(), out OsrsWikiLatestPriceModel? price) ? getPrice(price) ?? 0 : 0;
        }

        private static DateTime? GetPriceUpdated(Dictionary<string, OsrsWikiLatestPriceModel> latestPrices, int itemId, Func<OsrsWikiLatestPriceModel, long?> getUpdatedTime)
        {
            if (!latestPrices.TryGetValue(itemId.ToString(), out OsrsWikiLatestPriceModel? price))
                return null;

            long? unixTimestamp = getUpdatedTime(price);

            return unixTimestamp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(unixTimestamp.Value).LocalDateTime : null;
        }

        private class OsrsWikiLatestPricesResponse
        {
            public Dictionary<string, OsrsWikiLatestPriceModel> Data { get; set; } = new();
        }
    }
}
