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

        private readonly string _root;

        public FileSystemApi(string rootDir)
        {
            _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDir));
        }

        public IReadOnlyList<FileSystemEntry> List(string relPath)
        {
            var dir = new DirectoryInfo(Resolve(relPath));
            return dir.EnumerateFileSystemInfos()
                .OrderByDescending(info => info is DirectoryInfo)
                .ThenBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToEntry)
                .ToList();
        }

        public FileSystemEntry Stat(string relPath)
        {
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

        // Canonicalizes relPath against the root and rejects anything
        // escaping it — the central path-traversal guard for all plugins.
        private string Resolve(string relPath)
        {
            string fullPath = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(Path.Combine(_root, relPath)));
            if (fullPath != _root
                && !fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Path escapes the root directory: {relPath}");
            }
            return fullPath;
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
