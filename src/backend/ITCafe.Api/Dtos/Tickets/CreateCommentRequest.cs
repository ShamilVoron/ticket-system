namespace ITCafe.Api.Dtos.Tickets;

public record CreateCommentRequest(
    string AuthorName,
    string AuthorRole,
    string Text,
    bool IsInternal,
    string? CreatedAt = null,
    int? OkdeskId = null,
    string? AuthorUserId = null
);
