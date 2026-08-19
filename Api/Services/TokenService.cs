using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Api.Models;

namespace Api.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateAccessToken(Admin admin) =>
        GenerateToken(admin, _config["Jwt:AccessSecret"]!, TimeSpan.FromMinutes(15));

    public string GenerateRefreshToken(Admin admin) =>
        GenerateToken(admin, _config["Jwt:RefreshSecret"]!, TimeSpan.FromDays(7));

    private static string GenerateToken(Admin admin, string secret, TimeSpan lifetime)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id),
            new Claim("email", admin.Email),
            new Claim("companyId", admin.CompanyId),
            new Claim("kind", "admin"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.Add(lifetime), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}