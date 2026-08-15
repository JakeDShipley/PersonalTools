using PersonalTools.Data.Monitoring;
using PersonalTools.Entities.Monitoring;
using Microsoft.Extensions.Caching.Memory;

namespace PersonalTools.Classes.Monitoring;

public interface IServerMonitorFuncs
{
    Task<ServerMonitorSnapshot> GetSnapshot();
}

public sealed class ServerMonitorFuncs : IServerMonitorFuncs
{
    private readonly IServerMonitorData _data;
    private readonly ILogger<ServerMonitorFuncs> _logger;
    private readonly IMemoryCache _cache;

    public ServerMonitorFuncs(IServerMonitorData data, ILogger<ServerMonitorFuncs> logger, IMemoryCache cache)
    {
        _data = data;
        _logger = logger;
        _cache = cache;
    }

    public Task<ServerMonitorSnapshot> GetSnapshot()
    {
        return _cache.GetOrCreateAsync("monitoring-server-snapshot", entry =>
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
            double highest = new[] { reading.CpuUsagePercent, reading.MemoryUsagePercent, reading.StorageUsagePercent }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .DefaultIfEmpty(0)
                .Max();

            string health = highest >= 90 ? "Critical" : highest >= 75 ? "Attention" : "Healthy";
            string summary = health switch
            {
                "Critical" => "One or more resources are close to capacity.",
                "Attention" => "The server is healthy, but one resource deserves attention.",
                _ => "Core application resources are operating normally."
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
                ApplicationMemoryBytes = reading.ApplicationMemoryBytes,
                ManagedMemoryBytes = reading.ManagedMemoryBytes,
                ApplicationUptimeSeconds = reading.ApplicationUptimeSeconds
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Server monitoring snapshot could not be collected.");
            return Task.FromResult(new ServerMonitorSnapshot { CapturedUtc = DateTime.UtcNow });
        }
    }
}
