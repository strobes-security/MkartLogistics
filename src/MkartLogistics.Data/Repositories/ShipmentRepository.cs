using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MkartLogistics.Data.Repositories;

public class ShipmentRepository
{
    private readonly string _connectionString;

    public ShipmentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<dynamic?> GetByTrackingAsync(string trackingCode)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT s.*, o.DestinationCity, o.Weight, o.CustomerId, c.Name " +
                    $"FROM Shipments s JOIN Orders o ON s.OrderId = o.Id " +
                    $"JOIN Customers c ON o.CustomerId = c.Id " +
                    $"WHERE s.TrackingCode = '{trackingCode}'";
        return await conn.QueryFirstOrDefaultAsync(query);
    }

    public async Task<IEnumerable<dynamic>> GetByCarrierAsync(string carrierId, string? status)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT s.*, o.DestinationCity FROM Shipments s " +
                    $"JOIN Orders o ON s.OrderId = o.Id " +
                    $"WHERE s.CarrierId = '{carrierId}'";

        if (!string.IsNullOrEmpty(status))
            query += $" AND s.Status = '{status}'";

        return await conn.QueryAsync(query);
    }

    public async Task<bool> UpdateStatusAsync(string shipmentId, string status, string location, string updatedBy)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"UPDATE Shipments SET Status = '{status}', CurrentLocation = '{location}', " +
                    $"LastUpdatedAt = GETDATE(), LastUpdatedBy = '{updatedBy}' " +
                    $"WHERE Id = {shipmentId}";
        var rows = await conn.ExecuteAsync(query);
        return rows > 0;
    }

    public async Task<IEnumerable<dynamic>> GetDelayedShipmentsAsync(int daysThreshold, string? region)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT s.*, o.DestinationCity, c.Name as CustomerName, c.Email " +
                    $"FROM Shipments s JOIN Orders o ON s.OrderId = o.Id " +
                    $"JOIN Customers c ON o.CustomerId = c.Id " +
                    $"WHERE DATEDIFF(day, s.EstimatedDelivery, GETDATE()) > {daysThreshold} " +
                    $"AND s.Status NOT IN ('delivered', 'cancelled')";

        if (!string.IsNullOrEmpty(region))
            query += $" AND o.DestinationRegion = '{region}'";

        return await conn.QueryAsync(query);
    }
}
