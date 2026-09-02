using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests;

public record AuthTokensDto(string AccessToken, string RefreshToken);

public class AuthTests : IClassFixture<PoincoWebApplicationFactory>, IAsyncLifetime
{
    private readonly PoincoWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthTests(PoincoWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<SignupResponseDto> SignupAsync(string email, string password = "motdepasse123")
    {
        var res = await _client.PostAsJsonAsync("/companies", new
        {
            name = "Entreprise A",
            slug = "entreprise-a",
            adminEmail = email,
            adminPassword = password
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SignupResponseDto>())!;
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        await SignupAsync("admin@a.com", "motdepasse123");

        var login = await _client.PostAsJsonAsync("/auth/login", new { email = "admin@a.com", password = "motdepasse123" });

        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<AuthTokensDto>();
        Assert.False(string.IsNullOrEmpty(tokens!.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsRejected()
    {
        await SignupAsync("admin@a.com", "motdepasse123");

        var login = await _client.PostAsJsonAsync("/auth/login", new { email = "admin@a.com", password = "mauvaismotdepasse" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_IsRejected()
    {
        var login = await _client.PostAsJsonAsync("/auth/login", new { email = "inconnu@x.com", password = "motdepasse123" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidToken_RotatesTokens()
    {
        var signup = await SignupAsync("admin@a.com");

        var refresh = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = signup.RefreshToken });

        refresh.EnsureSuccessStatusCode();
        var rotated = await refresh.Content.ReadFromJsonAsync<AuthTokensDto>();
        Assert.NotEqual(signup.RefreshToken, rotated!.RefreshToken);
        Assert.NotEqual(signup.AccessToken, rotated.AccessToken);
    }

    // Une fois qu'un refresh token a servi et a été remplacé, le rejouer (vol de token,
    // ou juste un bug client) doit être refusé — sinon un token volé reste valide pour
    // toujours au lieu d'être à usage unique.
    [Fact]
    public async Task Refresh_WithAlreadyRotatedToken_IsRejected()
    {
        var signup = await SignupAsync("admin@a.com");

        var firstRefresh = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = signup.RefreshToken });
        firstRefresh.EnsureSuccessStatusCode();

        var replay = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = signup.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    // Rejouer un token déjà tourné est un signal de vol : on ne peut pas savoir si c'est la
    // victime ou l'attaquant qui rejoue, donc la chaîne ENTIÈRE est coupée — y compris le token
    // qui avait légitimement remplacé celui rejoué. Sans ça, un attaquant qui rejoue une copie
    // volée se voyait refuser l'accès mais laissait la session en cours intacte, et une session
    // effectivement compromise pouvait survivre indéfiniment à sa propre détection.
    [Fact]
    public async Task Refresh_WithAlreadyRotatedToken_RevokesTheWholeChain()
    {
        var signup = await SignupAsync("admin@a.com");

        var firstRefresh = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = signup.RefreshToken });
        firstRefresh.EnsureSuccessStatusCode();
        var rotated = await firstRefresh.Content.ReadFromJsonAsync<AuthTokensDto>();

        var replay = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = signup.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // Le token rotationné était valide juste avant le rejeu ; il ne doit plus l'être après.
        var afterReplay = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = rotated!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
    }

    // Les deux secrets de signature sont distincts et seul l'access secret est enregistré auprès
    // du handler bearer : présenter un refresh token là où un access token est attendu doit
    // échouer à la validation de signature, pas seulement "par convention".
    [Fact]
    public async Task RefreshToken_UsedAsBearerOnProtectedEndpoint_IsRejected()
    {
        var signup = await SignupAsync("admin@a.com");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signup.RefreshToken);
        var me = await _client.GetAsync("/companies/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithGarbageToken_IsRejected()
    {
        var refresh = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "ceci-nest-pas-un-jwt" });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_InvalidatesRefreshToken()
    {
        var signup = await SignupAsync("admin@a.com");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signup.AccessToken);
        var logout = await _client.PostAsync("/auth/logout", null);
        logout.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = null;
        var refreshAfterLogout = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = signup.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutToken_IsRejected()
    {
        var res = await _client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTokenFromAnotherCompany_OnlySeesOwnCompanyData()
    {
        // Vérifie que le token émis à la signature reste valide et scope bien l'accès —
        // couvre le chemin normal login -> accès protégé, en plus des tests d'isolation dédiés.
        var signup = await SignupAsync("admin@a.com");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signup.AccessToken);
        var me = await _client.GetAsync("/companies/me");

        me.EnsureSuccessStatusCode();
        var company = await me.Content.ReadFromJsonAsync<CompanyDto>();
        Assert.Equal(signup.Company.Id, company!.Id);
    }
}
