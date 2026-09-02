using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Api.Data;

namespace Api.Tests;

public class PoincoWebApplicationFactory : WebApplicationFactory<Program>
{
    // Secrets JWT épinglés pour les tests : ils ne signent que des tokens de test contre la
    // base PoincoTest locale, jamais un vrai réseau. Avant ça, la suite héritait silencieusement
    // des user-secrets du projet Api (ou du placeholder d'appsettings.json en leur absence) —
    // suite non hermétique, rouge/verte selon la machine du dev. UseSetting est le seul hook
    // visible par builder.Configuration AVANT builder.Build(), donc avant la garde de secrets
    // dans Program.cs : WebApplicationFactory le convertit en argument de ligne de commande
    // passé à Main, qui l'emporte sur les user-secrets et les variables d'environnement. Ça ne
    // marche que parce que Program.cs appelle WebApplication.CreateBuilder(args) — ne pas
    // retirer args là-bas, sinon ces valeurs deviennent invisibles et l'hôte de test bascule en
    // Production.
    public const string TestAccessSecret = "test-only-access-secret-not-a-real-secret-0001";
    public const string TestRefreshSecret = "test-only-refresh-secret-not-a-real-secret-0002";

    // Sous TestServer, HttpContext.Connection.RemoteIpAddress est null : tous les tests d'une
    // classe tombent donc dans la même partition du rate limiter et la suite se limiterait
    // elle-même (AuthTests fait à lui seul une dizaine de signups/logins). On desserre le quota
    // ici ; RateLimitingTests le resserre au contraire pour vérifier que la politique rejette.
    public const int TestAuthPermitLimit = 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:AccessSecret", TestAccessSecret);
        builder.UseSetting("Jwt:RefreshSecret", TestRefreshSecret);
        builder.UseSetting("RateLimiting:AuthPermitLimit", TestAuthPermitLimit.ToString());

        // Charge les User Secrets propres à Api.Tests (pas ceux de Api) — voir UserSecretsId
        // dans Api.Tests.csproj. Rien de sensible ne vit dans le code source.
        var config = new ConfigurationBuilder()
            .AddUserSecrets<PoincoWebApplicationFactory>()
            .Build();

        var connectionString = config.GetConnectionString("PoincoTest")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:PoincoTest manquant. Lancez : dotnet user-secrets set \"ConnectionStrings:PoincoTest\" \"...\" depuis Api.Tests/");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PoincoDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<PoincoDbContext>(options =>
                options.UseSqlServer(connectionString));
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PoincoDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    // Accès direct au DbContext pour les scénarios qu'aucun endpoint ne permet d'atteindre —
    // typiquement le soft-delete (il n'existe pas de DELETE dans l'API aujourd'hui), dont les
    // tests de réutilisation de PIN/email ont besoin comme point de départ.
    public async Task WithDbAsync(Func<PoincoDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PoincoDbContext>();
        await action(db);
    }
}

