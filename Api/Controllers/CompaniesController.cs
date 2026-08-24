using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Extensions;
using Api.Models;
using Api.Services;

namespace Api.Controllers;

public record CreateCompanyRequest(string Name, string Slug, string AdminEmail, string AdminPassword);
public record CompanyResponse(string Id, string Name, string Slug, string Timezone, int ClockOffsetMinutes);
public record SignupResponse(CompanyResponse Company, string AccessToken, string RefreshToken);

public class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Slug).NotEmpty().Matches("^[a-z0-9-]+$")
            .WithMessage("Le slug ne peut contenir que des lettres minuscules, chiffres et tirets");
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.AdminPassword).MinimumLength(8);
    }
}

[ApiController]
[Route("[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly PoincoDbContext _db;
    private readonly IValidator<CreateCompanyRequest> _validator;
    private readonly TokenService _tokens;

    public CompaniesController(PoincoDbContext db, IValidator<CreateCompanyRequest> validator, TokenService tokens)
    {
        _db = db;
        _validator = validator;
        _tokens = tokens;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CompanyResponse>> GetMine()
    {
        var companyId = User.GetCompanyId();
        var company = await _db.Companies
            .Where(c => c.Id == companyId)
            .Select(c => new CompanyResponse(c.Id, c.Name, c.Slug, c.Timezone, c.ClockOffsetMinutes))
            .FirstOrDefaultAsync();

        return company is null ? NotFound() : company;
    }

    [HttpPost]
    public async Task<ActionResult<SignupResponse>> Create(CreateCompanyRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (validation.ToProblem(this) is { } problem) return problem;

        var company = new Company { Name = request.Name, Slug = request.Slug };
        _db.Companies.Add(company);

        var admin = new Admin
        {
            CompanyId = company.Id,
            Email = request.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword)
        };
        _db.Admins.Add(admin);

        var accessToken = _tokens.GenerateAccessToken(admin);
        var refreshToken = _tokens.GenerateRefreshToken(admin);
        admin.RefreshTokenHash = TokenService.HashRefreshToken(refreshToken);

        await _db.SaveChangesAsync();

        var companyResponse = new CompanyResponse(company.Id, company.Name, company.Slug, company.Timezone, company.ClockOffsetMinutes);
        return StatusCode(StatusCodes.Status201Created, new SignupResponse(companyResponse, accessToken, refreshToken));
    }
}