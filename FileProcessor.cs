using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

public class FileProcessor
{
    private readonly Settings _settings;
    private readonly ILogger<FileProcessor> _logger;

    public FileProcessor(IOptions<Settings> settings, ILogger<FileProcessor> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public void EnsureFoldersExist(IReadOnlyCollection<SourceSettings> sources)
    {
        foreach (var source in sources)
        {
            Directory.CreateDirectory(source.SourceFolder);
        }

        Directory.CreateDirectory(_settings.DestinationFolder);
        Directory.CreateDirectory(_settings.QuarantineFolder);
    }

    public (int moved, int errors) ProcessBatch(SourceSettings source)
    {
        var files = Directory.GetFiles(source.SourceFolder, "*" + _settings.FileExtension)
            .Take(source.MaxFilesPerBatch)
            .ToList();

        int destCount = Directory.GetFiles(_settings.DestinationFolder).Length;
        int available = _settings.DestinationMaxFiles - destCount;

        if (available <= 0)
        {
            _logger.LogWarning(
                "Destination is full. Source {source} will wait for capacity",
                source.DisplayName);
            return (0, 0);
        }

        int moved = 0;
        int errors = 0;

        foreach (var file in files.Take(available))
        {
            if (TryMoveWithRetry(file, source))
                moved++;
            else
            {
                errors++;
                MoveToQuarantine(file, source);
            }
        }

        return (moved, errors);
    }

    private bool TryMoveWithRetry(string file, SourceSettings source)
    {
        for (int i = 0; i < _settings.MaxRetries; i++)
        {
            try
            {
                var dest = Path.Combine(_settings.DestinationFolder, Path.GetFileName(file));
                File.Move(file, dest, true);
                return true;
            }
            catch (Exception ex)
            {
                int delay = (int)Math.Pow(2, i); // exponential backoff
                _logger.LogWarning(
                    ex,
                    "Retry {retryAttempt} failed for source {source}. Retrying in {delaySeconds}s",
                    i + 1,
                    source.DisplayName,
                    delay);

                Thread.Sleep(delay * 1000);
            }
        }

        return false;
    }

    private void MoveToQuarantine(string file, SourceSettings source)
    {
        try
        {
            var dest = Path.Combine(_settings.QuarantineFolder, Path.GetFileName(file));
            File.Move(file, dest, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move file from source {source} to quarantine", source.DisplayName);
        }
    }
}
