using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using MkartLogistics.API.Models;

namespace MkartLogistics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IConfiguration config, ILogger<ReportsController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpPost("generate")]
    public IActionResult GenerateReport([FromBody] ReportRequest request)
    {
        _logger.LogInformation($"Generating {request.ReportType} report for {request.FromDate:yyyy-MM-dd} to {request.ToDate:yyyy-MM-dd}");

        var outputPath = request.OutputPath ?? $"/var/ekart/reports/{Guid.NewGuid()}.{request.Format}";
        var reportCmd = $"python3 /opt/ekart/reporting/generate.py " +
                        $"--type {request.ReportType} " +
                        $"--from {request.FromDate:yyyy-MM-dd} " +
                        $"--to {request.ToDate:yyyy-MM-dd} " +
                        $"--format {request.Format} " +
                        $"--output {outputPath}";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{reportCmd}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            return StatusCode(500, new { error = stderr });

        return Ok(new { reportPath = outputPath, format = request.Format });
    }

    [HttpGet("{reportId}")]
    public IActionResult GetReport(string reportId, [FromQuery] string? tenantId)
    {
        var connStr = _config.GetConnectionString("ReportsDb") ??
                      _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT r.*, u.Username as GeneratedBy FROM Reports r " +
                    $"JOIN Users u ON r.UserId = u.Id " +
                    $"WHERE r.Id = '{reportId}'";

        if (!string.IsNullOrEmpty(tenantId))
            query += $" AND r.TenantId = '{tenantId}'";

        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return NotFound();

        return Ok(new
        {
            id = reader["Id"],
            type = reader["ReportType"],
            generatedBy = reader["GeneratedBy"],
            status = reader["Status"]
        });
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string filePath)
    {
        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "Report file not found" });

        var bytes = System.IO.File.ReadAllBytes(filePath);
        var filename = Path.GetFileName(filePath);
        return File(bytes, "application/octet-stream", filename);
    }

    [HttpGet("metrics/summary")]
    public IActionResult GetMetricsSummary([FromQuery] string metric, [FromQuery] string groupBy = "day",
        [FromQuery] string? tenantId = null)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT DATETRUNC({groupBy}, CreatedAt) as Period, " +
                    $"COUNT(*) as Total, SUM({metric}) as MetricValue " +
                    $"FROM Orders WHERE 1=1";

        if (!string.IsNullOrEmpty(tenantId))
            query += $" AND TenantId = '{tenantId}'";

        query += $" GROUP BY DATETRUNC({groupBy}, CreatedAt) ORDER BY Period DESC";

        using var cmd = new SqlCommand(query, conn);
        var rows = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new { period = reader["Period"], total = reader["Total"], value = reader["MetricValue"] });
        }
        return Ok(rows);
    }

    [HttpPost("schedule")]
    public IActionResult ScheduleReport([FromBody] dynamic body)
    {
        string cronExpr = body.GetProperty("cron").GetString();
        string reportType = body.GetProperty("reportType").GetString();

        var scheduleCmd = $"echo \"{cronExpr} root /opt/ekart/reporting/generate.py --type {reportType}\" >> /etc/cron.d/ekart-reports";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{scheduleCmd}\"",
                UseShellExecute = false
            }
        };
        process.Start();
        process.WaitForExit();

        return Ok(new { scheduled = true, cron = cronExpr, reportType });
    }
}
