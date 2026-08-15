namespace PersonalTools.Entities.CSMatches;

public sealed class CSMatchDbModel
{
    public string MatchId { get; init; } = string.Empty;
    public string StartSide { get; init; } = string.Empty;
    public string MapName { get; init; } = string.Empty;
    public string GameType { get; init; } = string.Empty;
    public int TeamScore { get; init; }
    public int OpponentScore { get; init; }
    public int OvertimeCount { get; init; }
    public string? LeetifyMatchId { get; init; }
    public DateTime Created { get; init; }
    public DateTime Updated { get; init; }
}
