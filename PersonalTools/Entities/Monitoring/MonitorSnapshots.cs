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

    public double? ApplicationCpuUsagePercent { get; init; }
    public long ApplicationMemoryBytes { get; init; }
    public long ManagedMemoryBytes { get; init; }
    public double? ApplicationMemorySharePercent { get; init; }

    public int ProcessThreadCount { get; init; }
    public int AvailableProcessorCount { get; init; }
    public long ApplicationUptimeSeconds { get; init; }
}

public sealed class DatabaseMonitorSnapshot
{
    public bool IsAvailable { get; init; }
    public string Health { get; init; } = "Unavailable";
    public string Summary { get; init; } = "Database metrics are temporarily unavailable.";
    public DateTime CapturedUtc { get; init; } = DateTime.UtcNow;

    public bool HasRecentSample { get; init; }

    public double ResponseTimeMs { get; init; }
    public double ConnectionUsagePercent { get; init; }
    public long RunningOperations { get; init; }
    public double QueriesPerSecond { get; init; }

    public double SlowQueryPercent { get; init; }
    public double ConnectionErrorPercent { get; init; }

    public double BufferPoolHitPercent { get; init; }
    public string BufferPoolState { get; init; } = "Baseline";

    public double DiskTempTablePercent { get; init; }
    public string DiskTempTableState { get; init; } = "Baseline";

    public double ThreadCreationPercent { get; init; }
    public string ThreadCreationState { get; init; } = "Baseline";

    public long ActiveRowLockWaits { get; init; }
    public string RowLockState { get; init; } = "Healthy";

    public double NetworkReceiveBytesPerSecond { get; init; }
    public double NetworkSendBytesPerSecond { get; init; }

    public long UptimeSeconds { get; init; }

    public int RequiredStructuresAvailable { get; init; }
    public int RequiredStructuresTotal { get; init; }
}
