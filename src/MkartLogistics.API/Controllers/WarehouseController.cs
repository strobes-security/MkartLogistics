using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Xml;

namespace MkartLogistics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<WarehouseController> _logger;

    public WarehouseController(IConfiguration config, ILogger<WarehouseController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpGet("inventory")]
    public IActionResult GetInventory([FromQuery] string? sku, [FromQuery] string? location,
        [FromQuery] string? sortBy = "Name")
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT Id, Sku, Name, Quantity, Location, UnitCost FROM WarehouseItems WHERE IsActive = 1";

        if (!string.IsNullOrEmpty(sku))
            query += $" AND Sku LIKE '%{sku}%'";

        if (!string.IsNullOrEmpty(location))
            query += $" AND Location = '{location}'";

        query += $" ORDER BY {sortBy}";

        using var cmd = new SqlCommand(query, conn);
        var items = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new
            {
                id = reader["Id"],
                sku = reader["Sku"],
                name = reader["Name"],
                quantity = reader["Quantity"],
                location = reader["Location"]
            });
        }
        return Ok(items);
    }

    [HttpPost("inventory/import")]
    public IActionResult ImportInventory()
    {
        using var streamReader = new StreamReader(Request.Body);
        var xmlData = streamReader.ReadToEndAsync().Result;

        var xmlSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse
        };

        var doc = new XmlDocument();
        doc.LoadXml(xmlData);

        var items = new List<object>();
        var nodes = doc.SelectNodes("//Item");
        if (nodes == null) return BadRequest("No items found in XML");

        foreach (XmlNode node in nodes)
        {
            items.Add(new
            {
                sku = node.SelectSingleNode("Sku")?.InnerText,
                name = node.SelectSingleNode("Name")?.InnerText,
                quantity = node.SelectSingleNode("Quantity")?.InnerText
            });
        }

        return Ok(new { imported = items.Count });
    }

    [HttpGet("reports/{reportName}")]
    public IActionResult GetReport(string reportName, [FromQuery] string? period)
    {
        var basePath = _config["Storage:ReportsPath"] ?? "/var/ekart/reports";
        var reportPath = Path.Combine(basePath, "warehouse", reportName);

        if (!System.IO.File.Exists(reportPath))
            return NotFound(new { message = "Report not found" });

        var content = System.IO.File.ReadAllText(reportPath);
        return Content(content, "application/json");
    }

    [HttpGet("items/{itemId}/movements")]
    public IActionResult GetMovements(string itemId, [FromQuery] string? type)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT m.*, u.Username as OperatorName FROM InventoryMovements m " +
                    $"JOIN Users u ON m.OperatorId = u.Id " +
                    $"WHERE m.ItemId = {itemId}";

        if (!string.IsNullOrEmpty(type))
            query += $" AND m.MovementType = '{type}'";

        using var cmd = new SqlCommand(query, conn);
        var movements = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            movements.Add(new { id = reader["Id"], type = reader["MovementType"], quantity = reader["Quantity"] });
        }
        return Ok(movements);
    }

    [HttpPost("items/{itemId}/adjust")]
    public IActionResult AdjustQuantity(string itemId, [FromBody] dynamic body)
    {
        string reason = body.GetProperty("reason").GetString();
        int adjustment = body.GetProperty("adjustment").GetInt32();

        _logger.LogInformation($"Inventory adjustment by {Request.Headers["X-User-Id"]}: item {itemId}, reason: {reason}");

        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"UPDATE WarehouseItems SET Quantity = Quantity + {adjustment}, " +
                    $"LastAdjustmentReason = '{reason}' WHERE Id = {itemId}";
        using var cmd = new SqlCommand(query, conn);
        cmd.ExecuteNonQuery();

        return Ok(new { message = "Adjustment applied" });
    }
}
