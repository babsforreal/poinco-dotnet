using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests;

public record PunchDto(string Id, string EmployeeId, string Type, DateTimeOffset PunchedAt);

public class PunchIsolationTests : IClassFixture<PoincoWebApplicationFactory>, IAsyncLifetime
{
    private readonly PoincoWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PunchIsolationTests(PoincoWebApplicationFactory factory)
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

    // Un admin de l'Entreprise B ne doit jamais pouvoir pointer pour un employé de
    // l'Entreprise A, même en connaissant son EmployeeId (ex: deviné ou fuité).
    [Fact]
    public async Task PunchForAnotherCompanysEmployee_IsRejected()
    {
        var companyA = await SignupAsync("Entreprise A", "entreprise-a", "admin@a.com");
        var companyB = await SignupAsync("Entreprise B", "entreprise-b", "admin@b.com");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", companyA.AccessToken);
        var createEmployee = await _client.PostAsJsonAsync("/employees", new { name = "Jean Tremblay", pin = "1234" });
        createEmployee.EnsureSuccessStatusCode();
        var employeeA = await createEmployee.Content.ReadFromJsonAsync<EmployeeDto>();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", companyB.AccessToken);
        var attack = await _client.PostAsJsonAsync("/punches", new { employeeId = employeeA!.Id, type = "In" });

        Assert.Equal(HttpStatusCode.NotFound, attack.StatusCode);
    }

    private async Task<PunchDto> CreateEmployeeAndPunchAsync(SignupResponseDto company, string name, string pin)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", company.AccessToken);

        var createEmployee = await _client.PostAsJsonAsync("/employees", new { name, pin });
        createEmployee.EnsureSuccessStatusCode();
        var employee = await createEmployee.Content.ReadFromJsonAsync<EmployeeDto>();

        var createPunch = await _client.PostAsJsonAsync("/punches", new { employeeId = employee!.Id, type = "In" });
        createPunch.EnsureSuccessStatusCode();
        return (await createPunch.Content.ReadFromJsonAsync<PunchDto>())!;
    }

    // L'Entreprise B a ses PROPRES punchs ici, et on vérifie qu'elle voit exactement les siens.
    // La version précédente se contentait d'un Assert.Empty côté B : un GET /punches cassé pour
    // tout le monde (ou qui renverrait systématiquement une liste vide) l'aurait aussi satisfait,
    // donc le test ne distinguait pas "isolation correcte" de "endpoint mort".
    [Fact]
    public async Task PunchesRecordedByCompanyA_AreInvisibleToCompanyB()
    {
        var companyA = await SignupAsync("Entreprise A", "entreprise-a", "admin@a.com");
        var companyB = await SignupAsync("Entreprise B", "entreprise-b", "admin@b.com");

        var punchOfA = await CreateEmployeeAndPunchAsync(companyA, "Jean Tremblay", "1234");
        var punchOfB = await CreateEmployeeAndPunchAsync(companyB, "Marie Gagnon", "5678");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", companyB.AccessToken);
        var listAsB = await _client.GetAsync("/punches");
        listAsB.EnsureSuccessStatusCode();
        var punchesOfB = await listAsB.Content.ReadFromJsonAsync<List<PunchDto>>();

        Assert.Equal(new[] { punchOfB.Id }, punchesOfB!.Select(p => p.Id).ToArray());
        Assert.DoesNotContain(punchesOfB!, p => p.Id == punchOfA.Id);

        // Et symétriquement : A voit les siens, pas ceux de B.
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", companyA.AccessToken);
        var listAsA = await _client.GetAsync("/punches");
        listAsA.EnsureSuccessStatusCode();
        var punchesOfA = await listAsA.Content.ReadFromJsonAsync<List<PunchDto>>();

        Assert.Equal(new[] { punchOfA.Id }, punchesOfA!.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task CreatePunch_WithoutToken_IsRejected()
    {
        var res = await _client.PostAsJsonAsync("/punches", new { employeeId = "whatever", type = "In" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
