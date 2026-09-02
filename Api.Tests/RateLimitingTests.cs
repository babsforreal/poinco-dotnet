using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Api.Tests;

// Sans ce test, le rate limiting serait "configuré" sans preuve qu'il rejette quoi que ce soit :
// PoincoWebApplicationFactory desserre volontairement le quota pour que la suite ne se limite pas
// elle-même (RemoteIpAddress est null sous TestServer, donc tous les tests partagent une seule
// partition), ce qui rendrait une politique cassée parfaitement invisible partout ailleurs.
// On resserre donc ici, sur cette classe uniquement.
//
// Pas d'IClassFixture ici, contrairement aux autres classes de tests : l'état du limiteur vit
// dans l'hôte, et toutes les requêtes de la suite tombent dans la même partition ("unknown").
// Un fixture partagé ferait donc déborder le quota d'un test sur le suivant. xUnit instancie la
// classe une fois par test, donc chaque test obtient ici son propre hôte — et son propre compteur.
public class RateLimitingTests : IAsyncLifetime
{
    private const int PermitLimit = 3;

    private sealed class TightlyLimitedFactory : PoincoWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:AuthPermitLimit", PermitLimit.ToString());
        }
    }

    private readonly TightlyLimitedFactory _factory = new();
    private readonly HttpClient _client;

    public RateLimitingTests()
    {
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Login_BeyondThePermitLimit_IsRejectedWith429()
    {
        // Identifiants volontairement invalides : c'est le scénario bruteforce, et ça vérifie
        // au passage que le rejet vient bien du limiteur (429) et pas de l'auth (401).
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < PermitLimit + 2; i++)
        {
            var res = await _client.PostAsJsonAsync("/auth/login", new { email = "inconnu@x.com", password = "mauvais" });
            statuses.Add(res.StatusCode);
        }

        Assert.All(statuses.Take(PermitLimit), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        Assert.All(statuses.Skip(PermitLimit), s => Assert.Equal(HttpStatusCode.TooManyRequests, s));
    }

    // Les endpoints authentifiés ne portent pas la politique "auth" : un admin légitime qui liste
    // ses employés ne doit pas être coupé par le quota anti-bruteforce des endpoints publics.
    [Fact]
    public async Task AuthenticatedEndpoints_AreNotSubjectToTheAuthPolicy()
    {
        var signup = await _client.PostAsJsonAsync("/companies", new
        {
            name = "Entreprise A",
            slug = "entreprise-a",
            adminEmail = "admin@a.com",
            adminPassword = "motdepasse123"
        });
        signup.EnsureSuccessStatusCode();
        var tokens = await signup.Content.ReadFromJsonAsync<SignupResponseDto>();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        for (var i = 0; i < PermitLimit + 2; i++)
        {
            var res = await _client.GetAsync("/employees");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }
}
