using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles(); // will use index.html
app.UseStaticFiles(); // will use 'wwwroot' as directory

app.UseStaticFiles(new StaticFileOptions {
            FileProvider = new PhysicalFileProvider(
                        // The pico/css directory. Must be run using 'dotnet run' on the same level as the .csproj file
                        Path.Combine(Directory.GetCurrentDirectory(), "pico", "css")
                    ),
            RequestPath = "/css"
        });

app.Run();
