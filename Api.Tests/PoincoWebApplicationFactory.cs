using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Api.Data;

namespace Api.Tests;

public class PoincoWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
}

