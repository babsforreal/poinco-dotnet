using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests;

public record AdminDto(string Id, string CompanyId, string Email);

public class AdminIsolationTests : IClassFixture<PoincoWebApplicationFactory>, IAsyncLifetime
{
    private readonly PoincoWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminIsolationTests(PoincoWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<SignupResponseDto> SignupAsync(string name, string slug, string email)
    {
        var res = await _client.PostAsJsonAsync("/companies", new
        {
            name,
            slug,
            adminEmail = email,
            adminPassword = "motdepasse123"
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SignupResponseDto>())!;
    }

    // Verrou de régression pour la faille corrigée précédemment : un admin ne doit
    // jamais pouvoir créer un compte admin pour une autre entreprise que la sienne,
    // même en essayant d'injecter un companyId différent dans le corps de la requête.
    [Fact]
    public async Task CreatedAdmin_AlwaysBelongsToCallerCompany_EvenIfAttackerInjectsAnotherCompanyId()
    {
        var companyA = await SignupAsync("Entreprise A", "entreprise-a", "admin@a.com");
        var companyB = await SignupAsync("Entreprise B", "entreprise-b", "admin@b.com");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", companyA.AccessToken);

        // companyId ne fait plus partie de CreateAdminRequest — on tente quand même de
        // l'injecter dans le JSON brut pour vérifier qu'il est bien ignoré côté serveur.
        var attack = await _client.PostAsJsonAsync("/admins", new
        {
            email = "intrus@a.com",
            password = "motdepasse123",
            companyId = companyB.Company.Id
        });

        attack.EnsureSuccessStatusCode();
        var created = await attack.Content.ReadFromJsonAsync<AdminDto>();

        Assert.NotNull(created);
        Assert.Equal(companyA.Company.Id, created!.CompanyId);
        Assert.NotEqual(companyB.Company.Id, created.CompanyId);
    }

    [Fact]
    public async Task AdminsListedByCompanyA_AreInvisibleToCompanyB()
    {
        var companyA = await SignupAsync("Entreprise A", "entreprise-a", "admin@a.com");
        var companyB = await SignupAsync("Entreprise B", "entreprise-b", "admin@b.com");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", companyA.AccessToken);
        var createRes = await _client.PostAsJsonAsync("/admins", new { email = "collegue@a.com", password = "motdepasse123" });
        createRes.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", companyB.AccessToken);
        var listAsB = await _client.GetAsync("/admins");
        listAsB.EnsureSuccessStatusCode();
        var adminsOfB = await listAsB.Content.ReadFromJsonAsync<List<AdminDto>>();

        Assert.DoesNotContain(adminsOfB!, a => a.Email == "collegue@a.com");
    }

    [Fact]
    public async Task CreateAdmin_WithoutToken_IsRejected()
    {
        var res = await _client.PostAsJsonAsync("/admins", new { email = "personne@x.com", password = "motdepasse123" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
