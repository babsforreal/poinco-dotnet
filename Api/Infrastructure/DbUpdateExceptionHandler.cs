using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure;

// Convertit une violation de contrainte unique (slug, email, PIN déjà pris...) en
// 409 exploitable, plutôt que de laisser fuiter le 500 SQL Server brut par défaut.
public class DbUpdateExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException) return false;

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflit de données",
            Detail = "Cette valeur est déjà utilisée."
        }, cancellationToken);
        return true;
    }
}
