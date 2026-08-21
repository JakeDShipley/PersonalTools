namespace PersonalTools.Entities.CSDemos;

/// <summary>
/// Database transport shape for a user's cached demo catalogue. Demo files never pass through
/// Personal Tools; only lightweight metadata and the provider's short-lived source link are kept.
/// </summary>
public sealed class CSDemoDbModel
{
    public Guid DemoId { get; set; }
    public string Steam64Id { get; set; } = string.Empty;
    public string LeetifyMatchId { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public int TeamScore { get; set; }
    public int OpponentScore { get; set; }
    public bool IsWin { get; set; }
    public string ReplayUrl { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public DateTime PlayedAtUtc { get; set; }
    public DateTime RefreshedUtc { get; set; }
}

public sealed class CSDemoObj
{
    public Guid DemoId { get; set; }
    public string LeetifyMatchId { get; set; } = string.Empty;
    public DateTime PlayedAtUtc { get; set; }
    public string MapName { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public int TeamScore { get; set; }
    public int OpponentScore { get; set; }
    public bool IsWin { get; set; }
    public string ReplayUrl { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}

public sealed class CSDemoLibraryObj
{
    public string Steam64Id { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int RecentMatchCount { get; set; }
    public int AvailableDemoCount { get; set; }
    public DateTime? LastRefreshedUtc { get; set; }
    public bool WasRefreshed { get; set; }
    public List<CSDemoObj> Demos { get; set; } = [];
}
