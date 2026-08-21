namespace PersonalTools.Entities.CSMatches;

public sealed class CSMatchDbModel
{
    public Guid MatchId { get; set; }
    public string StartSide { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public int TeamScore { get; set; }
    public int OpponentScore { get; set; }
    public int OvertimeCount { get; set; }
    public string? LeetifyMatchId { get; set; }
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
}
