using Services;

namespace SDSM_Server.Tests;

public class FileSystemApiTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemApi _fs;

    public FileSystemApiTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sdsm-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, "vol1", "sub"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "vol2"));
        File.WriteAllText(Path.Combine(_tempDir, "vol1", "movie.mkv"), "123456");
        File.WriteAllText(Path.Combine(_tempDir, "vol1", "sub", "notes.txt"), "hi");
        File.WriteAllText(Path.Combine(_tempDir, "vol2", "other.txt"), "x");
        _fs = new FileSystemApi(new Dictionary<string, string>
        {
            ["Volume 1"] = Path.Combine(_tempDir, "vol1"),
            ["Volume 2"] = Path.Combine(_tempDir, "vol2"),
        });
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void List_Root_ReturnsVolumes()
    {
        var entries = _fs.List("");

        Assert.Equal(2, entries.Count);
        Assert.Equal("Volume 1", entries[0].Name);
        Assert.Equal("Volume 2", entries[1].Name);
        Assert.All(entries, e =>
        {
            Assert.True(e.IsDirectory);
            Assert.Equal("Volume", e.Kind);
            Assert.Null(e.SizeBytes);
        });
    }

    [Fact]
    public void List_Volume_ReturnsEntriesWithMetadata()
    {
        var entries = _fs.List("Volume 1");

        Assert.Equal(2, entries.Count);
        // Directories are sorted first
        Assert.Equal("sub", entries[0].Name);
        Assert.True(entries[0].IsDirectory);
        Assert.Null(entries[0].SizeBytes);
        Assert.Equal("Ordner", entries[0].Kind);

        Assert.Equal("movie.mkv", entries[1].Name);
        Assert.False(entries[1].IsDirectory);
        Assert.Equal(6, entries[1].SizeBytes);
        Assert.Equal("Matroska Video", entries[1].Kind);
        Assert.True(entries[1].ModifiedUtc > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public void List_Subdirectory()
    {
        var entries = _fs.List("Volume 1/sub");
        Assert.Equal("notes.txt", Assert.Single(entries).Name);
    }

    [Fact]
    public void List_UnknownExtension_GetsGenericKind()
    {
        File.WriteAllText(Path.Combine(_tempDir, "vol1", "data.xyz"), "");
        File.WriteAllText(Path.Combine(_tempDir, "vol1", "Makefile"), "");

        var entries = _fs.List("Volume 1");

        Assert.Equal("XYZ-Datei", entries.Single(e => e.Name == "data.xyz").Kind);
        Assert.Equal("Datei", entries.Single(e => e.Name == "Makefile").Kind);
    }

    [Theory]
    [InlineData("Volume 1/..")]
    [InlineData("Volume 1/../outside")]
    [InlineData("Volume 1/sub/../../../outside")]
    [InlineData("Volume 1/../Volume 2")]
    public void PathsEscapingTheirVolume_AreRejected(string path)
    {
        Assert.Throws<ArgumentException>(() => _fs.List(path));
        Assert.Throws<ArgumentException>(() => _fs.Stat(path));
        Assert.Throws<ArgumentException>(() => _fs.OpenRead(path));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("/etc")]
    [InlineData("no-such-volume/file")]
    public void UnknownVolume_IsRejected(string path)
    {
        Assert.Throws<DirectoryNotFoundException>(() => _fs.List(path));
        Assert.Throws<DirectoryNotFoundException>(() => _fs.Stat(path));
        Assert.Throws<DirectoryNotFoundException>(() => _fs.OpenRead(path));
    }

    [Fact]
    public void PathResolvingBackToVolumeRoot_IsAllowed()
    {
        var entries = _fs.List("Volume 1/sub/..");
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void OpenRead_ReadsFileContent()
    {
        using var reader = new StreamReader(_fs.OpenRead("Volume 1/movie.mkv"));
        Assert.Equal("123456", reader.ReadToEnd());
    }

    [Fact]
    public void Stat_ReturnsEntryForFileDirectoryAndVolume()
    {
        Assert.False(_fs.Stat("Volume 1/sub/notes.txt").IsDirectory);
        Assert.True(_fs.Stat("Volume 1/sub").IsDirectory);
        Assert.Equal("Volume", _fs.Stat("Volume 1").Kind);
    }

    [Fact]
    public void Stat_MissingPath_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => _fs.Stat("Volume 1/does-not-exist"));
    }

    [Fact]
    public void GetVolumes_ReportsCapacity()
    {
        var volumes = _fs.GetVolumes();

        Assert.Equal(["Volume 1", "Volume 2"], volumes.Select(v => v.Name));
        Assert.All(volumes, v =>
        {
            Assert.True(v.TotalBytes > 0);
            Assert.InRange(v.AvailableBytes, 0, v.TotalBytes);
        });
    }
}
