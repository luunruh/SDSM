using Services;

namespace SDSM_Server.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _file;

    public AuthServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sdsm-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _file = Path.Combine(_tempDir, "auth.json");
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void WithoutCredentialsFile_SetupIsRequired_AndVerifyFails()
    {
        var auth = new AuthService(_file);
        Assert.True(auth.SetupRequired);
        Assert.False(auth.Verify("admin", "whatever"));
    }

    [Fact]
    public void SetPassword_PersistsAndVerifies()
    {
        var auth = new AuthService(_file);
        auth.SetPassword("admin", "correct horse battery");

        Assert.False(auth.SetupRequired);
        Assert.True(auth.Verify("admin", "correct horse battery"));
        Assert.False(auth.Verify("admin", "wrong password"));
        Assert.False(auth.Verify("other", "correct horse battery"));
    }

    [Fact]
    public void Credentials_SurviveRestart()
    {
        new AuthService(_file).SetPassword("admin", "correct horse battery");

        var reloaded = new AuthService(_file);
        Assert.False(reloaded.SetupRequired);
        Assert.True(reloaded.Verify("admin", "correct horse battery"));
    }

    [Fact]
    public void StoredFile_ContainsNoPlaintextPassword()
    {
        new AuthService(_file).SetPassword("admin", "correct horse battery");
        Assert.DoesNotContain("correct horse battery", File.ReadAllText(_file));
    }
}
