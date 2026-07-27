namespace ITCafe.Api.Dtos.Messenger;

public record ChatMessageSearchResultDto(
    Guid MessageId,
    Guid ConversationId,
    string Body,
    DateTime CreatedAtUtc,
    string SenderFullName
);
