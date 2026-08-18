using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Data.Monitoring;
using PersonalTools.Entities.Monitoring;

namespace PersonalTools.Classes.Monitoring;

public interface IDatabaseMonitorFuncs
{
    Task<DatabaseMonitorSnapshot> GetSnapshot(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

public sealed class DatabaseMonitorFuncs : IDatabaseMonitorFuncs
{
    private const string SnapshotCacheKey = "monitoring-database-snapshot";
    private const string CounterCacheKey = "monitoring-database-counter-sample";

    private readonly IDatabaseMonitorData _data;
    private readonly ILogger<DatabaseMonitorFuncs> _logger;
    private readonly IMemoryCache _cache;

    public DatabaseMonitorFuncs(IDatabaseMonitorData data, ILogger<DatabaseMonitorFuncs> logger, IMemoryCache cache)
    {
        _data = data;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="forceRefresh"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<DatabaseMonitorSnapshot> GetSnapshot(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (forceRefresh)
            _cache.Remove(SnapshotCacheKey);

        return _cache.GetOrCreateAsync(SnapshotCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(12);

            // A cancelled browser request should not cancel collection for another request
            // that may be waiting for the same cached monitoring snapshot.
            return CollectSnapshot(CancellationToken.None);
        })!;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<DatabaseMonitorSnapshot> CollectSnapshot(CancellationToken cancellationToken)
    {
        try
        {
            DatabaseMonitorReading? reading = await _data.GetReading(cancellationToken);

            if (reading is null)
            {
                return Unavailable();
            }

            DateTime capturedUtc = DateTime.UtcNow;

            long sampledQuestions = reading.Questions;
            long sampledSlowQueries = reading.SlowQueries;
            long sampledAbortedConnects = reading.AbortedConnects;
            long sampledConnections = reading.Connections;
            long sampledTmpTables = reading.CreatedTmpTables;
            long sampledTmpDiskTables = reading.CreatedTmpDiskTables;
            long sampledThreadsCreated = reading.ThreadsCreated;
            long sampledBufferPoolReadRequests = reading.InnodbBufferPoolReadRequests;
            long sampledBufferPoolReads = reading.InnodbBufferPoolReads;
            long sampledBytesReceived = reading.BytesReceived;
            long sampledBytesSent = reading.BytesSent;

            double sampleSeconds = reading.UptimeSeconds;
            bool hasRecentSample = false;

            if (_cache.TryGetValue(CounterCacheKey, out DatabaseMonitorCounterSample? previous) && previous is not null)
            {
                double elapsedSeconds = (capturedUtc - previous.CapturedUtc).TotalSeconds;

                // Counters can move backwards if MariaDB restarts or status counters are reset.
                // In that case this reading becomes the new baseline rather than producing
                // negative or misleading rates.
                bool sameCounterWindow = reading.UptimeSeconds >= previous.UptimeSeconds && reading.Questions >= previous.Questions && reading.Connections >= previous.Connections;

                if (sameCounterWindow && elapsedSeconds >= 2 && elapsedSeconds <= 120)
                {
                    hasRecentSample = true;
                    sampleSeconds = elapsedSeconds;

                    sampledQuestions = Difference(reading.Questions, previous.Questions);
                    sampledSlowQueries = Difference(reading.SlowQueries, previous.SlowQueries);
                    sampledAbortedConnects = Difference(reading.AbortedConnects, previous.AbortedConnects);
                    sampledConnections = Difference(reading.Connections, previous.Connections);

                    sampledTmpTables = Difference(reading.CreatedTmpTables, previous.CreatedTmpTables);
                    sampledTmpDiskTables = Difference(reading.CreatedTmpDiskTables, previous.CreatedTmpDiskTables);

                    sampledThreadsCreated = Difference(reading.ThreadsCreated, previous.ThreadsCreated);

                    sampledBufferPoolReadRequests = Difference(reading.InnodbBufferPoolReadRequests, previous.InnodbBufferPoolReadRequests);
                    sampledBufferPoolReads = Difference(reading.InnodbBufferPoolReads, previous.InnodbBufferPoolReads);

                    sampledBytesReceived = Difference(reading.BytesReceived, previous.BytesReceived);
                    sampledBytesSent = Difference(reading.BytesSent, previous.BytesSent);
                }
            }

            _cache.Set(
                CounterCacheKey,
                new DatabaseMonitorCounterSample(reading.UptimeSeconds, reading.Questions, reading.SlowQueries, reading.AbortedConnects, reading.Connections, reading.CreatedTmpTables, reading.CreatedTmpDiskTables, reading.ThreadsCreated, reading.InnodbBufferPoolReadRequests, reading.InnodbBufferPoolReads, reading.BytesReceived, reading.BytesSent, capturedUtc),
                TimeSpan.FromMinutes(2));

            double connectionUsage = Percent(reading.ThreadsConnected, reading.MaxConnections);
            double queriesPerSecond = Rate(sampledQuestions, sampleSeconds);

            double slowQueryPercent = Percent(sampledSlowQueries, sampledQuestions);
            double connectionErrorPercent = Percent(sampledAbortedConnects, sampledConnections);

            double bufferPoolHitPercent = BufferPoolHitPercent(sampledBufferPoolReadRequests, sampledBufferPoolReads);
            double diskTempTablePercent = Percent(sampledTmpDiskTables, sampledTmpTables + sampledTmpDiskTables);
            double threadCreationPercent = Percent(sampledThreadsCreated, sampledConnections);

            double networkReceiveBytesPerSecond = Rate(sampledBytesReceived, sampleSeconds);
            double networkSendBytesPerSecond = Rate(sampledBytesSent, sampleSeconds);

            string bufferPoolState = BufferPoolState(hasRecentSample, sampledBufferPoolReadRequests, bufferPoolHitPercent);
            string diskTempTableState = DiskTempTableState(hasRecentSample, sampledTmpTables + sampledTmpDiskTables, diskTempTablePercent);
            string threadCreationState = ThreadCreationState(hasRecentSample, sampledConnections, threadCreationPercent);
            string rowLockState = RowLockState(reading.InnodbRowLockCurrentWaits);

            List<string> criticalReasons = new();
            List<string> attentionReasons = new();

            int missingStructures = Math.Max(0, reading.RequiredStructuresTotal - reading.RequiredStructuresAvailable);

            if (missingStructures > 0)
                criticalReasons.Add($"{missingStructures} required database structure{(missingStructures == 1 ? " is" : "s are")} missing.");

            if (connectionUsage >= 90)
                criticalReasons.Add($"Connection use is at {connectionUsage:0.#}%.");
            else if (connectionUsage >= 70)
                attentionReasons.Add($"Connection use is elevated at {connectionUsage:0.#}%.");

            if (reading.ResponseTimeMs >= 1000)
                criticalReasons.Add($"The database health check is taking {reading.ResponseTimeMs:0} ms.");
            else if (reading.ResponseTimeMs >= 350)
                attentionReasons.Add($"Database response time has risen to {reading.ResponseTimeMs:0} ms.");

            if (rowLockState == "Critical")
                criticalReasons.Add($"{reading.InnodbRowLockCurrentWaits} operations are currently waiting for row locks.");
            else if (rowLockState == "Attention")
                attentionReasons.Add($"{reading.InnodbRowLockCurrentWaits} operation{(reading.InnodbRowLockCurrentWaits == 1 ? " is" : "s are")} currently waiting for a row lock.");

            // Rate-based warnings only become health signals once we have a recent
            // interval. This avoids treating historical activity as a current problem.
            if (hasRecentSample)
            {
                if (sampledQuestions >= 20 && slowQueryPercent >= 5)
                    attentionReasons.Add($"The recent slow-query rate is {slowQueryPercent:0.##}%.");

                if (sampledConnections >= 10 && connectionErrorPercent >= 5)
                    attentionReasons.Add($"The recent failed-connection rate is {connectionErrorPercent:0.##}%.");

                if (bufferPoolState == "Attention")
                    attentionReasons.Add($"Buffer-pool efficiency has fallen to {bufferPoolHitPercent:0.##}%.");

                if (diskTempTableState == "Attention")
                    attentionReasons.Add($"{diskTempTablePercent:0.##}% of recent temporary tables were created on disk.");
            }

            string health = criticalReasons.Count > 0 ? "Critical" : attentionReasons.Count > 0 ? "Attention" : "Healthy";
            string summary = BuildSummary(health, criticalReasons, attentionReasons);

            return new DatabaseMonitorSnapshot
            {
                IsAvailable = true,
                Health = health,
                Summary = summary,
                CapturedUtc = capturedUtc,

                HasRecentSample = hasRecentSample,

                ResponseTimeMs = reading.ResponseTimeMs,
                ConnectionUsagePercent = connectionUsage,
                RunningOperations = reading.ThreadsRunning,
                QueriesPerSecond = queriesPerSecond,

                SlowQueryPercent = slowQueryPercent,
                ConnectionErrorPercent = connectionErrorPercent,

                BufferPoolHitPercent = bufferPoolHitPercent,
                BufferPoolState = bufferPoolState,

                DiskTempTablePercent = diskTempTablePercent,
                DiskTempTableState = diskTempTableState,

                ThreadCreationPercent = threadCreationPercent,
                ThreadCreationState = threadCreationState,

                ActiveRowLockWaits = reading.InnodbRowLockCurrentWaits,
                RowLockState = rowLockState,

                NetworkReceiveBytesPerSecond = networkReceiveBytesPerSecond,
                NetworkSendBytesPerSecond = networkSendBytesPerSecond,

                UptimeSeconds = reading.UptimeSeconds,

                RequiredStructuresAvailable = reading.RequiredStructuresAvailable,
                RequiredStructuresTotal = reading.RequiredStructuresTotal
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Database monitoring snapshot could not be collected.");
            return Unavailable();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private static DatabaseMonitorSnapshot Unavailable()
    {
        return new DatabaseMonitorSnapshot
        {
            CapturedUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="health"></param>
    /// <param name="criticalReasons"></param>
    /// <param name="attentionReasons"></param>
    /// <returns></returns>
    private static string BuildSummary(string health, List<string> criticalReasons, List<string> attentionReasons)
    {
        if (health == "Healthy")
            return "The database is responsive, connection capacity is comfortable and recent activity looks normal.";

        IEnumerable<string> reasons = health == "Critical" ? criticalReasons.Concat(attentionReasons) : attentionReasons;

        string summary = string.Join(" ", reasons.Take(2));

        return string.IsNullOrWhiteSpace(summary) ? "One or more database health signals need attention." : summary;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="hasRecentSample"></param>
    /// <param name="readRequests"></param>
    /// <param name="hitPercent"></param>
    /// <returns></returns>
    private static string BufferPoolState(bool hasRecentSample, long readRequests, double hitPercent)
    {
        if (!hasRecentSample || readRequests < 500)
            return "Baseline";

        return hitPercent < 98 ? "Attention" : "Healthy";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="hasRecentSample"></param>
    /// <param name="temporaryTables"></param>
    /// <param name="diskPercent"></param>
    /// <returns></returns>
    private static string DiskTempTableState(bool hasRecentSample, long temporaryTables, double diskPercent)
    {
        if (!hasRecentSample || temporaryTables < 20)
            return "Baseline";

        return diskPercent >= 30 ? "Attention" : "Healthy";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="hasRecentSample"></param>
    /// <param name="connections"></param>
    /// <param name="creationPercent"></param>
    /// <returns></returns>
    private static string ThreadCreationState(bool hasRecentSample, long connections, double creationPercent)
    {
        if (!hasRecentSample || connections < 10)
            return "Baseline";

        return creationPercent >= 20 ? "Attention" : "Healthy";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="currentWaits"></param>
    /// <returns></returns>
    private static string RowLockState(long currentWaits)
    {
        if (currentWaits >= 5)
            return "Critical";

        return currentWaits > 0 ? "Attention" : "Healthy";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="readRequests"></param>
    /// <param name="physicalReads"></param>
    /// <returns></returns>
    private static double BufferPoolHitPercent(long readRequests, long physicalReads)
    {
        if (readRequests <= 0)
            return 100;

        double hitPercent = (1 - physicalReads / (double)readRequests) * 100;

        return Math.Round(Math.Clamp(hitPercent, 0, 100), 2);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="total"></param>
    /// <returns></returns>
    private static double Percent(long value, long total)
    {
        if (total <= 0)
            return 0;

        return Math.Round(Math.Clamp(value * 100d / total, 0, 100), 2);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="seconds"></param>
    /// <returns></returns>
    private static double Rate(long value, double seconds)
    {
        if (seconds <= 0)
            return 0;

        return Math.Round(value / seconds, 2);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="current"></param>
    /// <param name="previous"></param>
    /// <returns></returns>
    private static long Difference(long current, long previous)
    {
        return current >= previous ? current - previous : 0;
    }

    private sealed record DatabaseMonitorCounterSample(long UptimeSeconds, long Questions, long SlowQueries, long AbortedConnects, long Connections, long CreatedTmpTables, long CreatedTmpDiskTables, long ThreadsCreated, long InnodbBufferPoolReadRequests, long InnodbBufferPoolReads, long BytesReceived, long BytesSent, DateTime CapturedUtc);
}