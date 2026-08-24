using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Extensions;
using Api.Models;

namespace Api.Controllers;

public record CreateAdminRequest(string Email, string Password);
public record AdminResponse(string Id, string CompanyId, string Email);

public class CreateAdminRequestValidator : AbstractValidator<CreateAdminRequest>
{
    public CreateAdminRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).MinimumLength(8).WithMessage("Le mot de passe doit contenir au moins 8 caractères");
    }
}

[Authorize]
[ApiController]
[Route("[controller]")]
public class AdminsController : ControllerBase
{
    private readonly PoincoDbContext _db;
    private readonly IValidator<CreateAdminRequest> _validator;

    public AdminsController(PoincoDbContext db, IValidator<CreateAdminRequest> validator)
    {
        _db = db;
        _validator = validator;
    }

    private string CompanyId => User.GetCompanyId();

    [HttpGet]
    public async Task<IEnumerable<AdminResponse>> GetAll(int page = 1, int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(page, 1);

        return await _db.Admins
            .Where(a => a.CompanyId == CompanyId)
            .OrderBy(a => a.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdminResponse(a.Id, a.CompanyId, a.Email))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<AdminResponse>> Create(CreateAdminRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (validation.ToProblem(this) is { } problem) return problem;

        var admin = new Admin
        {
            CompanyId = CompanyId,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        var response = new AdminResponse(admin.Id, admin.CompanyId, admin.Email);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}