using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

// Vérifie l'invariant central du soft-delete : une valeur unique libérée par un DeletedAt doit
// redevenir assignable. Le filtre applicatif (HasQueryFilter) la fait disparaître des requêtes,
// mais c'est l'index en base qui décide — s'il n'est pas filtré lui aussi, la base refuse une
// réutilisation que plus rien dans l'app n'explique. Les index Employee avaient déjà été corrigés ;
// IX_Admins_Email non, d'où AdminEmail_CanBeReused_AfterSoftDelete.
//
// Aucun endpoint DELETE n'existe aujourd'hui, donc le soft-delete est posé directement via le
// DbContext (WithDbAsync) — c'est le seul chemin disponible pour atteindre cet état.
public class SoftDeleteReuseTests : IClassFixture<PoincoWebApplicationFactory>, IAsyncLifetime
{
    private readonly PoincoWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SoftDeleteReuseTests(PoincoWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpResponseMessage> SignupRawAsync(string name, string slug, string email)
        => await _client.PostAsJsonAsync("/companies", new
        {
            name,
            slug,
            adminEmail = email,
            adminPassword = "motdepasse123"
        });

    private async Task<SignupResponseDto> SignupAsync(string name, string slug, string email)
    {
        var res = await SignupRawAsync(name, slug, email);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SignupResponseDto>())!;
    }

    private void AuthenticateAs(SignupResponseDto company)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", company.AccessToken);

    [Fact]
    public async Task EmployeePin_CanBeReused_AfterSoftDelete()
    {
        var company = await SignupAsync("Entreprise A", "entreprise-a", "admin@a.com");
        AuthenticateAs(company);

        var first = await _client.PostAsJsonAsync("/employees", new { name = "Jean Tremblay", pin = "1234" });
        first.EnsureSuccessStatusCode();
        var employee = await first.Content.ReadFromJsonAsync<EmployeeDto>();

        await _factory.WithDbAsync(async db =>
        {
            var row = await db.Employees.FirstAsync(e => e.Id == employee!.Id);
            row.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });

        var second = await _client.PostAsJsonAsync("/employees", new { name = "Marie Gagnon", pin = "1234" });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task AdminEmail_CanBeReused_AfterSoftDelete()
    {
        var company = await SignupAsync("Entreprise A", "entreprise-a", "admin@a.com");
        AuthenticateAs(company);

        var first = await _client.PostAsJsonAsync("/admins", new { email = "collegue@a.com", password = "motdepasse123" });
        first.EnsureSuccessStatusCode();
        var admin = await first.Content.ReadFromJsonAsync<AdminDto>();

        await _factory.WithDbAsync(async db =>
        {
            var row = await db.Admins.FirstAsync(a => a.Id == admin!.Id);
            row.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });

        var second = await _client.PostAsJsonAsync("/admins", new { email = "collegue@a.com", password = "motdepasse123" });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    // L'unicité du PIN est scopée par entreprise (index composite CompanyId+Pin) : deux
    // entreprises peuvent parfaitement avoir chacune un employé "1234", et c'est justement ce
    // qu'un index mal scopé casserait sans qu'aucun test d'isolation "liste vide" ne le voie.
    [Fact]
    public async Task SamePin_InTwoDifferentCompanies_IsAllowed()
    {
        var companyA = await SignupAsync("Entreprise A", "entreprise-a", "admin@a.com");
        var companyB = await SignupAsync("Entreprise B", "entreprise-b", "admin@b.com");

        AuthenticateAs(companyA);
        var forA = await _client.PostAsJsonAsync("/employees", new { name = "Jean Tremblay", pin = "1234" });

        AuthenticateAs(companyB);
        var forB = await _client.PostAsJsonAsync("/employees", new { name = "Marie Gagnon", pin = "1234" });

        Assert.Equal(HttpStatusCode.Created, forA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, forB.StatusCode);
    }

    [Fact]
    public async Task SamePin_TwiceInTheSameCompany_IsRejectedWithConflict()
    {
        var company = await SignupAsync("Entreprise A", "entreprise-a", "admin@a.com");
        AuthenticateAs(company);

        var first = await _client.PostAsJsonAsync("/employees", new { name = "Jean Tremblay", pin = "1234" });
        first.EnsureSuccessStatusCode();

        var duplicate = await _client.PostAsJsonAsync("/employees", new { name = "Marie Gagnon", pin = "1234" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    // Verrouille l'unicité GLOBALE d'Admin.Email : c'est une précondition de AuthController.Login,
    // qui résout l'admin par email seul, sans tenant dans la requête. Si quelqu'un scopait l'index
    // par CompanyId, ce test passerait au vert et le login deviendrait ambigu entre entreprises.
    [Fact]
    public async Task SecondSignup_WithAlreadyUsedAdminEmail_IsRejectedWithConflict()
    {
        await SignupAsync("Entreprise A", "entreprise-a", "admin@a.com");

        var second = await SignupRawAsync("Entreprise B", "entreprise-b", "admin@a.com");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
