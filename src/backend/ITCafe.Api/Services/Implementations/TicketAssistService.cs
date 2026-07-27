using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Dtos.Tickets;
using ITCafe.Api.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Services.Implementations;

/// <summary>
/// Rule-based field suggestions and reply drafts (KB / similar tickets; optional OpenAI).
/// </summary>
public class TicketAssistService : ITicketAssistService
{
    private const string AiProviderKey = "ai_provider";

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TicketAssistService> _logger;

    private static readonly (string[] Keywords, string Value)[] PriorityRules =
    [
        (["критич", "urgent", "авария", "не работает совсем", "всё лежит", "down", "срочн", "asap"], "Критический"),
        (["высок", "high", "срочно", "горит", "не могу работать", "блокир"], "Высокий"),
        (["низк", "low", "когда будет время", "не срочно", "по возможности"], "Низкий"),
    ];

    private static readonly (string[] Keywords, string Value)[] RequestTypeRules =
    [
        (["принтер", "мфу", "сканер", "картридж", "тонер"], "Настройка оборудования"),
        (["моноблок", "пк", "компьютер", "ноутбук", "системный блок", "железо"], "Поломка"),
        (["сеть", "интернет", "wifi", "wi-fi", "vpn", "роутер", "свитч", "кабель"], "Сеть / Интернет"),
        (["доступ", "пароль", "учётка", "учетк", "логин", "права", "админ"], "Доступы"),
        (["1с", "excel", "word", "office", "по ", "программ", "софт", "лицензи"], "Помощь с ПО"),
        (["установ", "настройк", "конфиг"], "Настройка ПО"),
        (["монтаж", "подключ", "установк оборудования"], "Монтаж / Подключение"),
        (["ремонт", "починить", "поломк"], "Ремонт"),
        (["подмен", "замен"], "Подменное оборудование"),
        (["счёт", "счет", "документ", "акт ", "договор"], "Документы / Счёт"),
        (["разработ", "доработ", "баг", "фича", "api"], "Разработка / Доработка"),
        (["консульт", "вопрос", "как сделать"], "Консультация"),
        (["то ", "обслуживан", "планов"], "Плановое ТО"),
    ];

    private static readonly (string[] Keywords, string Value)[] DepartmentRules =
    [
        (["выезд", "на объект", "на месте", "инженер выезд"], "Выездные инженеры"),
        (["ремонт", "сервис", "мастерская", "цех"], "Ремонт / сервис"),
        (["разработ", "доработ", "баг", "репозитор"], "Разработчики"),
        (["сервер", "админ", "домен", "active directory", "ad ", "dns", "dhcp"], "Системный администратор"),
        (["счёт", "счет", "оплат", "бухгалтер"], "Бухгалтерия"),
        (["закуп", "поставк"], "Закупки"),
        (["2 лин", "вторая лин", "эскалац"], "2 линия"),
        (["1 лин", "первая лин", "поддержк"], "1 линия"),
    ];

