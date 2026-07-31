namespace SDSM.PluginSdk;

/// Scoped filesystem access for plugins. All paths are relative to the
/// configured root directory; paths resolving outside it are rejected
/// with an ArgumentException.
public interface IFileSystemApi
{
    IReadOnlyList<FileSystemEntry> List(string relPath);
    FileSystemEntry Stat(string relPath);
    Stream OpenRead(string relPath);
}
