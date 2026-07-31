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

// Single-admin cookie auth; every endpoint requires the session unless
// explicitly [AllowAnonymous]. Static shell assets are served by
// middleware and stay reachable for the login page.
string authFile = builder.Configuration["Auth:File"] ?? Path.Combine(AppDir, "auth.json");
builder.Services.AddSingleton(new Services.AuthService(authFile));
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.Cookie.Name = "sdsm.session";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        // SPA: signal 401/403 instead of redirecting to a login page
        options.Events.OnRedirectToLogin = ctx => {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx => {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options => {
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddSingleton(pluginLoader);
pluginLoader.ConfigureServices(builder.Services);

var app = builder.Build();

foreach (string warning in pluginLoader.Warnings) {
    app.Logger.LogWarning("{Warning}", warning);
}

// Static shell/plugin assets must be served before the authorization
// middleware — the login page itself has to load unauthenticated.
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
pluginLoader.MapEndpoints(app);

app.Run();

static string VolumeNameFor(string path) {
    string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
    return string.IsNullOrEmpty(name) ? "Root" : name;
}
