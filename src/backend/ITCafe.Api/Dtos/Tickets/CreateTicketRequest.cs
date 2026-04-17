namespace ITCafe.Api.Dtos.Tickets;

public record CreateTicketRequest(
    string Title,
    string RequestType,
    string? SoftwareName,
    string Priority,
    string Department,
    string? Details,
    DateTime? DesiredAt,
    int? ClientId,
    int? ObjectId,
    string? Assignee,
    string[]? Assignees,
    string? CoordinatorBriefJson = null,
    bool? IsRepair = null,
    int? EquipmentId = null,
    string? RepairType = null,
    decimal? RepairCost = null,
    string? RepairFaults = null,
    string? RepairNotes = null,
    string? EquipmentType = null,
    string? EquipmentTypeLabel = null
);
