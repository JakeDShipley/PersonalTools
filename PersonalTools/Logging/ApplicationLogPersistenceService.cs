using PersonalTools.Data.Monitoring;

namespace PersonalTools.Logging;

/// <summary>
/// Drains application events in small batches. Failures are deliberately not written through
/// ILogger here because logging a failure to save logs would create an endless feedback loop.
/// </summary>
public sealed class ApplicationLogPersistenceService : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan FailureReportInterval = TimeSpan.FromMinutes(1);
    private readonly IApplicationLogStore _store;
    private readonly IServiceScopeFactory _scopeFactory;
    private DateTime _lastFailureReportedUtc = DateTime.MinValue;

    public ApplicationLogPersistenceService(IApplicationLogStore store, IServiceScopeFactory scopeFactory)
    {
        _store = store;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ApplicationLogReading first = await _store.ReadAsync(stoppingToken);
            List<ApplicationLogReading> batch = [first];

            // A tiny collection window turns clusters of framework events into one database call
            // without making log persistence feel delayed in the viewer.
            await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);

            while (batch.Count < BatchSize && _store.TryRead(out ApplicationLogReading? next))
            {
                if (next is not null)
                {
                    batch.Add(next);
                }
            }

            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IApplicationLogsData data = scope.ServiceProvider.GetRequiredService<IApplicationLogsData>();
                await data.SaveLogs(batch, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // GUID keys make retrying safe even if MariaDB accepted part of the previous call.
                _store.Requeue(batch);

                // ILogger feeds this same persistence queue, so using it here would cause an
                // endless loop. A throttled stderr message remains visible through systemd and
                // gives the server administrator the real stored-procedure or connection error.
                if (DateTime.UtcNow - _lastFailureReportedUtc >= FailureReportInterval)
                {
                    _lastFailureReportedUtc = DateTime.UtcNow;
                    Console.Error.WriteLine($"[{DateTime.UtcNow:O}] Application log persistence failed: {exception}");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
