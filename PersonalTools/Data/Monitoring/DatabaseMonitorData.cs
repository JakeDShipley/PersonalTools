using System.Diagnostics;
using MySqlConnector;

namespace PersonalTools.Data.Monitoring;

public interface IDatabaseMonitorData
{
    Task<DatabaseMonitorReading?> GetReading(CancellationToken cancellationToken = default);
}

public sealed record DatabaseMonitorReading(
    long UptimeSeconds,
    long ThreadsConnected,
    long ThreadsRunning,
    long MaxConnections,
    long Questions,
    long SlowQueries,
    long AbortedConnects,
    int RequiredStructuresAvailable,
    double ResponseTimeMs);

public sealed class DatabaseMonitorData : IDatabaseMonitorData
{
    private readonly IMariaDbDataAccess _database;

    public DatabaseMonitorData(IMariaDbDataAccess database) => _database = database;

    public async Task<DatabaseMonitorReading?> GetReading(CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        DatabaseMonitorReading? reading = await _database.GetDataSP(
            "sp_monitor_database_snapshot",
            reader => new DatabaseMonitorReading(
                ReadInt64(reader, "UptimeSeconds"),
                ReadInt64(reader, "ThreadsConnected"),
                ReadInt64(reader, "ThreadsRunning"),
                ReadInt64(reader, "MaxConnections"),
                ReadInt64(reader, "Questions"),
                ReadInt64(reader, "SlowQueries"),
                ReadInt64(reader, "AbortedConnects"),
                Convert.ToInt32(reader["RequiredStructuresAvailable"]),
                0),
            cancellationToken: cancellationToken);

        stopwatch.Stop();
        return reading is null ? null : reading with { ResponseTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1) };
    }

    private static long ReadInt64(MySqlDataReader reader, string name) => Convert.ToInt64(reader[name]);
}
