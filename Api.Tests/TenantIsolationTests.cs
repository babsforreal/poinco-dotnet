using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests;

public record CompanyDto(string Id, string Name, string Slug, string Timezone, int ClockOffsetMinutes);
public record SignupResponseDto(CompanyDto Company, string AccessToken, string RefreshToken);
public record EmployeeDto(string Id, string CompanyId, string Name);

public class TenantIsolationTests : IClassFixture<PoincoWebApplicationFactory>, IAsyncLifetime
{
    private readonly PoincoWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TenantIsolationTests(PoincoWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EmployeeCreatedByCompanyA_IsInvisibleToCompanyB()
    {
        var signupA = await _client.PostAsJsonAsync("/companies", new
        {
            name = "Entreprise A",
            slug = "entreprise-a",
            adminEmail = "admin@a.com",
            adminPassword = "motdepasse123"
        });
        signupA.EnsureSuccessStatusCode();
        var tokensA = await signupA.Content.ReadFromJsonAsync<SignupResponseDto>();

        var signupB = await _client.PostAsJsonAsync("/companies", new
        {
            name = "Entreprise B",
            slug = "entreprise-b",
            adminEmail = "admin@b.com",
            adminPassword = "motdepasse123"
        });
        signupB.EnsureSuccessStatusCode();
        var tokensB = await signupB.Content.ReadFromJsonAsync<SignupResponseDto>();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokensA!.AccessToken);
        var createEmployee = await _client.PostAsJsonAsync("/employees", new { name = "Jean Tremblay", pin = "1234" });
        createEmployee.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokensB!.AccessToken);
        var listAsB = await _client.GetAsync("/employees");
        listAsB.EnsureSuccessStatusCode();
        var employeesOfB = await listAsB.Content.ReadFromJsonAsync<List<EmployeeDto>>();

        Assert.Empty(employeesOfB!);
    }
}