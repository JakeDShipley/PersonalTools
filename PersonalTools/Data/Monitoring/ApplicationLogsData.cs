using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace PersonalTools.Data.Monitoring;

public interface IApplicationLogsData
{
    Task SaveLogs(IReadOnlyList<ApplicationLogReading> entries, CancellationToken cancellationToken = default);
    Task<List<ApplicationLogReading>> GetLogs(ApplicationLogQuery query, CancellationToken cancellationToken = default);
    Task<ApplicationLogSummaryModel> GetSummary(ApplicationLogQuery query, CancellationToken cancellationToken = default);
}

public sealed record ApplicationLogQuery(LogLevel MinimumLevel, string Search, int Page, int PageSize);

public sealed record ApplicationLogSummaryModel(
    DateTime? CaptureStartedUtc,
    int RetainedCount,
    int FilteredCount,
    int WarningCount,
    int ErrorCount,
    int CriticalCount);

public sealed class ApplicationLogsData : IApplicationLogsData
{
    private readonly IMariaDbDataAccess _database;

    public ApplicationLogsData(IMariaDbDataAccess database)
    {
        _database = database;
    }

    /// <summary>
    /// Sends one JSON batch to MariaDB. A busy request must never create one database round trip
    /// per log line, particularly when one exception produces several framework events.
    /// </summary>
    public Task SaveLogs(IReadOnlyList<ApplicationLogReading> entries, CancellationToken cancellationToken = default)
    {
        string payload = JsonSerializer.Serialize(entries.Select(entry => new
        {
            LogId = entry.LogId,
            CapturedUtc = entry.CapturedUtc.ToString("yyyy-MM-dd HH:mm:ss.ffffff"),
            Level = Convert.ToInt32(entry.Level),
            entry.EventId,
            entry.EventName,
            entry.Category,
            entry.Message,
            entry.Exception
        }));

        return _database.ExecuteSP(
            "sp_application_logs_write_bulk",
            [new MySqlParameter("p_logs", payload)],
            cancellationToken);
    }

    public Task<List<ApplicationLogReading>> GetLogs(ApplicationLogQuery query, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP(
            "sp_application_logs_get_page",
            ReadLog,
            QueryParameters(query),
            cancellationToken);
    }

    public async Task<ApplicationLogSummaryModel> GetSummary(ApplicationLogQuery query, CancellationToken cancellationToken = default)
    {
        return await _database.GetDataSP(
            "sp_application_logs_get_summary",
            reader => new ApplicationLogSummaryModel(
                reader.IsDBNull(reader.GetOrdinal("CaptureStartedUtc"))
                    ? null
                    : DateTime.SpecifyKind(reader.GetDateTime("CaptureStartedUtc"), DateTimeKind.Utc),
                reader.GetInt32("RetainedCount"),
                reader.GetInt32("FilteredCount"),
                reader.GetInt32("WarningCount"),
                reader.GetInt32("ErrorCount"),
                reader.GetInt32("CriticalCount")),
            FilterParameters(query),
            cancellationToken) ?? new ApplicationLogSummaryModel(null, 0, 0, 0, 0, 0);
    }

    private static MySqlParameter[] QueryParameters(ApplicationLogQuery query)
    {
        return
        [
            new MySqlParameter("p_minimum_level", Convert.ToInt32(query.MinimumLevel)),
            new MySqlParameter("p_search", query.Search),
            new MySqlParameter("p_offset", (query.Page - 1) * query.PageSize),
            new MySqlParameter("p_page_size", query.PageSize)
        ];
    }

    private static MySqlParameter[] FilterParameters(ApplicationLogQuery query)
    {
        return
        [
            new MySqlParameter("p_minimum_level", Convert.ToInt32(query.MinimumLevel)),
            new MySqlParameter("p_search", query.Search)
        ];
    }

    private static ApplicationLogReading ReadLog(MySqlDataReader reader)
    {
        return new ApplicationLogReading(
            reader.GetGuid("LogId"),
            DateTime.SpecifyKind(reader.GetDateTime("CapturedUtc"), DateTimeKind.Utc),
            (LogLevel)reader.GetInt32("Level"),
            reader.GetInt32("EventId"),
            reader.IsDBNull(reader.GetOrdinal("EventName")) ? null : reader.GetString("EventName"),
            reader.GetString("Category"),
            reader.GetString("Message"),
            reader.IsDBNull(reader.GetOrdinal("Exception")) ? null : reader.GetString("Exception"));
    }
}
