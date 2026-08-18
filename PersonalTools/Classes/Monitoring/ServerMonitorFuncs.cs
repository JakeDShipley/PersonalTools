using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Data.Monitoring;
using PersonalTools.Entities.Monitoring;

namespace PersonalTools.Classes.Monitoring;

public interface IServerMonitorFuncs
{
    Task<ServerMonitorSnapshot> GetSnapshot(bool forceRefresh = false);
}

public sealed class ServerMonitorFuncs : IServerMonitorFuncs
{
    private const string SnapshotCacheKey = "monitoring-server-snapshot";

    private readonly IServerMonitorData _data;
    private readonly ILogger<ServerMonitorFuncs> _logger;
    private readonly IMemoryCache _cache;

    public ServerMonitorFuncs(IServerMonitorData data, ILogger<ServerMonitorFuncs> logger, IMemoryCache cache)
    {
        _data = data;
        _logger = logger;
        _cache = cache;
    }

    public Task<ServerMonitorSnapshot> GetSnapshot(bool forceRefresh = false)
    {
        if (forceRefresh)
            _cache.Remove(SnapshotCacheKey);

        return _cache.GetOrCreateAsync(SnapshotCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(4);
            return CollectSnapshot();
        })!;
    }

    private Task<ServerMonitorSnapshot> CollectSnapshot()
    {
        try
        {
            ServerMonitorReading reading = _data.GetReading();

            (string Name, double? Value)[] resources =
            [
                ("Processor load", reading.CpuUsagePercent),
                ("Host memory use", reading.MemoryUsagePercent),
                ("Storage use", reading.StorageUsagePercent)
            ];

            (string Name, double? Value) highest = resources.Where(resource => resource.Value.HasValue).OrderByDescending(resource => resource.Value).FirstOrDefault();

            double highestValue = highest.Value ?? 0;

            string health = highestValue >= 90 ? "Critical" : highestValue >= 75 ? "Attention" : "Healthy";

            string summary = health switch
            {
                "Critical" => $"{highest.Name} is critically high at {highestValue:0.#}%.",
                "Attention" => $"{highest.Name} is elevated at {highestValue:0.#}%.",
                _ => "Host resource pressure is low and the Personal Tools process is operating normally."
            };

            return Task.FromResult(new ServerMonitorSnapshot
            {
                IsAvailable = true,
                Health = health,
                Summary = summary,
                CapturedUtc = DateTime.UtcNow,

                CpuUsagePercent = reading.CpuUsagePercent,
                MemoryUsagePercent = reading.MemoryUsagePercent,
                StorageUsagePercent = reading.StorageUsagePercent,

                ApplicationCpuUsagePercent = reading.ApplicationCpuUsagePercent,
                ApplicationMemoryBytes = reading.ApplicationMemoryBytes,
                ManagedMemoryBytes = reading.ManagedMemoryBytes,
                ApplicationMemorySharePercent = reading.ApplicationMemorySharePercent,

                ProcessThreadCount = reading.ProcessThreadCount,
                AvailableProcessorCount = reading.AvailableProcessorCount,
                ApplicationUptimeSeconds = reading.ApplicationUptimeSeconds
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Server monitoring snapshot could not be collected.");

            return Task.FromResult(new ServerMonitorSnapshot
            {
                CapturedUtc = DateTime.UtcNow
            });
        }
    }
}