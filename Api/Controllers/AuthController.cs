using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Services;

namespace Api.Controllers;

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record TokensResponse(string AccessToken, string RefreshToken);

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    // Hash factice, calculé une fois au premier usage avec le facteur de coût par défaut de
    // BCrypt.Net — donc aussi lent à vérifier qu'un vrai hash d'admin. Voir Login.
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword("mot-de-passe-factice-jamais-valide");

    private readonly PoincoDbContext _db;
    private readonly TokenService _tokens;

    public AuthController(PoincoDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<TokensResponse>> Login(LoginRequest request)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Email == request.Email);

        if (admin is null)
        {
            // Sans ça, un email inconnu répondait immédiatement alors qu'un email connu payait
            // le coût d'un BCrypt.Verify : l'écart de latence suffit à énumérer les emails
            // d'admins valides, en dehors de tout compte compromis. On vérifie donc contre un
            // hash factice pour que les deux chemins coûtent la même chose.
            BCrypt.Net.BCrypt.Verify(request.Password, DummyPasswordHash);
            return Unauthorized();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
            return Unauthorized();

        return await IssueTokens(admin);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokensResponse>> Refresh(RefreshRequest request)
    {
        var principal = _tokens.ValidateRefreshToken(request.RefreshToken);
        if (principal is null)
            return Unauthorized();

        var adminId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (adminId is null)
            return Unauthorized();

        // FirstOrDefaultAsync et pas FindAsync : FindAsync court-circuite la requête quand
        // l'entité est déjà suivie et n'applique pas le global query filter, donc un admin
        // soft-deleted pouvait encore rafraîchir sa session.
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Id == adminId);
        if (admin is null)
            return Unauthorized();

        if (!TokenService.VerifyRefreshToken(request.RefreshToken, admin.RefreshTokenHash))
        {
            // Signature et expiration valides mais hash différent : ce token a déjà été tourné.
            // Le client légitime ne détient jamais que le dernier émis, donc ce rejeu signifie
            // qu'une copie circule. Impossible de savoir laquelle des deux parties est
            // l'imposteur, on coupe donc la chaîne active des deux côtés : mieux vaut une
            // déconnexion visible qu'une session volée qui survit à sa détection.
            admin.RefreshTokenHash = null;
            await _db.SaveChangesAsync();
            return Unauthorized();
        }

        return await IssueTokens(admin);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var adminId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var admin = adminId is null
            ? null
            : await _db.Admins.FirstOrDefaultAsync(a => a.Id == adminId);
        if (admin is not null)
        {
            admin.RefreshTokenHash = null;
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    private async Task<TokensResponse> IssueTokens(Models.Admin admin)
    {
        var accessToken = _tokens.GenerateAccessToken(admin);
        var refreshToken = _tokens.GenerateRefreshToken(admin);

        admin.RefreshTokenHash = TokenService.HashRefreshToken(refreshToken);
        await _db.SaveChangesAsync();

        return new TokensResponse(accessToken, refreshToken);
    }
}