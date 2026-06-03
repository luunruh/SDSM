using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("files")]
public class FilesController : ControllerBase
{
    private List<Models.FileSystemEntry> GetEntries(string path) {
        List<Models.FileSystemEntry> entries = new List<Models.FileSystemEntry>();
        foreach (string name in Directory.GetFileSystemEntries(path)) {
            entries.Add(new Models.FileSystemEntry { 
                    Name = Path.GetFileName(name),
                    IsDirectory = Directory.Exists(name)
                });
        }
        return entries;
    }

    [HttpGet]
    [Route("")]
    public IResult GetFiles(Config config) {
        return Results.Ok(GetEntries(config.RootDir));
    }

    [HttpGet]
    [Route("{*path}")]
    public IResult GetFiles(string path, Config config) {
        // TODO: Path traversal possible
        return Results.Ok(GetEntries(Path.Combine(config.RootDir, path)));
    }
}
