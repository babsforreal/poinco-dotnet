using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Api.Infrastructure;
using Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    const string placeholderPrefix = "REPLACE_VIA_"; // doit matcher appsettings.json
    const int minSecretLength = 32; // 256 bits, minimum pour signer en HMAC-SHA256

    var accessSecret = builder.Configuration["Jwt:AccessSecret"];
    var refreshSecret = builder.Configuration["Jwt:RefreshSecret"];

    bool IsWeak(string? s) =>
        string.IsNullOrWhiteSpace(s) || s.StartsWith(placeholderPrefix) || s.Length < minSecretLength;

    if (IsWeak(accessSecret) || IsWeak(refreshSecret))
        throw new InvalidOperationException(
            "Jwt:AccessSecret et Jwt:RefreshSecret doivent être définis en production (variables " +
            "d'environnement Jwt__AccessSecret / Jwt__RefreshSecret ou un gestionnaire de secrets), " +
            "et faire au moins 32 caractères. Les placeholders d'appsettings.json ne sont pas utilisables telles quelles.");

    if (accessSecret == refreshSecret)
        throw new InvalidOperationException("Jwt:AccessSecret et Jwt:RefreshSecret doivent être différents.");
}

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:AccessSecret"]!)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
        };
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

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
