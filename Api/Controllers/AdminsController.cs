using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Data;
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

    private string CompanyId => User.FindFirst("companyId")!.Value;

    [HttpGet]
    public async Task<IEnumerable<AdminResponse>> GetAll()
    {
        return await _db.Admins
            .Where(a => a.CompanyId == CompanyId)
            .Select(a => new AdminResponse(a.Id, a.CompanyId, a.Email))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<AdminResponse>> Create(CreateAdminRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        var admin = new Admin
        {
            CompanyId = CompanyId,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        var response = new AdminResponse(admin.Id, admin.CompanyId, admin.Email);
        return CreatedAtAction(nameof(GetAll), new { id = admin.Id }, response);
    }
}