using System.Security.Claims;
using System.Text.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>
/// Предпочтения агента. Модель UserPreferences — Mongo-ориентированная;
/// храним JSON в SystemSettings под ключом agent_prefs_{userId}.
/// </summary>
[Authorize(Roles = StaffRoles.All)]
[ApiController]
[Route("api/[controller]")]
public class AgentPreferencesController : ControllerBase
{
    private const string KeyPrefix = "agent_prefs_";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _db;

    public AgentPreferencesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<object>> Get(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("userId is required.");

        var key = KeyPrefix + userId.Trim();
        var row = await _db.SystemSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);

        if (row == null || string.IsNullOrWhiteSpace(row.Value))
            return Ok(DefaultPrefs(userId.Trim()));

        try
        {
            var prefs = JsonSerializer.Deserialize<UserPreferences>(row.Value, JsonOpts)
                        ?? DefaultPrefs(userId.Trim());
            if (string.IsNullOrEmpty(prefs.UserId))
                prefs.UserId = userId.Trim();
            return Ok(prefs);
        }
        catch
        {
            return Ok(DefaultPrefs(userId.Trim()));
        }
    }

    [HttpPost]
    public async Task<ActionResult<object>> Save([FromBody] UserPreferences body)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        var userId = string.IsNullOrWhiteSpace(body.UserId)
            ? currentUserId
            : body.UserId.Trim();

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("userId is required.");

        body.UserId = userId;
        if (string.IsNullOrWhiteSpace(body.Theme))
            body.Theme = "light";
        body.BackgroundUrl ??= string.Empty;
        body.DashboardBlocks ??= new List<string>();
        if (string.IsNullOrWhiteSpace(body.AccentColor))
            body.AccentColor = "#23a836";
        if (string.IsNullOrWhiteSpace(body.WindowColor))
            body.WindowColor = "#ffffff";
        if (string.IsNullOrWhiteSpace(body.TextColor))
            body.TextColor = "#111827";

        var json = JsonSerializer.Serialize(body, JsonOpts);
        var key = KeyPrefix + userId;
        var now = DateTime.UtcNow;

        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (row == null)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = json,
                UpdatedAt = now,
            });
        }
        else
        {
            row.Value = json;
            row.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        return Ok(body);
    }

    private static UserPreferences DefaultPrefs(string userId) => new()
    {
        UserId = userId,
        Theme = "light",
        BackgroundUrl = string.Empty,
        DashboardBlocks = new List<string>(),
        AccentColor = "#23a836",
        WindowColor = "#ffffff",
        TextColor = "#111827",
    };
}
