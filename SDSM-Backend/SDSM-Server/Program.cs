using Microsoft.Extensions.FileProviders;
using Plugins;
using SDSM.PluginSdk;

string rootDir = "";
if (args.Length == 1) {
    rootDir = args[0];
}
if (string.IsNullOrEmpty(rootDir)) {
    rootDir = Directory.GetCurrentDirectory();
}

string AppPath = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
string AppDir = Path.GetDirectoryName(AppPath) ?? "";

var builder = WebApplication.CreateBuilder(args);

string pluginsDir = builder.Configuration["Plugins:Directory"] ?? Path.Combine(AppDir, "plugins");
var pluginLoader = PluginLoader.LoadFrom(pluginsDir);

builder.Services.AddControllers();
builder.Services.AddSingleton(new Config { RootDir = rootDir });
builder.Services.AddSingleton<IFileSystemApi>(new Services.FileSystemApi(rootDir));
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
