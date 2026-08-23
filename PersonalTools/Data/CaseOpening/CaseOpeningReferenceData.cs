using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Entities.CaseOpening;

namespace PersonalTools.Data.CaseOpening;

public interface ICaseOpeningReferenceData
{
    Task<List<CaseOpeningCaseObj>> GetCuratedCases(CancellationToken cancellationToken = default);
    Task<CaseOpeningCaseObj> GetCase(string caseKey, CancellationToken cancellationToken = default);
}

public sealed class CaseOpeningReferenceData : ICaseOpeningReferenceData
{
    private const string CaseApiUrl = "https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/crates.json";
    private const string SkinApiUrl = "https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/skins.json";
    private const string CacheKey = "case-opening-curated-catalogue";
    private static readonly IReadOnlyDictionary<string, string> CuratedCases = new Dictionary<string, string>
    {
        ["esports-2013"] = "crate-4002",
        ["esports-2013-winter"] = "crate-4005",
        ["weapon-case-1"] = "crate-4001",
        ["weapon-case-2"] = "crate-4004",
        ["weapon-case-3"] = "crate-4010",
        ["katowice-2014-challengers"] = "crate-4014",
        ["katowice-2014-legends"] = "crate-4015",
        ["esports-2014-summer"] = "crate-4019",
        ["cologne-2014-legends"] = "crate-4020",
        ["cologne-2014-cobblestone-souvenir"] = "crate-4027",
        ["breakout"] = "crate-4018",
        ["chroma-2"] = "crate-4089",
        ["gamma-2"] = "crate-4281",
        ["atlanta-2017-legends"] = "crate-4323",
        ["glove"] = "crate-4288",
        ["hydra"] = "crate-4352",
        ["spectrum-2"] = "crate-4403",
        ["clutch"] = "crate-4471",
        ["prisma"] = "crate-4598",
        ["shattered-web"] = "crate-4620",
        ["cs20"] = "crate-4669",
        ["prisma-2"] = "crate-4695",
        ["fracture"] = "crate-4698",
        ["broken-fang"] = "crate-4717",
        ["snakebite"] = "crate-4747",
        ["riptide"] = "crate-4790",
        ["stockholm-2021-legends"] = "crate-4803",
        ["antwerp-2022-legends"] = "crate-4832",
        ["recoil"] = "crate-4846",
        ["paris-2023-legends"] = "crate-4890",
        ["copenhagen-2024-legends"] = "crate-4923",
        ["dreams-and-nightmares"] = "crate-4818",
        ["revolution"] = "crate-4880",
        ["kilowatt"] = "crate-4904",
        ["gallery"] = "crate-7003",
        ["fever"] = "crate-7007",
        ["austin-2025-legends"] = "crate-5117"
    };
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public CaseOpeningReferenceData(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    /// <summary>
    /// Keeps the large public crate catalogue off the normal page response. The curated catalogue
    /// is cached server-side, while the browser only receives full contents for the selected case.
    /// </summary>
    public async Task<List<CaseOpeningCaseObj>> GetCuratedCases(CancellationToken cancellationToken = default)
    {
        List<CaseOpeningCaseObj>? cached = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            Task<string> cratesRequest = _httpClient.GetStringAsync(CaseApiUrl, cancellationToken);
            Task<string> skinsRequest = _httpClient.GetStringAsync(SkinApiUrl, cancellationToken);
            await Task.WhenAll(cratesRequest, skinsRequest);
            List<ApiCrate> crates = JsonSerializer.Deserialize<List<ApiCrate>>(await cratesRequest) ?? [];
            List<ApiSkin> skins = JsonSerializer.Deserialize<List<ApiSkin>>(await skinsRequest) ?? [];
            Dictionary<string, ApiSkin> skinsById = skins.ToDictionary(skin => skin.Id, StringComparer.Ordinal);
            return CuratedCases.Select(configured =>
            {
                ApiCrate crate = crates.FirstOrDefault(value => value.Id == configured.Value)
                    ?? throw new InvalidOperationException($"The {configured.Key} catalogue is unavailable right now.");
                bool stickerCapsule = crate.Type.Contains("Sticker", StringComparison.OrdinalIgnoreCase);
                bool souvenirPackage = crate.Type.Equals("Souvenir", StringComparison.OrdinalIgnoreCase);
                return new CaseOpeningCaseObj
                {
                    CaseKey = configured.Key,
                    Name = crate.Name,
                    Type = GetDisplayType(crate),
                    ImageUrl = crate.Image,
                    Odds = stickerCapsule
                        ? CaseOpeningOdds.Stickers
                        : souvenirPackage
                            ? CaseOpeningOdds.Souvenirs
                            : CaseOpeningOdds.Weapons,
                    Items = crate.Contains.Select(item => MapItem(item, false, stickerCapsule, skinsById))
                        .Concat(crate.ContainsRare.Select(item => MapItem(item, true, false, skinsById)))
                        .ToList()
                };
            }).ToList();
        });

