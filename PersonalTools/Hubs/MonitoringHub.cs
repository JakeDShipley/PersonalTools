using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PersonalTools.Hubs;

[Authorize]
public sealed class MonitoringHub : Hub
{
}

public sealed class MonitoringPulseService : BackgroundService
{
    private readonly IHubContext<MonitoringHub> _hub;

    public MonitoringPulseService(IHubContext<MonitoringHub> hub) => _hub = hub;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
        int pulse = 0;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            pulse++;
            await _hub.Clients.All.SendAsync("monitoringPulse", "server", stoppingToken);
            if (pulse % 3 == 0)
                await _hub.Clients.All.SendAsync("monitoringPulse", "database", stoppingToken);
        }
    }
}
