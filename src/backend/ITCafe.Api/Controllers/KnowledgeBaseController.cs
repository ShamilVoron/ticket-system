using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Knowledge Base: categories and articles (staff CRUD + public published read/search/suggest).</summary>
[ApiController]
[Route("api/[controller]")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly AppDbContext _db;

    public KnowledgeBaseController(AppDbContext db)
    {
        _db = db;
    }

    // ── Public / any auth ──────────────────────────────────────────────

    [HttpGet("articles/published")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> GetPublished()
    {
        var list = await _db.KbArticles.AsNoTracking()
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.UpdatedAtUtc)
            .Select(a => new
            {
                a.Id,
                a.CategoryId,
                CategoryName = a.Category != null ? a.Category.Name : null,
                a.Title,
                a.Body,
                a.Tags,
                a.UpdatedAtUtc,
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> Search([FromQuery] string? q)
    {
        var query = (q ?? "").Trim();
        if (query.Length < 2)
            return Ok(Array.Empty<object>());

        var lower = query.ToLowerInvariant();
        var list = await _db.KbArticles.AsNoTracking()
            .Where(a => a.IsPublished
                        && (a.Title.ToLower().Contains(lower)
                            || a.Body.ToLower().Contains(lower)
                            || a.Tags.ToLower().Contains(lower)))
            .OrderByDescending(a => a.UpdatedAtUtc)
            .Take(30)
            .Select(a => new
            {
                a.Id,
                a.CategoryId,
                a.Title,
                a.Tags,
                a.UpdatedAtUtc,
                Snippet = a.Body.Length > 200 ? a.Body.Substring(0, 200) + "…" : a.Body,
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("suggest")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> Suggest([FromQuery] string? ticketTitle)
    {
        var title = (ticketTitle ?? "").Trim();
        if (title.Length < 3)
            return Ok(Array.Empty<object>());

        var tokens = title
            .Split([' ', ',', '.', ';', ':', '-', '/', '\\', '(', ')', '"', '\'', '!', '?'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 3)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .Take(8)
            .ToList();

        if (tokens.Count == 0)
            return Ok(Array.Empty<object>());

        var published = await _db.KbArticles.AsNoTracking()
            .Where(a => a.IsPublished)
            .Select(a => new { a.Id, a.Title, a.Tags, a.Body, a.CategoryId })
            .ToListAsync();

        var scored = published
            .Select(a =>
            {
                var hay = $"{a.Title} {a.Tags} {a.Body}".ToLowerInvariant();
                var score = tokens.Count(t => hay.Contains(t));
                return new { a.Id, a.Title, a.Tags, a.CategoryId, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Title)
            .Take(5)
            .ToList();

        return Ok(scored);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<KbCategory>>> GetCategories()
    {
        var list = await _db.KbCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
        return Ok(list);
    }

    // ── Staff CRUD ─────────────────────────────────────────────────────

    [HttpGet("articles")]
    [Authorize(Roles = StaffRoles.All)]
    public async Task<ActionResult<IEnumerable<object>>> GetAllArticles()
    {
        var list = await _db.KbArticles.AsNoTracking()
            .OrderByDescending(a => a.UpdatedAtUtc)
            .Select(a => new
            {
                a.Id,
                a.CategoryId,
                CategoryName = a.Category != null ? a.Category.Name : null,
                a.Title,
                a.Body,
                a.Tags,
                a.IsPublished,
                a.UpdatedAtUtc,
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("categories")]
    [Authorize(Roles = StaffRoles.All)]
    public async Task<ActionResult<KbCategory>> SaveCategory([FromBody] KbCategory body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("Name is required.");

        if (body.Id > 0)
        {
            var existing = await _db.KbCategories.FirstOrDefaultAsync(c => c.Id == body.Id);
            if (existing == null) return NotFound();
            existing.Name = body.Name.Trim();
            existing.SortOrder = body.SortOrder;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var created = new KbCategory
        {
            Name = body.Name.Trim(),
            SortOrder = body.SortOrder,
        };
        _db.KbCategories.Add(created);
        await _db.SaveChangesAsync();
        return Ok(created);
    }

    [HttpDelete("categories/{id:int}")]
    [Authorize(Roles = StaffRoles.All)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var cat = await _db.KbCategories.FindAsync(id);
        if (cat == null) return NotFound();

        var articles = await _db.KbArticles.Where(a => a.CategoryId == id).ToListAsync();
        foreach (var a in articles)
            a.CategoryId = null;

        _db.KbCategories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("articles")]
    [Authorize(Roles = StaffRoles.All)]
    public async Task<ActionResult<KbArticle>> SaveArticle([FromBody] KbArticle body)
    {
        if (string.IsNullOrWhiteSpace(body.Title))
            return BadRequest("Title is required.");

        if (body.Id > 0)
        {
            var existing = await _db.KbArticles.FirstOrDefaultAsync(a => a.Id == body.Id);
            if (existing == null) return NotFound();
            existing.Title = body.Title.Trim();
            existing.Body = body.Body ?? "";
            existing.Tags = body.Tags ?? "";
            existing.CategoryId = body.CategoryId;
            existing.IsPublished = body.IsPublished;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var created = new KbArticle
        {
            Title = body.Title.Trim(),
            Body = body.Body ?? "",
            Tags = body.Tags ?? "",
            CategoryId = body.CategoryId,
            IsPublished = body.IsPublished,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        _db.KbArticles.Add(created);
        await _db.SaveChangesAsync();
        return Ok(created);
    }

    [HttpDelete("articles/{id:int}")]
    [Authorize(Roles = StaffRoles.All)]
    public async Task<IActionResult> DeleteArticle(int id)
    {
        var article = await _db.KbArticles.FindAsync(id);
        if (article == null) return NotFound();
        _db.KbArticles.Remove(article);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
