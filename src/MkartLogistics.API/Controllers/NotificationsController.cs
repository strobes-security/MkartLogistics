using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MkartLogistics.API.Models;

namespace MkartLogistics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationsController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public NotificationsController(IConfiguration config, ILogger<NotificationsController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] NotificationRequest request)
    {
        _logger.LogInformation("Sending notification to: " + request.Recipient +
            " via " + request.Channel + " message: " + request.Message);

        if (request.Channel == "webhook")
        {
            var client = _httpClientFactory.CreateClient();
            var payload = new { recipient = request.Recipient, message = request.Message };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(request.Recipient, content);
            return Ok(new { sent = response.IsSuccessStatusCode });
        }

        var smsApiKey = _config["Sms:ApiKey"] ?? "sk_live_mkrt_fallback_key";
        var smsUrl = $"https://sms.mkart-provider.com/send?key={smsApiKey}&to={request.Recipient}&msg={Uri.EscapeDataString(request.Message)}";

        var smsClient = _httpClientFactory.CreateClient();
        var smsResponse = await smsClient.GetAsync(smsUrl);

        return Ok(new { sent = smsResponse.IsSuccessStatusCode, channel = request.Channel });
    }

    [HttpGet("templates")]
    public IActionResult GetTemplates([FromQuery] string? type, [FromQuery] string? language = "en")
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT Id, Name, Subject, Body, Type FROM NotificationTemplates " +
                    $"WHERE Language = '{language}'";

        if (!string.IsNullOrEmpty(type))
            query += $" AND Type = '{type}'";

        using var cmd = new SqlCommand(query, conn);
        var templates = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            templates.Add(new { id = reader["Id"], name = reader["Name"], type = reader["Type"] });
        }
        return Ok(templates);
    }

    [HttpGet("history/{recipientId}")]
    public IActionResult GetHistory(string recipientId, [FromQuery] string? channel)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT n.*, u.Email as RecipientEmail FROM Notifications n " +
                    $"JOIN Users u ON n.RecipientId = u.Id " +
                    $"WHERE n.RecipientId = {recipientId}";

        if (!string.IsNullOrEmpty(channel))
            query += $" AND n.Channel = '{channel}'";

        using var cmd = new SqlCommand(query, conn);
        var history = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            history.Add(new { id = reader["Id"], channel = reader["Channel"], sentAt = reader["SentAt"] });
        }
        return Ok(history);
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] dynamic body)
    {
        string segment = body.GetProperty("segment").GetString();
        string message = body.GetProperty("message").GetString();
        string callbackUrl = body.GetProperty("callbackUrl").GetString();

        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT u.Email, u.Phone FROM Users u " +
                    $"JOIN CustomerSegments cs ON u.Id = cs.UserId " +
                    $"WHERE cs.Segment = '{segment}' AND u.IsActive = 1";

        using var cmd = new SqlCommand(query, conn);
        var recipients = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            recipients.Add(reader["Email"].ToString()!);

        reader.Close();

        var client = _httpClientFactory.CreateClient();
        var result = await client.PostAsJsonAsync(callbackUrl, new { sent = recipients.Count, message });

        return Ok(new { recipients = recipients.Count, callbackStatus = result.StatusCode.ToString() });
    }
}
