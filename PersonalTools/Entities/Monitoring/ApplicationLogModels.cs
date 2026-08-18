namespace PersonalTools.Entities.Monitoring;

public sealed class ApplicationLogEntry
{
    public long Id { get; init; }
    public DateTime CapturedUtc { get; init; }
    public string Level { get; init; } = string.Empty;
    public int EventId { get; init; }
    public string? EventName { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Exception { get; init; }
}

public sealed class ApplicationLogResult
{
    public DateTime CapturedUtc { get; init; }
    public DateTime CaptureStartedUtc { get; init; }
    public long LatestId { get; init; }
    public int RetainedCount { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public int CriticalCount { get; init; }
    public List<ApplicationLogEntry> Entries { get; init; } = new();
}