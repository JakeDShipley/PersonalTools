namespace PersonalTools.Entities.Monitoring;

public sealed class ServerMonitorSnapshot
{
    public bool IsAvailable { get; init; }
    public string Health { get; init; } = "Unavailable";
    public string Summary { get; init; } = "Server metrics are temporarily unavailable.";
    public DateTime CapturedUtc { get; init; } = DateTime.UtcNow;
    public double? CpuUsagePercent { get; init; }
    public double? MemoryUsagePercent { get; init; }
    public double? StorageUsagePercent { get; init; }
    public long ApplicationMemoryBytes { get; init; }
    public long ManagedMemoryBytes { get; init; }
    public long ApplicationUptimeSeconds { get; init; }
}

public sealed class DatabaseMonitorSnapshot
{
    public bool IsAvailable { get; init; }
    public string Health { get; init; } = "Unavailable";
    public string Summary { get; init; } = "Database metrics are temporarily unavailable.";
    public DateTime CapturedUtc { get; init; } = DateTime.UtcNow;
    public double ResponseTimeMs { get; init; }
    public double ConnectionUsagePercent { get; init; }
    public long RunningOperations { get; init; }
    public double QueriesPerSecond { get; init; }
    public double SlowQueryPercent { get; init; }
    public double ConnectionErrorPercent { get; init; }
    public long UptimeSeconds { get; init; }
    public int RequiredStructuresAvailable { get; init; }
    public int RequiredStructuresTotal { get; init; }
}
