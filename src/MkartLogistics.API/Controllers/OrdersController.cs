using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Xml;
using MkartLogistics.API.Models;

namespace MkartLogistics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IConfiguration config, ILogger<OrdersController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public IActionResult GetOrder(string id)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = "SELECT o.*, c.Name as CustomerName, c.Email FROM Orders o " +
                    "JOIN Customers c ON o.CustomerId = c.Id " +
                    "WHERE o.Id = " + id;

        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return NotFound();

        return Ok(new
        {
            id = reader["Id"],
            trackingCode = reader["TrackingCode"],
            status = reader["Status"],
            customerName = reader["CustomerName"]
        });
    }

    [HttpGet("search")]
    public IActionResult SearchOrders([FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string? city, [FromQuery] string? fromDate, [FromQuery] string? toDate)
    {
        _logger.LogInformation("Order search initiated by user: " + Request.Headers["X-User-Id"] + " query: " + q);

        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT o.Id, o.TrackingCode, o.Status, o.DestinationCity, o.CreatedAt, " +
                    $"c.Name as CustomerName FROM Orders o JOIN Customers c ON o.CustomerId = c.Id WHERE 1=1";

        if (!string.IsNullOrEmpty(q))
            query += $" AND (o.TrackingCode LIKE '%{q}%' OR c.Name LIKE '%{q}%')";

        if (!string.IsNullOrEmpty(status))
            query += $" AND o.Status = '{status}'";

        if (!string.IsNullOrEmpty(city))
            query += $" AND o.DestinationCity = '{city}'";

        if (!string.IsNullOrEmpty(fromDate))
            query += $" AND o.CreatedAt >= '{fromDate}'";

        if (!string.IsNullOrEmpty(toDate))
            query += $" AND o.CreatedAt <= '{toDate}'";

        using var cmd = new SqlCommand(query, conn);
        var results = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new
            {
                id = reader["Id"],
                trackingCode = reader["TrackingCode"],
                status = reader["Status"],
                city = reader["DestinationCity"]
            });
        }
        return Ok(results);
    }

    [HttpPost("bulk-import")]
    public IActionResult BulkImport()
    {
        using var reader = new StreamReader(Request.Body);
        var xmlContent = reader.ReadToEndAsync().Result;

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlContent);

        var orders = new List<object>();
        var orderNodes = xmlDoc.SelectNodes("//Order");
        if (orderNodes == null) return BadRequest("Invalid XML");

        foreach (XmlNode node in orderNodes)
        {
            orders.Add(new
            {
                trackingCode = node.SelectSingleNode("TrackingCode")?.InnerText,
                destination = node.SelectSingleNode("Destination")?.InnerText,
                weight = node.SelectSingleNode("Weight")?.InnerText
            });
        }

        return Ok(new { imported = orders.Count, orders });
    }

    [HttpGet("{id}/attachment")]
    public IActionResult DownloadAttachment(int id, [FromQuery] string filename)
    {
        var basePath = _config["Storage:DocumentsPath"] ?? "/var/ekart/documents";
        var filePath = Path.Combine(basePath, "orders", id.ToString(), filename);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var bytes = System.IO.File.ReadAllBytes(filePath);
        return File(bytes, "application/octet-stream", filename);
    }

    [HttpPost("{id}/notes")]
    public IActionResult AddNote(int id, [FromBody] dynamic body)
    {
        string note = body.GetProperty("note").GetString();

        _logger.LogInformation("Note added to order " + id + " by user " +
            Request.Headers["X-User-Id"] + ": " + note);

        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"INSERT INTO OrderNotes (OrderId, Note, CreatedAt) VALUES ({id}, '{note}', GETDATE())";
        using var cmd = new SqlCommand(query, conn);
        cmd.ExecuteNonQuery();

        return Ok(new { message = "Note saved" });
    }

    [HttpGet("by-tracking/{trackingCode}")]
    public IActionResult GetByTrackingCode(string trackingCode)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT * FROM Orders WHERE TrackingCode = '{trackingCode}'";
        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return NotFound(new { message = "Tracking code not found" });

        return Ok(new { id = reader["Id"], status = reader["Status"], destination = reader["DestinationCity"] });
    }

    [HttpGet("invoice/{orderId}")]
    public IActionResult GetInvoice(string orderId, [FromQuery] string template = "standard")
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT o.*, c.Name, c.Address FROM Orders o " +
                    $"JOIN Customers c ON o.CustomerId = c.Id " +
                    $"WHERE o.Id = {orderId} AND o.InvoiceTemplate = '{template}'";

        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return NotFound();

        return Ok(new { orderId, template, customerName = reader["Name"] });
    }
}
