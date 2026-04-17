namespace ITCafe.Api.Dtos.Tickets;

public record MigrateTicketRequest(
    string Title,
    string Problem,
    string Status,
    string Priority,
    string Department,
    string RequestType,
    int? ClientId,
    string? Assignee,
    int? ObjectId,
    int? OkdeskId,
    string? CreatedAt,
    bool IsFromOkdesk = false
);
