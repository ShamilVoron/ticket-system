using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

public class TicketAttachment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? CommentId { get; set; }
    public int? SubtaskId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;   // относительный путь в wwwroot/uploads/tickets/
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int? OkdeskId { get; set; }
}
