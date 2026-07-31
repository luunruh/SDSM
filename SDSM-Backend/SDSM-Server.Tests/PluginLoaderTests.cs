using Microsoft.Extensions.DependencyInjection;
using Plugins;

namespace SDSM_Server.Tests;

public class PluginLoaderTests : IDisposable
{
    private readonly string _pluginsDir;

    public PluginLoaderTests()
    {
        _pluginsDir = Path.Combine(Path.GetTempPath(), "sdsm-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_pluginsDir);
    }

    public void Dispose()
    {
        Directory.Delete(_pluginsDir, recursive: true);
    }

    private string CreatePlugin(string folderName, string? manifestJson, params string[] files)
    {
        string dir = Path.Combine(_pluginsDir, folderName);
        Directory.CreateDirectory(dir);
        if (manifestJson != null)
        {
            File.WriteAllText(Path.Combine(dir, "manifest.json"), manifestJson);
        }
        foreach (string file in files)
        {
            string path = Path.Combine(dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "");
        }
        return dir;
    }

    private string CreateBackendPlugin(string id)
    {
        string dir = CreatePlugin(id, $$"""
            { "id": "{{id}}", "name": "Test", "version": "1.0.0", "backend": "backend/SDSM.TestPlugin.dll" }
            """);
        string backendDir = Path.Combine(dir, "backend");
        Directory.CreateDirectory(backendDir);
        File.Copy(typeof(SDSM.TestPlugin.TestPlugin).Assembly.Location,
            Path.Combine(backendDir, "SDSM.TestPlugin.dll"));
        return dir;
    }

    [Fact]
    public void MissingPluginsDirectory_YieldsNoPluginsAndNoWarnings()
    {
        var loader = PluginLoader.LoadFrom(Path.Combine(_pluginsDir, "does-not-exist"));
        Assert.Empty(loader.Plugins);
        Assert.Empty(loader.Warnings);
    }

    [Fact]
    public void ValidUiOnlyPlugin_IsLoaded()
    {
        CreatePlugin("my-plugin", """
            { "id": "my-plugin", "name": "My Plugin", "version": "1.2.3",
              "ui": "ui/main.js", "nav": { "title": "Mine", "icon": "folder" } }
            """, "ui/main.js");

        var loader = PluginLoader.LoadFrom(_pluginsDir);

        var plugin = Assert.Single(loader.Plugins);
        Assert.Empty(loader.Warnings);
        Assert.Equal("my-plugin", plugin.Manifest.Id);
        Assert.Equal("My Plugin", plugin.Manifest.Name);
        Assert.Equal("1.2.3", plugin.Manifest.Version);
        Assert.Equal("ui/main.js", plugin.Manifest.Ui);
        Assert.Equal("Mine", plugin.Manifest.Nav?.Title);
        Assert.Null(plugin.Instance);
    }

    [Fact]
    public void MissingManifest_IsSkippedWithWarning()
    {
        CreatePlugin("no-manifest", null);
        var loader = PluginLoader.LoadFrom(_pluginsDir);
        Assert.Empty(loader.Plugins);
        Assert.Contains(loader.Warnings, w => w.Contains("no-manifest"));
    }

    [Fact]
    public void InvalidManifestJson_IsSkippedWithWarning()
    {
        CreatePlugin("broken", "{ not json");
        var loader = PluginLoader.LoadFrom(_pluginsDir);
        Assert.Empty(loader.Plugins);
        Assert.Contains(loader.Warnings, w => w.Contains("broken"));
    }

    [Fact]
    public void ManifestMissingRequiredFields_IsSkippedWithWarning()
    {
        CreatePlugin("incomplete", """{ "id": "incomplete" }""");
        var loader = PluginLoader.LoadFrom(_pluginsDir);
        Assert.Empty(loader.Plugins);
        Assert.Single(loader.Warnings);
    }

    [Fact]
    public void IdNotMatchingFolderName_IsSkippedWithWarning()
    {
        CreatePlugin("folder-a", """{ "id": "other-id", "name": "X", "version": "1.0.0" }""");
        var loader = PluginLoader.LoadFrom(_pluginsDir);
        Assert.Empty(loader.Plugins);
        Assert.Contains(loader.Warnings, w => w.Contains("other-id"));
    }

    [Fact]
    public void InvalidIdCharacters_IsSkippedWithWarning()
    {
        CreatePlugin("BadId", """{ "id": "BadId", "name": "X", "version": "1.0.0" }""");
        var loader = PluginLoader.LoadFrom(_pluginsDir);
        Assert.Empty(loader.Plugins);
        Assert.Single(loader.Warnings);
    }

    [Fact]
    public void DeclaredUiEntryMissing_IsSkippedWithWarning()
    {
        CreatePlugin("no-ui-file", """
            { "id": "no-ui-file", "name": "X", "version": "1.0.0", "ui": "ui/main.js" }
            """);
        var loader = PluginLoader.LoadFrom(_pluginsDir);
        Assert.Empty(loader.Plugins);
        Assert.Contains(loader.Warnings, w => w.Contains("ui/main.js"));
    }

    [Fact]
    public void InvalidPlugin_DoesNotPreventLoadingOthers()
    {
        CreatePlugin("broken", "{ not json");
        CreatePlugin("ok-plugin", """{ "id": "ok-plugin", "name": "Ok", "version": "1.0.0" }""");

        var loader = PluginLoader.LoadFrom(_pluginsDir);

        Assert.Equal("ok-plugin", Assert.Single(loader.Plugins).Manifest.Id);
        Assert.Single(loader.Warnings);
    }

    [Fact]
    public void BackendPlugin_LoadsInstanceFromAssembly()
    {
        CreateBackendPlugin("test-plugin");

        var loader = PluginLoader.LoadFrom(_pluginsDir);

        var plugin = Assert.Single(loader.Plugins);
        Assert.Empty(loader.Warnings);
        Assert.NotNull(plugin.Instance);
        Assert.Equal("test-plugin", plugin.Instance.Id);
    }

    [Fact]
    public void BackendPlugin_IdMismatchWithManifest_IsSkippedWithWarning()
    {
        // Assembly's plugin class reports "test-plugin", manifest says "wrong-id"
        CreateBackendPlugin("wrong-id");

        var loader = PluginLoader.LoadFrom(_pluginsDir);

        Assert.Empty(loader.Plugins);
        Assert.Contains(loader.Warnings, w => w.Contains("wrong-id"));
    }

    [Fact]
    public void DeclaredBackendAssemblyMissing_IsSkippedWithWarning()
    {
        CreatePlugin("no-dll", """
            { "id": "no-dll", "name": "X", "version": "1.0.0", "backend": "backend/gone.dll" }
            """);
        var loader = PluginLoader.LoadFrom(_pluginsDir);
        Assert.Empty(loader.Plugins);
        Assert.Contains(loader.Warnings, w => w.Contains("gone.dll"));
    }

    [Fact]
    public void ConfigureServices_DelegatesToPluginInstance()
    {
        CreateBackendPlugin("test-plugin");
        var loader = PluginLoader.LoadFrom(_pluginsDir);

        loader.ConfigureServices(new ServiceCollection());

        // The instance lives in its own AssemblyLoadContext, so observe via reflection
        object instance = Assert.Single(loader.Plugins).Instance!;
        bool configured = (bool)instance.GetType().GetProperty("ServicesConfigured")!.GetValue(instance)!;
        Assert.True(configured);
    }
}
