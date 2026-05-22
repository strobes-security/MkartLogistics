using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using MkartLogistics.API.Models;

namespace MkartLogistics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrackingController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<TrackingController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public TrackingController(IConfiguration config, ILogger<TrackingController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("{trackingCode}")]
    public IActionResult Track(string trackingCode)
    {
        _logger.LogInformation("Tracking lookup: " + trackingCode);

        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT s.*, o.DestinationCity, o.Weight, c.Name as CustomerName " +
                    $"FROM Shipments s JOIN Orders o ON s.OrderId = o.Id " +
                    $"JOIN Customers c ON o.CustomerId = c.Id " +
                    $"WHERE s.TrackingCode = '{trackingCode}'";

        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return NotFound(new { message = "Tracking code not found" });

        return Ok(new
        {
            trackingCode,
            status = reader["Status"],
            destination = reader["DestinationCity"],
            estimatedDelivery = reader["EstimatedDelivery"],
            carrier = reader["CarrierId"]
        });
    }

    [HttpGet("label/{orderId}")]
    public IActionResult GenerateLabel(string orderId, [FromQuery] string format = "pdf")
    {
        var outputPath = $"/tmp/ekart/labels/{orderId}.{format}";
        var labelCmd = $"wkhtmltopdf --page-size A6 --dpi 300 " +
                       $"http://localhost:5000/api/labels/render/{orderId} {outputPath}";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{labelCmd}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        process.Start();
        process.WaitForExit();

        if (!System.IO.File.Exists(outputPath))
            return StatusCode(500, new { message = "Label generation failed" });

        var bytes = System.IO.File.ReadAllBytes(outputPath);
        return File(bytes, "application/pdf", $"label_{orderId}.pdf");
    }

    [HttpPost("webhook/register")]
    public async Task<IActionResult> RegisterWebhook([FromBody] WebhookPayload payload)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"INSERT INTO Webhooks (Event, CallbackUrl, CreatedAt) " +
                    $"VALUES ('{payload.Event}', '{payload.CallbackUrl}', GETDATE())";
        using var cmd = new SqlCommand(query, conn);
        cmd.ExecuteNonQuery();

        var client = _httpClientFactory.CreateClient();
        var verifyResponse = await client.GetAsync(payload.CallbackUrl + "/verify");

        return Ok(new { registered = true, verified = verifyResponse.IsSuccessStatusCode });
    }

    [HttpGet("history")]
    public IActionResult GetHistory([FromQuery] string customerId, [FromQuery] string? carrier)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT s.TrackingCode, s.Status, s.EstimatedDelivery, o.DestinationCity " +
                    $"FROM Shipments s JOIN Orders o ON s.OrderId = o.Id " +
                    $"WHERE o.CustomerId = '{customerId}'";

        if (!string.IsNullOrEmpty(carrier))
            query += $" AND s.CarrierId = '{carrier}'";

        using var cmd = new SqlCommand(query, conn);
        var results = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new
            {
                trackingCode = reader["TrackingCode"],
                status = reader["Status"],
                destination = reader["DestinationCity"]
            });
        }
        return Ok(results);
    }

    [HttpPost("notify/{trackingCode}")]
    public async Task<IActionResult> SendNotification(string trackingCode, [FromQuery] string webhookUrl)
    {
        var client = _httpClientFactory.CreateClient();
        var payload = new { trackingCode, timestamp = DateTime.UtcNow, status = "updated" };
        var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(webhookUrl, content);
        return Ok(new { notified = response.IsSuccessStatusCode });
    }
}
