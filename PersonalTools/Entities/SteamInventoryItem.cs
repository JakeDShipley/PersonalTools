namespace PersonalTools.Entities
{
    public class SteamInventoryItem
    {
        public string AssetId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string MarketHashName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public string InspectLink { get; set; } = string.Empty;
        public int Amount { get; set; } = 1;
        public bool Tradable { get; set; }
        public bool Marketable { get; set; }
        public List<string> Details { get; set; } = new();
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
}
