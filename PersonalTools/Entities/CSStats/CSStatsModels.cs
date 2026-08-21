using System.Text.Json.Serialization;

namespace PersonalTools.Entities.CSStats;

public sealed class LeetifyProfileModel
{
    [JsonPropertyName("privacy_mode")] public string PrivacyMode { get; set; } = string.Empty;
    [JsonPropertyName("winrate")] public double? WinRate { get; set; }
    [JsonPropertyName("total_matches")] public int TotalMatches { get; set; }
    [JsonPropertyName("first_match_date")] public DateTime? FirstMatchDate { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("steam64_id")] public string Steam64Id { get; set; } = string.Empty;
    [JsonPropertyName("ranks")] public LeetifyRanksModel Ranks { get; set; } = new();
    [JsonPropertyName("rating")] public LeetifyRatingModel Rating { get; set; } = new();
    [JsonPropertyName("stats")] public LeetifyStatsModel Stats { get; set; } = new();
}

public sealed class LeetifyRanksModel
{
    [JsonPropertyName("leetify")] public double? Leetify { get; set; }
    [JsonPropertyName("premier")] public int? Premier { get; set; }
    [JsonPropertyName("faceit")] public int? Faceit { get; set; }
    [JsonPropertyName("faceit_elo")] public int? FaceitElo { get; set; }
    [JsonPropertyName("competitive")] public List<LeetifyCompetitiveRankModel> Competitive { get; set; } = [];
}

public sealed class LeetifyCompetitiveRankModel
{
    [JsonPropertyName("map_name")] public string MapName { get; set; } = string.Empty;
    [JsonPropertyName("rank")] public int Rank { get; set; }
}

public sealed class LeetifyRatingModel
{
    [JsonPropertyName("aim")] public double? Aim { get; set; }
    [JsonPropertyName("positioning")] public double? Positioning { get; set; }
    [JsonPropertyName("utility")] public double? Utility { get; set; }
    [JsonPropertyName("clutch")] public double? Clutch { get; set; }
    [JsonPropertyName("opening")] public double? Opening { get; set; }
}

public sealed class LeetifyStatsModel
{
    [JsonPropertyName("accuracy_enemy_spotted")] public double? AccuracyEnemySpotted { get; set; }
    [JsonPropertyName("accuracy_head")] public double? AccuracyHead { get; set; }
    [JsonPropertyName("counter_strafing_good_shots_ratio")] public double? CounterStrafing { get; set; }
    [JsonPropertyName("preaim")] public double? PreAim { get; set; }
    [JsonPropertyName("reaction_time_ms")] public double? ReactionTimeMs { get; set; }
    [JsonPropertyName("spray_accuracy")] public double? SprayAccuracy { get; set; }
    [JsonPropertyName("traded_deaths_success_percentage")] public double? TradedDeaths { get; set; }
    [JsonPropertyName("trade_kills_success_percentage")] public double? TradeKills { get; set; }
    [JsonPropertyName("flashbang_hit_foe_per_flashbang")] public double? EnemiesFlashedPerFlash { get; set; }
    [JsonPropertyName("he_foes_damage_avg")] public double? HeDamage { get; set; }
}

public sealed class CSStatsProfileObj
{
    public string Steam64Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PrivacyMode { get; set; } = string.Empty;
    public string SteamProfileUrl { get; set; } = string.Empty;
    public string LeetifyProfileUrl { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public double? WinRate { get; set; }
    public int TotalMatches { get; set; }
    public int EstimatedWins { get; set; }
    public int EstimatedLosses { get; set; }
    public int ReportCount { get; set; }
    public CSStatsAccountStandingObj AccountStanding { get; set; } = new();
    public DateTime? FirstMatchDate { get; set; }
    public CSStatsRanksObj Ranks { get; set; } = new();
    public CSStatsRatingsObj Ratings { get; set; } = new();
    public CSStatsPerformanceObj Performance { get; set; } = new();
    public CSStatsDataConfidenceObj DataConfidence { get; set; } = new();
}

public sealed class CSStatsAccountStandingObj
{
    public List<CSStatsBanRecordObj> Records { get; set; } = [];
    public List<CSStatsBanSourceObj> Sources { get; set; } = [];
}

public sealed class CSStatsBanRecordObj
{
    public string Platform { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime? BannedUtc { get; set; }
    public int? DaysSinceBan { get; set; }
}

public sealed class CSStatsBanSourceObj
{
    public string Platform { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class CSStatsDataConfidenceObj
{
    public int Score { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public sealed class CSStatsRanksObj
{
    public int? Premier { get; set; }
    public int? FaceitLevel { get; set; }
    public int? FaceitElo { get; set; }
    public double? Leetify { get; set; }
    public List<CSStatsCompetitiveRankObj> Competitive { get; set; } = [];
}

public sealed class CSStatsCompetitiveRankObj
{
    public string MapName { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string RankName { get; set; } = string.Empty;
}

public sealed class CSStatsRatingsObj
{
    public double? Aim { get; set; }
    public double? Positioning { get; set; }
    public double? Utility { get; set; }
    public double? Clutch { get; set; }
    public double? Opening { get; set; }
}

public sealed class CSStatsPerformanceObj
{
    public double? ReactionTimeMs { get; set; }
    public double? PreAimDegrees { get; set; }
    public double? Accuracy { get; set; }
    public double? HeadAccuracy { get; set; }
    public double? SprayAccuracy { get; set; }
    public double? CounterStrafing { get; set; }
    public double? TradedDeaths { get; set; }
    public double? TradeKills { get; set; }
    public double? EnemiesFlashedPerFlash { get; set; }
    public double? HeDamage { get; set; }
}
