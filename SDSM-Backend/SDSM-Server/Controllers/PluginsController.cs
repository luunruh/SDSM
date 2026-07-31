using Microsoft.AspNetCore.Mvc;
using Plugins;

[ApiController]
[Route("api/plugins")]
public class PluginsController : ControllerBase
{
    [HttpGet]
    [Route("")]
    public IResult GetPlugins(PluginLoader loader)
    {
        return Results.Ok(loader.Plugins.Select(p => new
        {
            id = p.Manifest.Id,
            name = p.Manifest.Name,
            version = p.Manifest.Version,
            ui = p.Manifest.Ui != null ? $"/plugins/{p.Manifest.Id}/{p.Manifest.Ui}" : null,
            nav = p.Manifest.Nav != null
                ? new { title = p.Manifest.Nav.Title, icon = p.Manifest.Nav.Icon }
                : null,
        }));
    }
}
