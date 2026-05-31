using Microsoft.Extensions.FileProviders;

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

app.Run();
