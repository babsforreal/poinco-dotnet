using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

    // BCrypt tronque silencieusement toute entrée à 72 octets — inadapté pour hasher un
    // refresh token (un JWT), qui dépasse largement cette taille et partage un long préfixe
    // commun (header + claims sub/email/companyId) avec tout autre token émis pour le même
    // admin. Résultat avec BCrypt : deux tokens différents peuvent produire le même hash,
    // ce qui casse la protection anti-rejeu de la rotation. BCrypt est de toute façon conçu
    // pour des mots de passe humains à faible entropie, pas pour des tokens déjà aléatoires
    // — un simple SHA-256 en comparaison à temps constant est le bon outil ici.
    public static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    public static bool VerifyRefreshToken(string refreshToken, string? storedHash)
    {
        if (storedHash is null) return false;
        var actualHash = HashRefreshToken(refreshToken);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHash), Encoding.UTF8.GetBytes(storedHash));
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
            // jti garantit que deux tokens émis pour le même admin dans la même seconde
            // (iat/exp n'ont qu'une précision à la seconde) ne sont jamais identiques —
            // sinon la rotation de refresh token ne protège de rien contre le replay.
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.Add(lifetime), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}