namespace SDSM.PluginSdk;

public class VolumeInfo
{
    public required string Name { get; set; }
    public long TotalBytes { get; set; }
    public long AvailableBytes { get; set; }
}
