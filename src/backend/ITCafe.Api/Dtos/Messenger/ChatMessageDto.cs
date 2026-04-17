namespace ITCafe.Api.Dtos.Messenger;

public record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    string SenderUserId,
    string SenderFullName,
    string Body,
    DateTime CreatedAtUtc,
    string? AttachmentUrl,
    string? AttachmentMimeType,
    string? AttachmentFileName,
    IReadOnlyList<ReactionDto> Reactions
);
