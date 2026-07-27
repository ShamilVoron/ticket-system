using ITCafe.Api.Data;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Services.Hosted;

/// <summary>
/// Every 60s: check open tickets for SLA 80% / breach; pause when status contains «Ожидание».
/// Fires automation triggers and Telegram events.
/// </summary>
public class SlaMonitorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaMonitorHostedService> _logger;

    public SlaMonitorHostedService(IServiceScopeFactory scopeFactory, ILogger<SlaMonitorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await TickAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SlaMonitorHostedService tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task TickAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var sla = sp.GetRequiredService<ISlaService>();
        var automation = sp.GetRequiredService<IAutomationEngineService>();
        var telegram = sp.GetRequiredService<ITelegramNotificationService>();

        var tickets = await db.Tickets
            .Where(t => t.ClosedAt == null
                        && t.Status != "Закрыт"
                        && !t.Status.Contains("Решен"))
            .OrderByDescending(t => t.Id)
            .Take(300)
            .ToListAsync(ct);

        foreach (var ticket in tickets)
        {
            if (ticket.Status.Contains("Ожидание", StringComparison.OrdinalIgnoreCase))
                continue;

            var info = await sla.GetTicketSlaAsync(ticket.Id);
            if (info == null) continue;

            var now = DateTime.UtcNow;

            if (info.ReactionDeadline.HasValue && info.ReactionMinutes > 0)
            {
                await CheckWindowAsync(
                    db, automation, telegram, ticket, ct,
                    windowKind: "reaction",
                    createdAt: ticket.CreatedAt,
                    deadline: info.ReactionDeadline.Value,
                    totalMinutes: info.ReactionMinutes,
                    now);
            }

            if (info.ResolutionDeadline.HasValue && info.ResolutionMinutes > 0)
            {
                await CheckWindowAsync(
                    db, automation, telegram, ticket, ct,
                    windowKind: "resolution",
                    createdAt: ticket.CreatedAt,
                    deadline: info.ResolutionDeadline.Value,
                    totalMinutes: info.ResolutionMinutes,
                    now);
            }
        }
    }

    private async Task CheckWindowAsync(
        AppDbContext db,
        IAutomationEngineService automation,
        ITelegramNotificationService telegram,
        Ticket ticket,
        CancellationToken ct,
        string windowKind,
        DateTime createdAt,
        DateTime deadline,
        int totalMinutes,
        DateTime now)
    {
        var elapsed = (now - createdAt).TotalMinutes;
        var pct = totalMinutes <= 0 ? 0 : elapsed / totalMinutes;

        if (pct >= 1.0 || now >= deadline)
        {
            var key = $"sla_alert:{ticket.Id}:{windowKind}:breach";
            if (!await AlreadyAsync(db, key, ct))
            {
                await MarkAsync(db, key, ct);
                await automation.EvaluateTriggerAsync("sla_breach", ticket, ct);
                try
                {
                    await telegram.NotifyEventAsync(ticket, "sla_breach",
                        new Dictionary<string, string>
                        {
                            ["message"] = $"SLA breach ({windowKind}) ticket #{ticket.Id}: {ticket.Title}",
                            ["slaWindow"] = windowKind,
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SLA breach Telegram failed for {TicketId}", ticket.Id);
                }
            }
            return;
        }

        if (pct >= 0.8)
        {
            var key = $"sla_alert:{ticket.Id}:{windowKind}:80";
            if (!await AlreadyAsync(db, key, ct))
            {
                await MarkAsync(db, key, ct);
                await automation.EvaluateTriggerAsync("sla_80", ticket, ct);
                try
                {
                    await telegram.NotifyEventAsync(ticket, "sla_80",
                        new Dictionary<string, string>
                        {
                            ["message"] = $"SLA 80% ({windowKind}) ticket #{ticket.Id}: {ticket.Title}",
                            ["slaWindow"] = windowKind,
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SLA 80 Telegram failed for {TicketId}", ticket.Id);
                }
            }
        }
    }

    private static async Task<bool> AlreadyAsync(AppDbContext db, string key, CancellationToken ct)
        => await db.SystemSettings.AsNoTracking().AnyAsync(s => s.Key == key, ct);

    private static async Task MarkAsync(AppDbContext db, string key, CancellationToken ct)
    {
        var row = await db.SystemSettings.FindAsync([key], ct);
        if (row == null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = DateTime.UtcNow.ToString("O"),
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            row.Value = DateTime.UtcNow.ToString("O");
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}
