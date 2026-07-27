using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Services.Implementations;

public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(AppDbContext context, IHttpClientFactory httpClientFactory, ILogger<TelegramNotificationService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyNewTicketAsync(Ticket ticket)
    {
        var extra = await ResolveTicketExtrasAsync(ticket);
        await NotifyAsync("new_ticket", ticket, extra: extra);
    }

    public async Task NotifyStatusChangedAsync(Ticket ticket, string oldStatus, string newStatus)
    {
        var extra = await ResolveTicketExtrasAsync(ticket);
        await NotifyAsync("status_changed", ticket, oldStatus, newStatus, extra: extra);
    }

    public async Task NotifyFieldReportAddedAsync(Ticket ticket, FieldReport report)
    {
        var extra = await ResolveTicketExtrasAsync(ticket);
        extra["fieldReport"] = $"{report.ActionType} — {report.EngineerName} ({report.VisitDate:dd.MM.yyyy})";
        await NotifyAsync("field_report_added", ticket, report: report, extra: extra);
    }

    public async Task NotifySubtaskCreatedAsync(Ticket ticket, TicketSubtask subtask)
    {
        var extra = await ResolveTicketExtrasAsync(ticket);
        extra["createdByName"] = await ResolveEmployeeNameAsync(subtask.CreatedByUserId);
        extra["subtask"] = $"{subtask.Title} ({subtask.Status})";
        await NotifyAsync("subtask_created", ticket, subtask: subtask, extra: extra);
    }

    public async Task NotifyAssigneeChangedAsync(Ticket ticket, string? oldAssigneeUserId = null)
    {
        var extra = await ResolveTicketExtrasAsync(ticket);
        if (!string.IsNullOrWhiteSpace(oldAssigneeUserId))
            extra["oldAssignee"] = await ResolveEmployeeNameAsync(oldAssigneeUserId);
        await NotifyAsync("assignee_changed", ticket, extra: extra);
    }

    public async Task NotifyEventAsync(Ticket ticket, string eventType, Dictionary<string, string>? extra = null)
    {
        var merged = await ResolveTicketExtrasAsync(ticket);
        if (extra != null)
        {
            foreach (var kv in extra)
                merged[kv.Key] = kv.Value;
        }
        await NotifyAsync(eventType, ticket, extra: merged);
    }

    public async Task<bool> TestTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://api.telegram.org/bot{token}/getMe");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task NotifyAsync(string eventType, Ticket ticket, string? oldStatus = null, string? newStatus = null, FieldReport? report = null, TicketSubtask? subtask = null, Dictionary<string, string>? extra = null)
    {
        var settings = await GetEnabledSettingsAsync(eventType);
        _logger.LogDebug("Telegram NotifyAsync: event={EventType}, settingsCount={Count}, ticketId={TicketId}", eventType, settings.Count, ticket.Id);
        if (!settings.Any()) return;

        foreach (var setting in settings)
        {
            var chatId = await ResolveChatIdAsync(setting, ticket);
            _logger.LogDebug("Telegram NotifyAsync: resolving chatId for settingId={SettingId}, targetType={TargetType}, chatId={ChatId}", setting.Id, setting.TargetType, chatId ?? "(null)");
            if (string.IsNullOrWhiteSpace(chatId)) continue;

            var message = FormatMessage(setting.Template, ticket, oldStatus, newStatus, report, subtask, extra, _logger);
            _logger.LogDebug("Telegram NotifyAsync: sending message to {ChatId}, length={Length}", chatId, message.Length);
            try { await SendMessageAsync(chatId, message, ticket.Id); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to send Telegram {EventType} notification", eventType); }
        }
    }

    private async Task<Dictionary<string, string>> ResolveTicketExtrasAsync(Ticket ticket)
    {
        var extra = new Dictionary<string, string>();

        var clientName = string.Empty;
        if (ticket.ClientId > 0)
        {
            var client = await _context.Clients.FindAsync(ticket.ClientId);
            if (client != null)
            {
                clientName = client.FullName;
                var company = await _context.Companies.FindAsync(client.CompanyId);
                if (company != null && string.IsNullOrWhiteSpace(clientName))
                    clientName = company.Name;
            }
        }
        extra["clientName"] = clientName;

        var objectName = string.Empty;
        if (ticket.ObjectId.HasValue)
        {
            var obj = await _context.ServiceObjects.FindAsync(ticket.ObjectId.Value);
            if (obj != null) objectName = obj.Name;
        }
        extra["objectName"] = objectName;

        extra["assignee"] = ticket.Assignee ?? string.Empty;
        extra["createdAt"] = ticket.CreatedAt.ToString("dd.MM.yyyy HH:mm");

        return extra;
    }

    private async Task<string> ResolveEmployeeNameAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return string.Empty;
        var emp = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
        return emp?.FullName ?? userId;
    }

    private async Task<List<TelegramBotSetting>> GetEnabledSettingsAsync(string eventType)
    {
        var token = await GetBotTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return new List<TelegramBotSetting>();

        var settings = await _context.TelegramBotSettings
            .Where(t => t.EventType == eventType && t.IsEnabled)
            .ToListAsync();

        // Fallback to new_ticket template for field_report_added and subtask_created
        if (!settings.Any() && (eventType == "field_report_added" || eventType == "subtask_created"))
        {
            settings = await _context.TelegramBotSettings
                .Where(t => t.EventType == "new_ticket" && t.IsEnabled)
                .ToListAsync();
        }

        return settings;
    }

    private async Task<string?> GetBotTokenAsync()
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "telegram_bot_token");
        return setting?.Value;
    }

    private async Task<string?> ResolveChatIdAsync(TelegramBotSetting setting, Ticket ticket)
    {
        if (setting.TargetType == "assignee")
        {
            var assigneeId = ticket.Assignee?.Split(',').FirstOrDefault(a => !string.IsNullOrWhiteSpace(a))?.Trim();
            if (string.IsNullOrWhiteSpace(assigneeId)) return null;
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == assigneeId);
            if (string.IsNullOrWhiteSpace(emp?.TelegramChatId))
            {
                emp = await _context.Employees.FirstOrDefaultAsync(e => e.FullName == assigneeId);
            }
            return emp?.TelegramChatId;
        }

        if (setting.TargetType == "reporter")
        {
            var reporter = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == ticket.CreatedByUserId);
            return reporter?.TelegramChatId;
        }

        if (setting.TargetType == "employee")
        {
            if (string.IsNullOrWhiteSpace(setting.TargetEmployeeId)) return null;
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == setting.TargetEmployeeId);
            if (!string.IsNullOrWhiteSpace(emp?.TelegramChatId)) return emp.TelegramChatId;
            return setting.ChatId;
        }

        return setting.ChatId;
    }

    private async Task<string> BuildTicketUrlAsync(int ticketId)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "frontend_url");
        var baseUrl = setting?.Value;
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://localhost:3000";
        baseUrl = baseUrl.TrimEnd('/');
        return $"{baseUrl}/tickets/{ticketId}";
    }

    private async Task SendMessageAsync(string chatId, string text, int ticketId)
    {
        var token = await GetBotTokenAsync();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(text))
            return;

        var ticketUrl = await BuildTicketUrlAsync(ticketId);
        var markup = new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    new { text = "🔗 Открыть заявку", url = ticketUrl }
                }
            }
        };

        var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["chat_id"] = chatId,
            ["text"] = text,
            ["parse_mode"] = "HTML",
            ["reply_markup"] = JsonSerializer.Serialize(markup, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        });

        var response = await client.PostAsync($"https://api.telegram.org/bot{token}/sendMessage", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Telegram API error: {Status} {Body}", response.StatusCode, errorBody);
        }
    }

    private static string FormatMessage(string template, Ticket ticket, string? oldStatus = null, string? newStatus = null, FieldReport? report = null, TicketSubtask? subtask = null, Dictionary<string, string>? extra = null, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(template))
            template = "📋 <b>Заявка #{id}</b>\n📌 {title}";

        template = template.Replace("\\r\\n", "\n").Replace("\\r", "\n").Replace("\\n", "\n");

        var values = new Dictionary<string, string>
        {
            ["id"] = ticket.Id.ToString(),
            ["title"] = EscapeHtmlValue(ticket.Title),
            ["status"] = EscapeHtmlValue(newStatus ?? ticket.Status),
            ["priority"] = EscapeHtmlValue(ticket.Priority),
            ["department"] = EscapeHtmlValue(ticket.Department),
            ["requestType"] = EscapeHtmlValue(ticket.RequestType),
            ["oldStatus"] = EscapeHtmlValue(oldStatus ?? string.Empty),
        };

        if (extra != null)
        {
            values["clientName"] = EscapeHtmlValue(extra.GetValueOrDefault("clientName"));
            values["objectName"] = EscapeHtmlValue(extra.GetValueOrDefault("objectName"));
            values["assignee"] = EscapeHtmlValue(extra.GetValueOrDefault("assignee"));
            values["oldAssignee"] = EscapeHtmlValue(extra.GetValueOrDefault("oldAssignee"));
            values["createdAt"] = EscapeHtmlValue(extra.GetValueOrDefault("createdAt"));
            if (extra.TryGetValue("message", out var messageVal))
                values["message"] = EscapeHtmlValue(messageVal);
            if (extra.TryGetValue("slaWindow", out var sw))
                values["slaWindow"] = EscapeHtmlValue(sw);
        }

        if (report != null)
        {
            values["engineerName"] = EscapeHtmlValue(report.EngineerName);
            values["visitDate"] = EscapeHtmlValue(report.VisitDate.ToString("dd.MM.yyyy HH:mm"));
            values["actionType"] = EscapeHtmlValue(report.ActionType);
            values["equipmentType"] = EscapeHtmlValue(report.EquipmentType);
            values["equipmentSerial"] = EscapeHtmlValue(report.EquipmentSerial);
            values["equipmentStatus"] = EscapeHtmlValue(report.EquipmentStatus);
            values["workDone"] = EscapeHtmlValue(report.WorkDone);
            values["transferredTo"] = EscapeHtmlValue(report.TransferredTo);
        }

        if (subtask != null)
        {
            values["subtaskTitle"] = EscapeHtmlValue(subtask.Title);
            values["subtaskDescription"] = EscapeHtmlValue(subtask.Description);
            values["subtaskStatus"] = EscapeHtmlValue(subtask.Status);

            if (extra != null && extra.TryGetValue("createdByName", out var createdByName))
                values["createdByName"] = EscapeHtmlValue(createdByName);
        }

        // Fallback summary placeholders for field_report/subtask events using new_ticket template
        if (extra != null)
        {
            if (extra.TryGetValue("fieldReport", out var fr)) values["fieldReport"] = EscapeHtmlValue(fr);
            if (extra.TryGetValue("subtask", out var st)) values["subtask"] = EscapeHtmlValue(st);
        }

        var msg = template;
        foreach (var kv in values)
        {
            msg = msg.Replace($"{{{kv.Key}}}", kv.Value);
        }

        // Удаляем строки с незаменёнными placeholder'ами
        var lines = msg.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line => !Regex.IsMatch(line, @"\{[a-zA-Z0-9_]+\}"))
            .ToList();
        msg = string.Join("\n", lines);

        // Сжимаем множественные переносы и обрезаем края
        msg = Regex.Replace(msg, @"(\r?\n){2,}", "\n").Trim('\n');

        if (string.IsNullOrWhiteSpace(msg) && logger != null)
            logger.LogWarning("Telegram FormatMessage produced empty output for template with {Length} chars after replacement", template.Length);

        return msg;
    }

    private static string EscapeHtmlValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
