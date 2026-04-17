namespace ITCafe.Api.Dtos.Tickets;

public record FieldReportDto(
    int Id,
    int TicketId,
    string EngineerName,
    DateTime VisitDate,
    string ActionType,
    string EquipmentType,
    string EquipmentSerial,
    string EquipmentStatus,
    string WorkDone,
    string TransferredTo
);
