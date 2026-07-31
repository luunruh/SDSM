using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SDSM.PluginSdk;

public interface ISdsmPlugin
{
    /// Must match the "id" in the plugin's manifest.json.
    string Id { get; }

    void ConfigureServices(IServiceCollection services);

    /// The group is already mounted at /api/plugins/{id}/.
    void MapEndpoints(IEndpointRouteBuilder group);
}
