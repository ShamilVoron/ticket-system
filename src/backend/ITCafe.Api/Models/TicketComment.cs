using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

public class TicketComment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    /// <summary>Связь с <see cref="Employee.UserId"/> для отображения аватара.</summary>
    public string? AuthorUserId { get; set; }
    public string AuthorRole { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? OkdeskId { get; set; }
    /// <summary>JSON-массив реакций: [{"emoji":"🐧","userId":"...","userName":"..."}]</summary>
    public string? ReactionsJson { get; set; }
    /// <summary>RFC Message-Id входящего email (для трединга IMAP In-Reply-To / References).</summary>
    public string? EmailMessageId { get; set; }
}
