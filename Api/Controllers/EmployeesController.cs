using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Extensions;
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

    private string CompanyId => User.GetCompanyId();

    [HttpGet]
    public async Task<IEnumerable<EmployeeResponse>> GetAll(int page = 1, int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(page, 1);

        return await _db.Employees
            .Where(e => e.CompanyId == CompanyId)
            .OrderBy(e => e.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmployeeResponse(e.Id, e.CompanyId, e.Name, e.EmployeeNumber, e.CardUid, e.IsActive, e.LastPunchType, e.LastPunchedAt))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (validation.ToProblem(this) is { } problem) return problem;

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
        return StatusCode(StatusCodes.Status201Created, response);
    }
}