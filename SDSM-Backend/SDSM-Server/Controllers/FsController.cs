using Microsoft.AspNetCore.Mvc;
using SDSM.PluginSdk;

[ApiController]
[Route("api/fs")]
public class FsController : ControllerBase
{
    private readonly IFileSystemApi _fs;

    public FsController(IFileSystemApi fs)
    {
        _fs = fs;
    }

    [HttpGet]
    [Route("list/{*path}")]
    [Route("list")]
    public IResult List(string? path)
    {
        try
        {
            return Results.Ok(_fs.List(path ?? ""));
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
        }
        catch (DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
    }

    [HttpGet]
    [Route("download/{*path}")]
    public IResult Download(string path)
    {
        try
        {
            return Results.File(_fs.OpenRead(path), "application/octet-stream",
                fileDownloadName: Path.GetFileName(path));
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
        }
        catch (IOException e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Thrown i.a. when the path is a directory
            return Results.BadRequest();
        }
    }
}
