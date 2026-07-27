using ITCafe.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Справочник клиентов.</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ClientsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        var list = await (
            from c in _db.Clients.AsNoTracking()
            join co in _db.Companies.AsNoTracking() on c.CompanyId equals co.Id into gj
            from co in gj.DefaultIfEmpty()
            orderby c.FullName
            select new
            {
                c.Id,
                c.FullName,
                c.Email,
                c.CompanyId,
                c.OkdeskId,
                CompanyName = co != null ? co.Name : null,
            }
        ).ToListAsync();

        return Ok(list);
    }
}
