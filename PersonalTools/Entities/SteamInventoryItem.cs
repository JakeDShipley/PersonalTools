namespace PersonalTools.Entities
{
    public class SteamInventoryItem
    {
        public string AssetId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string MarketHashName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public string InspectLink { get; set; } = string.Empty;
        public int Amount { get; set; } = 1;
        public bool Tradable { get; set; }
        public bool Marketable { get; set; }
        // Steam descriptions can contain line breaks and simple emphasis around stickers.
        // The data layer sanitises this before it ever reaches the browser.
        public List<string> DetailsHtml { get; set; } = new();
    }

    public class SteamInventoryResult
    {
        public string SteamId { get; set; } = string.Empty;
        public string ProfileUrl { get; set; } = string.Empty;
        public List<SteamInventoryItem> Items { get; set; } = new();
    }

    public class SteamProfileLookupResult
    {
        public string SteamId64 { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string ProfileUrl { get; set; } = string.Empty;
    }

    public sealed class SteamPublicProfile
    {
        public string SteamId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
    }
}
