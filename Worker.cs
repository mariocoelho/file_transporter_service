using Microsoft.Extensions.Options;

namespace file_transporter_service;

public class Worker(
    FileProcessor fileProcessor,
    IOptions<Settings> settings,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly Settings _settings = settings.Value;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting File Transporter Service at {time}", DateTimeOffset.Now);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var sources = _settings.GetConfiguredSources();

            if (sources.Count == 0)
            {
                logger.LogWarning("No source folders are configured. Service will remain idle.");
                return;
            }

            fileProcessor.EnsureFoldersExist(sources);

            foreach (var source in sources)
            {
                logger.LogInformation(
                    "Source configured. Name: {name}; Source: {source}; Destination: {destination}; Interval: {interval}s; Batch size: {batchSize}",
                    source.DisplayName,
                    source.SourceFolder,
                    _settings.DestinationFolder,
                    source.ScanIntervalSeconds,
                    source.MaxFilesPerBatch);
            }

            var nextRunBySource = sources.ToDictionary(
                source => source,
                _ => DateTimeOffset.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                var dueSources = nextRunBySource
                    .Where(entry => entry.Value <= now)
                    .Select(entry => entry.Key)
                    .ToList();

                foreach (var source in dueSources)
                {
                    try
                    {
                        logger.LogDebug("Starting batch for source {source}", source.DisplayName);

                        var (moved, errors) = await fileProcessor.ProcessBatchAsync(source, stoppingToken);
                        logger.LogInformation(
                            "Batch finished for source {source}. Moved: {moved}; Errors: {errors}",
                            source.DisplayName,
                            moved,
                            errors);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unhandled error while processing source {source}", source.DisplayName);
                    }

                    var interval = Math.Max(1, source.ScanIntervalSeconds);
                    nextRunBySource[source] = DateTimeOffset.UtcNow.AddSeconds(interval);
                }

                var delay = nextRunBySource.Count == 0
                    ? TimeSpan.FromSeconds(1)
                    : nextRunBySource.Values.Min() - DateTimeOffset.UtcNow;

                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("File Transporter worker cancellation requested.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Worker terminated unexpectedly");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping File Transporter Service at {time}", DateTimeOffset.Now);
        await base.StopAsync(cancellationToken);
    }
}
