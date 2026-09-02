using Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

// Le handler répondait 409 "Cette valeur est déjà utilisée" à TOUTE DbUpdateException : un
// deadlock, un timeout ou une violation de clé étrangère étaient présentés au client comme une
// erreur de saisie, et sortaient des logs d'erreur puisqu'on répondait "normalement". Ces tests
// verrouillent le fait que seules les vraies violations d'unicité (SQL 2601/2627) sont
// interceptées, tout le reste repartant au pipeline par défaut (500 + log).
//
// SqlException n'a pas de constructeur public, donc la branche 409 elle-même n'est pas
// atteignable unitairement — elle est couverte de bout en bout par SoftDeleteReuseTests
// (SamePin_TwiceInTheSameCompany / SecondSignup_WithAlreadyUsedAdminEmail).
public class DbUpdateExceptionHandlerTests
{
    private static DbUpdateExceptionHandler CreateHandler()
        => new(NullLogger<DbUpdateExceptionHandler>.Instance);

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/employees";
        return context;
    }

    [Fact]
    public async Task DbUpdateException_WithoutSqlInnerException_IsNotHandled()
    {
        var context = CreateContext();
        var exception = new DbUpdateException("échec de sauvegarde", new InvalidOperationException("pas du SQL"));

        var handled = await CreateHandler().TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
        Assert.NotEqual(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task DbUpdateException_WithNoInnerException_IsNotHandled()
    {
        var context = CreateContext();

        var handled = await CreateHandler().TryHandleAsync(context, new DbUpdateException(), CancellationToken.None);

        Assert.False(handled);
        Assert.NotEqual(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task ExceptionThatIsNotADbUpdateException_IsNotHandled()
    {
        var context = CreateContext();

        var handled = await CreateHandler().TryHandleAsync(context, new TimeoutException(), CancellationToken.None);

        Assert.False(handled);
        Assert.NotEqual(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }
}
