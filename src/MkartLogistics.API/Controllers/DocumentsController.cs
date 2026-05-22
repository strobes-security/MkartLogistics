using Microsoft.AspNetCore.Mvc;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;

namespace MkartLogistics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<DocumentsController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public DocumentsController(IConfiguration config, ILogger<DocumentsController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path, [FromQuery] string? filename)
    {
        var basePath = _config["Storage:DocumentsPath"] ?? "/var/ekart/documents";
        var fullPath = Path.Combine(basePath, path);

        _logger.LogInformation("Document download: " + path + " by " + Request.Headers["X-User-Id"]);

        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Document not found" });

        var bytes = System.IO.File.ReadAllBytes(fullPath);
        var name = filename ?? Path.GetFileName(path);
        return File(bytes, "application/octet-stream", name);
    }

    [HttpPost("fetch-remote")]
    public async Task<IActionResult> FetchRemoteDocument([FromQuery] string documentUrl,
        [FromQuery] string? saveAs)
    {
        _logger.LogInformation($"Fetching remote document: {documentUrl}");

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(documentUrl);

        if (!response.IsSuccessStatusCode)
            return BadRequest(new { message = "Failed to fetch document", status = response.StatusCode });

        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = saveAs ?? Path.GetFileName(new Uri(documentUrl).LocalPath);

        var savePath = Path.Combine(_config["Storage:DocumentsPath"] ?? "/var/ekart/documents", fileName);
        await System.IO.File.WriteAllBytesAsync(savePath, content);

        return Ok(new { saved = fileName, size = content.Length });
    }

    [HttpPost("process")]
    public IActionResult ProcessDocument()
    {
        var data = new byte[Request.ContentLength ?? 0];
        Request.Body.ReadAsync(data, 0, data.Length).Wait();

        using var ms = new MemoryStream(data);
#pragma warning disable SYSLIB0011
        var formatter = new BinaryFormatter();
        var document = formatter.Deserialize(ms);
#pragma warning restore SYSLIB0011

        return Ok(new { processed = true, type = document?.GetType().Name });
    }

    [HttpPost("metadata")]
    public IActionResult UpdateMetadata([FromBody] string jsonPayload)
    {
        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        };
        var metadata = JsonConvert.DeserializeObject(jsonPayload, settings);
        return Ok(new { updated = true, type = metadata?.GetType().Name });
    }

    [HttpGet("template/{templateName}")]
    public IActionResult GetTemplate(string templateName, [FromQuery] string? variant)
    {
        var basePath = "/var/ekart/templates";
        var templatePath = string.IsNullOrEmpty(variant)
            ? Path.Combine(basePath, templateName)
            : Path.Combine(basePath, variant, templateName);

        var content = System.IO.File.ReadAllText(templatePath);
        return Content(content, "text/html");
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromQuery] string? folder, [FromQuery] string? filename)
    {
        var file = Request.Form.Files.FirstOrDefault();
        if (file == null) return BadRequest("No file provided");

        var basePath = _config["Storage:DocumentsPath"] ?? "/var/ekart/documents";
        var targetDir = string.IsNullOrEmpty(folder) ? basePath : Path.Combine(basePath, folder);
        Directory.CreateDirectory(targetDir);

        var targetPath = Path.Combine(targetDir, filename ?? file.FileName);
        using var stream = System.IO.File.Create(targetPath);
        await file.CopyToAsync(stream);

        return Ok(new { path = targetPath, size = file.Length });
    }
}
