using System.Text.Json.Serialization;

namespace PersonalTools.Entities.CSMatches
{
    public class LeetifyTeamScoreModel
    {
        [JsonPropertyName("team_number")]
        public int TeamNumber { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }
    }

    public class LeetifyPlayerStatModel
    {
        [JsonPropertyName("steam64_id")]
        public string Steam64Id { get; set; } = string.Empty;

        [JsonPropertyName("initial_team_number")]
        public int InitialTeamNumber { get; set; }
    }

    public class LeetifyMatchModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("finished_at")]
        public DateTime FinishedAt { get; set; }

        [JsonPropertyName("data_source")]
        public string DataSource { get; set; } = string.Empty;

        [JsonPropertyName("map_name")]
        public string MapName { get; set; } = string.Empty;

        [JsonPropertyName("team_scores")]
        public List<LeetifyTeamScoreModel> TeamScores { get; set; } = new();

        [JsonPropertyName("stats")]
        public List<LeetifyPlayerStatModel> Stats { get; set; } = new();
    }

    public class CSMatchLeetifyPreviewObj
    {
        public string LeetifyMatchId { get; set; } = string.Empty;
        public DateTime PlayedAtUtc { get; set; }
        public string MapName { get; set; } = string.Empty;
        public string GameType { get; set; } = string.Empty;
        public string StartSide { get; set; } = string.Empty;
        public int TeamScore { get; set; }
        public int OpponentScore { get; set; }
        public int OvertimeCount { get; set; }
        public bool AlreadyImported { get; set; }
    }
}
