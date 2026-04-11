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
        logger.LogInformation(
            "File transporter is running. Source: {source}; Destination: {destination}; Interval: {interval}s",
            _settings.SourceFolder,
            _settings.DestinationFolder,
            _settings.ScanIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                fileProcessor.EnsureFoldersExist();

                var (moved, errors) = fileProcessor.ProcessBatch();
                logger.LogInformation("Batch finished. Moved: {moved}; Errors: {errors}", moved, errors);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while processing files");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.ScanIntervalSeconds), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping File Transporter Service at {time}", DateTimeOffset.Now);
        await base.StopAsync(cancellationToken);
    }
}
