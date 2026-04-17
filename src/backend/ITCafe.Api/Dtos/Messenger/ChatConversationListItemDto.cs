namespace ITCafe.Api.Dtos.Messenger;

public record ChatConversationListItemDto(
    Guid Id,
    bool IsGroup,
    string? Title,
    string? PeerUserId,
    string DisplayName,
    string? AvatarUrl,
    string? LastMessagePreview,
    DateTime LastMessageAtUtc,
    int UnreadCount = 0
);
