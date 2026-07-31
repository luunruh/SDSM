using Services;

namespace SDSM_Server.Tests;

public class FileSystemApiTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemApi _fs;

    public FileSystemApiTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sdsm-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "movie.mkv"), "123456");
        File.WriteAllText(Path.Combine(_root, "sub", "notes.txt"), "hi");
        _fs = new FileSystemApi(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void List_ReturnsEntriesWithMetadata()
    {
        var entries = _fs.List("");

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
        var entries = _fs.List("sub");
        Assert.Equal("notes.txt", Assert.Single(entries).Name);
    }

    [Fact]
    public void List_UnknownExtension_GetsGenericKind()
    {
        File.WriteAllText(Path.Combine(_root, "data.xyz"), "");
        File.WriteAllText(Path.Combine(_root, "Makefile"), "");

        var entries = _fs.List("");

        Assert.Equal("XYZ-Datei", entries.Single(e => e.Name == "data.xyz").Kind);
        Assert.Equal("Datei", entries.Single(e => e.Name == "Makefile").Kind);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("sub/../../outside")]
    [InlineData("/etc")]
    public void PathsEscapingRoot_AreRejected(string path)
    {
        Assert.Throws<ArgumentException>(() => _fs.List(path));
        Assert.Throws<ArgumentException>(() => _fs.Stat(path));
        Assert.Throws<ArgumentException>(() => _fs.OpenRead(path));
    }

    [Fact]
    public void PathResolvingBackToRoot_IsAllowed()
    {
        var entries = _fs.List("sub/..");
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void OpenRead_ReadsFileContent()
    {
        using var reader = new StreamReader(_fs.OpenRead("movie.mkv"));
        Assert.Equal("123456", reader.ReadToEnd());
    }

    [Fact]
    public void Stat_ReturnsEntryForFileAndDirectory()
    {
        Assert.False(_fs.Stat("sub/notes.txt").IsDirectory);
        Assert.True(_fs.Stat("sub").IsDirectory);
    }

    [Fact]
    public void Stat_MissingPath_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => _fs.Stat("does-not-exist"));
    }
}
