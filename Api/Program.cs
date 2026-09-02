using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Api.Infrastructure;
using Api.Services;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Le nom de l'environnement n'est pas une frontière de sécurité : une clé de signature
// committée dans git reste forgeable par quiconque lit le dépôt, que l'app tourne en
// Staging, "Prod" (faute de frappe) ou Production. D'où une garde inconditionnelle, dans
// tous les environnements — appsettings.json ne définit plus de section Jwt du tout, donc
// une config manquante tombe directement dans IsWeak (IsNullOrWhiteSpace). Les tests
// injectent leurs propres secrets via UseSetting dans PoincoWebApplicationFactory (voir le
// commentaire là-bas) et satisfont donc cette garde comme n'importe quel environnement réel.
const string placeholderPrefix = "REPLACE_VIA_"; // valeur historique d'appsettings.json ; garde utile si quelqu'un la recolle depuis git ou la doc
const int minSecretLength = 32; // 256 bits, minimum pour signer en HMAC-SHA256

var accessSecret = builder.Configuration["Jwt:AccessSecret"];
var refreshSecret = builder.Configuration["Jwt:RefreshSecret"];

bool IsWeak(string? s) =>
    string.IsNullOrWhiteSpace(s) || s.StartsWith(placeholderPrefix, StringComparison.Ordinal) || s.Length < minSecretLength;

if (IsWeak(accessSecret) || IsWeak(refreshSecret))
    throw new InvalidOperationException(
        "Jwt:AccessSecret et Jwt:RefreshSecret doivent être définis (>= 32 caractères, sans être " +
        "un placeholder) dans TOUS les environnements — le nom de l'environnement n'est pas une " +
        "protection. En local : dotnet user-secrets set \"Jwt:AccessSecret\" \"<valeur>\" --project Api " +
        "(idem pour Jwt:RefreshSecret). En déploiement : variables d'environnement " +
        "Jwt__AccessSecret / Jwt__RefreshSecret. Pour générer une valeur : openssl rand -base64 48.");

if (accessSecret == refreshSecret)
    throw new InvalidOperationException("Jwt:AccessSecret et Jwt:RefreshSecret doivent être différents.");

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<Api.Data.PoincoDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Poinco")));

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accessSecret!)), // valeur déjà validée par la garde ci-dessus, pas une relecture de config
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
        };
    });

// Les endpoints non authentifiés (login, refresh, signup) sont les seuls qu'un inconnu peut
// marteler : bruteforce de mot de passe, énumération d'emails, création de comptes en masse.
// Fenêtre fixe partitionnée par IP, via le rate limiter natif d'ASP.NET Core (pas de package).
//
// Les bornes viennent de la config plutôt que d'être en dur : sous TestServer,
// RemoteIpAddress est null, donc toute la suite d'intégration tomberait dans une seule
// partition et se ferait limiter elle-même. PoincoWebApplicationFactory épingle un quota large,
// et RateLimitingTests un quota bas pour prouver que la politique rejette vraiment.
const string authRateLimitPolicy = "auth";
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
var authWindowMinutes = builder.Configuration.GetValue("RateLimiting:AuthWindowMinutes", 5);

builder.Services.AddRateLimiter(options =>
{
    // 503 par défaut, ce qui annoncerait à tort une panne serveur.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(authRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Clé de repli quand l'IP est inconnue (TestServer, socket Unix) : tout le monde
            // partage alors une partition — restrictif par défaut plutôt que non limité.
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(authWindowMinutes),
                QueueLimit = 0, // on rejette tout de suite, on ne fait pas patienter un bruteforce
            }));
});

builder.Services.AddScoped<TokenService>();
builder.Services.AddAuthorization();
builder.Services.AddExceptionHandler<DbUpdateExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Avant l'authentification : un bruteforce ne doit pas pouvoir consommer de cycles de
// vérification de mot de passe/JWT avant d'être refusé.
app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
