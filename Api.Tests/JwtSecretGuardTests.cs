using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

// Verrouille la garde de secrets JWT de Program.cs : elle doit refuser de démarrer sur un
// secret manquant, placeholder, trop court ou identique entre access/refresh, dans TOUS les
// environnements — c'est justement ce qui a changé (avant, seul "Production" était protégé).
//
// Aucun test ici ne touche la base de données. La garde s'exécute avant AddDbContext dans
// Program.cs, donc un WebApplicationFactory<Program> nu ne construit jamais de DbContext pour
// les scénarios qui doivent lever une exception. Les deux scénarios qui construisent l'hôte en
// entier (secret valide à la limite, secrets épinglés du factory de test) passent par
// PoincoWebApplicationFactory pour ne jamais résoudre la vraie chaîne de connexion de dev.
public class JwtSecretGuardTests
{
    // Recopiés en dur : appsettings.json ne contient plus ces placeholders (c'est le but de ce
    // changement), mais le test doit continuer à couvrir "quelqu'un recolle cette valeur depuis
    // git ou la documentation".
    private const string PlaceholderAccessSecret = "REPLACE_VIA_USER_SECRETS_OR_ENV_VAR__Jwt__AccessSecret";
    private const string PlaceholderRefreshSecret = "REPLACE_VIA_USER_SECRETS_OR_ENV_VAR__Jwt__RefreshSecret";

    private const string ValidAccessSecret = "guard-tests-valid-access-secret-aaaaaaaaaaaa";
    private const string ValidRefreshSecret = "guard-tests-valid-refresh-secret-bbbbbbbbbbbb";

    // UseSetting (via ConfigureWebHost) est le seul hook visible par builder.Configuration avant
    // builder.Build(), donc avant la garde — voir le commentaire dans PoincoWebApplicationFactory.
    // On l'utilise ici pour piloter explicitement les deux secrets (et, pour un cas, le nom de
    // l'environnement) plutôt que de compter sur l'absence de config : une machine de dev a
    // souvent déjà de vrais user-secrets Api, et un test de sécurité ne doit pas dépendre de
    // l'état ambiant de la machine qui l'exécute.
    private sealed class BadSecretsFactory(string? accessSecret, string? refreshSecret, string? environment = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (environment is not null)
                builder.UseEnvironment(environment);

            builder.UseSetting("Jwt:AccessSecret", accessSecret);
            builder.UseSetting("Jwt:RefreshSecret", refreshSecret);
        }
    }

    private static InvalidOperationException AssertRefusesToBuild(WebApplicationFactory<Program> factory)
    {
        using (factory)
        {
            return Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        }
    }

    [Fact]
    public void Host_WithMissingSecrets_RefusesToBuild()
    {
        var ex = AssertRefusesToBuild(new BadSecretsFactory("", ""));
        Assert.Contains("Jwt:AccessSecret", ex.Message);
    }

    [Fact]
    public void Host_WithWhitespaceOnlySecret_RefusesToBuild()
    {
        var ex = AssertRefusesToBuild(new BadSecretsFactory("   ", ValidRefreshSecret));
        Assert.Contains("Jwt:AccessSecret", ex.Message);
    }

    [Fact]
    public void Host_WithPlaceholderAccessSecret_RefusesToBuild()
    {
        var ex = AssertRefusesToBuild(new BadSecretsFactory(PlaceholderAccessSecret, ValidRefreshSecret));
        Assert.Contains("Jwt:AccessSecret", ex.Message);
    }

    [Fact]
    public void Host_WithPlaceholderRefreshSecret_RefusesToBuild()
    {
        var ex = AssertRefusesToBuild(new BadSecretsFactory(ValidAccessSecret, PlaceholderRefreshSecret));
        Assert.Contains("Jwt:RefreshSecret", ex.Message);
    }

    [Fact]
    public void Host_WithSecretJustBelowMinimumLength_RefusesToBuild()
    {
        var tooShort = new string('a', 31); // juste sous le minimum de 32 caractères (256 bits)
        var ex = AssertRefusesToBuild(new BadSecretsFactory(tooShort, ValidRefreshSecret));
        Assert.Contains("Jwt:AccessSecret", ex.Message);
    }

    [Fact]
    public void Host_WithIdenticalAccessAndRefreshSecrets_RefusesToBuild()
    {
        var ex = AssertRefusesToBuild(new BadSecretsFactory(ValidAccessSecret, ValidAccessSecret));
        Assert.Contains("différents", ex.Message);
    }

    // La garde doit s'appliquer quel que soit le nom de l'environnement — c'est exactement le
    // bug corrigé : avant, seule une comparaison exacte à "Production" (insensible à la casse)
    // déclenchait la vérification, donc "Staging", "Prod" ou même "Development" démarraient sur
    // le placeholder committé. "Development" est le cas qui reproduit le bug d'origine.
    [Theory]
    [InlineData("Staging")]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Host_WithPlaceholderSecret_RefusesToBuild_RegardlessOfEnvironmentName(string environmentName)
    {
        var ex = AssertRefusesToBuild(new BadSecretsFactory(PlaceholderAccessSecret, ValidRefreshSecret, environmentName));
        Assert.Contains("Jwt:AccessSecret", ex.Message);
    }

    [Fact]
    public void Host_ErrorMessage_DoesNotContainTheOffendingSecretValue()
    {
        var ex = AssertRefusesToBuild(new BadSecretsFactory(PlaceholderAccessSecret, ValidRefreshSecret));

        Assert.DoesNotContain(PlaceholderAccessSecret, ex.Message);
    }

    // Cas limite positif : exactement 32 caractères doit être ACCEPTÉ (pas juste "tout ce qui
    // est court est rejeté" — la limite elle-même doit passer). Utilise PoincoWebApplicationFactory
    // pour que la construction complète de l'hôte pointe vers la base de test, jamais une base
    // de dev réelle.
    private sealed class ValidBoundarySecretsFactory : PoincoWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder); // épingle la connexion de test + les secrets par défaut
            builder.UseSetting("Jwt:AccessSecret", new string('a', 32));
        }
    }

    [Fact]
    public void Host_WithSecretAtMinimumLength_Builds()
    {
        using var factory = new ValidBoundarySecretsFactory();

        using var client = factory.CreateClient();

        var config = factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal(new string('a', 32), config["Jwt:AccessSecret"]);
    }

    // Vérifie que la valeur EFFECTIVEMENT vue par l'app est bien celle épinglée dans
    // PoincoWebApplicationFactory, pas seulement que l'hôte a démarré sans lever d'exception.
    // Sans ce test, une régression qui casse silencieusement l'injection (par ex. UseSetting
    // remplacé par ConfigureAppConfiguration, qui s'exécute trop tard) resterait invisible sur
    // une machine de dev qui a par ailleurs de vrais user-secrets Api valides : la suite
    // resterait verte, mais pour la mauvaise raison — et ce dépôt n'a pas de CI pour l'attraper
    // autrement.
    [Fact]
    public void TestFactory_ExposesThePinnedTestSecrets_NotTheDeveloperOrPlaceholderOnes()
    {
        using var factory = new PoincoWebApplicationFactory();
        var config = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal(PoincoWebApplicationFactory.TestAccessSecret, config["Jwt:AccessSecret"]);
        Assert.Equal(PoincoWebApplicationFactory.TestRefreshSecret, config["Jwt:RefreshSecret"]);
    }
}
