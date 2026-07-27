using System.Text.RegularExpressions;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace ITCafe.Api.Services.Implementations;

/// <summary>
/// IMAP ingest: UNSEEN messages → reply to existing ticket (via EmailMessageId) or new ticket.
/// Settings keys: email_ingest_enabled, imap_host, imap_port, imap_user, imap_password, imap_use_ssl.
/// </summary>
public class EmailIngestService : IEmailIngestService
{
    private static readonly Regex AngleId = new(@"<[^>]+>", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly ILogger<EmailIngestService> _logger;

    public EmailIngestService(AppDbContext db, ILogger<EmailIngestService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.SystemSettings.AsNoTracking().ToListAsync(cancellationToken);
        string Get(string key) =>
            settings.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase))?.Value
            ?? string.Empty;

        var enabledRaw = Get("email_ingest_enabled");
        var enabled = string.Equals(enabledRaw, "true", StringComparison.OrdinalIgnoreCase)
                      || enabledRaw == "1";
        if (!enabled)
            return;

        var host = Get("imap_host").Trim();
        var user = Get("imap_user").Trim();
        var password = Get("imap_password");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
        {
            _logger.LogDebug("Email ingest enabled but imap_host/imap_user not configured");
            return;
        }

        if (!int.TryParse(Get("imap_port"), out var port) || port <= 0)
            port = 993;

        var useSslRaw = Get("imap_use_ssl");
        var useSsl = string.IsNullOrWhiteSpace(useSslRaw)
            || string.Equals(useSslRaw, "true", StringComparison.OrdinalIgnoreCase)
            || useSslRaw == "1";

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(host, port, useSsl, cancellationToken);
            await client.AuthenticateAsync(user, password, cancellationToken);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            var uids = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);
            foreach (var uid in uids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var message = await inbox.GetMessageAsync(uid, cancellationToken);
                    await ProcessMessageAsync(message, cancellationToken);
                    await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Email ingest failed for UID {Uid}", uid);
                }
            }

            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email IMAP poll failed");
        }
    }

    private async Task ProcessMessageAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var messageId = NormalizeMessageId(message.MessageId);
        var replyIds = CollectReplyIds(message);

        if (replyIds.Count > 0)
        {
            var matched = await _db.TicketComments
                .AsNoTracking()
                .Where(c => c.EmailMessageId != null && replyIds.Contains(c.EmailMessageId))
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (matched != null)
            {
                var body = ExtractBody(message);
                var fromName = message.From.Mailboxes.FirstOrDefault()?.Name
                               ?? message.From.Mailboxes.FirstOrDefault()?.Address
                               ?? "Email";
                _db.TicketComments.Add(new TicketComment
                {
                    TicketId = matched.TicketId,
                    AuthorName = fromName,
                    AuthorRole = "email",
                    Text = string.IsNullOrWhiteSpace(body) ? "(пустое письмо)" : body,
                    IsInternal = false,
                    CreatedAt = DateTime.UtcNow,
                    EmailMessageId = messageId,
                });
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Email reply attached to ticket {TicketId} (Message-Id {MessageId})",
                    matched.TicketId, messageId);
                return;
            }
        }

        await CreateTicketFromEmailAsync(message, messageId, cancellationToken);
    }

    private async Task CreateTicketFromEmailAsync(
        MimeMessage message, string? messageId, CancellationToken cancellationToken)
    {
        var subject = string.IsNullOrWhiteSpace(message.Subject) ? "(без темы)" : message.Subject.Trim();
        var body = ExtractBody(message);
        var fromMailbox = message.From.Mailboxes.FirstOrDefault();
        var fromName = fromMailbox?.Name ?? fromMailbox?.Address ?? "Email";
        var fromEmail = fromMailbox?.Address?.Trim().ToLowerInvariant();

        var clientId = 0;
        if (!string.IsNullOrEmpty(fromEmail))
        {
            var client = await _db.Clients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Email.ToLower() == fromEmail, cancellationToken);
            if (client != null)
                clientId = client.CompanyId;
        }

        if (clientId <= 0)
        {
            clientId = await _db.Companies.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (clientId <= 0)
            clientId = 1;

        var ticket = new Ticket
        {
            Title = subject.Length > 500 ? subject[..500] : subject,
            Problem = string.IsNullOrWhiteSpace(body) ? subject : body,
            Status = "Открыт",
            Priority = "Средний",
            Department = "Поддержка",
            RequestType = "Email",
            CreatedAt = DateTime.UtcNow,
            ClientId = clientId,
            CreatedByRole = "email",
            CreatedByUserId = string.Empty,
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);

        // Store Message-Id so subsequent replies (In-Reply-To) can thread onto this ticket.
        _db.TicketComments.Add(new TicketComment
        {
            TicketId = ticket.Id,
            AuthorName = fromName,
            AuthorRole = "email",
            Text = $"[Email] {subject}",
            IsInternal = true,
            CreatedAt = DateTime.UtcNow,
            EmailMessageId = messageId,
        });
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Email created ticket {TicketId} from Message-Id {MessageId}",
            ticket.Id, messageId);
    }

    private static List<string> CollectReplyIds(MimeMessage message)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddRaw(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (Match m in AngleId.Matches(raw))
            {
                var n = NormalizeMessageId(m.Value);
                if (!string.IsNullOrEmpty(n)) ids.Add(n);
            }

            var single = NormalizeMessageId(raw);
            if (!string.IsNullOrEmpty(single)) ids.Add(single);
        }

        AddRaw(message.InReplyTo);
        foreach (var r in message.References)
            AddRaw(r);

        return ids.ToList();
    }

    private static string? NormalizeMessageId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (!s.StartsWith('<')) s = "<" + s;
        if (!s.EndsWith('>')) s += ">";
        return s;
    }

    private static string ExtractBody(MimeMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.TextBody))
            return message.TextBody.Trim();

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            var html = message.HtmlBody;
            var noTags = Regex.Replace(html, "<[^>]+>", " ");
            noTags = System.Net.WebUtility.HtmlDecode(noTags);
            return Regex.Replace(noTags, @"\s+", " ").Trim();
        }

        return string.Empty;
    }
}
