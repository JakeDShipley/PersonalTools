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
    long Connections,
    long CreatedTmpTables,
    long CreatedTmpDiskTables,
    long ThreadsCreated,
    long InnodbBufferPoolReadRequests,
    long InnodbBufferPoolReads,
    long InnodbRowLockCurrentWaits,
    long BytesReceived,
    long BytesSent,
    int RequiredStructuresAvailable,
    int RequiredStructuresTotal,
    double ResponseTimeMs);

public sealed class DatabaseMonitorData : IDatabaseMonitorData
{
    private readonly IMariaDbDataAccess _database;

    public DatabaseMonitorData(IMariaDbDataAccess database)
    {
        _database = database;
    }

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
                ReadInt64(reader, "Connections"),
                ReadInt64(reader, "CreatedTmpTables"),
                ReadInt64(reader, "CreatedTmpDiskTables"),
                ReadInt64(reader, "ThreadsCreated"),
                ReadInt64(reader, "InnodbBufferPoolReadRequests"),
                ReadInt64(reader, "InnodbBufferPoolReads"),
                ReadInt64(reader, "InnodbRowLockCurrentWaits"),
                ReadInt64(reader, "BytesReceived"),
                ReadInt64(reader, "BytesSent"),
                Convert.ToInt32(reader["RequiredStructuresAvailable"]),
                Convert.ToInt32(reader["RequiredStructuresTotal"]),
                0),
            cancellationToken: cancellationToken);

        stopwatch.Stop();

        return reading is null
            ? null
            : reading with { ResponseTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1) };
    }

    private static long ReadInt64(MySqlDataReader reader, string name)
    {
        return Convert.ToInt64(reader[name]);
    }
}