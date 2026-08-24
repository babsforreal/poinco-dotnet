using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Extensions;
using Api.Models;

namespace Api.Controllers;

public record CreatePunchRequest(string EmployeeId, PunchType Type, DateTimeOffset? PunchedAt);

public record PunchResponse(string Id, string EmployeeId, PunchType Type, DateTimeOffset PunchedAt, DateTimeOffset CreatedAt);

public class CreatePunchRequestValidator : AbstractValidator<CreatePunchRequest>
{
    public CreatePunchRequestValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
    }
}

[Authorize]
[ApiController]
[Route("[controller]")]
public class PunchesController : ControllerBase
{
    private readonly PoincoDbContext _db;
    private readonly IValidator<CreatePunchRequest> _validator;

    public PunchesController(PoincoDbContext db, IValidator<CreatePunchRequest> validator)
    {
        _db = db;
        _validator = validator;
    }

    private string CompanyId => User.GetCompanyId();

    [HttpGet]
    public async Task<IEnumerable<PunchResponse>> GetAll(int page = 1, int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(page, 1);

        return await _db.Punches
            .Where(p => p.CompanyId == CompanyId)
            .OrderByDescending(p => p.PunchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PunchResponse(p.Id, p.EmployeeId, p.Type, p.PunchedAt, p.CreatedAt))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<PunchResponse>> Create(CreatePunchRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (validation.ToProblem(this) is { } problem) return problem;

        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == CompanyId);
        if (employee is null)
            return NotFound();

        var punchedAt = request.PunchedAt ?? DateTimeOffset.UtcNow;

        var punch = new Punch
        {
            CompanyId = CompanyId,
            EmployeeId = employee.Id,
            Type = request.Type,
            PunchedAt = punchedAt
        };

        _db.Punches.Add(punch);

        // Ne fait avancer le "dernier pointage" affiché sur la fiche employé que si
        // ce punch est le plus récent connu — sinon la correction d'un punch oublié
        // (PunchedAt dans le passé) ferait reculer ce statut dérivé.
        if (employee.LastPunchedAt is null || punchedAt >= employee.LastPunchedAt)
        {
            employee.LastPunchType = request.Type;
            employee.LastPunchedAt = punchedAt;
        }

        await _db.SaveChangesAsync();

        var response = new PunchResponse(punch.Id, punch.EmployeeId, punch.Type, punch.PunchedAt, punch.CreatedAt);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}