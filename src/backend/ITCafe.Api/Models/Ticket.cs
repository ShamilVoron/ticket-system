using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

public class Ticket
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string Status { get; set; } = "Открыт";
    public string Priority { get; set; } = "Средний";
    public string Department { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public int ClientId { get; set; }
    public int? ObjectId { get; set; }
    public string Assignee { get; set; } = string.Empty;
    public int? OkdeskId { get; set; }
    public bool IsFromOkdesk { get; set; }

    /// <summary>Сырой JSON брифа координатора (форма для выездного инженера).</summary>
    public string CoordinatorBriefJson { get; set; } = string.Empty;

    /// <summary>JSON-массив ссылок на внешние таски [{url,number}].</summary>
    public string TaskLinksJson { get; set; } = string.Empty;

    /// <summary>Альтернативное наименование (для клиентских заявок, видно только сотрудникам).</summary>
    public string AlternativeTitle { get; set; } = string.Empty;

    /// <summary>Роль создателя заявки (client / coordinator / …).</summary>
    public string CreatedByRole { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;

    // ── Делегирование ──
    public string DelegatedFrom { get; set; } = string.Empty;
    public string DelegatedTo { get; set; } = string.Empty;
    public string DelegationReason { get; set; } = string.Empty;
    public DateTime? DelegatedAt { get; set; }

    // Repair reporting (derived from Tickets; snapshot stored on Ticket)
    public bool IsRepair { get; set; } = false;
    public int? EquipmentId { get; set; }
    public string RepairType { get; set; } = string.Empty;
    public decimal? RepairCost { get; set; }

    // Snapshot fields captured at the time of repair ticket creation/update.
    public string RepairClientName { get; set; } = string.Empty;
    public string RepairEquipmentName { get; set; } = string.Empty;
    public string RepairSerialNumber { get; set; } = string.Empty;
    public string RepairLocation { get; set; } = string.Empty;
    public string RepairFaults { get; set; } = string.Empty;
    public string RepairNotes { get; set; } = string.Empty;
    public string RepairFundStatus { get; set; } = string.Empty;
    public string RepairEquipmentType { get; set; } = string.Empty;
}
