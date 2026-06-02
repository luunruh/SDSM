using Microsoft.Extensions.FileProviders;

string rootDir = "";
if (args.Length == 1) {
    rootDir = args[0];
}

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string AppPath = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
string AppDir = Path.GetDirectoryName(AppPath) ?? "";

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

app.MapGet("/files", () => {
            List<Models.FileSystemEntry> entries = new List<Models.FileSystemEntry>();
            foreach (string name in Directory.GetFileSystemEntries(rootDir)) {
                entries.Add(new Models.FileSystemEntry { 
                        Name = name,
                        IsDirectory = Directory.Exists(name)
                    });
            }
            return Results.Ok(entries);
        });

app.MapGet("/files/{*path}", (string path) => {
            List<Models.FileSystemEntry> entries = new List<Models.FileSystemEntry>();
            // TODO: Fix path traversal
            foreach (string name in Directory.GetFileSystemEntries(Path.Combine(rootDir, path))) {
                entries.Add(new Models.FileSystemEntry { 
                        Name = name,
                        IsDirectory = Directory.Exists(name)
                    });
            }
            return Results.Ok(entries);
        });

app.Run();
