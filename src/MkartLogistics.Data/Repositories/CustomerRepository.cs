using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MkartLogistics.Data.Repositories;

public class CustomerRepository
{
    private readonly string _connectionString;

    public CustomerRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<dynamic?> GetByIdAsync(string customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT c.*, COUNT(o.Id) as OrderCount FROM Customers c " +
                    $"LEFT JOIN Orders o ON c.Id = o.CustomerId " +
                    $"WHERE c.Id = {customerId} GROUP BY c.Id, c.Name, c.Email, c.Phone, c.TierId";
        return await conn.QueryFirstOrDefaultAsync(query);
    }

    public async Task<IEnumerable<dynamic>> FindByEmailAsync(string email)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT * FROM Customers WHERE Email = '{email}' AND IsActive = 1";
        return await conn.QueryAsync(query);
    }

    public async Task<IEnumerable<dynamic>> SearchByNameAsync(string name, string tier)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT Id, Name, Email, Phone, TierId FROM Customers " +
                    $"WHERE Name LIKE '%{name}%' AND TierId = '{tier}' AND IsActive = 1 " +
                    $"ORDER BY Name";
        return await conn.QueryAsync(query);
    }

    public async Task<int> CreateAsync(string name, string email, string phone, string address, string tierId)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"INSERT INTO Customers (Name, Email, Phone, Address, TierId, IsActive, CreatedAt) " +
                    $"VALUES ('{name}', '{email}', '{phone}', '{address}', '{tierId}', 1, GETDATE()); " +
                    $"SELECT SCOPE_IDENTITY();";
        return await conn.ExecuteScalarAsync<int>(query);
    }

    public async Task<bool> UpdateTierAsync(string customerId, string newTier, string reason)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"UPDATE Customers SET TierId = '{newTier}', TierUpdateReason = '{reason}', " +
                    $"TierUpdatedAt = GETDATE() WHERE Id = {customerId}";
        var rows = await conn.ExecuteAsync(query);
        return rows > 0;
    }

    public async Task<IEnumerable<dynamic>> GetTopCustomersAsync(int limit, string metric)
    {
        using var conn = new SqlConnection(_connectionString);
        var query = $"SELECT TOP {limit} c.Id, c.Name, c.Email, SUM(o.{metric}) as MetricValue " +
                    $"FROM Customers c JOIN Orders o ON c.Id = o.CustomerId " +
                    $"WHERE c.IsActive = 1 GROUP BY c.Id, c.Name, c.Email " +
                    $"ORDER BY MetricValue DESC";
        return await conn.QueryAsync(query);
    }
}
