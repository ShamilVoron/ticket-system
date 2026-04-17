using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

public class ServiceObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    /// <summary>Статус обслуживания: "Да по предоплате r_keeper", "Нет r_keeper" и т.п.</summary>
    public string MaintenanceStatus { get; set; } = string.Empty;
    public string LegalEntity { get; set; } = string.Empty;
    /// <summary>Подробное описание условий обслуживания / ПО объекта</summary>
    public string Description { get; set; } = string.Empty;
    public int? ClientId { get; set; }
    public int? OkdeskId { get; set; }
    public string? ExternalCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAtUtc { get; set; }
    public string SyncSource { get; set; } = string.Empty;
    public string MaintenanceComment { get; set; } = string.Empty;
    public string DirectoriesOwner { get; set; } = string.Empty;
    public string SysAdmin { get; set; } = string.Empty;
    public string ServerServices { get; set; } = string.Empty;
}
