using Microsoft.Extensions.Logging;
using PersonalTools.Data.Monitoring;
using PersonalTools.Entities.Monitoring;

namespace PersonalTools.Classes.Monitoring;

public interface ILogsViewerFuncs
{
    ApplicationLogResult GetLogs(long afterId = 0, string minimumLevel = "Information", string? search = null, int take = 200);
}

public sealed class LogsViewerFuncs : ILogsViewerFuncs
{
    private readonly IApplicationLogStore _store;

    public LogsViewerFuncs(IApplicationLogStore store)
    {
        _store = store;
    }

    public ApplicationLogResult GetLogs(long afterId = 0, string minimumLevel = "Information", string? search = null, int take = 200)
    {
        LogLevel level = ParseMinimumLevel(minimumLevel);
        int resultLimit = Math.Clamp(take, 1, 250);

        ApplicationLogStoreSnapshot snapshot = _store.Read(
            Math.Max(0, afterId),
            level,
            search,
            resultLimit);

        return new ApplicationLogResult
        {
            CapturedUtc = DateTime.UtcNow,
            CaptureStartedUtc = snapshot.CaptureStartedUtc,
            LatestId = snapshot.LatestId,
            RetainedCount = snapshot.RetainedCount,
            WarningCount = snapshot.WarningCount,
            ErrorCount = snapshot.ErrorCount,
            CriticalCount = snapshot.CriticalCount,
            Entries = snapshot.Entries
                .Select(entry => new ApplicationLogEntry
                {
                    Id = entry.Id,
                    CapturedUtc = entry.CapturedUtc,
                    Level = entry.Level.ToString(),
                    EventId = entry.EventId,
                    EventName = entry.EventName,
                    Category = entry.Category,
                    Message = entry.Message,
                    Exception = entry.Exception
                })
                .ToList()
        };
    }

    private static LogLevel ParseMinimumLevel(string minimumLevel)
    {
        if (Enum.TryParse(minimumLevel, true, out LogLevel level) && level != LogLevel.None)
        {
            return level;
        }

        return LogLevel.Information;
    }
}