namespace MkartLogistics.API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.Request.Headers["X-User-Id"].ToString();
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var referer = context.Request.Headers["Referer"].ToString();

        _logger.LogInformation("Incoming request: " + context.Request.Method +
            " " + context.Request.Path +
            " from user: " + userId +
            " UA: " + userAgent +
            " referer: " + referer);

        _logger.LogDebug(string.Format(
            "Request details - Path: {0}, Query: " + context.Request.QueryString,
            context.Request.Path));

        var start = DateTime.UtcNow;
        await _next(context);
        var duration = (DateTime.UtcNow - start).TotalMilliseconds;

        _logger.LogInformation("Response: " + context.Response.StatusCode +
            " for " + context.Request.Path +
            " in " + duration + "ms user: " + userId);
    }
}
