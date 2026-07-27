using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Справочник компаний (юрлиц).</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CompaniesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Company>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var query = _db.Companies.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        var list = await query.OrderBy(c => c.Name).ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<Company>> Create([FromBody] Company body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("Name is required.");

        var company = new Company
        {
            Name = body.Name.Trim(),
            Email = body.Email?.Trim(),
            Phone = body.Phone?.Trim(),
            HqAddress = body.HqAddress?.Trim(),
            ExternalCode = body.ExternalCode?.Trim(),
            OkdeskId = body.OkdeskId,
            IsActive = body.IsActive,
            SyncSource = body.SyncSource ?? string.Empty,
        };

        _db.Companies.Add(company);
        await _db.SaveChangesAsync();
        return Ok(company);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Company body)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(body.Name))
            company.Name = body.Name.Trim();
        company.Email = body.Email?.Trim();
        company.Phone = body.Phone?.Trim();
        company.HqAddress = body.HqAddress?.Trim();
        company.ExternalCode = body.ExternalCode?.Trim();
        company.OkdeskId = body.OkdeskId;
        company.IsActive = body.IsActive;
        if (body.SyncSource != null)
            company.SyncSource = body.SyncSource;

        await _db.SaveChangesAsync();
        return Ok(company);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound();

        // Soft delete
        company.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
