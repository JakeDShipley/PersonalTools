using PersonalTools.Data;
using PersonalTools.Data.CSStats;
using PersonalTools.Entities;
using PersonalTools.Entities.CSStats;

namespace PersonalTools.Classes.CSStats;

public interface ICSStatsFuncs
{
    Task<CSStatsProfileObj> GetProfile(Guid userId, string profileReference, CancellationToken cancellationToken = default);
}

public sealed class CSStatsFuncs : ICSStatsFuncs
{
    private static readonly string[] CompetitiveRanks =
    [
        "Unranked", "Silver I", "Silver II", "Silver III", "Silver IV", "Silver Elite", "Silver Elite Master",
        "Gold Nova I", "Gold Nova II", "Gold Nova III", "Gold Nova Master", "Master Guardian I", "Master Guardian II",
        "Master Guardian Elite", "Distinguished Master Guardian", "Legendary Eagle", "Legendary Eagle Master",
        "Supreme Master First Class", "The Global Elite"
    ];

    private readonly ISteamInventoryData _steamData;
    private readonly ILeetifyProfileData _leetifyData;
    private readonly IReportedPlayersFuncs _reports;
    private readonly IAccountStandingData _standing;
    private readonly IAppSettingsFuncs _settings;

    public CSStatsFuncs(ISteamInventoryData steamData, ILeetifyProfileData leetifyData, IReportedPlayersFuncs reports, IAccountStandingData standing, IAppSettingsFuncs settings)
    {
        _steamData = steamData;
        _leetifyData = leetifyData;
        _reports = reports;
        _standing = standing;
        _settings = settings;
    }

