using System.Text;
using System.Text.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Services.Implementations;

public class OkdeskSyncService : IOkdeskSyncService
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OkdeskSyncService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // Ticket System status -> Okdesk status code
    private static readonly Dictionary<string, string> StatusToOkdesk = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Открыт"] = "opened",
        ["В работе"] = "in_work",
        ["Отложен"] = "postponed",
        ["Ожидание клиента"] = "on_hold",
        ["Решено"] = "resolved",
        ["Решён"] = "resolved",
        ["Закрыт"] = "closed",
        ["Требуется координатор"] = "pending",
        ["У инженера / в ремонте"] = "in_work",
    };

    // Ticket System priority -> Okdesk priority code
    private static readonly Dictionary<string, string> PriorityToOkdesk = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Низкий"] = "low",
        ["Средний"] = "medium",
        ["Высокий"] = "high",
        ["Критический"] = "critical",
    };

    public OkdeskSyncService(AppDbContext context, IHttpClientFactory httpClientFactory, ILogger<OkdeskSyncService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync()
    {
        var (url, token) = await GetCredentialsAsync();
        return !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(token);
    }

    public async Task<bool> TestConnectionAsync()
    {
        var (url, token) = await GetCredentialsAsync();
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var response = await SendGetAsync(url, token, "/api/v1/issues?page=1&count=1");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Okdesk connection test failed");
            return false;
        }
    }

    public async Task SyncTicketAsync(Ticket ticket)
    {
        if (!ticket.OkdeskId.HasValue)
            return;

        var (url, token) = await GetCredentialsAsync();
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            return;

        var issue = new Dictionary<string, object?>();

        var statusCode = MapStatus(ticket.Status);
        if (!string.IsNullOrEmpty(statusCode))
            issue["status"] = statusCode;

        var priorityCode = MapPriority(ticket.Priority);
        if (!string.IsNullOrEmpty(priorityCode))
            issue["priority"] = priorityCode;

        issue["title"] = ticket.Title;
        issue["description"] = ticket.Problem;

        var assigneeOkdeskId = await ResolveAssigneeOkdeskIdAsync(ticket.Assignee);
        if (assigneeOkdeskId.HasValue)
            issue["assignee_id"] = assigneeOkdeskId.Value;

        var payload = new Dictionary<string, object> { ["issue"] = issue };
        var json = JsonSerializer.Serialize(payload, JsonOpts);

        try
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await SendPutAsync(url, token, $"/api/v1/issues/{ticket.OkdeskId.Value}", content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Okdesk sync ticket {OkdeskId} failed: {Status} {Body}", ticket.OkdeskId.Value, response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Okdesk sync ticket {OkdeskId} succeeded", ticket.OkdeskId.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Okdesk sync ticket {OkdeskId} exception", ticket.OkdeskId.Value);
        }
    }

    public async Task SyncTicketCommentAsync(Ticket ticket, TicketComment comment, string? authorUserId)
    {
        if (!ticket.OkdeskId.HasValue)
            return;

        var (url, token) = await GetCredentialsAsync();
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            return;

        var issueComment = new Dictionary<string, object?>
        {
            ["content"] = comment.Text,
            ["public"] = !comment.IsInternal,
        };

        var authorOkdeskId = await ResolveAuthorOkdeskIdAsync(authorUserId);
        if (authorOkdeskId.HasValue)
            issueComment["author_id"] = authorOkdeskId.Value;

        var json = JsonSerializer.Serialize(issueComment, JsonOpts);

        try
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var fullUrl = $"{url}/api/v1/issues/{ticket.OkdeskId.Value}/comments?api_token={token}";
            System.IO.File.AppendAllText("/tmp/okdesk_sync.log", $"[{DateTime.UtcNow:O}] Comment sync payload: {json}\nURL: {fullUrl}\n");
            var response = await SendPostAsync(url, token, $"/api/v1/issues/{ticket.OkdeskId.Value}/comments", content);
            var body = await response.Content.ReadAsStringAsync();
            System.IO.File.AppendAllText("/tmp/okdesk_sync.log", $"[{DateTime.UtcNow:O}] Comment sync response: {response.StatusCode} {body}\n");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Okdesk sync comment for ticket {OkdeskId} failed: {Status} {Body}", ticket.OkdeskId.Value, response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Okdesk sync comment for ticket {OkdeskId} succeeded", ticket.OkdeskId.Value);
            }
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText("/tmp/okdesk_sync.log", $"[{DateTime.UtcNow:O}] Comment sync exception: {ex}\n");
            _logger.LogWarning(ex, "Okdesk sync comment for ticket {OkdeskId} exception", ticket.OkdeskId.Value);
        }
    }

    private async Task<(string Url, string Token)> GetCredentialsAsync()
    {
        var settings = await _context.SystemSettings.AsNoTracking().ToListAsync();
        var url = settings.FirstOrDefault(s => s.Key == "OkdeskApiUrl")?.Value ?? string.Empty;
        var token = settings.FirstOrDefault(s => s.Key == "OkdeskApiToken")?.Value ?? string.Empty;
        return (url.Trim().TrimEnd('/'), token.Trim());
    }

    private static string BuildUrl(string baseUrl, string path, string token)
    {
        var sep = path.Contains('?') ? "&" : "?";
        return $"{baseUrl}{path}{sep}api_token={Uri.EscapeDataString(token)}";
    }

    private static async Task<HttpResponseMessage> SendPutAsync(string url, string token, string path, StringContent content)
    {
        using var client = new HttpClient();
        var fullUrl = BuildUrl(url, path, token);
        return await client.PutAsync(fullUrl, content);
    }

    private static async Task<HttpResponseMessage> SendPostAsync(string url, string token, string path, StringContent content)
    {
        using var client = new HttpClient();
        var fullUrl = BuildUrl(url, path, token);
        return await client.PostAsync(fullUrl, content);
    }

    private static async Task<HttpResponseMessage> SendGetAsync(string url, string token, string path)
    {
        using var client = new HttpClient();
        var fullUrl = BuildUrl(url, path, token);
        return await client.GetAsync(fullUrl);
    }

    private static string? MapStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        return StatusToOkdesk.TryGetValue(status.Trim(), out var code) ? code : null;
    }

    private static string? MapPriority(string priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return null;
        return PriorityToOkdesk.TryGetValue(priority.Trim(), out var code) ? code : null;
    }

    private async Task<int?> ResolveAssigneeOkdeskIdAsync(string assigneeCsv)
    {
        var firstUserId = assigneeCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstUserId))
            return null;

        var emp = await _context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == firstUserId);
        return emp?.OkdeskId;
    }

    private async Task<int?> ResolveAuthorOkdeskIdAsync(string? authorUserId)
    {
        if (string.IsNullOrWhiteSpace(authorUserId))
            return null;

        var emp = await _context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == authorUserId);
        return emp?.OkdeskId;
    }
}
