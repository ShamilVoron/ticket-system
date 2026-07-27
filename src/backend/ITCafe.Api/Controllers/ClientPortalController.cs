using System.Security.Claims;
using ITCafe.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Клиентский портал: объекты своей компании.</summary>
[Authorize(Roles = "client")]
[ApiController]
[Route("api/[controller]")]
public class ClientPortalController : ControllerBase
{
    private readonly AppDbContext _db;

    public ClientPortalController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Объекты обслуживания компании клиента.
    /// CompanyId резолвится так же, как в AuthController ticket-context (Client по email → CompanyId).
    /// ServiceObject.ClientId хранит Id компании.
    /// </summary>
    [HttpGet("service-objects")]
    public async Task<ActionResult<IEnumerable<object>>> GetServiceObjects()
    {
        var companyId = await ResolveClientCompanyIdAsync();
        if (companyId == null)
            return Ok(Array.Empty<object>());

        var list = await _db.ServiceObjects.AsNoTracking()
            .Where(o => o.IsActive && o.ClientId == companyId.Value)
            .OrderBy(o => o.Name)
            .Select(o => new
            {
                o.Id,
                o.Name,
                o.Address,
                o.MaintenanceStatus,
                clientId = o.ClientId,
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Опционально: заявки компании клиента (Create остаётся в TicketsController).</summary>
    [HttpGet("tickets")]
    public async Task<ActionResult<IEnumerable<object>>> GetTickets()
    {
        var companyId = await ResolveClientCompanyIdAsync();
        if (companyId == null)
            return Ok(Array.Empty<object>());

        var list = await _db.Tickets.AsNoTracking()
            .Where(t => t.ClientId == companyId.Value)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.CreatedAt,
                t.ObjectId,
                t.RequestType,
            })
            .ToListAsync();

        return Ok(list);
    }

    private async Task<int?> ResolveClientCompanyIdAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
            return null;

        var client = await _db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email.ToLower() == email);
        return client?.CompanyId;
    }
}
