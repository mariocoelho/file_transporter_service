using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading;

public class FileProcessor
{
    private readonly Settings _settings;
    private readonly ILogger<FileProcessor> _logger;

    public FileProcessor(IOptions<Settings> settings, ILogger<FileProcessor> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public void EnsureFoldersExist()
    {
        Directory.CreateDirectory(_settings.SourceFolder);
        Directory.CreateDirectory(_settings.DestinationFolder);
        Directory.CreateDirectory(_settings.QuarantineFolder);
    }

    public (int moved, int errors) ProcessBatch()
    {
        var files = Directory.GetFiles(_settings.SourceFolder, "*" + _settings.FileExtension)
            .Take(_settings.MaxFilesPerBatch)
            .ToList();

        int destCount = Directory.GetFiles(_settings.DestinationFolder).Length;
        int available = _settings.DestinationMaxFiles - destCount;

        if (available <= 0)
        {
            _logger.LogWarning("Destino cheio");
            return (0, 0);
        }

        int moved = 0;
        int errors = 0;

        foreach (var file in files.Take(available))
        {
            if (TryMoveWithRetry(file))
                moved++;
            else
            {
                errors++;
                MoveToQuarantine(file);
            }
        }

        return (moved, errors);
    }

    private bool TryMoveWithRetry(string file)
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
                _logger.LogWarning(ex, "Retry {retryAttempt} falhou. Nova tentativa em {delaySeconds}s", i + 1, delay);

                Thread.Sleep(delay * 1000);
            }
        }

        return false;
    }

    private void MoveToQuarantine(string file)
    {
        try
        {
            var dest = Path.Combine(_settings.QuarantineFolder, Path.GetFileName(file));
            File.Move(file, dest, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao mover para quarentena");
        }
    }
}
