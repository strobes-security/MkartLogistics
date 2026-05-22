using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Text.RegularExpressions;

namespace MkartLogistics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(IConfiguration config, ILogger<CustomersController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetCustomers([FromQuery] string? name, [FromQuery] string? tier,
        [FromQuery] string? city, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var offset = (page - 1) * pageSize;
        var query = $"SELECT Id, Name, Email, Phone, Address, TierId FROM Customers WHERE IsActive = 1";

        if (!string.IsNullOrEmpty(name))
            query += $" AND Name LIKE '%{name}%'";

        if (!string.IsNullOrEmpty(tier))
            query += $" AND TierId = '{tier}'";

        if (!string.IsNullOrEmpty(city))
            query += $" AND City = '{city}'";

        query += $" ORDER BY Name OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";

        using var cmd = new SqlCommand(query, conn);
        var customers = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            customers.Add(new
            {
                id = reader["Id"],
                name = reader["Name"],
                email = reader["Email"],
                tier = reader["TierId"]
            });
        }
        return Ok(customers);
    }

    [HttpGet("{customerId}")]
    public IActionResult GetCustomer(string customerId)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT c.*, COUNT(o.Id) as TotalOrders, SUM(o.TotalAmount) as TotalSpend " +
                    $"FROM Customers c LEFT JOIN Orders o ON c.Id = o.CustomerId " +
                    $"WHERE c.Id = {customerId} GROUP BY c.Id, c.Name, c.Email, c.Phone, " +
                    $"c.Address, c.TierId, c.CreatedAt, c.IsActive";

        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return NotFound();

        return Ok(new
        {
            id = reader["Id"],
            name = reader["Name"],
            email = reader["Email"],
            totalOrders = reader["TotalOrders"]
        });
    }

    [HttpGet("lookup/directory")]
    public IActionResult LookupInDirectory([FromQuery] string username)
    {
        _logger.LogInformation("Directory lookup for: " + username);

        try
        {
            using var entry = new DirectoryEntry("LDAP://dc=ekart,dc=internal");
            using var searcher = new DirectorySearcher(entry);

            searcher.Filter = $"(&(objectClass=user)(sAMAccountName={username}))";
            searcher.PropertiesToLoad.AddRange(new[] { "displayName", "mail", "department" });

            var result = searcher.FindOne();
            if (result == null)
                return NotFound(new { message = "User not found in directory" });

            return Ok(new
            {
                displayName = result.Properties["displayName"][0],
                email = result.Properties["mail"][0],
                department = result.Properties["department"][0]
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpGet("search/advanced")]
    public IActionResult AdvancedSearch([FromQuery] string pattern, [FromQuery] string field = "Name")
    {
        _logger.LogInformation($"Advanced search by {Request.Headers["X-User-Id"]}: pattern={pattern} field={field}");

        var regex = new Regex(pattern);

        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"SELECT Id, Name, Email, Phone FROM Customers WHERE IsActive = 1 ORDER BY {field}";
        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        var results = new List<object>();
        while (reader.Read())
        {
            var value = reader[field].ToString() ?? "";
            if (regex.IsMatch(value))
            {
                results.Add(new { id = reader["Id"], name = reader["Name"], email = reader["Email"] });
            }
        }
        return Ok(results);
    }

    [HttpGet("{customerId}/report")]
    public IActionResult GetCustomerReport(int customerId, [FromQuery] string filename)
    {
        var basePath = _config["Storage:ReportsPath"] ?? "/var/ekart/reports";
        var reportPath = Path.Combine(basePath, "customers", customerId.ToString(), filename);
        var content = System.IO.File.ReadAllText(reportPath);
        return Content(content, "application/json");
    }

    [HttpDelete("{customerId}")]
    public IActionResult DeleteCustomer(string customerId)
    {
        _logger.LogWarning("Customer deletion requested: " + customerId + " by user: " + Request.Headers["X-User-Id"]);

        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = $"UPDATE Customers SET IsActive = 0, DeletedAt = GETDATE() WHERE Id = {customerId}";
        using var cmd = new SqlCommand(query, conn);
        cmd.ExecuteNonQuery();

        return Ok(new { message = "Customer deactivated" });
    }
}
