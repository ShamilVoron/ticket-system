using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Учёт оборудования.</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly AppDbContext _db;

    public EquipmentController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Equipment>>> GetAll(
        [FromQuery] string? tab = null,
        [FromQuery] string? equipmentType = null,
        [FromQuery] string? fundStatus = null)
    {
        var query = _db.Equipment.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(tab))
            query = query.Where(e => e.Tab == tab);
        if (!string.IsNullOrWhiteSpace(equipmentType))
            query = query.Where(e => e.EquipmentType == equipmentType);
        if (!string.IsNullOrWhiteSpace(fundStatus))
            query = query.Where(e => e.FundStatus == fundStatus);

        var list = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<Equipment>> Create([FromBody] Equipment body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("Name is required.");

        var entity = MapNew(body);
        entity.CreatedAt = DateTime.UtcNow;

        _db.Equipment.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Equipment body)
    {
        var entity = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null) return NotFound();

        Apply(entity, body);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null) return NotFound();

        _db.Equipment.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private static Equipment MapNew(Equipment body)
    {
        var entity = new Equipment();
        Apply(entity, body);
        return entity;
    }

    private static void Apply(Equipment entity, Equipment body)
    {
        if (!string.IsNullOrWhiteSpace(body.Tab))
            entity.Tab = body.Tab.Trim();
        if (!string.IsNullOrWhiteSpace(body.EquipmentType))
            entity.EquipmentType = body.EquipmentType.Trim();
        if (!string.IsNullOrWhiteSpace(body.FundStatus))
            entity.FundStatus = body.FundStatus.Trim();
        if (!string.IsNullOrWhiteSpace(body.Name))
            entity.Name = body.Name.Trim();
        if (!string.IsNullOrWhiteSpace(body.Category))
            entity.Category = body.Category.Trim();

        entity.SerialNumber = body.SerialNumber?.Trim() ?? string.Empty;
        entity.Location = body.Location?.Trim() ?? string.Empty;
        entity.Status = body.Status?.Trim() ?? string.Empty;
        entity.ClientName = body.ClientName?.Trim() ?? string.Empty;
        entity.Notes = body.Notes?.Trim() ?? string.Empty;
        entity.Defect = body.Defect?.Trim() ?? string.Empty;
        entity.Processor = body.Processor?.Trim() ?? string.Empty;
        entity.Ram = body.Ram?.Trim() ?? string.Empty;
        entity.DiskInfo = body.DiskInfo?.Trim() ?? string.Empty;
        entity.OsInfo = body.OsInfo?.Trim() ?? string.Empty;
        entity.Interfaces = body.Interfaces?.Trim() ?? string.Empty;
        entity.Completeness = body.Completeness?.Trim() ?? string.Empty;
        entity.Faults = body.Faults?.Trim() ?? string.Empty;
        entity.InstallPosition = body.InstallPosition?.Trim() ?? string.Empty;
        entity.PowerSpecs = body.PowerSpecs?.Trim() ?? string.Empty;
        entity.IssuedTo = body.IssuedTo?.Trim() ?? string.Empty;
        entity.PurchaseDate = body.PurchaseDate;
        entity.IssueDate = body.IssueDate;
    }
}
