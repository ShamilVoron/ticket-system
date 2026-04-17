using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? HqAddress { get; set; }
    public int? OkdeskId { get; set; }
    public string? ExternalCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAtUtc { get; set; }
    public string SyncSource { get; set; } = string.Empty;
}
