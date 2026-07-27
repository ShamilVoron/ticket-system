namespace ITCafe.Api.Dtos.Tickets;

/// <summary>
/// Unified timeline entry for a ticket (created / comment / field_report).
/// </summary>
public record TimelineItemDto(
    string Type,
    DateTime At,
    string? Channel,
    string? AuthorName,
    string? Text,
    bool? IsInternal,
    int? EntityId,
    string? ActionType,
    string? EquipmentType
);
