namespace ITCafe.Api.Dtos.Tickets;

public record CreateFieldReportRequest(
    string EngineerName,
    DateTime? VisitDate,
    string ActionType,
    string EquipmentType,
    string EquipmentSerial,
    string EquipmentStatus,
    string WorkDone,
    string TransferredTo
);
