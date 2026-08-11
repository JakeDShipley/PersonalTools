namespace PersonalTools.Entities.GrandExchange
{
    /// <summary>
    /// Contains the item details and latest Grand Exchange prices shown on the lookup page.
    /// </summary>
    public class GrandExchangeItemObj
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public bool MembersOnly { get; set; }
        public int BuyLimit { get; set; }
        public int HighPrice { get; set; }
        public DateTime? HighPriceUpdated { get; set; }
        public int LowPrice { get; set; }
        public DateTime? LowPriceUpdated { get; set; }
        public bool HasPriceData => HighPrice > 0 || LowPrice > 0;
    }
}
