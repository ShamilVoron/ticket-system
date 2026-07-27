using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>CRUD for automation rules (staff).</summary>
[Authorize(Roles = StaffRoles.All)]
[ApiController]
[Route("api/[controller]")]
public class AutomationRulesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AutomationRulesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AutomationRule>>> GetAll()
    {
        var list = await _db.AutomationRules.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<AutomationRule>> Save([FromBody] AutomationRule body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("Name is required.");
        if (string.IsNullOrWhiteSpace(body.Trigger))
            return BadRequest("Trigger is required.");

        if (body.Id > 0)
        {
            var existing = await _db.AutomationRules.FirstOrDefaultAsync(r => r.Id == body.Id);
            if (existing == null) return NotFound();
            existing.Name = body.Name.Trim();
            existing.IsActive = body.IsActive;
            existing.Trigger = body.Trigger.Trim();
            existing.ConditionsJson = string.IsNullOrWhiteSpace(body.ConditionsJson) ? "{}" : body.ConditionsJson;
            existing.ActionsJson = string.IsNullOrWhiteSpace(body.ActionsJson) ? "[]" : body.ActionsJson;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var created = new AutomationRule
        {
            Name = body.Name.Trim(),
            IsActive = body.IsActive,
            Trigger = body.Trigger.Trim(),
            ConditionsJson = string.IsNullOrWhiteSpace(body.ConditionsJson) ? "{}" : body.ConditionsJson,
            ActionsJson = string.IsNullOrWhiteSpace(body.ActionsJson) ? "[]" : body.ActionsJson,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.AutomationRules.Add(created);
        await _db.SaveChangesAsync();
        return Ok(created);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await _db.AutomationRules.FindAsync(id);
        if (rule == null) return NotFound();
        _db.AutomationRules.Remove(rule);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
