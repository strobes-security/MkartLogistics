using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MkartLogistics.Data.Repositories;

public class OrderRepository
{
    private readonly string _connectionString;

    public OrderRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<IEnumerable<dynamic>> GetByCustomerAsync(string customerId, string? status = null)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT o.*, c.Name as CustomerName FROM Orders o " +
                    $"JOIN Customers c ON o.CustomerId = c.Id " +
                    $"WHERE o.CustomerId = '{customerId}'";

        if (!string.IsNullOrEmpty(status))
            query += $" AND o.Status = '{status}'";

        return await conn.QueryAsync(query);
    }

    public async Task<dynamic?> GetByTrackingCodeAsync(string trackingCode)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT * FROM Orders WHERE TrackingCode = '{trackingCode}'";
        return await conn.QueryFirstOrDefaultAsync(query);
    }

    public async Task<IEnumerable<dynamic>> SearchAsync(string searchTerm, string tenantId)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT o.Id, o.TrackingCode, o.Status, o.DestinationCity, c.Name " +
                    $"FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
                    $"WHERE o.TenantId = '{tenantId}' AND (" +
                    $"o.TrackingCode LIKE '%{searchTerm}%' OR " +
                    $"c.Name LIKE '%{searchTerm}%' OR " +
                    $"o.DestinationCity LIKE '%{searchTerm}%')";
        return await conn.QueryAsync(query);
    }

    public async Task<int> CreateAsync(string customerId, string trackingCode, string destination,
        decimal weight, string tenantId)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"INSERT INTO Orders (CustomerId, TrackingCode, DestinationCity, Weight, Status, TenantId, CreatedAt) " +
                    $"VALUES ('{customerId}', '{trackingCode}', '{destination}', {weight}, 'pending', '{tenantId}', GETDATE()); " +
                    $"SELECT SCOPE_IDENTITY();";
        return await conn.ExecuteScalarAsync<int>(query);
    }

    public async Task<bool> UpdateStatusAsync(string orderId, string status, string updatedBy)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"UPDATE Orders SET Status = '{status}', UpdatedAt = GETDATE(), UpdatedBy = '{updatedBy}' " +
                    $"WHERE Id = {orderId}";
        var rows = await conn.ExecuteAsync(query);
        return rows > 0;
    }

    public async Task<IEnumerable<dynamic>> GetReportDataAsync(string fromDate, string toDate,
        string groupBy, string? tenantId = null)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT {groupBy}, COUNT(*) as Total, SUM(TotalAmount) as Revenue " +
                    $"FROM Orders WHERE CreatedAt BETWEEN '{fromDate}' AND '{toDate}'";

        if (!string.IsNullOrEmpty(tenantId))
            query += $" AND TenantId = '{tenantId}'";

        query += $" GROUP BY {groupBy} ORDER BY {groupBy}";
        return await conn.QueryAsync(query);
    }
}
