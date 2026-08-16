namespace PersonalTools.Classes.Tracker
{
    // Periodically closes Resolved tracker items once they've sat resolved longer than the
    // configured threshold (Settings page, default 5 days). ITrackerFuncs is scoped, so this
    // (a singleton BackgroundService) resolves it through a fresh DI scope on every tick rather
    // than injecting it directly.
    public class TrackerAutoCloseService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TrackerAutoCloseService> _logger;

        public TrackerAutoCloseService(IServiceScopeFactory scopeFactory, ILogger<TrackerAutoCloseService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new(Interval);

            do
            {
                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    ITrackerFuncs tracker = scope.ServiceProvider.GetRequiredService<ITrackerFuncs>();
                    await tracker.AutoCloseResolvedItems();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tracker auto-close pass failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
