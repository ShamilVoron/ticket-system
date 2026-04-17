using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.EntityFrameworkCore;
using ITCafe.Api.Services;
using ITCafe.Api.Services.Contracts;

namespace ITCafe.Api.Controllers;

/// <summary>Управление подзадачами внутри заявки.</summary>
[Authorize(Roles = "support_l1,support_l2,developer,field_engineer,accountant,head_engineers,head_support,head_dev,sysadmin,coordinator,director,super_admin,procurement,head_repair,agent")]
[ApiController]
[Route("api/tickets/{ticketId:int}/subtasks")]
public class SubtasksController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TicketRealtimeBroadcaster _realtime;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITelegramNotificationService _telegram;

    public SubtasksController(AppDbContext context, TicketRealtimeBroadcaster realtime, IHttpContextAccessor httpContextAccessor, ITelegramNotificationService telegram)
    {
        _context = context;
        _realtime = realtime;
        _httpContextAccessor = httpContextAccessor;
        _telegram = telegram;
    }

    private string? CurrentUserId() =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

    private string? CurrentUserRole() =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value;

    private bool IsStaff() => !string.Equals(CurrentUserRole(), "client", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> HasPermissionAsync(string permissionKey)
    {
        var uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid)) return false;
        var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == uid);
        if (emp == null) return false;
        try
        {
            var perms = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(emp.PermissionsJson);
            if (perms != null && perms.TryGetValue(permissionKey, out var val)) return val;
        }
        catch { }
        return false;
    }

    private async Task EnsureTicketInteractionAccessAsync(Ticket ticket)
    {
        if (IsStaff())
        {
            var uid = CurrentUserId();
            if (!string.IsNullOrEmpty(ticket.CreatedByUserId) && ticket.CreatedByUserId == uid) return;
            if (!string.IsNullOrEmpty(ticket.Assignee))
            {
                var assignees = ticket.Assignee.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (assignees.Contains(uid)) return;
            }
            if (await HasPermissionAsync("ticketInteractForeign")) return;
        }

        var userId = CurrentUserId();
        if (!string.IsNullOrEmpty(ticket.CreatedByUserId) && ticket.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("You are not authorized to interact with this ticket.");
    }

    private void EnsureTicketAccess(Ticket ticket)
    {
        if (IsStaff()) return;
        var uid = CurrentUserId();
        if (!string.IsNullOrEmpty(ticket.CreatedByUserId) && ticket.CreatedByUserId != uid)
            throw new UnauthorizedAccessException("You are not authorized to access this ticket.");
    }

    private async Task<bool> EnsureTicketAccessAsync(int ticketId)
    {
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) return false;
        EnsureTicketAccess(ticket);
        return true;
    }

    public record SubtaskDto(
        int Id, int TicketId, string Title, string Description,
        string Status, string[] KnowledgeableUserIds, string[] KnowledgeableNames,
        string CreatedByUserId, string CreatedByName, DateTime CreatedAt);

    public record CreateSubtaskRequest(string Title, string? Description, string? Status,
        string[]? KnowledgeableUserIds, string? CreatedByUserId);

    public record UpdateSubtaskRequest(string? Title, string? Description, string? Status,
        string[]? KnowledgeableUserIds);

    private async Task<Dictionary<string, string>> EmployeeNamesAsync()
    {
        return await _context.Employees.AsNoTracking()
            .Where(e => !string.IsNullOrEmpty(e.UserId))
            .ToDictionaryAsync(e => e.UserId, e => e.FullName ?? e.UserId);
    }

    private static string ResolveName(string userId, Dictionary<string, string> map)
        => map.TryGetValue(userId, out var n) ? n : userId;

    private static string[] SplitIds(string s)
        => string.IsNullOrWhiteSpace(s) ? [] :
           s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static List<string> TicketNotifyRecipients(Ticket t, string? excludeUserId)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in SplitIds(t.Assignee))
            if (!string.IsNullOrWhiteSpace(a)) set.Add(a);
        if (!string.IsNullOrEmpty(t.CreatedByUserId)) set.Add(t.CreatedByUserId);
        if (!string.IsNullOrEmpty(excludeUserId)) set.Remove(excludeUserId);
        return set.ToList();
    }

    private SubtaskDto MapDto(TicketSubtask s, Dictionary<string, string> names)
    {
        var ids = SplitIds(s.KnowledgeableUserIds);
        return new SubtaskDto(
            s.Id, s.TicketId, s.Title, s.Description, s.Status,
            ids,
            ids.Select(id => ResolveName(id, names)).ToArray(),
            s.CreatedByUserId,
            ResolveName(s.CreatedByUserId, names),
            s.CreatedAt);
    }

    /// <summary>Возвращает подзадачи заявки.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SubtaskDto>>> GetAll(int ticketId)
    {
        if (!await EnsureTicketAccessAsync(ticketId))
            return NotFound("Ticket not found");
        var names = await EmployeeNamesAsync();
        var list = await _context.TicketSubtasks
            .Where(s => s.TicketId == ticketId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
        return list.Select(s => MapDto(s, names)).ToList();
    }

    /// <summary>Создаёт подзадачу в заявке.</summary>
    [HttpPost]
    public async Task<ActionResult<SubtaskDto>> Create(int ticketId, CreateSubtaskRequest req)
    {
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) return NotFound("Ticket not found");
        await EnsureTicketInteractionAccessAsync(ticket);

        var subtask = new TicketSubtask
        {
            TicketId = ticketId,
            Title = (req.Title ?? "").Trim(),
            Description = (req.Description ?? "").Trim(),
            Status = string.IsNullOrWhiteSpace(req.Status) ? "в процессе" : req.Status.Trim(),
            KnowledgeableUserIds = req.KnowledgeableUserIds != null
                ? string.Join(",", req.KnowledgeableUserIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                : "",
            CreatedByUserId = (req.CreatedByUserId ?? "").Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.TicketSubtasks.Add(subtask);
        await _context.SaveChangesAsync();
        var actor = CurrentUserId();
        await _realtime.NotifyTicketChangedAsync(
            ticketId,
            "subtask",
            actor,
            $"Подзадача к заявке #{ticketId}: {subtask.Title}",
            TicketNotifyRecipients(ticket, actor));
        await _telegram.NotifySubtaskCreatedAsync(ticket, subtask);

        var names = await EmployeeNamesAsync();
        return CreatedAtAction(nameof(GetAll), new { ticketId }, MapDto(subtask, names));
    }

    /// <summary>Обновляет подзадачу.</summary>
    [HttpPatch("{subtaskId:int}")]
    public async Task<ActionResult<SubtaskDto>> Update(int ticketId, int subtaskId, UpdateSubtaskRequest req)
    {
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) return NotFound("Ticket not found");
        await EnsureTicketInteractionAccessAsync(ticket);
        var subtask = await _context.TicketSubtasks.FirstOrDefaultAsync(s => s.Id == subtaskId && s.TicketId == ticketId);
        if (subtask == null) return NotFound();

        if (req.Title != null) subtask.Title = req.Title.Trim();
        if (req.Description != null) subtask.Description = req.Description.Trim();
        if (req.Status != null) subtask.Status = req.Status.Trim();
        if (req.KnowledgeableUserIds != null)
            subtask.KnowledgeableUserIds = string.Join(",", req.KnowledgeableUserIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        await _context.SaveChangesAsync();
        var actorP = CurrentUserId();
        await _realtime.NotifyTicketChangedAsync(
            ticketId,
            "subtask",
            actorP,
            $"Подзадача обновлена (заявка #{ticketId}): {subtask.Title}",
            TicketNotifyRecipients(ticket, actorP));

        var names = await EmployeeNamesAsync();
        return MapDto(subtask, names);
    }

    /// <summary>Удаляет подзадачу.</summary>
    [HttpDelete("{subtaskId:int}")]
    public async Task<IActionResult> Delete(int ticketId, int subtaskId)
    {
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) return NotFound("Ticket not found");
        await EnsureTicketInteractionAccessAsync(ticket);
        var subtask = await _context.TicketSubtasks.FirstOrDefaultAsync(s => s.Id == subtaskId && s.TicketId == ticketId);
        if (subtask == null) return NotFound();

        var attachments = await _context.TicketAttachments
            .Where(a => a.TicketId == ticketId && a.SubtaskId == subtaskId)
            .ToListAsync();
        _context.TicketAttachments.RemoveRange(attachments);

        _context.TicketSubtasks.Remove(subtask);
        await _context.SaveChangesAsync();
        var actorD = CurrentUserId();
        await _realtime.NotifyTicketChangedAsync(
            ticketId,
            "subtask",
            actorD,
            $"Подзадача удалена (заявка #{ticketId})",
            TicketNotifyRecipients(ticket, actorD));
        return NoContent();
    }
}