    public TicketAssistService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<TicketAssistService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public SuggestFieldsResponse SuggestFields(SuggestFieldsRequest request)
    {
        var text = $"{request.Title} {request.Problem}".ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text))
            return new SuggestFieldsResponse(null, null, null);

        string? priority = MatchFirst(text, PriorityRules) ?? null;
        string? requestType = MatchFirst(text, RequestTypeRules);
        string? department = MatchFirst(text, DepartmentRules);

        // Equipment keywords often imply field / repair dept when not already set
        if (department == null && ContainsAny(text, ["принтер", "моноблок", "киоск", "оборудование", "мфу"]))
            department = "Выездные инженеры";

        return new SuggestFieldsResponse(requestType, priority, department);
    }

    public async Task<SuggestReplyResponse> SuggestReplyAsync(int ticketId)
    {
        var ticket = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        var provider = await GetAiProviderAsync();
        if (string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    var openai = await TryOpenAiSuggestAsync(ticket, apiKey);
                    if (!string.IsNullOrWhiteSpace(openai))
                        return new SuggestReplyResponse(openai.Trim(), "openai");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OpenAI suggest-reply failed for ticket {TicketId}, falling back to local", ticketId);
                }
            }
        }

        var local = await BuildLocalSuggestionAsync(ticket);
        return new SuggestReplyResponse(local, "local");
    }

    private async Task<string> GetAiProviderAsync()
    {
        var setting = await _db.SystemSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AiProviderKey);
        var value = (setting?.Value ?? "none").Trim().ToLowerInvariant();
        return value is "openai" or "local" or "none" ? value : "none";
    }

    private async Task<string> BuildLocalSuggestionAsync(Models.Ticket ticket)
    {
        var hay = $"{ticket.Title} {ticket.Problem}".Trim();
        var tokens = Tokenize(hay);

        var kbLines = new List<string>();
        if (tokens.Count > 0)
        {
            var articles = await _db.KbArticles.AsNoTracking()
                .Where(a => a.IsPublished)
                .Select(a => new { a.Title, a.Body, a.Tags })
                .ToListAsync();

            var scoredKb = articles
                .Select(a =>
                {
                    var blob = $"{a.Title} {a.Tags} {a.Body}".ToLowerInvariant();
                    var score = tokens.Count(t => blob.Contains(t));
                    return new { a.Title, a.Body, Score = score };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(3)
                .ToList();

            foreach (var a in scoredKb)
            {
                var snippet = (a.Body ?? "").Trim();
                if (snippet.Length > 280) snippet = snippet[..280] + "…";
                kbLines.Add($"• {a.Title}: {snippet}");
            }
        }

        var similarCommentLines = new List<string>();
        if (tokens.Count > 0)
        {
            var candidates = await _db.Tickets.AsNoTracking()
                .Where(t => t.Id != ticket.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(80)
                .Select(t => new { t.Id, t.Title, t.Problem })
                .ToListAsync();

            var similarIds = candidates
                .Select(t =>
                {
                    var blob = $"{t.Title} {t.Problem}".ToLowerInvariant();
                    var score = tokens.Count(tok => blob.Contains(tok));
                    return new { t.Id, Score = score };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(3)
                .Select(x => x.Id)
                .ToList();

            if (similarIds.Count > 0)
            {
                var comments = await _db.TicketComments.AsNoTracking()
                    .Where(c => similarIds.Contains(c.TicketId) && !c.IsInternal && c.Text.Length > 20)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(8)
                    .Select(c => new { c.TicketId, c.AuthorName, c.Text })
                    .ToListAsync();

                foreach (var c in comments.Take(4))
                {
                    var text = c.Text.Trim();
                    if (text.Length > 220) text = text[..220] + "…";
                    similarCommentLines.Add($"• [#{c.TicketId}] {c.AuthorName}: {text}");
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Здравствуйте! По заявке «{ticket.Title}» предлагаю следующий ответ:");
        sb.AppendLine();

        if (kbLines.Count > 0)
        {
            sb.AppendLine("На основе базы знаний:");
            foreach (var line in kbLines)
                sb.AppendLine(line);
            sb.AppendLine();
        }

        if (similarCommentLines.Count > 0)
        {
            sb.AppendLine("Похожие ответы из других заявок:");
            foreach (var line in similarCommentLines)
                sb.AppendLine(line);
            sb.AppendLine();
        }

        if (kbLines.Count == 0 && similarCommentLines.Count == 0)
        {
            sb.AppendLine("К сожалению, в базе знаний и похожих заявках пока мало совпадений.");
            sb.AppendLine("Уточните, пожалуйста, детали проблемы и шаги, которые уже пробовали — мы поможем разобраться.");
        }
        else
        {
            sb.AppendLine("Если проблема сохраняется, напишите — продолжим разбор.");
        }

        return sb.ToString().Trim();
    }

    private async Task<string?> TryOpenAiSuggestAsync(Models.Ticket ticket, string apiKey)
    {
        var recent = await _db.TicketComments.AsNoTracking()
            .Where(c => c.TicketId == ticket.Id && !c.IsInternal)
            .OrderByDescending(c => c.CreatedAt)
            .Take(6)
            .Select(c => new { c.AuthorName, c.Text })
            .ToListAsync();

        recent.Reverse();
        var history = string.Join("\n", recent.Select(c => $"{c.AuthorName}: {c.Text}"));

        var systemPrompt =
            "Ты помощник поддержки IT. Напиши краткий вежливый ответ клиенту на русском по заявке. " +
            "Без markdown-заголовков, 2–6 предложений.";
        var userPrompt =
            $"Заявка: {ticket.Title}\nОписание: {ticket.Problem}\n" +
            (string.IsNullOrWhiteSpace(history) ? "" : $"Недавние комментарии:\n{history}\n") +
            "Сформулируй ответ сотруднику поддержки (черновик комментария).";

        var payload = new
        {
            model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini",
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            temperature = 0.4,
            max_tokens = 500,
        };

        var client = _httpClientFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var res = await client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI HTTP {Status} for suggest-reply", (int)res.StatusCode);
            return null;
        }

        await using var stream = await res.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        return content;
    }

    private static string? MatchFirst(string text, (string[] Keywords, string Value)[] rules)
    {
        foreach (var (keywords, value) in rules)
        {
            if (ContainsAny(text, keywords))
                return value;
        }
        return null;
    }

    private static bool ContainsAny(string text, string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static List<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '-', '/', '\\', '(', ')', '"', '\'', '!', '?', '\n', '\r', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 3)
            .Distinct()
            .Take(10)
            .ToList();
}
