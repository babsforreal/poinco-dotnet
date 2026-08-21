using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    var accessSecret = builder.Configuration["Jwt:AccessSecret"];
    var refreshSecret = builder.Configuration["Jwt:RefreshSecret"];
    if (string.IsNullOrWhiteSpace(accessSecret) || accessSecret.StartsWith("REPLACE_VIA_") ||
        string.IsNullOrWhiteSpace(refreshSecret) || refreshSecret.StartsWith("REPLACE_VIA_"))
    {
        throw new InvalidOperationException(
            "Jwt:AccessSecret et Jwt:RefreshSecret doivent être définis en production (variables " +
            "d'environnement Jwt__AccessSecret / Jwt__RefreshSecret ou un gestionnaire de secrets). " +
            "Les valeurs placeholder d'appsettings.json ne sont pas utilisables telles quelles.");
    }
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

var app = builder.Build();

// Configure the HTTP request pipeline.
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
