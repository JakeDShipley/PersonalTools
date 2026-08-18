using Microsoft.Extensions.Logging;

namespace PersonalTools.Data.Monitoring;

public interface IApplicationLogStore
{
    void Write(LogLevel level, EventId eventId, string category, string message, string? exception);
    ApplicationLogStoreSnapshot Read(long afterId, LogLevel minimumLevel, string? search, int take);
}

public sealed record ApplicationLogReading(
    long Id,
    DateTime CapturedUtc,
    LogLevel Level,
    int EventId,
    string? EventName,
    string Category,
    string Message,
    string? Exception);

public sealed record ApplicationLogStoreSnapshot(
    DateTime CaptureStartedUtc,
    long LatestId,
    int RetainedCount,
    int WarningCount,
    int ErrorCount,
    int CriticalCount,
    IReadOnlyList<ApplicationLogReading> Entries);

public sealed class ApplicationLogStore : IApplicationLogStore
{
    private const int MaxEntries = 2000;

    private readonly object _sync = new();
    private readonly Queue<ApplicationLogReading> _entries = new();
    private readonly DateTime _captureStartedUtc = DateTime.UtcNow;
    private long _nextId;

    public void Write(LogLevel level, EventId eventId, string category, string message, string? exception)
    {
        lock (_sync)
        {
            _nextId++;

            _entries.Enqueue(new ApplicationLogReading(
                _nextId,
                DateTime.UtcNow,
                level,
                eventId.Id,
                eventId.Name,
                Limit(category, 500),
                Limit(message, 10000),
                string.IsNullOrWhiteSpace(exception) ? null : Limit(exception, 30000)));

            while (_entries.Count > MaxEntries)
            {
                _entries.Dequeue();
            }
        }
    }

    public ApplicationLogStoreSnapshot Read(long afterId, LogLevel minimumLevel, string? search, int take)
    {
        ApplicationLogReading[] retained;

        lock (_sync)
        {
            retained = _entries.ToArray();
        }

        string? searchValue = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();

        IEnumerable<ApplicationLogReading> filtered = retained
            .Where(entry => entry.Id > afterId && entry.Level >= minimumLevel);

        if (searchValue is not null)
        {
            filtered = filtered.Where(entry =>
                entry.Category.Contains(searchValue, StringComparison.OrdinalIgnoreCase) ||
                entry.Message.Contains(searchValue, StringComparison.OrdinalIgnoreCase) ||
                (entry.Exception?.Contains(searchValue, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        List<ApplicationLogReading> entries = filtered
            .OrderByDescending(entry => entry.Id)
            .Take(take)
            .ToList();

        return new ApplicationLogStoreSnapshot(
            _captureStartedUtc,
            retained.Length > 0 ? retained[^1].Id : 0,
            retained.Length,
            retained.Count(entry => entry.Level == LogLevel.Warning),
            retained.Count(entry => entry.Level == LogLevel.Error),
            retained.Count(entry => entry.Level == LogLevel.Critical),
            entries);
    }

    private static string Limit(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength), "…");
    }
}