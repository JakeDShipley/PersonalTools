using System.Text.Json.Serialization;

namespace PersonalTools.Entities.GrandExchange
{
    /// <summary>
    /// Represents one item returned by the OSRS Wiki mapping endpoint.
    /// </summary>
    public class OsrsWikiItemModel
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("examine")]
        public string Examine { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;

        [JsonPropertyName("members")]
        public bool Members { get; set; }

        [JsonPropertyName("limit")]
        public int? Limit { get; set; }
    }
}
