using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Объекты обслуживания.</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ServiceObjectsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ServiceObjectsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceObject>>> GetAll(
        [FromQuery] int? clientId = null,
        [FromQuery] bool includeInactive = false)
    {
        var query = _db.ServiceObjects.AsNoTracking().AsQueryable();

        if (!includeInactive)
            query = query.Where(o => o.IsActive);
        if (clientId.HasValue)
            query = query.Where(o => o.ClientId == clientId.Value);

        var list = await query.OrderBy(o => o.Name).ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<ServiceObject>> Create([FromBody] ServiceObject body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("Name is required.");

        var entity = new ServiceObject
        {
            Name = body.Name.Trim(),
            Address = body.Address?.Trim() ?? string.Empty,
            MaintenanceStatus = body.MaintenanceStatus?.Trim() ?? string.Empty,
            LegalEntity = body.LegalEntity?.Trim() ?? string.Empty,
            Description = body.Description?.Trim() ?? string.Empty,
            ClientId = body.ClientId,
            OkdeskId = body.OkdeskId,
            ExternalCode = body.ExternalCode?.Trim(),
            IsActive = body.IsActive,
            SyncSource = body.SyncSource ?? string.Empty,
            MaintenanceComment = body.MaintenanceComment?.Trim() ?? string.Empty,
            DirectoriesOwner = body.DirectoriesOwner?.Trim() ?? string.Empty,
            SysAdmin = body.SysAdmin?.Trim() ?? string.Empty,
            ServerServices = body.ServerServices?.Trim() ?? string.Empty,
        };

        _db.ServiceObjects.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServiceObject body)
    {
        var entity = await _db.ServiceObjects.FirstOrDefaultAsync(o => o.Id == id);
        if (entity == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(body.Name))
            entity.Name = body.Name.Trim();
        entity.Address = body.Address?.Trim() ?? entity.Address;
        entity.MaintenanceStatus = body.MaintenanceStatus?.Trim() ?? entity.MaintenanceStatus;
        entity.LegalEntity = body.LegalEntity?.Trim() ?? entity.LegalEntity;
        entity.Description = body.Description?.Trim() ?? entity.Description;
        entity.ClientId = body.ClientId;
        entity.OkdeskId = body.OkdeskId;
        entity.ExternalCode = body.ExternalCode?.Trim();
        entity.IsActive = body.IsActive;
        if (body.SyncSource != null)
            entity.SyncSource = body.SyncSource;
        entity.MaintenanceComment = body.MaintenanceComment?.Trim() ?? entity.MaintenanceComment;
        entity.DirectoriesOwner = body.DirectoriesOwner?.Trim() ?? entity.DirectoriesOwner;
        entity.SysAdmin = body.SysAdmin?.Trim() ?? entity.SysAdmin;
        entity.ServerServices = body.ServerServices?.Trim() ?? entity.ServerServices;

        await _db.SaveChangesAsync();
        return Ok(entity);
    }
}