        return cached ?? throw new InvalidOperationException("The case catalogue is unavailable right now.");
    }

    public async Task<CaseOpeningCaseObj> GetCase(string caseKey, CancellationToken cancellationToken = default)
    {
        return (await GetCuratedCases(cancellationToken)).FirstOrDefault(item => item.CaseKey == caseKey)
            ?? throw new InvalidOperationException("That case is not available.");
    }

    private static string GetDisplayType(ApiCrate crate)
    {
        if (crate.Type.Contains("Sticker", StringComparison.OrdinalIgnoreCase))
        {
            return "Sticker Capsule";
        }

        if (crate.Type.Equals("Souvenir", StringComparison.OrdinalIgnoreCase))
        {
            return "Souvenir Package";
        }

        return crate.Name.Contains("eSports", StringComparison.OrdinalIgnoreCase)
            ? "Esports Case"
            : "Weapon Case";
    }

    private static CaseOpeningItemObj MapItem(
        ApiItem item,
        bool rareSpecial,
        bool stickerCapsule,
        IReadOnlyDictionary<string, ApiSkin> skinsById)
    {
        skinsById.TryGetValue(item.Id, out ApiSkin? skin);
        return new CaseOpeningItemObj
        {
            SourceItemId = item.Id,
            Name = item.Name,
            MarketHashName = item.Name,
            ImageUrl = item.Image,
            Description = PlainText(skin?.Description),
            WeaponName = skin?.Weapon?.Name ?? (stickerCapsule ? "Sticker" : string.Empty),
            PatternName = skin?.Pattern?.Name ?? string.Empty,
            PaintIndex = item.PaintIndex,
            Phase = item.Phase,
            RarityKey = rareSpecial ? "rare-special" : RarityKey(item.Rarity.Name, stickerCapsule),
            RarityName = rareSpecial ? "Rare Special Item" : item.Rarity.Name,
            RarityColor = rareSpecial ? "#e4ae39" : item.Rarity.Color,
            IsRareSpecial = rareSpecial,
            SupportsStatTrak = skin?.StatTrak == true,
            MinFloat = skin?.MinFloat,
            MaxFloat = skin?.MaxFloat
        };
    }

    private static string PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        HtmlDocument document = new();
        document.LoadHtml(html.Replace("\\n", " ").Replace('\n', ' ').Replace('\r', ' '));
        return WebUtility.HtmlDecode(document.DocumentNode.InnerText).Trim();
    }

    private static string RarityKey(string rarityName, bool stickerCapsule)
    {
        if (stickerCapsule)
        {
            return rarityName switch
            {
                "High Grade" => "high-grade",
                "Remarkable" => "remarkable",
                "Exotic" => "exotic",
                _ => "high-grade"
            };
        }

        return rarityName switch
        {
            "Mil-Spec Grade" => "mil-spec",
            "Restricted" => "restricted",
            "Classified" => "classified",
            "Covert" => "covert",
            _ => "mil-spec"
        };
    }

    private sealed class ApiCrate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;
        [JsonPropertyName("contains")]
        public List<ApiItem> Contains { get; set; } = [];
        [JsonPropertyName("contains_rare")]
        public List<ApiItem> ContainsRare { get; set; } = [];
    }

    private sealed class ApiItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;
        [JsonPropertyName("paint_index")]
        public string PaintIndex { get; set; } = string.Empty;
        [JsonPropertyName("phase")]
        public string Phase { get; set; } = string.Empty;
        [JsonPropertyName("rarity")]
        public ApiRarity Rarity { get; set; } = new();
    }

    private sealed class ApiRarity
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;
    }

    private sealed class ApiSkin
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        [JsonPropertyName("weapon")]
        public ApiNamedValue? Weapon { get; set; }
        [JsonPropertyName("pattern")]
        public ApiNamedValue? Pattern { get; set; }
        [JsonPropertyName("min_float")]
        public decimal? MinFloat { get; set; }
        [JsonPropertyName("max_float")]
        public decimal? MaxFloat { get; set; }
        [JsonPropertyName("stattrak")]
        public bool StatTrak { get; set; }
    }

    private sealed class ApiNamedValue
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}

public static class CaseOpeningOdds
{
    public static List<CaseOpeningOddsObj> Weapons =>
    [
        new() { RarityKey = "mil-spec", RarityName = "Mil-Spec", RarityColor = "#4b69ff", Percentage = 79.92m },
        new() { RarityKey = "restricted", RarityName = "Restricted", RarityColor = "#8847ff", Percentage = 15.98m },
        new() { RarityKey = "classified", RarityName = "Classified", RarityColor = "#d32ce6", Percentage = 3.20m },
        new() { RarityKey = "covert", RarityName = "Covert", RarityColor = "#eb4b4b", Percentage = 0.64m },
        new() { RarityKey = "rare-special", RarityName = "Rare Special Item", RarityColor = "#e4ae39", Percentage = 0.26m }
    ];

    public static List<CaseOpeningOddsObj> Stickers =>
    [
        new() { RarityKey = "high-grade", RarityName = "High Grade", RarityColor = "#4b69ff", Percentage = 80m },
        new() { RarityKey = "remarkable", RarityName = "Remarkable (Holo)", RarityColor = "#8847ff", Percentage = 16m },
        new() { RarityKey = "exotic", RarityName = "Exotic (Foil)", RarityColor = "#d32ce6", Percentage = 4m }
    ];

    // Souvenir packages have no knife/glove special-item pool. Keeping their odds separate means
    // the simulated roll can only select rarities that their curated contents genuinely contain.
    public static List<CaseOpeningOddsObj> Souvenirs =>
    [
        new() { RarityKey = "mil-spec", RarityName = "Base collection", RarityColor = "#4b69ff", Percentage = 80m },
        new() { RarityKey = "restricted", RarityName = "Restricted", RarityColor = "#8847ff", Percentage = 16m },
        new() { RarityKey = "classified", RarityName = "Classified", RarityColor = "#d32ce6", Percentage = 3.4m },
        new() { RarityKey = "covert", RarityName = "Covert", RarityColor = "#eb4b4b", Percentage = 0.6m }
    ];
}
