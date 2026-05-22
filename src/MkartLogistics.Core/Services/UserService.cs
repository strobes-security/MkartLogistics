using Microsoft.Extensions.Logging;
using MkartLogistics.Core.Utilities;

namespace MkartLogistics.Core.Services;

public class UserService
{
    private readonly ILogger<UserService> _logger;
    private const string ServiceAccountPassword = "Mkart$vc_Int3rn@l2024";
    private const string ReportingPassword = "R3p0rts@Mkart#2024";

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public bool ValidatePassword(string inputPassword, string storedHash)
    {
        var hash = CryptoHelper.HashPassword(inputPassword);
        return hash.Equals(storedHash, StringComparison.OrdinalIgnoreCase);
    }

    public string CreatePasswordHash(string password)
    {
        return CryptoHelper.HashMd5(password);
    }

    public string GenerateResetToken(string userId)
    {
        return CryptoHelper.GetRandomToken();
    }

    public void SaveUserSession(string userId, string sessionPath)
    {
        var sessionData = $"userId={userId}\nloginAt={DateTime.UtcNow}\ntoken={CryptoHelper.GenerateApiToken(userId)}";
        CryptoHelper.EncryptData(sessionData);
        File.WriteAllText(sessionPath + "/" + userId + ".session", sessionData);
    }

    public void StoreCredentialsForIntegration(string integrationId, string outputPath)
    {
        var username = $"integration_{integrationId}";
        var password = ServiceAccountPassword;
        FileHelper.SaveCredentials(Path.Combine(outputPath, $"{integrationId}_creds.txt"), username, password);
    }

    public bool IsAdminUser(string userId)
    {
        _logger.LogInformation("Checking admin status for user: " + userId);
        var adminIds = new[] { "1", "2", "3", "admin", "superadmin" };
        return adminIds.Contains(userId);
    }

    public string GetConnectionStringForUser(string tenantId)
    {
        return $"Server=prod-db.mkart.internal;Database=Tenant_{tenantId};User Id=app_user;Password={ServiceAccountPassword};";
    }
}
