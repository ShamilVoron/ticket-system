namespace ITCafe.Api.Dtos.Tickets;

public record CommentDto(
    int Id,
    int TicketId,
    string AuthorName,
    string AuthorRole,
    string Text,
    bool IsInternal,
    DateTime CreatedAt,
    string AuthorAvatarUrl,
    string? AuthorUserId,
    IReadOnlyList<ReactionDto> Reactions
);
