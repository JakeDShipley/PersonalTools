using System.Text.Json.Serialization;

namespace PersonalTools.Entities.GrandExchange
{
    /// <summary>
    /// Represents the latest high and low trades returned by the OSRS Wiki prices endpoint.
    /// </summary>
    public class OsrsWikiLatestPriceModel
    {
        [JsonPropertyName("high")]
        public int? High { get; set; }

        [JsonPropertyName("highTime")]
        public long? HighTime { get; set; }

        [JsonPropertyName("low")]
        public int? Low { get; set; }

        [JsonPropertyName("lowTime")]
        public long? LowTime { get; set; }
    }
}
