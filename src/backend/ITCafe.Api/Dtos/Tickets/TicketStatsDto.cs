namespace ITCafe.Api.Dtos.Tickets;

public record TicketStatsDto(
    int TotalToday,
    int OpenToday,
    int InProgressToday,
    int RepairToday
);
