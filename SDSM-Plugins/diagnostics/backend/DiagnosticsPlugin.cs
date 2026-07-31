using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SDSM.PluginSdk;

namespace SDSM.Plugin.Diagnostics;

public class DiagnosticsPlugin : ISdsmPlugin
{
    public string Id => "diagnostics";

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        group.MapGet("/stats", (IFileSystemApi fs) => Results.Ok(new
        {
            volumes = fs.GetVolumes(),
            uptimeSeconds = (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
            loadAverage = ReadLoadAverage(),
        }));
    }

    // Host diagnostics, not the hosted filesystem — /proc is outside
    // IFileSystemApi's scope by design.
    private static double[] ReadLoadAverage()
    {
        try
        {
            string[] parts = File.ReadAllText("/proc/loadavg").Split(' ');
            return
            [
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture),
                double.Parse(parts[2], CultureInfo.InvariantCulture),
            ];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or IndexOutOfRangeException)
        {
            return [];
        }
    }
}
