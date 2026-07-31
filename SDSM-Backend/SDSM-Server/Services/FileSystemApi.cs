using SDSM.PluginSdk;

namespace Services
{
    public class FileSystemApi : IFileSystemApi
    {
        private static readonly Dictionary<string, string> KindByExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            [".mkv"] = "Matroska Video",
            [".mp4"] = "MPEG-4 Video",
            [".avi"] = "AVI Video",
            [".mp3"] = "MP3 Audio",
            [".flac"] = "FLAC Audio",
            [".jpg"] = "JPEG-Bild",
            [".jpeg"] = "JPEG-Bild",
            [".png"] = "PNG-Bild",
            [".zip"] = "ZIP-Archiv",
            [".tar"] = "TAR-Archiv",
            [".gz"] = "GZip-Archiv",
            [".pdf"] = "PDF-Dokument",
            [".txt"] = "Textdatei",
            [".md"] = "Markdown",
            [".nfo"] = "Info-Datei",
        };

        private readonly List<(string Name, string Root)> _volumes;

        public FileSystemApi(IReadOnlyDictionary<string, string> volumes)
        {
            _volumes = volumes
                .Select(v => (v.Key, Path.TrimEndingDirectorySeparator(Path.GetFullPath(v.Value))))
                .OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<VolumeInfo> GetVolumes()
        {
            var drives = new List<DriveInfo>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                    {
                        drives.Add(drive);
                    }
                }
                catch (IOException)
                {
                    // Pseudo mounts can refuse queries; skip them
                }
            }

            return _volumes.Select(volume =>
            {
                DriveInfo? mount = drives
                    .Where(d => IsUnderMount(volume.Root, d.RootDirectory.FullName))
                    .OrderByDescending(d => d.RootDirectory.FullName.Length)
                    .FirstOrDefault();
                return new VolumeInfo
                {
                    Name = volume.Name,
                    TotalBytes = mount?.TotalSize ?? 0,
                    AvailableBytes = mount?.AvailableFreeSpace ?? 0,
                };
            }).ToList();
        }

        public IReadOnlyList<FileSystemEntry> List(string relPath)
        {
            relPath = Normalize(relPath);
            if (relPath == "")
            {
                return _volumes.Select(VolumeEntry).ToList();
            }
            var dir = new DirectoryInfo(Resolve(relPath));
            return dir.EnumerateFileSystemInfos()
                .OrderByDescending(info => info is DirectoryInfo)
                .ThenBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToEntry)
                .ToList();
        }

        public FileSystemEntry Stat(string relPath)
        {
            (string volumeName, string rest) = SplitVolume(Normalize(relPath));
            if (rest == "")
            {
                return VolumeEntry(Volume(volumeName));
            }
            string fullPath = Resolve(relPath);
            FileSystemInfo info = Directory.Exists(fullPath)
                ? new DirectoryInfo(fullPath)
                : new FileInfo(fullPath);
            if (!info.Exists)
            {
                throw new FileNotFoundException(null, relPath);
            }
            return ToEntry(info);
        }

        public Stream OpenRead(string relPath)
        {
            return new FileStream(Resolve(relPath), FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private static string Normalize(string relPath)
        {
            return relPath.Trim('/');
        }

        private static (string VolumeName, string Remainder) SplitVolume(string relPath)
        {
            if (relPath == "")
            {
                throw new ArgumentException("Path must start with a volume name");
            }
            int slash = relPath.IndexOf('/');
            return slash < 0 ? (relPath, "") : (relPath[..slash], relPath[(slash + 1)..]);
        }

        private (string Name, string Root) Volume(string name)
        {
            foreach (var volume in _volumes)
            {
                if (volume.Name == name)
                {
                    return volume;
                }
            }
            throw new DirectoryNotFoundException($"Unknown volume: {name}");
        }

        // Canonicalizes relPath against its volume root and rejects
        // anything escaping it — the central path-traversal guard for
        // all plugins.
        private string Resolve(string relPath)
        {
            (string volumeName, string rest) = SplitVolume(Normalize(relPath));
            string root = Volume(volumeName).Root;
            if (rest == "")
            {
                return root;
            }
            string fullPath = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(Path.Combine(root, rest)));
            if (fullPath != root
                && !fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Path escapes its volume: {relPath}");
            }
            return fullPath;
        }

        private static bool IsUnderMount(string path, string mountRoot)
        {
            mountRoot = Path.TrimEndingDirectorySeparator(mountRoot);
            if (mountRoot == "")
            {
                return true; // filesystem root
            }
            return path == mountRoot
                || mountRoot == Path.DirectorySeparatorChar.ToString()
                || path.StartsWith(mountRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static FileSystemEntry VolumeEntry((string Name, string Root) volume)
        {
            var dir = new DirectoryInfo(volume.Root);
            return new FileSystemEntry
            {
                Name = volume.Name,
                IsDirectory = true,
                SizeBytes = null,
                Kind = "Volume",
                ModifiedUtc = dir.Exists ? dir.LastWriteTimeUtc : default,
            };
        }

        private static FileSystemEntry ToEntry(FileSystemInfo info)
        {
            bool isDirectory = info is DirectoryInfo;
            return new FileSystemEntry
            {
                Name = info.Name,
                IsDirectory = isDirectory,
                SizeBytes = info is FileInfo file ? file.Length : null,
                Kind = isDirectory ? "Ordner" : KindForFile(info.Name),
                ModifiedUtc = info.LastWriteTimeUtc,
            };
        }

        private static string KindForFile(string name)
        {
            string ext = Path.GetExtension(name);
            if (KindByExtension.TryGetValue(ext, out string? kind))
            {
                return kind;
            }
            return ext.Length > 1 ? $"{ext[1..].ToUpperInvariant()}-Datei" : "Datei";
        }
    }
}
