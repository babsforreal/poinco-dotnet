using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Api.Data;
using Api.Models;

namespace Api.Controllers;

public record CreateCompanyRequest(string Name, string Slug);

public record CompanyResponse(string Id, string Name, string Slug, string Timezone, int ClockOffsetMinutes);

[ApiController]
[Route("[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly PoincoDbContext _db;

    public CompaniesController(PoincoDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IEnumerable<CompanyResponse>> GetAll()
    {
        return await _db.Companies
            .Select(c => new CompanyResponse(c.Id, c.Name, c.Slug, c.Timezone, c.ClockOffsetMinutes))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<CompanyResponse>> Create(CreateCompanyRequest request)
    {
        var company = new Company
        {
            Name = request.Name,
            Slug = request.Slug
        };

        _db.Companies.Add(company);
        await _db.SaveChangesAsync();

        var response = new CompanyResponse(company.Id, company.Name, company.Slug, company.Timezone, company.ClockOffsetMinutes);
        return CreatedAtAction(nameof(GetAll), new { id = company.Id }, response);
    }
}