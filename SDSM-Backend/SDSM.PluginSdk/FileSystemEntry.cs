namespace SDSM.PluginSdk;

public class FileSystemEntry
{
    public required string Name { get; set; }
    public required bool IsDirectory { get; set; }
    public long? SizeBytes { get; set; }
    public required string Kind { get; set; }
    public DateTime ModifiedUtc { get; set; }
}
