using System.Security.Cryptography;
using System.Text.Json;
using ITCafe.Api.Authentication;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Системные настройки: статусы, SLA, Telegram, ключи интеграций.</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SystemSettingsController : ControllerBase
{
    private const string RolePermissionDefaultsKey = "StaffRolePermissionDefaults";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public SystemSettingsController(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    // ---- Statuses ----

    [HttpGet("statuses")]
    public async Task<ActionResult<IEnumerable<SystemStatus>>> GetStatuses()
    {
        var list = await _db.SystemStatuses.AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("statuses")]
    public async Task<ActionResult<SystemStatus>> SaveStatus([FromBody] SystemStatus body)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return BadRequest("Name is required.");

        if (body.Id > 0)
        {
            var existing = await _db.SystemStatuses.FirstOrDefaultAsync(s => s.Id == body.Id);
            if (existing == null) return NotFound();

            existing.Name = body.Name.Trim();
            existing.ColorClass = body.ColorClass ?? string.Empty;
            existing.SortOrder = body.SortOrder;
            existing.RoleFilter = body.RoleFilter ?? string.Empty;
            existing.IsDefault = body.IsDefault;
            existing.IsActive = body.IsActive;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var created = new SystemStatus
        {
            Name = body.Name.Trim(),
            ColorClass = body.ColorClass ?? string.Empty,
            SortOrder = body.SortOrder,
            RoleFilter = body.RoleFilter ?? string.Empty,
            IsDefault = body.IsDefault,
            IsActive = body.IsActive,
        };
        _db.SystemStatuses.Add(created);
        await _db.SaveChangesAsync();
        return Ok(created);
    }

    [HttpDelete("statuses/{id:int}")]
    public async Task<IActionResult> DeleteStatus(int id)
    {
        var existing = await _db.SystemStatuses.FirstOrDefaultAsync(s => s.Id == id);
        if (existing == null) return NotFound();
        _db.SystemStatuses.Remove(existing);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ---- SLA ----

    [HttpGet("sla")]
    public async Task<ActionResult<IEnumerable<SlaPolicy>>> GetSla()
    {
        var list = await _db.SlaPolicies.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        return Ok(list);
    }

    [HttpPost("sla")]
    public async Task<ActionResult<SlaPolicy>> SaveSla([FromBody] SlaPolicy body)
    {
        if (body.Id > 0)
        {
            var existing = await _db.SlaPolicies.FirstOrDefaultAsync(s => s.Id == body.Id);
            if (existing == null) return NotFound();

            existing.Priority = body.Priority ?? "*";
            existing.RequestType = body.RequestType ?? "*";
            existing.Department = body.Department ?? "*";
            existing.ClientCategory = body.ClientCategory ?? "*";
            existing.ReactionMinutes = body.ReactionMinutes;
            existing.ResolutionMinutes = body.ResolutionMinutes;
            existing.IsActive = body.IsActive;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var created = new SlaPolicy
        {
            Priority = body.Priority ?? "*",
            RequestType = body.RequestType ?? "*",
            Department = body.Department ?? "*",
            ClientCategory = body.ClientCategory ?? "*",
            ReactionMinutes = body.ReactionMinutes,
            ResolutionMinutes = body.ResolutionMinutes,
            IsActive = body.IsActive,
        };
        _db.SlaPolicies.Add(created);
        await _db.SaveChangesAsync();
        return Ok(created);
    }

    [HttpDelete("sla/{id:int}")]
    public async Task<IActionResult> DeleteSla(int id)
    {
        var existing = await _db.SlaPolicies.FirstOrDefaultAsync(s => s.Id == id);
        if (existing == null) return NotFound();
        _db.SlaPolicies.Remove(existing);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ---- Telegram ----

    [HttpGet("telegram")]
    public async Task<ActionResult<IEnumerable<TelegramBotSetting>>> GetTelegram()
    {
        var list = await _db.TelegramBotSettings.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
        return Ok(list);
    }

    [HttpPost("telegram")]
    public async Task<ActionResult<TelegramBotSetting>> SaveTelegram([FromBody] TelegramBotSetting body)
    {
        if (string.IsNullOrWhiteSpace(body.EventType))
            return BadRequest("EventType is required.");

        if (body.Id > 0)
        {
            var existing = await _db.TelegramBotSettings.FirstOrDefaultAsync(t => t.Id == body.Id);
            if (existing == null) return NotFound();

            existing.EventType = body.EventType.Trim();
            existing.IsEnabled = body.IsEnabled;
            existing.ChatId = body.ChatId ?? string.Empty;
            existing.Template = body.Template ?? string.Empty;
            existing.AlertThresholdMinutes = body.AlertThresholdMinutes;
            existing.TargetType = body.TargetType ?? "chat";
            existing.TargetEmployeeId = body.TargetEmployeeId;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var created = new TelegramBotSetting
        {
            EventType = body.EventType.Trim(),
            IsEnabled = body.IsEnabled,
            ChatId = body.ChatId ?? string.Empty,
            Template = body.Template ?? string.Empty,
            AlertThresholdMinutes = body.AlertThresholdMinutes,
            TargetType = body.TargetType ?? "chat",
            TargetEmployeeId = body.TargetEmployeeId,
        };
        _db.TelegramBotSettings.Add(created);
        await _db.SaveChangesAsync();
        return Ok(created);
    }

    [HttpDelete("telegram/{id:int}")]
    public async Task<IActionResult> DeleteTelegram(int id)
    {
        var existing = await _db.TelegramBotSettings.FirstOrDefaultAsync(t => t.Id == id);
        if (existing == null) return NotFound();
        _db.TelegramBotSettings.Remove(existing);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ---- Key/value settings ----

    [HttpGet("settings")]
    public async Task<ActionResult<Dictionary<string, string>>> GetSettings()
    {
        var rows = await _db.SystemSettings.AsNoTracking().ToListAsync();
        var map = rows.ToDictionary(r => r.Key, r => r.Value);
        return Ok(map);
    }

    [HttpPost("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] SaveSettingsRequest request)
    {
        if (request.Values == null || request.Values.Count == 0)
            return BadRequest("values is required.");

        var now = DateTime.UtcNow;
        foreach (var (key, value) in request.Values)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (row == null)
            {
                _db.SystemSettings.Add(new SystemSetting
                {
                    Key = key,
                    Value = value ?? string.Empty,
                    UpdatedAt = now,
                });
            }
            else
            {
                row.Value = value ?? string.Empty;
                row.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    // ---- Role permission defaults ----

    [HttpGet("role-permission-defaults")]
    public async Task<ActionResult<object>> GetRolePermissionDefaults()
    {
        var row = await _db.SystemSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == RolePermissionDefaultsKey);

        if (row == null || string.IsNullOrWhiteSpace(row.Value))
            return Ok(new Dictionary<string, Dictionary<string, bool>>());

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, bool>>>(
                row.Value, JsonOpts);
            return Ok(parsed ?? new Dictionary<string, Dictionary<string, bool>>());
        }
        catch
        {
            return Ok(new Dictionary<string, Dictionary<string, bool>>());
        }
    }

    [HttpPost("role-permission-defaults")]
    public async Task<IActionResult> SaveRolePermissionDefaults([FromBody] SaveRolePermissionDefaultsRequest request)
    {
        if (request.Defaults == null)
            return BadRequest("defaults is required.");

        var json = JsonSerializer.Serialize(request.Defaults);
        var now = DateTime.UtcNow;
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == RolePermissionDefaultsKey);
        if (row == null)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = RolePermissionDefaultsKey,
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
        return Ok();
    }

    // ---- Staff API key ----

    [HttpGet("staff-api-key/status")]
    public async Task<ActionResult<object>> GetStaffApiKeyStatus()
    {
        var hashRow = await _db.SystemSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == StaffApiKeyAuthenticationDefaults.HashSettingKey);
        var userRow = await _db.SystemSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == StaffApiKeyAuthenticationDefaults.UserIdSettingKey);

        var configured = hashRow != null
            && !string.IsNullOrWhiteSpace(hashRow.Value)
            && userRow != null
            && !string.IsNullOrWhiteSpace(userRow.Value);

        return Ok(new
        {
            configured,
            boundUserId = configured ? userRow!.Value : null,
        });
    }

    [HttpPost("staff-api-key")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<object>> GenerateStaffApiKey([FromBody] GenerateStaffApiKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest("userId is required.");

        var account = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == request.UserId);
        if (account == null)
            return NotFound("User not found.");
        if (string.Equals(account.Role, "client", StringComparison.OrdinalIgnoreCase))
            return BadRequest("API key can only be bound to a staff user.");

        var apiKey = "ts_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var hash = BCrypt.Net.BCrypt.HashPassword(apiKey);
        var now = DateTime.UtcNow;

        await UpsertSettingAsync(StaffApiKeyAuthenticationDefaults.HashSettingKey, hash, now);
        await UpsertSettingAsync(StaffApiKeyAuthenticationDefaults.UserIdSettingKey, request.UserId.Trim(), now);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            apiKey,
            userId = request.UserId.Trim(),
            message = "Сохраните ключ сейчас — повторно показать его нельзя.",
        });
    }

    [HttpDelete("staff-api-key")]
    [Authorize(Roles = "super_admin")]
    public async Task<IActionResult> RevokeStaffApiKey()
    {
        var hashRow = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == StaffApiKeyAuthenticationDefaults.HashSettingKey);
        var userRow = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == StaffApiKeyAuthenticationDefaults.UserIdSettingKey);

        if (hashRow != null) _db.SystemSettings.Remove(hashRow);
        if (userRow != null) _db.SystemSettings.Remove(userRow);

        await _db.SaveChangesAsync();
        return Ok();
    }

    // ---- Okdesk ----

    [HttpPost("okdesk/test-connection")]
    public async Task<ActionResult<object>> TestOkdeskConnection()
    {
        var settings = await _db.SystemSettings.AsNoTracking().ToListAsync();
        var url = (settings.FirstOrDefault(s => s.Key == "OkdeskApiUrl")?.Value ?? string.Empty)
            .Trim().TrimEnd('/');
        var token = (settings.FirstOrDefault(s => s.Key == "OkdeskApiToken")?.Value ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            return Ok(new { valid = false });

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var testUrl =
                $"{url}/api/v1/issues?page=1&count=1&api_token={Uri.EscapeDataString(token)}";
            using var response = await client.GetAsync(testUrl);
            return Ok(new { valid = response.IsSuccessStatusCode });
        }
        catch
        {
            return Ok(new { valid = false });
        }
    }

    /// <summary>
    /// Bulk import companies (+ open issues when available) from Okdesk.
    /// Requires OkdeskApiUrl / OkdeskApiToken in system settings.
    /// </summary>
    [HttpPost("okdesk/import")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<object>> ImportOkdesk(
        [FromServices] ITCafe.Api.Services.Contracts.IOkdeskSyncService okdesk)
    {
        var result = await okdesk.ImportAsync();
        return Ok(new
        {
            result.CompaniesFetched,
            result.CompaniesUpserted,
            result.IssuesFetched,
            result.IssuesUpserted,
            result.Warning,
        });
    }

    private async Task UpsertSettingAsync(string key, string value, DateTime now)
    {
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (row == null)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = now,
            });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = now;
        }
    }

    public record SaveSettingsRequest(Dictionary<string, string>? Values);

    public record SaveRolePermissionDefaultsRequest(
        Dictionary<string, Dictionary<string, bool>>? Defaults);

    public record GenerateStaffApiKeyRequest(string UserId);
}
