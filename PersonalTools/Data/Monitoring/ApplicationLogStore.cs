using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PersonalTools.Data.Monitoring;

public interface IApplicationLogStore
{
    void Write(LogLevel level, EventId eventId, string category, string message, string? exception);
    ValueTask<ApplicationLogReading> ReadAsync(CancellationToken cancellationToken);
    bool TryRead(out ApplicationLogReading? entry);
    void Requeue(IEnumerable<ApplicationLogReading> entries);
}

public sealed record ApplicationLogReading(
    Guid LogId,
    DateTime CapturedUtc,
    LogLevel Level,
    int EventId,
    string? EventName,
    string Category,
    string Message,
    string? Exception);

/// <summary>
/// Accepts log events without making the request that produced them wait for MariaDB. The
/// bounded channel protects the application if the database is unavailable for a while.
/// </summary>
public sealed class ApplicationLogStore : IApplicationLogStore
{
    private readonly Channel<ApplicationLogReading> _entries = Channel.CreateBounded<ApplicationLogReading>(
        new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public void Write(LogLevel level, EventId eventId, string category, string message, string? exception)
    {
        _entries.Writer.TryWrite(new ApplicationLogReading(
            Guid.NewGuid(),
            DateTime.UtcNow,
            level,
            eventId.Id,
            eventId.Name,
            Limit(category, 500),
            Limit(message, 10000),
            string.IsNullOrWhiteSpace(exception) ? null : Limit(exception, 30000)));
    }

    public ValueTask<ApplicationLogReading> ReadAsync(CancellationToken cancellationToken)
    {
        return _entries.Reader.ReadAsync(cancellationToken);
    }

    public bool TryRead(out ApplicationLogReading? entry)
    {
        return _entries.Reader.TryRead(out entry);
    }

    public void Requeue(IEnumerable<ApplicationLogReading> entries)
    {
        foreach (ApplicationLogReading entry in entries)
        {
            _entries.Writer.TryWrite(entry);
        }
    }

    private static string Limit(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength), "…");
    }
}
