using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Api.Data;
using Api.Models;

namespace Api.Controllers;

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record TokensResponse(string AccessToken, string RefreshToken);

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly PoincoDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(PoincoDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokensResponse>> Login(LoginRequest request)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Email == request.Email);
        if (admin is null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
            return Unauthorized();

        return await IssueTokens(admin);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokensResponse>> Refresh(RefreshRequest request)
    {
        var principal = ValidateRefreshToken(request.RefreshToken);
        if (principal is null)
            return Unauthorized();

        var adminId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var admin = await _db.Admins.FindAsync(adminId);

        if (admin is null || admin.RefreshTokenHash is null ||
            !BCrypt.Net.BCrypt.Verify(request.RefreshToken, admin.RefreshTokenHash))
            return Unauthorized();

        return await IssueTokens(admin);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var adminId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var admin = await _db.Admins.FindAsync(adminId);
        if (admin is not null)
        {
            admin.RefreshTokenHash = null;
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    private async Task<TokensResponse> IssueTokens(Admin admin)
    {
        var accessToken = GenerateToken(admin, _config["Jwt:AccessSecret"]!, TimeSpan.FromMinutes(15));
        var refreshToken = GenerateToken(admin, _config["Jwt:RefreshSecret"]!, TimeSpan.FromDays(7));

        admin.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);
        await _db.SaveChangesAsync();

        return new TokensResponse(accessToken, refreshToken);
    }

    private ClaimsPrincipal? ValidateRefreshToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:RefreshSecret"]!));

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
            }, out _);
        }
        catch
        {
            return null;
        }
    }

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
