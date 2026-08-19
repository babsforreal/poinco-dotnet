using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Models;

namespace Api.Controllers;

public record CreatePunchRequest(string EmployeeId, PunchType Type, DateTimeOffset? PunchedAt);

public record PunchResponse(string Id, string EmployeeId, PunchType Type, DateTimeOffset PunchedAt, DateTimeOffset CreatedAt);

[Authorize]
[ApiController]
[Route("[controller]")]
public class PunchesController : ControllerBase
{
    private readonly PoincoDbContext _db;

    public PunchesController(PoincoDbContext db)
    {
        _db = db;
    }

    private string CompanyId => User.FindFirst("companyId")!.Value;

    [HttpGet]
    public async Task<IEnumerable<PunchResponse>> GetAll()
    {
        return await _db.Punches
            .Where(p => p.CompanyId == CompanyId)
            .Select(p => new PunchResponse(p.Id, p.EmployeeId, p.Type, p.PunchedAt, p.CreatedAt))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<PunchResponse>> Create(CreatePunchRequest request)
    {
        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == CompanyId);
        if (employee is null)
            return NotFound();

        var punchedAt = request.PunchedAt ?? DateTimeOffset.UtcNow;

        var punch = new Punch
        {
            CompanyId = employee.CompanyId,
            EmployeeId = employee.Id,
            Type = request.Type,
            PunchedAt = punchedAt
        };

        _db.Punches.Add(punch);
        employee.LastPunchType = request.Type;
        employee.LastPunchedAt = punchedAt;
        await _db.SaveChangesAsync();

        var response = new PunchResponse(punch.Id, punch.EmployeeId, punch.Type, punch.PunchedAt, punch.CreatedAt);
        return CreatedAtAction(nameof(GetAll), new { id = punch.Id }, response);
    }
}