using System.Security.Claims;

namespace Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    // Centralise la lecture du tenant courant : le `!` sur FindFirst(...).Value,
    // recopié dans chaque controller, masquait silencieusement une claim absente
    // en NullReferenceException plutôt qu'un message explicite.
    public static string GetCompanyId(this ClaimsPrincipal user) =>
        user.FindFirst("companyId")?.Value
            ?? throw new InvalidOperationException("Le token ne contient pas de claim \"companyId\".");
}
