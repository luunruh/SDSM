using Microsoft.Extensions.FileProviders;
using Plugins;
using SDSM.PluginSdk;

string AppPath = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
string AppDir = Path.GetDirectoryName(AppPath) ?? "";

var builder = WebApplication.CreateBuilder(args);

// Volumes come from the "Volumes" config section (name -> path); a
// single CLI argument is a shorthand for one volume named after the
// directory. Fallback: the current directory.
var volumes = new Dictionary<string, string>();
foreach (var child in builder.Configuration.GetSection("Volumes").GetChildren()) {
    if (child.Value != null) {
        volumes[child.Key] = child.Value;
    }
}
if (args.Length == 1 && !args[0].StartsWith('-')) {
    string path = Path.GetFullPath(args[0]);
    volumes = new Dictionary<string, string> { [VolumeNameFor(path)] = path };
}
if (volumes.Count == 0) {
    string cwd = Directory.GetCurrentDirectory();
    volumes[VolumeNameFor(cwd)] = cwd;
}

string pluginsDir = builder.Configuration["Plugins:Directory"] ?? Path.Combine(AppDir, "plugins");
var pluginLoader = PluginLoader.LoadFrom(pluginsDir);

builder.Services.AddControllers();
builder.Services.AddSingleton<IFileSystemApi>(new Services.FileSystemApi(volumes));
builder.Services.AddSingleton(pluginLoader);
pluginLoader.ConfigureServices(builder.Services);

var app = builder.Build();

foreach (string warning in pluginLoader.Warnings) {
    app.Logger.LogWarning("{Warning}", warning);
}

app.MapControllers();
pluginLoader.MapEndpoints(app);

app.UseDefaultFiles(); // will use index.html
app.UseStaticFiles();
app.UseFileServer(new FileServerOptions {
        FileProvider = new PhysicalFileProvider(
                Path.Combine(AppDir, "static")
            ),
        RequestPath = "",
        EnableDefaultFiles = true
    });
app.UseFileServer(new FileServerOptions {
        FileProvider = new PhysicalFileProvider(
                Path.Combine(AppDir, "css")
            ),
        RequestPath = "/css"
    });
app.UseFileServer(new FileServerOptions {
        FileProvider = new PhysicalFileProvider(
                Path.Combine(AppDir, "js")
            ),
        RequestPath = "/js"
    });

// Serve each plugin's folder (manifest + ui assets) read-only
foreach (LoadedPlugin plugin in pluginLoader.Plugins) {
    app.UseStaticFiles(new StaticFileOptions {
            FileProvider = new PhysicalFileProvider(plugin.Directory),
            RequestPath = $"/plugins/{plugin.Manifest.Id}"
        });
}

app.Run();

static string VolumeNameFor(string path) {
    string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
    return string.IsNullOrEmpty(name) ? "Root" : name;
}
