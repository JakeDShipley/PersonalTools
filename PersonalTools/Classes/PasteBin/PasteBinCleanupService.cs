using PersonalTools.Data.PasteBin;

namespace PersonalTools.Classes.PasteBin;

public sealed class PasteBinCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PasteBinCleanupService> _logger;

    public PasteBinCleanupService(IServiceScopeFactory scopeFactory, ILogger<PasteBinCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanup(stoppingToken);
        using PeriodicTimer timer = new(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunCleanup(stoppingToken);
    }

    private async Task RunCleanup(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IPasteBinData data = scope.ServiceProvider.GetRequiredService<IPasteBinData>();
            IPasteBinFileStorage storage = scope.ServiceProvider.GetRequiredService<IPasteBinFileStorage>();
            List<Entities.PasteBin.PasteBinExpiredFile> expired = await data.GetExpiredPastes(cancellationToken);

            // Delete metadata first. A failed physical delete then becomes an orphan which the
            // second pass can safely retry without exposing an expired paste.
            await data.DeleteExpiredPastes(cancellationToken);
            foreach (var item in expired.Where(item => !string.IsNullOrWhiteSpace(item.StoredFileName)))
            {
                try { await storage.DeleteFile(item.StoredFileName); }
                catch (Exception exception) { _logger.LogWarning(exception, "Expired paste file cleanup failed for paste {PasteId}.", item.PasteId); }
            }

            HashSet<string> activeNames = await data.GetStoredFileNames(cancellationToken);
            DateTime orphanCutoffUtc = DateTime.UtcNow.AddHours(-2);
            foreach (FileInfo file in storage.GetStoredFiles().Where(file => file.LastWriteTimeUtc < orphanCutoffUtc && !activeNames.Contains(file.Name)))
            {
                try { await storage.DeleteFile(file.Name); }
                catch (Exception exception) { _logger.LogWarning(exception, "Orphan Paste Bin file cleanup failed for {StoredFileName}.", file.Name); }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste Bin expiry cleanup failed and will be retried on the next cycle.");
        }
    }
}
