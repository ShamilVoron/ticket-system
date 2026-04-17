namespace ITCafe.Api.Dtos.Tickets;

public record MigrateRepairTicketRequest(
    string ClientName,
    string EquipmentType,
    string RepairType,
    decimal Cost,
    string Restaurant,
    string Month,
    int? OkdeskId = null
);
