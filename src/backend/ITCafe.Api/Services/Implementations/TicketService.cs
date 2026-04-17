using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Dtos;
using ITCafe.Api.Dtos.Tickets;
using ITCafe.Api.Helpers;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Services.Implementations;

public class TicketService : ITicketService
{
    private readonly AppDbContext _context;
    private readonly TicketRealtimeBroadcaster _realtime;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITelegramNotificationService _telegram;
    private readonly IOkdeskSyncService _okdesk;

    private static readonly JsonSerializerOptions BriefJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Dictionary<string, string[]> RepairCompanyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["burger"] = new[] { "бургер-бк", "бургер бк" }
    };

    public TicketService(AppDbContext context, TicketRealtimeBroadcaster realtime, IHttpContextAccessor httpContextAccessor, ITelegramNotificationService telegram, IOkdeskSyncService okdesk, IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _realtime = realtime;
        _httpContextAccessor = httpContextAccessor;
        _telegram = telegram;
        _okdesk = okdesk;
        _scopeFactory = scopeFactory;
    }

    private string? CurrentUserId() =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

    private string? CurrentUserRole() =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value;

    private readonly IServiceScopeFactory _scopeFactory;

    private void FireAndForgetOkdeskSync(int ticketId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var okdesk = scope.ServiceProvider.GetRequiredService<IOkdeskSyncService>();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ticket = await context.Tickets.FindAsync(ticketId);
                if (ticket != null)
                    await okdesk.SyncTicketAsync(ticket);
            }
            catch { /* swallowed to avoid blocking UI */ }
        });
    }

    private void FireAndForgetOkdeskCommentSync(int ticketId, int commentId, string? authorUserId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var okdesk = scope.ServiceProvider.GetRequiredService<IOkdeskSyncService>();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ticket = await context.Tickets.FindAsync(ticketId);
                var comment = await context.TicketComments.FindAsync(commentId);
                if (ticket != null && comment != null)
                    await okdesk.SyncTicketCommentAsync(ticket, comment, authorUserId);
            }
            catch { /* swallowed */ }
        });
    }

    /// <summary>Не клиент: учитываем все role-claims в JWT (иногда несколько или нестандартный type).</summary>
    private bool IsStaff()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return false;

        var roles = user.Claims
            .Where(c =>
                c.Type == ClaimTypes.Role
                || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, "Role", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value?.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Count == 0)
            return !string.Equals(CurrentUserRole(), "client", StringComparison.OrdinalIgnoreCase);

        return roles.Any(r => !string.Equals(r, "client", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Латиница, похожая на кириллицу (копипаст из Excel/OKDesk), чтобы «Oткрыт» всё равно считался открытым.
    /// </summary>
    private static string NormalizeHomoglyphsToCyrillic(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                'A' or 'a' => 'а',
                'E' or 'e' => 'е',
                'K' or 'k' => 'к',
                'M' or 'm' => 'м',
                'O' or 'o' => 'о',
                'P' or 'p' => 'р',
                'C' or 'c' => 'с',
                'T' or 't' => 'т',
                'X' or 'x' => 'х',
                'Y' or 'y' => 'у',
                _ => ch,
            });
        }
        return sb.ToString();
    }

    /// <summary>Статус «открыта заявка» — без учёта регистра, плюс имя из справочника IsDefault.</summary>
    private static bool IsOpenTicketStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var s = status.Trim();
        if (string.Equals(s, "open", StringComparison.OrdinalIgnoreCase)) return true;
        var normalized = NormalizeHomoglyphsToCyrillic(s).ToLowerInvariant();
        return normalized is "открыт" or "открыта";
    }

    /// <summary>Совпадение с системным статусом по умолчанию («Открыт» или переименованный в настройках).</summary>
    private async Task<bool> IsTicketInOpenStateAsync(string? status)
    {
        if (IsOpenTicketStatus(status)) return true;
        if (string.IsNullOrWhiteSpace(status)) return false;

        var trimmed = status.Trim();
        var defaultOpen = await _context.SystemStatuses.AsNoTracking()
            .Where(s => s.IsDefault && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Name)
            .FirstOrDefaultAsync();

        return defaultOpen != null
            && string.Equals(trimmed, defaultOpen.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private const string InProgressStatus = "В работе";

    private void EnsureTicketAccess(Ticket ticket)
    {
        if (IsStaff()) return;
        var uid = CurrentUserId();
        if (!string.IsNullOrEmpty(ticket.CreatedByUserId) && ticket.CreatedByUserId != uid)
            throw new UnauthorizedAccessException("You are not authorized to access this ticket.");
    }

    private static bool HasGlobalTicketManageRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim().ToLowerInvariant();
        return r is "super_admin" or "coordinator" or "director" or "sysadmin"
            or "head_support" or "head_engineers" or "head_dev" or "head_repair";
    }

    private static bool IsFieldEngineerRole(string? role) =>
        !string.IsNullOrWhiteSpace(role)
        && string.Equals(role.Trim(), "field_engineer", StringComparison.OrdinalIgnoreCase);

    private void EnsureTicketModificationAccess(Ticket ticket)
    {
        var uid = CurrentUserId();
        var role = CurrentUserRole();
        if (HasGlobalTicketManageRole(role))
        {
            return;
        }
        if (!string.IsNullOrEmpty(ticket.CreatedByUserId) && ticket.CreatedByUserId == uid)
            return;
        if (!string.IsNullOrEmpty(ticket.Assignee) && SplitAssignees(ticket.Assignee).Contains(uid))
            return;
        throw new UnauthorizedAccessException("You are not authorized to modify this ticket.");
    }

    private async Task<bool> HasPermissionAsync(string permissionKey)
    {
        var uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid)) return false;
        var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == uid);
        if (emp == null) return false;
        try
        {
            var perms = JsonSerializer.Deserialize<Dictionary<string, bool>>(emp.PermissionsJson);
            if (perms != null && perms.TryGetValue(permissionKey, out var val)) return val;
        }
        catch { }
        return false;
    }

    private async Task EnsureTicketInteractionAccessAsync(Ticket ticket)
    {
        try
        {
            EnsureTicketModificationAccess(ticket);
            return;
        }
        catch (UnauthorizedAccessException) { }

        if (await HasPermissionAsync("ticketInteractForeign")) return;

        throw new UnauthorizedAccessException("You are not authorized to interact with this ticket.");
    }

    private static string Sanitize(string? input) =>
        string.IsNullOrWhiteSpace(input) ? string.Empty : WebUtility.HtmlDecode(input.Trim());

    public async Task<IEnumerable<TicketDto>> GetTicketsAsync(string? assignee = null)
    {
        var query = _context.Tickets.AsNoTracking().AsQueryable();
        if (!IsStaff())
        {
            var uid = CurrentUserId();
            query = query.Where(t => t.CreatedByUserId == uid);
        }

        if (!string.IsNullOrWhiteSpace(assignee))
        {
            var a = assignee.Trim();
            query = query.Where(t =>
                t.Assignee == a ||
                t.Assignee.StartsWith(a + ",") ||
                t.Assignee.EndsWith("," + a) ||
                t.Assignee.Contains("," + a + ","));
        }

        var tickets = await query.ToListAsync();
        var clients = await _context.Clients.AsNoTracking().ToDictionaryAsync(c => c.Id);
        var companies = await _context.Companies.AsNoTracking().ToDictionaryAsync(c => c.Id);
        var objects = await _context.ServiceObjects.AsNoTracking().ToDictionaryAsync(o => o.Id);
        var empNames = await LoadEmployeeNameMapAsync();
        var subtaskCounts = await _context.TicketSubtasks
            .GroupBy(s => s.TicketId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var ticketIds = tickets.Select(t => t.Id).ToList();
        var currentUserId = CurrentUserId();

        var commentTextsByTicket = await _context.TicketComments
            .Where(c => ticketIds.Contains(c.TicketId))
            .GroupBy(c => c.TicketId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(c => c.Text).ToArray());

        var taskLinksByTicket = await _context.Tickets
            .Where(t => ticketIds.Contains(t.Id) && !string.IsNullOrEmpty(t.TaskLinksJson))
            .ToDictionaryAsync(
                t => t.Id,
                t => ExtractTaskLinkUrls(t.TaskLinksJson));

        var readStates = await _context.UserTicketReadStates
            .Where(r => r.UserId == currentUserId && ticketIds.Contains(r.TicketId))
            .ToDictionaryAsync(r => r.TicketId, r => r.LastReadAt);

        var latestCommentTimes = await _context.TicketComments
            .Where(c => ticketIds.Contains(c.TicketId) && c.AuthorUserId != currentUserId)
            .GroupBy(c => c.TicketId)
            .Select(g => new { g.Key, MaxAt = g.Max(c => c.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.MaxAt);

        var ticketCreatedByOthers = tickets
            .Where(t => t.CreatedByUserId != currentUserId)
            .ToDictionary(t => t.Id, t => t.CreatedAt);

        bool IsUnread(int ticketId)
        {
            if (!readStates.TryGetValue(ticketId, out var lastRead))
            {
                // Never read: unread if created by someone else or has comments from others
                if (ticketCreatedByOthers.ContainsKey(ticketId)) return true;
                if (latestCommentTimes.ContainsKey(ticketId)) return true;
                return false;
            }

            if (ticketCreatedByOthers.TryGetValue(ticketId, out var createdAt) && createdAt > lastRead)
                return true;

            if (latestCommentTimes.TryGetValue(ticketId, out var commentTime) && commentTime > lastRead)
                return true;

            return false;
        }

        return tickets
            .Select(t => MapTicket(t,
                ResolveTicketClientName(t, clients, companies).Length > 0
                    ? ResolveTicketClientName(t, clients, companies)
                    : ResolveClientName(t.ClientId, clients, companies),
                t.ObjectId.HasValue ? objects.GetValueOrDefault(t.ObjectId.Value) : null,
                empNames,
                subtaskCounts.GetValueOrDefault(t.Id, 0),
                commentTextsByTicket.GetValueOrDefault(t.Id),
                taskLinksByTicket.GetValueOrDefault(t.Id),
                IsUnread(t.Id)))
            .ToList();
    }

    public async Task<Dtos.Common.PagedResult<TicketDto>> GetTicketsPagedAsync(Dtos.Tickets.GetTicketsRequest request)
    {
        var query = _context.Tickets.AsNoTracking().AsQueryable();

        if (!IsStaff())
        {
            var uid = CurrentUserId();
            query = query.Where(t => t.CreatedByUserId == uid);
        }

        // Filters
        if (request.Statuses?.Length > 0)
        {
            var set = request.Statuses.Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            query = query.Where(t => set.Contains(t.Status));
        }

        if (request.Departments?.Length > 0)
        {
            var set = request.Departments.Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            query = query.Where(t => set.Contains(t.Department));
        }

        if (request.Assignees?.Length > 0)
        {
            var set = request.Assignees.Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            query = query.Where(t => set.Any(a =>
                t.Assignee == a ||
                t.Assignee.StartsWith(a + ",") ||
                t.Assignee.EndsWith("," + a) ||
                t.Assignee.Contains("," + a + ",")));
        }

        if (request.ClientNames?.Length > 0)
        {
            var set = request.ClientNames.Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            query = query.Where(t => set.Contains(t.RepairClientName) ||
                _context.Companies.Any(c => c.Id == t.ClientId && set.Contains(c.Name)) ||
                _context.Clients.Any(c => c.Id == t.ClientId && set.Contains(c.FullName)));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim();
            var digits = new string(q.Where(char.IsDigit).ToArray());
            var isIdSearch = !string.IsNullOrEmpty(digits) && (q.StartsWith("#") || digits == q);

            query = query.Where(t =>
                EF.Functions.ILike(t.Title, $"%{q}%") ||
                (t.AlternativeTitle != null && EF.Functions.ILike(t.AlternativeTitle, $"%{q}%")) ||
                (t.Problem != null && EF.Functions.ILike(t.Problem, $"%{q}%")) ||
                (t.RepairClientName != null && EF.Functions.ILike(t.RepairClientName, $"%{q}%")) ||
                _context.Companies.Any(c => c.Id == t.ClientId && EF.Functions.ILike(c.Name, $"%{q}%")) ||
                _context.Clients.Any(c => c.Id == t.ClientId && EF.Functions.ILike(c.FullName, $"%{q}%")) ||
                (isIdSearch && t.Id.ToString().Contains(digits)) ||
                _context.TicketComments.Any(c => c.TicketId == t.Id && EF.Functions.ILike(c.Text, $"%{q}%")) ||
                (t.TaskLinksJson != null && EF.Functions.ILike(t.TaskLinksJson, $"%{q}%")));
        }

        // Sorting
        var sortKey = request.SortKey?.ToLowerInvariant() ?? "date";
        var sortOrder = request.SortOrder?.ToLowerInvariant() ?? "desc";
        query = sortKey switch
        {
            "id" => sortOrder == "asc" ? query.OrderBy(t => t.Id) : query.OrderByDescending(t => t.Id),
            "title" => sortOrder == "asc" ? query.OrderBy(t => t.Title) : query.OrderByDescending(t => t.Title),
            "client" => sortOrder == "asc" ? query.OrderBy(t => t.RepairClientName) : query.OrderByDescending(t => t.RepairClientName),
            "status" => sortOrder == "asc" ? query.OrderBy(t => t.Status) : query.OrderByDescending(t => t.Status),
            "priority" => sortOrder == "asc" ? query.OrderBy(t => t.Priority) : query.OrderByDescending(t => t.Priority),
            "assignee" => sortOrder == "asc" ? query.OrderBy(t => t.Assignee) : query.OrderByDescending(t => t.Assignee),
            _ => sortOrder == "asc" ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt),
        };

        var totalCount = await query.CountAsync();
        var tickets = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var ticketIds = tickets.Select(t => t.Id).ToList();
        var currentUserId = CurrentUserId();

        var clients = await _context.Clients.AsNoTracking().ToDictionaryAsync(c => c.Id);
        var companies = await _context.Companies.AsNoTracking().ToDictionaryAsync(c => c.Id);
        var objects = await _context.ServiceObjects.AsNoTracking().ToDictionaryAsync(o => o.Id);
        var empNames = await LoadEmployeeNameMapAsync();
        var subtaskCounts = await _context.TicketSubtasks
            .Where(s => ticketIds.Contains(s.TicketId))
            .GroupBy(s => s.TicketId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var commentTextsByTicket = await _context.TicketComments
            .Where(c => ticketIds.Contains(c.TicketId))
            .GroupBy(c => c.TicketId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(c => c.Text).ToArray());

        var taskLinksByTicket = await _context.Tickets
            .Where(t => ticketIds.Contains(t.Id) && !string.IsNullOrEmpty(t.TaskLinksJson))
            .ToDictionaryAsync(
                t => t.Id,
                t => ExtractTaskLinkUrls(t.TaskLinksJson));

        var readStates = await _context.UserTicketReadStates
            .Where(r => r.UserId == currentUserId && ticketIds.Contains(r.TicketId))
            .ToDictionaryAsync(r => r.TicketId, r => r.LastReadAt);

        var latestCommentTimes = await _context.TicketComments
            .Where(c => ticketIds.Contains(c.TicketId) && c.AuthorUserId != currentUserId)
            .GroupBy(c => c.TicketId)
            .Select(g => new { g.Key, MaxAt = g.Max(c => c.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.MaxAt);

        var ticketCreatedByOthers = tickets
            .Where(t => t.CreatedByUserId != currentUserId)
            .ToDictionary(t => t.Id, t => t.CreatedAt);

        bool IsUnread(int ticketId)
        {
            if (!readStates.TryGetValue(ticketId, out var lastRead))
            {
                if (ticketCreatedByOthers.ContainsKey(ticketId)) return true;
                if (latestCommentTimes.ContainsKey(ticketId)) return true;
                return false;
            }
            if (ticketCreatedByOthers.TryGetValue(ticketId, out var createdAt) && createdAt > lastRead) return true;
            if (latestCommentTimes.TryGetValue(ticketId, out var commentTime) && commentTime > lastRead) return true;
            return false;
        }

        var items = tickets
            .Select(t => MapTicket(t,
                ResolveTicketClientName(t, clients, companies).Length > 0
                    ? ResolveTicketClientName(t, clients, companies)
                    : ResolveClientName(t.ClientId, clients, companies),
                t.ObjectId.HasValue ? objects.GetValueOrDefault(t.ObjectId.Value) : null,
                empNames,
                subtaskCounts.GetValueOrDefault(t.Id, 0),
                commentTextsByTicket.GetValueOrDefault(t.Id),
                taskLinksByTicket.GetValueOrDefault(t.Id),
                IsUnread(t.Id)))
            .ToList();

        return new Dtos.Common.PagedResult<TicketDto>(items, totalCount, request.Page, request.PageSize);
    }

    public async Task<Dtos.Tickets.TicketStatsDto> GetTicketStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var query = _context.Tickets.AsNoTracking().AsQueryable();
        if (!IsStaff())
        {
            var uid = CurrentUserId();
            query = query.Where(t => t.CreatedByUserId == uid);
        }

        var todayTickets = await query
            .Where(t => t.CreatedAt >= today && t.CreatedAt < tomorrow)
            .ToListAsync();

        var total = todayTickets.Count;
        var open = todayTickets.Count(t => t.Status == "Открыт");
        var inProgress = todayTickets.Count(t => t.Status == "В работе");
        var repair = todayTickets.Count(t => t.IsRepair);

        return new Dtos.Tickets.TicketStatsDto(total, open, inProgress, repair);
    }

    public async Task<TicketDto?> GetTicketAsync(int id)
    {
        var t = await _context.Tickets.FindAsync(id);
        if (t == null) return null;

        EnsureTicketAccess(t);

        // Загружаем только нужные данные вместо полных таблиц
        var empNames = await LoadEmployeeNameMapAsync();
        var stCount = await _context.TicketSubtasks.CountAsync(s => s.TicketId == id);
        var o = t.ObjectId.HasValue
            ? await _context.ServiceObjects.AsNoTracking().FirstOrDefaultAsync(so => so.Id == t.ObjectId.Value)
            : null;

        var cn = string.Empty;
        if (t.IsRepair && !string.IsNullOrWhiteSpace(t.RepairClientName))
        {
            cn = t.RepairClientName.Trim();
        }
        else if (t.ClientId > 0)
        {
            var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == t.ClientId);
            if (client != null && !string.IsNullOrWhiteSpace(client.FullName))
                cn = client.FullName;
            else
            {
                var company = await _context.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == t.ClientId);
                if (company != null && !string.IsNullOrWhiteSpace(company.Name))
                    cn = company.Name;
            }
        }

        return MapTicket(t, cn, o, empNames, stCount, null, null, false);
    }

    public async Task<bool> UpdateStatusAsync(int id, string status)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return false;
        EnsureTicketModificationAccess(ticket);
        var oldStatus = ticket.Status;
        ticket.Status = status;
        var closedStatuses = new[] { "Закрыт", "Решён", "Решено" };
        if (closedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            ticket.ClosedAt = DateTime.UtcNow;
        else
            ticket.ClosedAt = null;
        await _context.SaveChangesAsync();
        var actorStatus = CurrentUserId();
        await _realtime.NotifyTicketChangedAsync(
            id,
            "status",
            actorStatus,
            $"Статус заявки #{id}: {oldStatus} → {status}",
            BuildTicketNotificationRecipients(ticket, actorStatus));
        await _telegram.NotifyStatusChangedAsync(ticket, oldStatus, status);
        FireAndForgetOkdeskSync(ticket.Id);
        return true;
    }

    public async Task<bool> UpdateAssigneeAsync(int id, string assignee, string[]? assignees)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return false;
        EnsureTicketModificationAccess(ticket);
        if (IsFieldEngineerRole(CurrentUserRole()))
            throw new UnauthorizedAccessException("Исполнителей меняйте через делегирование.");
        var oldAssignee = ticket.Assignee;
        ticket.Assignee = JoinAssignees(assignees, assignee);
        await _context.SaveChangesAsync();
        var oldSet = SplitAssignees(oldAssignee).ToHashSet(StringComparer.Ordinal);
        var newSet = SplitAssignees(ticket.Assignee).ToHashSet(StringComparer.Ordinal);
        var added = newSet.Where(u => !oldSet.Contains(u)).ToList();
        var actorAssign = CurrentUserId();
        if (added.Count > 0)
        {
            await _realtime.NotifyTicketChangedAsync(
                id,
                "assigned",
                actorAssign,
                $"Вас назначили по заявке #{id}",
                added);
        }
        else
        {
            await _realtime.NotifyTicketChangedAsync(
                id,
                "assigned",
                actorAssign,
                $"Исполнители заявки #{id} обновлены",
                BuildTicketNotificationRecipients(ticket, actorAssign));
        }
        if (!string.IsNullOrWhiteSpace(ticket.Assignee) && ticket.Assignee != oldAssignee)
            await _telegram.NotifyAssigneeChangedAsync(ticket, oldAssignee);
        FireAndForgetOkdeskSync(ticket.Id);
        return true;
    }

    public async Task<bool> UpdateLinksAsync(int id, string taskLinksJson)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return false;
        EnsureTicketModificationAccess(ticket);

        if (!string.IsNullOrWhiteSpace(taskLinksJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(taskLinksJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                        {
                            var url = urlProp.GetString();
                            if (!string.IsNullOrWhiteSpace(url) && !IsAllowedUrl(url))
                                throw new ArgumentException("Invalid URL in task links. Only HTTP and HTTPS are allowed.");
                        }
                    }
                }
            }
            catch (JsonException)
            {
                throw new ArgumentException("Invalid task links JSON.");
            }
        }

        ticket.TaskLinksJson = taskLinksJson ?? string.Empty;
        await _context.SaveChangesAsync();
        await _realtime.NotifyTicketChangedAsync(id);
        FireAndForgetOkdeskSync(ticket.Id);
        return true;
    }

    private static bool IsAllowedUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string[] ExtractTaskLinkUrls(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
            var urls = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                {
                    var url = urlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(url)) urls.Add(url);
                }
            }
            return urls.ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    public async Task<bool> UpdateTitleAsync(int id, string? title, string? alternativeTitle)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return false;
        EnsureTicketModificationAccess(ticket);
        if (title != null)
            ticket.Title = Sanitize(title);
        // Выездной инженер не меняет альтернативное название (только UI скрыт; API тоже игнорирует).
        if (alternativeTitle != null && !IsFieldEngineerRole(CurrentUserRole()))
            ticket.AlternativeTitle = Sanitize(alternativeTitle);
        await _context.SaveChangesAsync();
        await _realtime.NotifyTicketChangedAsync(id);
        FireAndForgetOkdeskSync(ticket.Id);
        return true;
    }

    public async Task<bool> UpdateProblemAsync(int id, string problem)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return false;
        EnsureTicketModificationAccess(ticket);
        ticket.Problem = Sanitize(problem);
        await _context.SaveChangesAsync();
        await _realtime.NotifyTicketChangedAsync(id);
        FireAndForgetOkdeskSync(ticket.Id);
        return true;
    }

    public async Task MarkTicketAsReadAsync(int ticketId, string userId)
    {
        var existing = await _context.UserTicketReadStates
            .FirstOrDefaultAsync(r => r.UserId == userId && r.TicketId == ticketId);
        if (existing != null)
        {
            existing.LastReadAt = DateTime.UtcNow;
        }
        else
        {
            _context.UserTicketReadStates.Add(new UserTicketReadState
            {
                UserId = userId,
                TicketId = ticketId,
                LastReadAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task<bool> DelegateAsync(int id, string delegatedFrom, string delegatedTo, string reason)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return false;
        await EnsureTicketInteractionAccessAsync(ticket);

        var currentUserId = CurrentUserId();
        if (!string.IsNullOrEmpty(currentUserId) && delegatedFrom != currentUserId)
            throw new UnauthorizedAccessException("You can only delegate on your own behalf.");

        ticket.DelegatedFrom = delegatedFrom;
        ticket.DelegatedTo = delegatedTo;
        ticket.DelegationReason = Sanitize(reason);
        ticket.DelegatedAt = DateTime.UtcNow;
        var oldAssignee = ticket.Assignee;
        var current = SplitAssignees(ticket.Assignee)
            .Where(a => a != delegatedFrom)
            .ToList();
        if (!current.Contains(delegatedTo))
            current.Add(delegatedTo);
        ticket.Assignee = string.Join(",", current);
        await _context.SaveChangesAsync();
        var oldSetDel = SplitAssignees(oldAssignee).ToHashSet(StringComparer.Ordinal);
        var newSetDel = SplitAssignees(ticket.Assignee).ToHashSet(StringComparer.Ordinal);
        var addedDel = newSetDel.Where(u => !oldSetDel.Contains(u)).ToList();
        var actorDel = CurrentUserId();
        if (addedDel.Count > 0)
        {
            await _realtime.NotifyTicketChangedAsync(
                id,
                "assigned",
                actorDel,
                $"Вас назначили по заявке #{id} (делегирование)",
                addedDel);
        }
        else
        {
            await _realtime.NotifyTicketChangedAsync(
                id,
                "assigned",
                actorDel,
                $"Заявка #{id}: делегирование",
                BuildTicketNotificationRecipients(ticket, actorDel));
        }
        if (!string.IsNullOrWhiteSpace(ticket.Assignee) && ticket.Assignee != oldAssignee)
            await _telegram.NotifyAssigneeChangedAsync(ticket, delegatedFrom);
        FireAndForgetOkdeskSync(ticket.Id);
        return true;
    }

    public async Task<bool> UpdateFieldsAsync(int id, string? priority, string? department, string? requestType)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return false;
        EnsureTicketModificationAccess(ticket);
        if (IsFieldEngineerRole(CurrentUserRole()))
            throw new UnauthorizedAccessException("Выездной инженер не меняет параметры заявки.");
        if (!string.IsNullOrWhiteSpace(priority)) ticket.Priority = Sanitize(priority);
        if (!string.IsNullOrWhiteSpace(department)) ticket.Department = Sanitize(department);
        if (!string.IsNullOrWhiteSpace(requestType)) ticket.RequestType = Sanitize(requestType);
        await _context.SaveChangesAsync();
        await _realtime.NotifyTicketChangedAsync(id);
        FireAndForgetOkdeskSync(ticket.Id);
        return true;
    }

    public async Task<IEnumerable<FieldReportDto>> GetFieldReportsAsync(int id)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found");
        EnsureTicketAccess(ticket);

        return await _context.FieldReports
            .Where(r => r.TicketId == id)
            .OrderByDescending(r => r.VisitDate)
            .Select(r => new FieldReportDto(
                r.Id, r.TicketId, r.EngineerName, r.VisitDate,
                r.ActionType, r.EquipmentType, r.EquipmentSerial,
                r.EquipmentStatus, r.WorkDone, r.TransferredTo))
            .ToListAsync();
    }

    public async Task<FieldReportDto> AddFieldReportAsync(int id, CreateFieldReportRequest request, string currentUserName)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found");
        await EnsureTicketInteractionAccessAsync(ticket);

        var report = new FieldReport
        {
            TicketId = id,
            EngineerName = currentUserName,
            VisitDate = request.VisitDate?.ToUniversalTime() ?? DateTime.UtcNow,
            ActionType = request.ActionType,
            EquipmentType = request.EquipmentType,
            EquipmentSerial = request.EquipmentSerial,
            EquipmentStatus = request.EquipmentStatus,
            WorkDone = Sanitize(request.WorkDone),
            TransferredTo = Sanitize(request.TransferredTo)
        };

        _context.FieldReports.Add(report);
        await _context.SaveChangesAsync();
        var actorReport = CurrentUserId();
        var actionLabel = string.IsNullOrWhiteSpace(report.ActionType) ? "акт выезда" : report.ActionType.Trim();
        await _realtime.NotifyTicketChangedAsync(
            id,
            "field_report",
            actorReport,
            $"Акт к заявке #{id}: {actionLabel}",
            BuildTicketNotificationRecipients(ticket, actorReport));
        await _telegram.NotifyFieldReportAddedAsync(ticket, report);

        return new FieldReportDto(
            report.Id, report.TicketId, report.EngineerName, report.VisitDate,
            report.ActionType, report.EquipmentType, report.EquipmentSerial,
            report.EquipmentStatus, report.WorkDone, report.TransferredTo);
    }

    public async Task<FieldReportDto> UpdateFieldReportAsync(int ticketId, int reportId, UpdateFieldReportRequest request)
    {
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found");
        EnsureTicketModificationAccess(ticket);
        var report = await _context.FieldReports.FirstOrDefaultAsync(r => r.Id == reportId && r.TicketId == ticketId);
        if (report == null) throw new KeyNotFoundException("Report not found");

        if (request.EngineerName != null) report.EngineerName = Sanitize(request.EngineerName);
        if (request.VisitDate.HasValue) report.VisitDate = request.VisitDate.Value.ToUniversalTime();
        if (request.ActionType != null) report.ActionType = request.ActionType.Trim();
        if (request.EquipmentType != null) report.EquipmentType = request.EquipmentType.Trim();
        if (request.EquipmentSerial != null) report.EquipmentSerial = request.EquipmentSerial.Trim();
        if (request.EquipmentStatus != null) report.EquipmentStatus = request.EquipmentStatus.Trim();
        if (request.WorkDone != null) report.WorkDone = Sanitize(request.WorkDone);
        if (request.TransferredTo != null) report.TransferredTo = Sanitize(request.TransferredTo);

        await _context.SaveChangesAsync();
        var actor = CurrentUserId();
        await _realtime.NotifyTicketChangedAsync(
            ticketId,
            "field_report",
            actor,
            $"Акт обновлён (заявка #{ticketId})",
            BuildTicketNotificationRecipients(ticket, actor));

        return new FieldReportDto(
            report.Id, report.TicketId, report.EngineerName, report.VisitDate,
            report.ActionType, report.EquipmentType, report.EquipmentSerial,
            report.EquipmentStatus, report.WorkDone, report.TransferredTo);
    }

    public async Task<IEnumerable<CommentDto>> GetCommentsAsync(int id)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found");
        EnsureTicketAccess(ticket);

        var comments = await _context.TicketComments
            .Where(c => c.TicketId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var userIds = comments
            .Where(c => !string.IsNullOrEmpty(c.AuthorUserId))
            .Select(c => c.AuthorUserId!)
            .Distinct()
            .ToList();

        var employeeByUser = await _context.Employees.AsNoTracking()
            .Where(e => userIds.Contains(e.UserId))
            .ToDictionaryAsync(e => e.UserId, e => e);

        return comments.Select(c =>
        {
            Employee? emp = null;
            if (!string.IsNullOrEmpty(c.AuthorUserId))
                employeeByUser.TryGetValue(c.AuthorUserId, out emp);

            var av = emp?.AvatarUrl ?? string.Empty;
            var name = (c.AuthorName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name) || string.Equals(name, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                var fn = emp?.FullName?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(fn))
                    name = fn;
                else if (string.IsNullOrEmpty(name))
                    name = "Unknown";
            }

            return new CommentDto(c.Id, c.TicketId, name, c.AuthorRole, c.Text, c.IsInternal, c.CreatedAt, av, c.AuthorUserId, ReactionJsonParser.Parse(c.ReactionsJson));
        }).ToList();
    }

    public async Task<CommentDto> AddCommentAsync(int id, CreateCommentRequest request, string currentUserId, string currentUserName, string currentUserRole)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found");
        await EnsureTicketInteractionAccessAsync(ticket);

        if (request.OkdeskId.HasValue)
        {
            var existingComment = await _context.TicketComments.FirstOrDefaultAsync(c => c.OkdeskId == request.OkdeskId);
            if (existingComment != null)
            {
                var av0 = await GetEmployeeAvatarUrlAsync(existingComment.AuthorUserId);
                return new CommentDto(existingComment.Id, existingComment.TicketId, existingComment.AuthorName,
                    existingComment.AuthorRole, existingComment.Text, existingComment.IsInternal, existingComment.CreatedAt, av0,
                    existingComment.AuthorUserId, ReactionJsonParser.Parse(existingComment.ReactionsJson));
            }
        }

        var comment = new TicketComment
        {
            TicketId = id,
            AuthorName = !string.IsNullOrWhiteSpace(request.AuthorName) ? request.AuthorName : currentUserName,
            AuthorUserId = !string.IsNullOrWhiteSpace(request.AuthorUserId) ? request.AuthorUserId : currentUserId,
            AuthorRole = !string.IsNullOrWhiteSpace(request.AuthorRole) ? request.AuthorRole : currentUserRole,
            Text = Sanitize(request.Text),
            IsInternal = request.IsInternal,
            OkdeskId = request.OkdeskId,
            CreatedAt = DateTime.TryParse(request.CreatedAt, out var cdt) ? cdt.ToUniversalTime() : DateTime.UtcNow
        };

        _context.TicketComments.Add(comment);

        var statusBeforeComment = ticket.Status;
        var bumpToInProgress = IsStaff() && await IsTicketInOpenStateAsync(ticket.Status);
        if (bumpToInProgress)
        {
            ticket.Status = InProgressStatus;
            ticket.ClosedAt = null;
        }

        await _context.SaveChangesAsync();
        var textPreview = comment.Text.Trim();
        if (textPreview.Length > 140)
            textPreview = textPreview[..140] + "…";
        await _realtime.NotifyTicketChangedAsync(
            id,
            "comment",
            currentUserId,
            $"{comment.AuthorName}: {textPreview}",
            BuildTicketNotificationRecipients(ticket, currentUserId));
        if (bumpToInProgress)
            await _telegram.NotifyStatusChangedAsync(ticket, statusBeforeComment, InProgressStatus);

        FireAndForgetOkdeskCommentSync(ticket.Id, comment.Id, comment.AuthorUserId);
        if (bumpToInProgress)
            FireAndForgetOkdeskSync(ticket.Id);

        var av = await GetEmployeeAvatarUrlAsync(comment.AuthorUserId);
        return new CommentDto(
            comment.Id, comment.TicketId, comment.AuthorName, comment.AuthorRole,
            comment.Text, comment.IsInternal, comment.CreatedAt, av, comment.AuthorUserId,
            ReactionJsonParser.Parse(comment.ReactionsJson));
    }

    public async Task<CommentDto> ToggleCommentReactionAsync(int ticketId, int commentId, string userId, string userName, string emoji)
    {
        var ticket = await _context.Tickets.FindAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");
        await EnsureTicketInteractionAccessAsync(ticket);

        var comment = await _context.TicketComments.FirstOrDefaultAsync(c => c.Id == commentId && c.TicketId == ticketId)
            ?? throw new KeyNotFoundException("Comment not found");

        var reactions = ReactionJsonParser.Parse(comment.ReactionsJson);
        var existing = reactions.FirstOrDefault(r => r.Emoji == emoji && r.UserId == userId);
        if (existing != null)
        {
            reactions.Remove(existing);
        }
        else
        {
            reactions.Add(new ReactionDto(emoji, userId, userName));
        }
        comment.ReactionsJson = ReactionJsonParser.Serialize(reactions);
        await _context.SaveChangesAsync();

        var av = await GetEmployeeAvatarUrlAsync(comment.AuthorUserId);
        var authorName = comment.AuthorName;
        if (!string.IsNullOrWhiteSpace(comment.AuthorUserId))
        {
            var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == comment.AuthorUserId);
            if (emp != null) authorName = emp.FullName;
        }
        if (string.IsNullOrEmpty(authorName)) authorName = "Unknown";

        return new CommentDto(
            comment.Id, comment.TicketId, authorName, comment.AuthorRole,
            comment.Text, comment.IsInternal, comment.CreatedAt, av, comment.AuthorUserId,
            reactions);
    }

    public async Task<Ticket> MigrateTicketAsync(MigrateTicketRequest request)
    {
        if (!IsStaff()) throw new UnauthorizedAccessException("Only staff can migrate tickets.");

        if (request.OkdeskId.HasValue)
        {
            var existing = await _context.Tickets.FirstOrDefaultAsync(t => t.OkdeskId == request.OkdeskId);
            if (existing != null) return existing;
        }

        var clientId = request.ClientId ?? await _context.Clients.Select(c => c.Id).FirstOrDefaultAsync();
        var ticket = new Ticket
        {
            Title = Sanitize(request.Title),
            Problem = Sanitize(request.Problem),
            Status = request.Status,
            Priority = request.Priority,
            Department = request.Department ?? "1 линия",
            RequestType = request.RequestType ?? "",
            ClientId = clientId > 0 ? clientId : 1,
            ObjectId = request.ObjectId,
            Assignee = string.IsNullOrWhiteSpace(request.Assignee) ? string.Empty : request.Assignee.Trim(),
            OkdeskId = request.OkdeskId,
            IsFromOkdesk = request.IsFromOkdesk,
            CreatedAt = DateTime.TryParse(request.CreatedAt, out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow,
            CreatedByUserId = CurrentUserId() ?? string.Empty
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();
        return ticket;
    }

    public async Task<Ticket> MigrateRepairTicketAsync(MigrateRepairTicketRequest request)
    {
        if (!IsStaff()) throw new UnauthorizedAccessException("Only staff can migrate repair tickets.");

        if (request.OkdeskId.HasValue)
        {
            var existing = await _context.Tickets.FirstOrDefaultAsync(t => t.OkdeskId == request.OkdeskId);
            if (existing != null) return existing;
        }

        if (string.IsNullOrWhiteSpace(request.Month) || !DateTime.TryParse(request.Month.Trim() + "-01", out var mdt))
            throw new ArgumentException("Invalid month. Expected YYYY-MM.");

        var createdAt = new DateTime(mdt.Year, mdt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var requestedClientName = (request.ClientName ?? string.Empty).Trim();

        var company =
            !string.IsNullOrWhiteSpace(requestedClientName)
                ? await _context.Companies.FirstOrDefaultAsync(c => c.Name.ToLower() == requestedClientName.ToLower())
                : null;

        if (company == null && !string.IsNullOrWhiteSpace(requestedClientName))
        {
            var needle = requestedClientName.ToLower();
            company = await _context.Companies.FirstOrDefaultAsync(c => c.Name.ToLower().Contains(needle));
        }
        if (company == null)
        {
            foreach (var kv in RepairCompanyAliases)
            {
                if (requestedClientName.ToLower().Contains(kv.Key))
                {
                    foreach (var alias in kv.Value)
                    {
                        company = await _context.Companies.FirstOrDefaultAsync(c => c.Name.ToLower().Contains(alias));
                        if (company != null) break;
                    }
                    if (company != null) break;
                }
            }
        }

        var clientId = company?.Id ?? await _context.Companies.Select(c => c.Id).FirstOrDefaultAsync();
        if (clientId <= 0) clientId = await _context.Clients.Select(c => c.Id).FirstOrDefaultAsync();
        if (clientId <= 0) clientId = 1;

        var clientName = (company?.Name ?? requestedClientName).Trim();
        var equipType = (request.EquipmentType ?? string.Empty).Trim();
        var repType = (request.RepairType ?? string.Empty).Trim();
        var restaurant = (request.Restaurant ?? string.Empty).Trim();
        var title = $"Ремонт ({equipType}) — {restaurant}";

        var ticket = new Ticket
        {
            Title = Sanitize(title),
            Problem = string.Empty,
            Status = "Закрыт",
            Priority = "Средний",
            Department = "Ремонт / сервис",
            RequestType = "Ремонт",
            CreatedAt = createdAt,
            ClientId = clientId,
            ObjectId = null,
            Assignee = "Импорт",
            OkdeskId = request.OkdeskId,
            IsRepair = true,
            EquipmentId = null,
            RepairType = Sanitize(repType),
            RepairCost = request.Cost,
            RepairClientName = Sanitize(clientName),
            RepairEquipmentType = Sanitize(equipType),
            RepairEquipmentName = string.Empty,
            RepairSerialNumber = string.Empty,
            RepairFundStatus = string.Empty,
            RepairLocation = Sanitize(restaurant),
            RepairFaults = string.Empty,
            RepairNotes = "Импорт из Google Sheet",
            CreatedByUserId = CurrentUserId() ?? string.Empty
        };

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();
        return ticket;
    }

    public async Task<Ticket> CreateTicketAsync(CreateTicketRequest request)
    {
        var resolvedClientId = request.ClientId ?? 0;

        CoordinatorBriefPayload? brief = null;
        var rawBrief = request.CoordinatorBriefJson?.Trim();
        if (!string.IsNullOrEmpty(rawBrief))
        {
            try
            {
                brief = JsonSerializer.Deserialize<CoordinatorBriefPayload>(rawBrief, BriefJsonOpts);
            }
            catch
            {
                brief = null;
            }
        }

        if (resolvedClientId <= 0 && brief != null && !string.IsNullOrWhiteSpace(brief.LegalEntity))
        {
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Name.ToLower() == brief.LegalEntity!.Trim().ToLower());
            if (company == null)
            {
                var needle = brief.LegalEntity!.Trim().ToLower();
                company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Name.ToLower().Contains(needle));
            }
            if (company != null)
                resolvedClientId = company.Id;
        }

        var resolvedObjectId = request.ObjectId;
        if (!resolvedObjectId.HasValue && brief?.ObjectId > 0)
            resolvedObjectId = brief.ObjectId;

        var tail = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.SoftwareName))
            tail.Add($"ПО: {request.SoftwareName}");
        if (request.DesiredAt.HasValue)
            tail.Add($"Желаемый срок (календарь): {request.DesiredAt:dd.MM.yyyy HH:mm}");
        if (!string.IsNullOrWhiteSpace(request.Details))
            tail.Add(request.Details!);

        string problem;
        var briefJsonStored = string.Empty;
        string taskLinksJsonForTicket = string.Empty;
        if (brief is { } b && b.HasAnyContent())
        {
            var empMap = await LoadEmployeeNameMapAsync();
            string ResolveUid(string uid) =>
                string.IsNullOrWhiteSpace(uid) ? uid : (empMap.TryGetValue(uid.Trim(), out var nm) ? nm : uid.Trim());
            var head = CoordinatorBriefFormatter.ToProblemText(b, ResolveUid);
            briefJsonStored = rawBrief ?? string.Empty;
            if (tail.Count > 0)
                problem = head + "\n\n---\n" + string.Join("\n", tail);
            else
                problem = head;

            if (b.TaskLinks is { Count: > 0 } tl)
            {
                var linkDtos = tl
                    .Where(l => !string.IsNullOrWhiteSpace(l.Url) || !string.IsNullOrWhiteSpace(l.Number) ||
                                !string.IsNullOrWhiteSpace(l.Comment))
                    .Select(l => new
                    {
                        url = (l.Url ?? "").Trim(),
                        number = (l.Number ?? "").Trim(),
                        comment = (l.Comment ?? "").Trim()
                    })
                    .ToList();
                if (linkDtos.Count > 0)
                    taskLinksJsonForTicket = JsonSerializer.Serialize(linkDtos, BriefJsonOpts);
            }
        }
        else
        {
            problem = tail.Count > 0 ? string.Join("\n", tail) : request.RequestType;
        }

        var ticket = new Ticket
        {
            Title = Sanitize(request.Title),
            Problem = Sanitize(problem),
            RequestType = request.RequestType,
            Priority = request.Priority,
            Department = request.Department ?? string.Empty,
            Status = "Открыт",
            CreatedAt = DateTime.UtcNow,
            ClientId = resolvedClientId,
            ObjectId = resolvedObjectId,
            Assignee = JoinAssignees(request.Assignees, request.Assignee),
            CoordinatorBriefJson = briefJsonStored,
            TaskLinksJson = taskLinksJsonForTicket,
            CreatedByRole = CurrentUserRole(),
            CreatedByUserId = CurrentUserId() ?? string.Empty
        };

        Equipment? eq = null;
        var isRepair = request.IsRepair ?? false;
        if (isRepair)
        {
            ticket.IsRepair = true;
            ticket.RepairType = (request.RepairType ?? string.Empty).Trim();
            ticket.RepairCost = request.RepairCost;

            if (request.EquipmentId.HasValue)
            {
                eq = await _context.Equipment.FirstOrDefaultAsync(e => e.Id == request.EquipmentId.Value);
                if (eq == null)
                    throw new ArgumentException("equipmentId not found");

                ticket.EquipmentId = eq.Id;
                ticket.RepairClientName = (eq.ClientName ?? string.Empty).Trim();
                ticket.RepairEquipmentName = (eq.Name ?? string.Empty).Trim();
                ticket.RepairSerialNumber = (eq.SerialNumber ?? string.Empty).Trim();
                ticket.RepairLocation = (eq.Location ?? string.Empty).Trim();
                ticket.RepairFundStatus = (eq.FundStatus ?? string.Empty).Trim();
                ticket.RepairEquipmentType = (eq.EquipmentType ?? string.Empty).Trim();

                var faultsSnap = (request.RepairFaults ?? string.Empty).Trim();
                var notesSnap = (request.RepairNotes ?? string.Empty).Trim();
                ticket.RepairFaults = Sanitize(faultsSnap.Length > 0 ? faultsSnap : (eq.Faults ?? string.Empty).Trim());
                ticket.RepairNotes = Sanitize(notesSnap.Length > 0 ? notesSnap : (eq.Notes ?? string.Empty).Trim());
            }
            else
            {
                ticket.RepairEquipmentType = (request.EquipmentType ?? string.Empty).Trim();
                ticket.RepairEquipmentName = (request.EquipmentTypeLabel ?? request.EquipmentType ?? string.Empty).Trim();
                ticket.RepairFaults = Sanitize((request.RepairFaults ?? string.Empty).Trim());
                ticket.RepairNotes = Sanitize((request.RepairNotes ?? string.Empty).Trim());
            }
        }

        // Track equipment movement from replacement fund (repair or brief)
        var equipmentIdsToMove = new List<int>();
        if (request.EquipmentId.HasValue)
            equipmentIdsToMove.Add(request.EquipmentId.Value);
        if (brief?.Equipment != null)
        {
            foreach (var item in brief.Equipment)
            {
                if (item.EquipmentId.HasValue && !equipmentIdsToMove.Contains(item.EquipmentId.Value))
                    equipmentIdsToMove.Add(item.EquipmentId.Value);
            }
        }

        foreach (var eid in equipmentIdsToMove)
        {
            var equipment = eq != null && eq.Id == eid ? eq : await _context.Equipment.FirstOrDefaultAsync(e => e.Id == eid);
            if (equipment != null)
            {
                equipment.Status = "выдано";
                equipment.IssueDate = DateTime.UtcNow;
                equipment.IssuedTo = !string.IsNullOrWhiteSpace(ticket.Assignee)
                    ? ticket.Assignee
                    : (CurrentUserId() ?? string.Empty);

                if (ticket.ClientId > 0)
                {
                    var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == ticket.ClientId);
                    if (company != null)
                        equipment.ClientName = company.Name;
                }

                if (ticket.ObjectId.HasValue)
                {
                    var serviceObject = await _context.ServiceObjects.FirstOrDefaultAsync(o => o.Id == ticket.ObjectId.Value);
                    if (serviceObject != null)
                        equipment.Location = !string.IsNullOrWhiteSpace(serviceObject.Address)
                            ? serviceObject.Address
                            : serviceObject.Name;
                }
            }
        }

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();
        var creatorId = CurrentUserId();
        var assigneeIds = SplitAssignees(ticket.Assignee);
        IReadOnlyList<string>? createRecipients = assigneeIds.Length > 0 ? assigneeIds : null;
        await _realtime.NotifyTicketChangedAsync(
            ticket.Id,
            "created",
            creatorId,
            $"Новая заявка #{ticket.Id}: {ticket.Title}",
            createRecipients);
        await _telegram.NotifyNewTicketAsync(ticket);
        if (!string.IsNullOrWhiteSpace(ticket.Assignee))
            await _telegram.NotifyAssigneeChangedAsync(ticket);
        return ticket;
    }

    // Helpers

    private static string ResolveNameOrKeep(string? val, Dictionary<string, string>? map)
    {
        if (string.IsNullOrWhiteSpace(val)) return string.Empty;
        if (map != null && map.TryGetValue(val, out var name)) return name;
        return val;
    }

    private static string[] SplitAssignees(string s) =>
        string.IsNullOrWhiteSpace(s) ? [] :
        s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Получатели браузерных уведомлений: исполнители + автор заявки, без инициатора события.</summary>
    private List<string> BuildTicketNotificationRecipients(Ticket ticket, string? excludeUserId)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in SplitAssignees(ticket.Assignee))
        {
            if (!string.IsNullOrWhiteSpace(raw)) set.Add(raw);
        }
        if (!string.IsNullOrEmpty(ticket.CreatedByUserId))
            set.Add(ticket.CreatedByUserId);
        if (!string.IsNullOrEmpty(excludeUserId))
            set.Remove(excludeUserId);
        return set.ToList();
    }

    private static string JoinAssignees(string[]? arr, string? fallback) =>
        arr != null && arr.Length > 0
            ? string.Join(",", arr.Select(a => a.Trim()).Where(a => a.Length > 0))
            : (fallback ?? string.Empty);

    private TicketDto MapTicket(Ticket t, string clientName, ServiceObject? o,
        Dictionary<string, string>? employeeNameByUserId = null, int subtaskCount = 0,
        string[]? commentTexts = null, string[]? taskLinkUrls = null, bool hasUnread = false)
    {
        var rawIds = SplitAssignees(t.Assignee);
        var resolvedNames = employeeNameByUserId != null
            ? rawIds.Select(id => employeeNameByUserId.TryGetValue(id, out var name) ? name : id).ToArray()
            : rawIds;

        string[]? briefKnowledgeableUserIds = null;
        if (!string.IsNullOrWhiteSpace(t.CoordinatorBriefJson))
        {
            try
            {
                var brief = JsonSerializer.Deserialize<CoordinatorBriefPayload>(t.CoordinatorBriefJson, BriefJsonOpts);
                briefKnowledgeableUserIds = brief?.KnowledgeableUserIds?.ToArray();
            }
            catch { /* ignore parse errors */ }
        }

        if (employeeNameByUserId != null && briefKnowledgeableUserIds != null)
        {
            briefKnowledgeableUserIds = briefKnowledgeableUserIds
                .Select(id => employeeNameByUserId.TryGetValue(id, out var name) ? name : id)
                .ToArray();
        }

        return new TicketDto(
            t.Id, t.CreatedAt, t.ClosedAt, t.Assignee, resolvedNames, WebUtility.HtmlDecode(t.Title),
            clientName,
            WebUtility.HtmlDecode(t.Problem), t.Status, t.Priority, t.Department, t.RequestType,
            o?.Name ?? string.Empty,
            t.ObjectId,
            t.ClientId,
            t.OkdeskId,
            t.CoordinatorBriefJson ?? string.Empty,
            t.IsRepair,
            t.EquipmentId,
            t.RepairType ?? string.Empty,
            t.RepairCost,
            t.RepairClientName ?? string.Empty,
            t.RepairEquipmentName ?? string.Empty,
            t.RepairSerialNumber ?? string.Empty,
            t.RepairLocation ?? string.Empty,
            WebUtility.HtmlDecode(t.RepairFaults) ?? string.Empty,
            WebUtility.HtmlDecode(t.RepairNotes) ?? string.Empty,
            t.RepairFundStatus ?? string.Empty,
            t.RepairEquipmentType ?? string.Empty,
            t.TaskLinksJson ?? string.Empty,
            WebUtility.HtmlDecode(t.AlternativeTitle) ?? string.Empty,
            t.CreatedByRole ?? string.Empty,
            ResolveNameOrKeep(t.DelegatedFrom, employeeNameByUserId),
            ResolveNameOrKeep(t.DelegatedTo, employeeNameByUserId),
            t.DelegationReason ?? string.Empty,
            t.DelegatedAt,
            rawIds,
            subtaskCount,
            briefKnowledgeableUserIds,
            commentTexts,
            taskLinkUrls,
            t.IsFromOkdesk,
            hasUnread
        );
    }

    private string ResolveClientName(int clientId, Dictionary<int, Client> clients, Dictionary<int, Company> companies)
    {
        if (clients.TryGetValue(clientId, out var cl) && !string.IsNullOrWhiteSpace(cl.FullName))
            return cl.FullName;
        if (companies.TryGetValue(clientId, out var co) && !string.IsNullOrWhiteSpace(co.Name))
            return co.Name;
        return string.Empty;
    }

    private static string ResolveTicketClientName(Ticket t, Dictionary<int, Client> clients, Dictionary<int, Company> companies)
    {
        if (t.IsRepair && !string.IsNullOrWhiteSpace(t.RepairClientName))
            return t.RepairClientName.Trim();
        return string.Empty;
    }

    private async Task<Dictionary<string, string>> LoadEmployeeNameMapAsync()
    {
        var map = await _context.Employees.AsNoTracking()
            .Where(e => !string.IsNullOrEmpty(e.UserId))
            .ToDictionaryAsync(
                e => e.UserId,
                e => !string.IsNullOrWhiteSpace(e.FullName) ? e.FullName.Trim() : e.UserId);

        var accounts = await _context.UserAccounts.AsNoTracking()
            .Where(a => a.Role != "client" && !string.IsNullOrEmpty(a.UserId))
            .Select(a => new { a.UserId, a.FullName })
            .ToListAsync();

        foreach (var a in accounts)
        {
            if (map.ContainsKey(a.UserId)) continue;
            var name = (a.FullName ?? string.Empty).Trim();
            map[a.UserId] = !string.IsNullOrEmpty(name) ? name : a.UserId;
        }

        return map;
    }

    private async Task<string> GetEmployeeAvatarUrlAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return string.Empty;
        var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId);
        return emp?.AvatarUrl ?? string.Empty;
    }
}
