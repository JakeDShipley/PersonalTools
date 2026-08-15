using PersonalTools.Data.Monitoring;
using PersonalTools.Entities.Monitoring;
using Microsoft.Extensions.Caching.Memory;

namespace PersonalTools.Classes.Monitoring;

public interface IDatabaseMonitorFuncs
{
    Task<DatabaseMonitorSnapshot> GetSnapshot(CancellationToken cancellationToken = default);
}

public sealed class DatabaseMonitorFuncs : IDatabaseMonitorFuncs
{
    private const int RequiredStructureCount = 12;
    private readonly IDatabaseMonitorData _data;
    private readonly ILogger<DatabaseMonitorFuncs> _logger;
    private readonly IMemoryCache _cache;

    public DatabaseMonitorFuncs(IDatabaseMonitorData data, ILogger<DatabaseMonitorFuncs> logger, IMemoryCache cache)
    {
        _data = data;
        _logger = logger;
        _cache = cache;
    }

    public Task<DatabaseMonitorSnapshot> GetSnapshot(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _cache.GetOrCreateAsync("monitoring-database-snapshot", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(12);
            return CollectSnapshot(CancellationToken.None);
        })!;
    }

    private async Task<DatabaseMonitorSnapshot> CollectSnapshot(CancellationToken cancellationToken)
    {
        try
        {
            DatabaseMonitorReading? reading = await _data.GetReading(cancellationToken);
            if (reading is null) return Unavailable();

            double connectionUsage = Percent(reading.ThreadsConnected, reading.MaxConnections);
            double slowQueryPercent = Percent(reading.SlowQueries, reading.Questions);
            double connectionErrorPercent = Percent(reading.AbortedConnects, reading.Questions + reading.AbortedConnects);
            double queriesPerSecond = reading.UptimeSeconds > 0 ? Math.Round(reading.Questions / (double)reading.UptimeSeconds, 2) : 0;

            bool structuresReady = reading.RequiredStructuresAvailable == RequiredStructureCount;
            string health = !structuresReady || connectionUsage >= 90 || reading.ResponseTimeMs >= 1000
                ? "Critical"
                : connectionUsage >= 70 || reading.ResponseTimeMs >= 350 || slowQueryPercent >= 2
                    ? "Attention"
                    : "Healthy";

            string summary = health switch
            {
                "Critical" => "A database health check needs attention.",
                "Attention" => "The database is responding, with one metric worth watching.",
                _ => "The database is responsive and operating within normal limits."
            };

            return new DatabaseMonitorSnapshot
            {
                IsAvailable = true,
                Health = health,
                Summary = summary,
                CapturedUtc = DateTime.UtcNow,
                ResponseTimeMs = reading.ResponseTimeMs,
                ConnectionUsagePercent = connectionUsage,
                RunningOperations = reading.ThreadsRunning,
                QueriesPerSecond = queriesPerSecond,
                SlowQueryPercent = slowQueryPercent,
                ConnectionErrorPercent = connectionErrorPercent,
                UptimeSeconds = reading.UptimeSeconds,
                RequiredStructuresAvailable = reading.RequiredStructuresAvailable,
                RequiredStructuresTotal = RequiredStructureCount
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Database monitoring snapshot could not be collected.");
            return Unavailable();
        }
    }

    private static DatabaseMonitorSnapshot Unavailable() => new() { CapturedUtc = DateTime.UtcNow };
    private static double Percent(long value, long total) => total <= 0 ? 0 : Math.Round(Math.Clamp(value * 100d / total, 0, 100), 2);
}
