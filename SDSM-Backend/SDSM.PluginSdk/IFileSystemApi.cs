namespace SDSM.PluginSdk;

/// Scoped filesystem access for plugins. The filesystem is a set of
/// named volumes; paths are "VolumeName/rel/path" ("" lists the
/// volumes). Paths resolving outside their volume are rejected with an
/// ArgumentException, unknown volumes with a DirectoryNotFoundException.
public interface IFileSystemApi
{
    IReadOnlyList<VolumeInfo> GetVolumes();
    IReadOnlyList<FileSystemEntry> List(string relPath);
    FileSystemEntry Stat(string relPath);
    Stream OpenRead(string relPath);
}
