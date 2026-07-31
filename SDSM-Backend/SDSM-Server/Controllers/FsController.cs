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

    [HttpPost]
    [Route("mkdir/{*path}")]
    public IResult MakeDirectory(string path)
    {
        return Execute(() => _fs.CreateDirectory(path));
    }

    [HttpPut]
    [Route("upload/{*path}")]
    [DisableRequestSizeLimit]
    public async Task<IResult> Upload(string path)
    {
        try
        {
            await _fs.SaveAsync(path, Request.Body);
            return Results.NoContent();
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

    [HttpDelete]
    [Route("delete/{*path}")]
    public IResult Delete(string path)
    {
        return Execute(() => _fs.Delete(path));
    }

    public record RenameRequest(string NewName);

    [HttpPost]
    [Route("rename/{*path}")]
    public IResult Rename(string path, [FromBody] RenameRequest request)
    {
        return Execute(() => _fs.Rename(path, request.NewName));
    }

    private static IResult Execute(Action action)
    {
        try
        {
            action();
            return Results.NoContent();
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
        }
        catch (IOException e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
        catch (IOException)
        {
            // e.g. rename target already exists
            return Results.Conflict();
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