    public async Task<CSStatsProfileObj> GetProfile(Guid userId, string profileReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileReference))
            throw new InvalidOperationException("Enter a Steam profile URL, custom name, or SteamID64.");

        string steam64Id = await _steamData.ResolveSteamId(profileReference.Trim(), cancellationToken);
        Task<SteamPublicProfile?> steamProfileTask = _steamData.GetPublicProfile(steam64Id, cancellationToken);
        LeetifyProfileModel profile = await _leetifyData.GetProfile(steam64Id, cancellationToken);
        SteamPublicProfile? steamProfile = await steamProfileTask;
        LeetifyRanksModel ranks = profile.Ranks ?? new LeetifyRanksModel();
        LeetifyRatingModel rating = profile.Rating ?? new LeetifyRatingModel();
        LeetifyStatsModel stats = profile.Stats ?? new LeetifyStatsModel();
        double? winRate = profile.WinRate is null ? null : Math.Clamp(profile.WinRate.Value > 1 ? profile.WinRate.Value / 100 : profile.WinRate.Value, 0, 1);
        int estimatedWins = winRate is null ? 0 : (int)Math.Round(profile.TotalMatches * winRate.Value, MidpointRounding.AwayFromZero);

        CSStatsProfileObj result = new()
        {
            Steam64Id = string.IsNullOrWhiteSpace(profile.Steam64Id) ? steam64Id : profile.Steam64Id,
            Name = !string.IsNullOrWhiteSpace(steamProfile?.DisplayName)
                ? steamProfile.DisplayName
                : string.IsNullOrWhiteSpace(profile.Name) ? "CS2 player" : profile.Name,
            PrivacyMode = profile.PrivacyMode,
            SteamProfileUrl = $"https://steamcommunity.com/profiles/{steam64Id}",
            LeetifyProfileUrl = $"https://leetify.com/app/profile/{steam64Id}",
            AvatarUrl = steamProfile?.AvatarUrl ?? string.Empty,
            WinRate = winRate,
            TotalMatches = profile.TotalMatches,
            EstimatedWins = estimatedWins,
            EstimatedLosses = Math.Max(0, profile.TotalMatches - estimatedWins),
            FirstMatchDate = profile.FirstMatchDate,
            Ranks = new CSStatsRanksObj
            {
                Premier = ranks.Premier,
                FaceitLevel = ranks.Faceit,
                FaceitElo = ranks.FaceitElo,
                Leetify = ranks.Leetify,
                Competitive = (ranks.Competitive ?? [])
                    .Where(rank => rank.Rank > 0)
                    .OrderByDescending(rank => rank.Rank)
                    .Select(rank => new CSStatsCompetitiveRankObj
                    {
                        MapName = FormatMapName(rank.MapName),
                        Rank = rank.Rank,
                        RankName = rank.Rank >= 0 && rank.Rank < CompetitiveRanks.Length ? CompetitiveRanks[rank.Rank] : $"Rank {rank.Rank}"
                    }).ToList()
            },
            Ratings = new CSStatsRatingsObj
            {
                Aim = rating.Aim,
                Positioning = rating.Positioning,
                Utility = rating.Utility,
                Clutch = rating.Clutch,
                Opening = rating.Opening
            },
            Performance = new CSStatsPerformanceObj
            {
                ReactionTimeMs = stats.ReactionTimeMs,
                PreAimDegrees = stats.PreAim,
                Accuracy = AsPercent(stats.AccuracyEnemySpotted),
                HeadAccuracy = AsPercent(stats.AccuracyHead),
                SprayAccuracy = AsPercent(stats.SprayAccuracy),
                CounterStrafing = AsPercent(stats.CounterStrafing),
                TradedDeaths = AsPercent(stats.TradedDeaths),
                TradeKills = AsPercent(stats.TradeKills),
                EnemiesFlashedPerFlash = stats.EnemiesFlashedPerFlash,
                HeDamage = stats.HeDamage
            }
        };

        result.DataConfidence = BuildDataConfidence(result);
        result.ReportCount = await _reports.GetReportCount(result.Steam64Id, cancellationToken);
        result.AccountStanding = await _standing.GetStanding(result.Steam64Id, await _settings.GetSecret(userId, AppSettingKey.SteamWebApiKey, cancellationToken), cancellationToken);
        return result;
    }

    private static double? AsPercent(double? value) => value;

    private static CSStatsDataConfidenceObj BuildDataConfidence(CSStatsProfileObj profile)
    {
        double[] signals =
        [
            profile.Ratings.Aim ?? double.NaN,
            profile.Ratings.Positioning ?? double.NaN,
            profile.Ratings.Utility ?? double.NaN,
            profile.Ratings.Clutch ?? double.NaN,
            profile.Ratings.Opening ?? double.NaN,
            profile.Performance.ReactionTimeMs ?? double.NaN,
            profile.Performance.PreAimDegrees ?? double.NaN,
            profile.Performance.Accuracy ?? double.NaN,
            profile.Performance.HeadAccuracy ?? double.NaN,
            profile.Performance.CounterStrafing ?? double.NaN
        ];
        int availableSignals = signals.Count(value => !double.IsNaN(value));
        int sampleScore = (int)Math.Round(Math.Min(profile.TotalMatches, 250) / 250d * 50);
        int signalScore = (int)Math.Round(availableSignals / 10d * 35);
        int rankTypes = (profile.Ranks.Premier.HasValue ? 1 : 0)
            + (profile.Ranks.FaceitLevel.HasValue ? 1 : 0)
            + (profile.Ranks.Competitive.Count > 0 ? 1 : 0);
        int score = Math.Clamp(sampleScore + signalScore + rankTypes * 5, 0, 100);

        return new CSStatsDataConfidenceObj
        {
            Score = score,
            Label = score >= 80 ? "Strong coverage" : score >= 50 ? "Moderate coverage" : "Limited coverage",
            Explanation = "Shows how complete this profile view is, based on tracked matches, available Leetify signals and rank coverage. It is not Valve Trust Factor and does not assess player conduct."
        };
    }

    private static string FormatMapName(string mapName)
    {
        string clean = mapName.Replace("de_", string.Empty, StringComparison.OrdinalIgnoreCase).Replace('_', ' ');
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(clean);
    }
}
