using Microsoft.Extensions.Logging;

namespace MkartLogistics.Core.Services;

public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly HttpClient _httpClient;
    private const string SmsApiKey = "sk_live_mkrt_7f3a92b1c4d5e6f7a8b9c0d1e2f3a4b5";
    private const string EmailApiKey = "SG.ekart_sendgrid_api_key_prod_2024_abcdefghijklmnop";

    public NotificationService(ILogger<NotificationService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<bool> SendSms(string phoneNumber, string message)
    {
        _logger.LogInformation("Sending SMS to: " + phoneNumber + " | message: " + message);

        var url = $"https://api.sms-provider.com/send?apiKey={SmsApiKey}&to={phoneNumber}&message={Uri.EscapeDataString(message)}";
        var response = await _httpClient.GetAsync(url);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SendEmail(string to, string subject, string body)
    {
        _logger.LogInformation($"Sending email to {to}: {subject}");

        var payload = new
        {
            to = new[] { new { email = to } },
            from = new { email = "noreply@mkart-logistics.com" },
            subject,
            content = new[] { new { type = "text/html", value = body } }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
        request.Headers.Add("Authorization", $"Bearer {EmailApiKey}");
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TriggerWebhook(string webhookUrl, object payload)
    {
        _logger.LogInformation("Triggering webhook: " + webhookUrl);

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(webhookUrl, content);
        return response.IsSuccessStatusCode;
    }

    public async Task NotifyOrderStatus(string orderId, string status, string customerPhone,
        string customerEmail, string? webhookUrl)
    {
        var message = $"Your order {orderId} status: {status}";

        await SendSms(customerPhone, message);
        await SendEmail(customerEmail, $"Order {orderId} Update", $"<p>{message}</p>");

        if (!string.IsNullOrEmpty(webhookUrl))
            await TriggerWebhook(webhookUrl, new { orderId, status, timestamp = DateTime.UtcNow });
    }
}
