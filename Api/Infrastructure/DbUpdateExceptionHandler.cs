using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure;

// Convertit une violation de contrainte unique (slug, email, PIN déjà pris...) en
// 409 exploitable, plutôt que de laisser fuiter le 500 SQL Server brut par défaut.
public class DbUpdateExceptionHandler : IExceptionHandler
{
    // 2627 = violation de contrainte UNIQUE/PRIMARY KEY, 2601 = violation d'index unique.
    // Ce sont les DEUX seuls cas où "cette valeur est déjà utilisée" est une réponse vraie.
    // Avant, toute DbUpdateException devenait un 409 : un deadlock (1205), un timeout, une
    // violation de clé étrangère (547) étaient annoncés au client comme un conflit de saisie,
    // et disparaissaient des logs d'erreur puisqu'on répondait "normalement". Tout le reste
    // repart donc au framework (return false) -> 500 + log standard.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private readonly ILogger<DbUpdateExceptionHandler> _logger;

    public DbUpdateExceptionHandler(ILogger<DbUpdateExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException) return false;

        if (exception.InnerException is not SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sqlException)
        {
            _logger.LogError(
                exception,
                "DbUpdateException non liée à une contrainte unique sur {Method} {Path} — laissée au gestionnaire par défaut (500).",
                httpContext.Request.Method,
                httpContext.Request.Path);
            return false;
        }

        // On logue le numéro d'erreur, pas le message SQL : celui-ci contient la valeur dupliquée
        // (email d'admin, PIN d'employé) et n'a rien à faire dans les logs applicatifs.
        _logger.LogWarning(
            "Violation de contrainte unique (SQL {Number}) sur {Method} {Path} — réponse 409.",
            sqlException.Number,
            httpContext.Request.Method,
            httpContext.Request.Path);

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
