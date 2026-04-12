using System.Collections.Generic;
using System.Linq;

public class Settings
{
    public string DestinationFolder { get; set; } = string.Empty;
    public string QuarantineFolder { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public int DestinationMaxFiles { get; set; }
    public int MaxRetries { get; set; }
    public List<SourceSettings> Sources { get; set; } = [];

    // Legacy single-source settings kept for backward compatibility.
    public string SourceFolder { get; set; } = string.Empty;
    public int MaxFilesPerBatch { get; set; }
    public int ScanIntervalSeconds { get; set; }

    public IReadOnlyList<SourceSettings> GetConfiguredSources()
    {
        if (Sources.Count > 0)
        {
            return Sources
                .Where(source => !string.IsNullOrWhiteSpace(source.SourceFolder))
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(SourceFolder))
        {
            return [];
        }

        return
        [
            new SourceSettings
            {
                Name = "default",
                SourceFolder = SourceFolder,
                MaxFilesPerBatch = MaxFilesPerBatch,
                ScanIntervalSeconds = ScanIntervalSeconds
            }
        ];
    }
}

public class SourceSettings
{
    public string Name { get; set; } = string.Empty;
    public string SourceFolder { get; set; } = string.Empty;
    public int MaxFilesPerBatch { get; set; }
    public int ScanIntervalSeconds { get; set; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? SourceFolder : Name;
}
