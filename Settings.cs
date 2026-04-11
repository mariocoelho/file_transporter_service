public class Settings
{
    public string SourceFolder { get; set; } = string.Empty;
    public string DestinationFolder { get; set; } = string.Empty;
    public string QuarantineFolder { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public int MaxFilesPerBatch { get; set; }
    public int DestinationMaxFiles { get; set; }
    public int ScanIntervalSeconds { get; set; }
    public int MaxRetries { get; set; }
}
