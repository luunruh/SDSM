using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SDSM.PluginSdk;

namespace SDSM.TestPlugin;

// Fixture for PluginLoader tests — not shipped.
public class TestPlugin : ISdsmPlugin
{
    public string Id => "test-plugin";

    public bool ServicesConfigured { get; private set; }

    public void ConfigureServices(IServiceCollection services)
    {
        ServicesConfigured = true;
    }

    public void MapEndpoints(IEndpointRouteBuilder group)
    {
    }
}
