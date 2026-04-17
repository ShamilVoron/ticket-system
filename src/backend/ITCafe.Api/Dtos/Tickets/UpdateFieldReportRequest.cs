namespace ITCafe.Api.Dtos.Tickets;

public record UpdateFieldReportRequest(
    string? EngineerName,
    DateTime? VisitDate,
    string? ActionType,
    string? EquipmentType,
    string? EquipmentSerial,
    string? EquipmentStatus,
    string? WorkDone,
    string? TransferredTo
);
