using Mapster;
using Microsoft.Extensions.Logging;
using PersonalTools.Data.Monitoring;
using PersonalTools.Entities.Monitoring;

namespace PersonalTools.Classes.Monitoring;

public interface ILogsViewerFuncs
{
    Task<ApplicationLogResult> GetLogs(
        int page = 1,
        int pageSize = 25,
        string minimumLevel = "Information",
        string? search = null,
        CancellationToken cancellationToken = default);
}

public sealed class LogsViewerFuncs : ILogsViewerFuncs
{
    private static readonly int[] AllowedPageSizes = [10, 25, 50, 100];
    private readonly IApplicationLogsData _data;

    public LogsViewerFuncs(IApplicationLogsData data)
    {
        _data = data;
    }

    /// <summary>
    /// Keeps filtering and paging in MariaDB so the browser receives only the rows it can show.
    /// The entries and summary are independent queries and can run together on separate connections.
    /// </summary>
    public async Task<ApplicationLogResult> GetLogs(
        int page = 1,
        int pageSize = 25,
        string minimumLevel = "Information",
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        int safePage = Math.Max(1, page);
        int safePageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : 25;
        string safeSearch = (search ?? string.Empty).Trim();
        LogLevel level = ParseMinimumLevel(minimumLevel);
        ApplicationLogQuery query = new(level, safeSearch, safePage, safePageSize);

        Task<List<ApplicationLogReading>> entriesTask = _data.GetLogs(query, cancellationToken);
        Task<ApplicationLogSummaryModel> summaryTask = _data.GetSummary(query, cancellationToken);
        await Task.WhenAll(entriesTask, summaryTask);

        ApplicationLogSummaryModel summary = await summaryTask;
        int pageCount = Math.Max(1, (int)Math.Ceiling(summary.FilteredCount / (double)safePageSize));

        return new ApplicationLogResult
        {
            CapturedUtc = DateTime.UtcNow,
            CaptureStartedUtc = summary.CaptureStartedUtc,
            Page = safePage,
            PageSize = safePageSize,
            PageCount = pageCount,
            FilteredCount = summary.FilteredCount,
            RetainedCount = summary.RetainedCount,
            WarningCount = summary.WarningCount,
            ErrorCount = summary.ErrorCount,
            CriticalCount = summary.CriticalCount,
            Entries = (await entriesTask).Adapt<List<ApplicationLogEntry>>()
        };
    }

    private static LogLevel ParseMinimumLevel(string minimumLevel)
    {
        return Enum.TryParse(minimumLevel, true, out LogLevel level) && level != LogLevel.None
            ? level
            : LogLevel.Information;
    }
}
