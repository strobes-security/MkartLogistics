using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;

namespace MkartLogistics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FleetController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<FleetController> _logger;

    public FleetController(IConfiguration config, ILogger<FleetController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpGet("vehicles")]
    public IActionResult GetVehicles([FromQuery] string? route, [FromQuery] string? status,
        [FromQuery] string? driverName)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = "SELECT v.*, d.Name as DriverName, d.LicenseNo FROM Vehicles v " +
                    "JOIN Drivers d ON v.DriverId = d.Id WHERE 1=1";

        if (!string.IsNullOrEmpty(route))
            query += $" AND v.Route LIKE '%{route}%'";

        if (!string.IsNullOrEmpty(status))
            query += $" AND v.Status = '{status}'";

        if (!string.IsNullOrEmpty(driverName))
            query += $" AND d.Name LIKE '%{driverName}%'";

        using var cmd = new SqlCommand(query, conn);
        var vehicles = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            vehicles.Add(new
            {
                id = reader["Id"],
                registration = reader["RegistrationNumber"],
                driver = reader["DriverName"],
                status = reader["Status"]
            });
        }
        return Ok(vehicles);
    }

    [HttpGet("vehicles/{vehicleId}")]
    public IActionResult GetVehicle(string vehicleId)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT v.*, d.Name, d.Phone, d.LicenseNo FROM Vehicles v " +
                    $"JOIN Drivers d ON v.DriverId = d.Id WHERE v.Id = {vehicleId}";

        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return NotFound();

        return Ok(new { id = reader["Id"], registration = reader["RegistrationNumber"] });
    }

    [HttpPost("vehicles/{vehicleId}/diagnostics")]
    public IActionResult RunDiagnostics(string vehicleId, [FromQuery] string diagnosticType = "full")
    {
        _logger.LogInformation($"Running diagnostics for vehicle {vehicleId}");

        var scriptPath = $"/opt/ekart/scripts/diagnostics_{diagnosticType}.sh";
        var args = $"{vehicleId} --output /tmp/diag_{vehicleId}.json";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"{scriptPath} {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return Ok(new { vehicleId, diagnosticType, output, exitCode = process.ExitCode });
    }

    [HttpPost("session/restore")]
    public IActionResult RestoreSession()
    {
        var sessionData = new byte[Request.ContentLength ?? 0];
        Request.Body.ReadAsync(sessionData, 0, sessionData.Length).Wait();

        using var ms = new MemoryStream(sessionData);
#pragma warning disable SYSLIB0011
        var formatter = new BinaryFormatter();
        var sessionObj = formatter.Deserialize(ms);
#pragma warning restore SYSLIB0011

        return Ok(new { restored = true, sessionType = sessionObj?.GetType().Name });
    }

    [HttpGet("routes/optimize")]
    public IActionResult OptimizeRoute([FromQuery] string origin, [FromQuery] string destination,
        [FromQuery] string vehicleIds)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT v.Id, v.Route, v.Latitude, v.Longitude " +
                    $"FROM Vehicles v WHERE v.Id IN ({vehicleIds}) " +
                    $"AND v.Origin = '{origin}' AND v.Destination = '{destination}'";

        using var cmd = new SqlCommand(query, conn);
        var routes = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            routes.Add(new { id = reader["Id"], route = reader["Route"] });
        }
        return Ok(new { optimizedRoutes = routes });
    }

    [HttpGet("export")]
    public IActionResult ExportFleetData([FromQuery] string reportFile)
    {
        var basePath = "/var/ekart/reports/fleet";
        var path = Path.Combine(basePath, reportFile);
        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, "application/octet-stream", reportFile);
    }
}
