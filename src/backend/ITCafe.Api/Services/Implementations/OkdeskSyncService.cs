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

    /// <summary>
    /// Bulk import from Okdesk REST API.
    /// Paths are approximate per Okdesk public API docs (/api/v1/companies, /api/v1/issues);
    /// response shapes vary by account — parsing is defensive.
    /// </summary>
    public async Task<OkdeskImportResult> ImportAsync(CancellationToken cancellationToken = default)
    {
        var (url, token) = await GetCredentialsAsync();
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            return new OkdeskImportResult(0, 0, 0, 0, "OkdeskApiUrl / OkdeskApiToken not configured");

        var companiesFetched = 0;
        var companiesUpserted = 0;
        var issuesFetched = 0;
        var issuesUpserted = 0;
        string? warning = null;

        try
        {
            // Approximate path: GET /api/v1/companies?page=&count= (api_token query)
            var page = 1;
            const int pageSize = 50;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await SendGetAsync(url, token, $"/api/v1/companies?page={page}&count={pageSize}");
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    warning = $"Companies fetch failed: {(int)response.StatusCode} {body}";
                    _logger.LogWarning("Okdesk import companies page {Page}: {Status} {Body}", page, response.StatusCode, body);
                    break;
                }

                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "[]" : body);
                var items = ExtractArray(doc.RootElement);
                if (items.Count == 0)
                    break;

                companiesFetched += items.Count;
                foreach (var item in items)
                {
                    var okdeskId = TryGetInt(item, "id");
                    if (!okdeskId.HasValue) continue;

                    var name = TryGetString(item, "name") ?? $"Okdesk company #{okdeskId}";
                    var email = TryGetString(item, "email");
                    var phone = TryGetString(item, "phone") ?? TryGetString(item, "additional_phone_number");

                    var existing = await _context.Companies
                        .FirstOrDefaultAsync(c => c.OkdeskId == okdeskId.Value, cancellationToken);
                    if (existing == null)
                    {
                        _context.Companies.Add(new Company
                        {
                            Name = name,
                            Email = email,
                            Phone = phone,
                            OkdeskId = okdeskId.Value,
                            IsActive = true,
                            LastSyncedAtUtc = DateTime.UtcNow,
                            SyncSource = "okdesk_import",
                        });
                    }
                    else
                    {
                        existing.Name = name;
                        if (!string.IsNullOrWhiteSpace(email)) existing.Email = email;
                        if (!string.IsNullOrWhiteSpace(phone)) existing.Phone = phone;
                        existing.LastSyncedAtUtc = DateTime.UtcNow;
                        existing.SyncSource = "okdesk_import";
                    }

                    companiesUpserted++;
                }

                await _context.SaveChangesAsync(cancellationToken);
                if (items.Count < pageSize)
                    break;
                page++;
                if (page > 200) break; // safety
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Okdesk company import exception");
            warning = (warning == null ? "" : warning + "; ") + "Companies: " + ex.Message;
        }

        try
        {
            // Approximate path: open issues — status[]=opened (param name may differ by Okdesk version)
            var response = await SendGetAsync(url, token, "/api/v1/issues?page=1&count=50&status[]=opened");
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                warning = (warning == null ? "" : warning + "; ")
                          + $"Issues fetch skipped/failed: {(int)response.StatusCode}";
                _logger.LogWarning("Okdesk import issues: {Status} {Body}", response.StatusCode, body);
            }
            else
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "[]" : body);
                var items = ExtractArray(doc.RootElement);
                issuesFetched = items.Count;

                foreach (var item in items)
                {
                    var okdeskId = TryGetInt(item, "id");
                    if (!okdeskId.HasValue) continue;

                    var existing = await _context.Tickets
                        .FirstOrDefaultAsync(t => t.OkdeskId == okdeskId.Value, cancellationToken);
                    if (existing != null)
                    {
                        existing.Title = TryGetString(item, "title") ?? existing.Title;
                        existing.IsFromOkdesk = true;
                        issuesUpserted++;
                        continue;
                    }

                    var companyOkdeskId = TryGetNestedInt(item, "company", "id")
                                          ?? TryGetInt(item, "company_id");
                    var clientId = 1;
                    if (companyOkdeskId.HasValue)
                    {
                        var co = await _context.Companies.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.OkdeskId == companyOkdeskId.Value, cancellationToken);
                        if (co != null) clientId = co.Id;
                    }

                    _context.Tickets.Add(new Ticket
                    {
                        Title = TryGetString(item, "title") ?? $"Okdesk issue #{okdeskId}",
                        Problem = TryGetString(item, "description") ?? string.Empty,
                        Status = "Открыт",
                        Priority = "Средний",
                        Department = "Поддержка",
                        RequestType = "Okdesk",
                        CreatedAt = DateTime.UtcNow,
                        ClientId = clientId,
                        OkdeskId = okdeskId.Value,
                        IsFromOkdesk = true,
                        CreatedByRole = "okdesk_import",
                    });
                    issuesUpserted++;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Okdesk issues import exception");
            warning = (warning == null ? "" : warning + "; ") + "Issues: " + ex.Message;
        }

        return new OkdeskImportResult(companiesFetched, companiesUpserted, issuesFetched, issuesUpserted, warning);
    }

    private static List<JsonElement> ExtractArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().ToList();

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in new[] { "companies", "issues", "data", "items", "results" })
            {
                if (root.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    return arr.EnumerateArray().ToList();
            }
        }

        return new List<JsonElement>();
    }

    private static int? TryGetInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s)) return s;
        return null;
    }

    private static int? TryGetNestedInt(JsonElement el, string objName, string prop)
    {
        if (!el.TryGetProperty(objName, out var obj) || obj.ValueKind != JsonValueKind.Object)
            return null;
        return TryGetInt(obj, prop);
    }

    private static string? TryGetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.ToString(),
            _ => null,
        };
    }
}
