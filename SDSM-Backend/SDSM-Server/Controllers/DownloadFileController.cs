using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("downloadfile")]
public class DownloadFileController : ControllerBase
{
    [HttpGet]
    [Route("{*path}")]
    public IActionResult Download(string path, Config config) {
        // TODO: Path traversal possible
        string fullPath = Path.Combine(config.RootDir, path);
        return File(new FileStream(fullPath, FileMode.Open), "application/octet-stream");
    }
}
