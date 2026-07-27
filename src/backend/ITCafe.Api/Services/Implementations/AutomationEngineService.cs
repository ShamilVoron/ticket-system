using System.Text.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Services.Implementations;

public class AutomationEngineService : IAutomationEngineService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly string[] PriorityLadder =
        ["Низкий", "Средний", "Высокий", "Критический"];

    private readonly AppDbContext _db;
    private readonly ITelegramNotificationService _telegram;
    private readonly ILogger<AutomationEngineService> _logger;

    public AutomationEngineService(
        AppDbContext db,
        ITelegramNotificationService telegram,
        ILogger<AutomationEngineService> logger)
    {
        _db = db;
        _telegram = telegram;
        _logger = logger;
    }

    public Task EvaluateTicketCreatedAsync(Ticket ticket, CancellationToken ct = default)
        => EvaluateTriggerAsync("ticket_created", ticket, ct);

    public async Task EvaluateTriggerAsync(string trigger, Ticket ticket, CancellationToken ct = default)
    {
        var rules = await _db.AutomationRules.AsNoTracking()
            .Where(r => r.IsActive && r.Trigger == trigger)
            .ToListAsync(ct);

        foreach (var rule in rules)
        {
            if (!await MatchesConditionsAsync(rule, ticket, ct))
                continue;
            if (await AlreadyFiredAsync(rule.Id, ticket.Id, trigger, ct))
                continue;

            await ApplyActionsAsync(rule, ticket, ct);
            await MarkFiredAsync(rule.Id, ticket.Id, trigger, ct);
        }
    }

    public async Task RunPeriodicAsync(CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddMinutes(-5);
        var recent = await _db.Tickets
            .Where(t => t.CreatedAt >= since)
            .OrderByDescending(t => t.Id)
            .Take(50)
            .ToListAsync(ct);

        foreach (var ticket in recent)
        {
            await EvaluateTicketCreatedAsync(ticket, ct);
            await EvaluateVipIfNeededAsync(ticket, ct);
        }

        // status_resolved: tickets resolved but not closed, for auto-close after 72h
        var resolved = await _db.Tickets
            .Where(t => t.ClosedAt == null && t.Status.Contains("Решен"))
            .Take(200)
            .ToListAsync(ct);

        foreach (var ticket in resolved)
            await EvaluateTriggerAsync("status_resolved", ticket, ct);

        // Also re-check VIP on open tickets with email domains (light scan)
        var openVipCandidates = await _db.Tickets
            .Where(t => t.ClosedAt == null
                        && t.Status != "Закрыт"
                        && !t.Status.Contains("Решен"))
            .OrderByDescending(t => t.Id)
            .Take(100)
            .ToListAsync(ct);

        foreach (var ticket in openVipCandidates)
            await EvaluateVipIfNeededAsync(ticket, ct);
    }

    private Task EvaluateVipIfNeededAsync(Ticket ticket, CancellationToken ct)
        => EvaluateTriggerAsync("vip_email_domain", ticket, ct);

    private async Task<bool> MatchesConditionsAsync(AutomationRule rule, Ticket ticket, CancellationToken ct)
    {
        Dictionary<string, JsonElement>? cond;
        try
        {
            cond = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                string.IsNullOrWhiteSpace(rule.ConditionsJson) ? "{}" : rule.ConditionsJson, JsonOpts);
        }
        catch
        {
            return true;
        }

        if (cond == null || cond.Count == 0)
            return true;

        if (cond.TryGetValue("department", out var deptEl))
        {
            var dept = deptEl.GetString();
            if (!string.IsNullOrWhiteSpace(dept)
                && !string.Equals(ticket.Department, dept, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (cond.TryGetValue("priority", out var priEl))
        {
            var pri = priEl.GetString();
            if (!string.IsNullOrWhiteSpace(pri)
                && !string.Equals(ticket.Priority, pri, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (cond.TryGetValue("emailDomain", out var domEl)
            || cond.TryGetValue("vipDomain", out domEl))
        {
            var domain = (domEl.GetString() ?? "").Trim().TrimStart('@').ToLowerInvariant();
            if (!string.IsNullOrEmpty(domain))
            {
                var email = await ResolveClientEmailAsync(ticket.ClientId, ct);
                if (string.IsNullOrEmpty(email) || !email.EndsWith("@" + domain, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        if (rule.Trigger == "status_resolved")
        {
            var hours = 72.0;
            if (cond.TryGetValue("autoCloseAfterHours", out var hoursEl)
                && hoursEl.ValueKind == JsonValueKind.Number)
                hours = hoursEl.GetDouble();

            var resolvedAt = await GetOrSetResolvedAtAsync(ticket.Id, ct);
            if ((DateTime.UtcNow - resolvedAt).TotalHours < hours)
                return false;
        }

        return true;
    }

    private async Task ApplyActionsAsync(AutomationRule rule, Ticket ticket, CancellationToken ct)
    {
        List<AutomationAction>? actions;
        try
        {
            actions = JsonSerializer.Deserialize<List<AutomationAction>>(
                string.IsNullOrWhiteSpace(rule.ActionsJson) ? "[]" : rule.ActionsJson, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid ActionsJson on rule {RuleId}", rule.Id);
            return;
        }

        if (actions == null || actions.Count == 0)
            return;

        var tracked = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticket.Id, ct);
        if (tracked == null) return;

        var dirty = false;
        foreach (var action in actions)
        {
            var type = (action.Type ?? "").Trim().ToLowerInvariant();
            var p = action.Params ?? new Dictionary<string, JsonElement>();

            switch (type)
            {
                case "assign_department":
                case "set_department":
                {
                    var dept = GetParamString(p, "department");
                    if (!string.IsNullOrWhiteSpace(dept) && tracked.Department != dept)
                    {
                        tracked.Department = dept;
                        dirty = true;
                    }
                    break;
                }
                case "escalate_priority":
                {
                    var idx = Array.FindIndex(PriorityLadder,
                        x => string.Equals(x, tracked.Priority, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0 && idx < PriorityLadder.Length - 1)
                    {
                        tracked.Priority = PriorityLadder[idx + 1];
                        dirty = true;
                    }
                    break;
                }
                case "set_priority":
                {
                    var pri = GetParamString(p, "priority");
                    if (!string.IsNullOrWhiteSpace(pri) && tracked.Priority != pri)
                    {
                        tracked.Priority = pri;
                        dirty = true;
                    }
                    break;
                }
                case "tag_title":
                {
                    var tag = GetParamString(p, "tag") ?? "[AUTO]";
                    if (!tracked.Title.Contains(tag, StringComparison.OrdinalIgnoreCase))
                    {
                        tracked.Title = $"{tag} {tracked.Title}".Trim();
                        dirty = true;
                    }
                    break;
                }
                case "set_setting":
                {
                    var key = GetParamString(p, "key");
                    var value = GetParamString(p, "value") ?? "";
                    if (!string.IsNullOrWhiteSpace(key))
                        await UpsertSettingAsync(key!, value, ct);
                    break;
                }
                case "notify_telegram":
                case "notify":
                {
                    var evt = GetParamString(p, "eventType") ?? "automation";
                    var msg = GetParamString(p, "message");
                    try
                    {
                        await _telegram.NotifyEventAsync(tracked, evt!,
                            msg == null ? null : new Dictionary<string, string> { ["message"] = msg });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Telegram notify failed for ticket {TicketId}", tracked.Id);
                    }
                    break;
                }
                case "auto_close":
                case "close":
                {
                    if (tracked.ClosedAt == null)
                    {
                        tracked.Status = "Закрыт";
                        tracked.ClosedAt = DateTime.UtcNow;
                        dirty = true;
                    }
                    break;
                }
            }
        }

        if (dirty)
        {
            await _db.SaveChangesAsync(ct);
            ticket.Department = tracked.Department;
            ticket.Priority = tracked.Priority;
            ticket.Title = tracked.Title;
            ticket.Status = tracked.Status;
            ticket.ClosedAt = tracked.ClosedAt;
            _logger.LogInformation("Automation rule {RuleId} applied to ticket {TicketId}", rule.Id, ticket.Id);
        }
    }

    private async Task<string?> ResolveClientEmailAsync(int clientId, CancellationToken ct)
    {
        if (clientId <= 0) return null;
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client == null) return null;
        if (!string.IsNullOrWhiteSpace(client.Email))
            return client.Email.Trim().ToLowerInvariant();

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == client.CompanyId, ct);
        return company?.Email?.Trim().ToLowerInvariant();
    }

    private async Task<DateTime> GetOrSetResolvedAtAsync(int ticketId, CancellationToken ct)
    {
        var key = $"auto_resolved_at:{ticketId}";
        var existing = await _db.SystemSettings.FindAsync([key], ct);
        if (existing != null && DateTime.TryParse(existing.Value, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            return parsed.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : parsed.ToUniversalTime();

        var now = DateTime.UtcNow;
        await UpsertSettingAsync(key, now.ToString("O"), ct);
        return now;
    }

    private async Task<bool> AlreadyFiredAsync(int ruleId, int ticketId, string trigger, CancellationToken ct)
    {
        // status_resolved auto-close should re-evaluate until closed; others fire once
        if (trigger == "status_resolved")
            return false;

        var key = FiredKey(ruleId, ticketId, trigger);
        return await _db.SystemSettings.AsNoTracking().AnyAsync(s => s.Key == key, ct);
    }

    private Task MarkFiredAsync(int ruleId, int ticketId, string trigger, CancellationToken ct)
    {
        if (trigger == "status_resolved")
            return Task.CompletedTask;
        return UpsertSettingAsync(FiredKey(ruleId, ticketId, trigger), DateTime.UtcNow.ToString("O"), ct);
    }

    private static string FiredKey(int ruleId, int ticketId, string trigger)
        => $"auto_fired:{ruleId}:{ticketId}:{trigger}";

    private async Task UpsertSettingAsync(string key, string value, CancellationToken ct)
    {
        var row = await _db.SystemSettings.FindAsync([key], ct);
        if (row == null)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    private static string? GetParamString(Dictionary<string, JsonElement> p, string key)
    {
        if (!p.TryGetValue(key, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private sealed class AutomationAction
    {
        public string? Type { get; set; }
        public Dictionary<string, JsonElement>? Params { get; set; }
    }
}
