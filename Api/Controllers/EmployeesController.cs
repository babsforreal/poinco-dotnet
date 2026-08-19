using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Models;

namespace Api.Controllers;

public record CreateEmployeeRequest(string Name, string Pin, string? EmployeeNumber, string? CardUid);

public record EmployeeResponse(string Id, string CompanyId, string Name, string? EmployeeNumber, string? CardUid, bool IsActive, PunchType? LastPunchType, DateTimeOffset? LastPunchedAt);

public class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Pin).Matches(@"^\d{4}$").WithMessage("Le PIN doit contenir 4 chiffres");
    }
}

[Authorize]
[ApiController]
[Route("[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly PoincoDbContext _db;
    private readonly IValidator<CreateEmployeeRequest> _validator;

    public EmployeesController(PoincoDbContext db, IValidator<CreateEmployeeRequest> validator)
    {
        _db = db;
        _validator = validator;
    }

    private string CompanyId => User.FindFirst("companyId")!.Value;

    [HttpGet]
    public async Task<IEnumerable<EmployeeResponse>> GetAll()
    {
        return await _db.Employees
            .Where(e => e.CompanyId == CompanyId)
            .Select(e => new EmployeeResponse(e.Id, e.CompanyId, e.Name, e.EmployeeNumber, e.CardUid, e.IsActive, e.LastPunchType, e.LastPunchedAt))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        var employee = new Employee
        {
            CompanyId = CompanyId,
            Name = request.Name,
            Pin = request.Pin,
            EmployeeNumber = request.EmployeeNumber,
            CardUid = request.CardUid
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        var response = new EmployeeResponse(employee.Id, employee.CompanyId, employee.Name, employee.EmployeeNumber, employee.CardUid, employee.IsActive, employee.LastPunchType, employee.LastPunchedAt);
        return CreatedAtAction(nameof(GetAll), new { id = employee.Id }, response);
    }
}