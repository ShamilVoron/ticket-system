namespace ITCafe.Api.Dtos.Messenger;

/// <summary>Синхронизация списка чатов. LastMessageSenderUserId — автор последнего сообщения (для push). SidebarEventKind: posted | deleted | updated.</summary>
public record ChatSidebarSyncDto(
    Guid ConversationId,
    bool IsGroup,
    string? Title,
    string? PeerUserId,
    string DisplayName,
    string? AvatarUrl,
    string? LastMessagePreview,
    DateTime LastMessageAtUtc,
    string? LastMessageSenderUserId = null,
    string SidebarEventKind = "updated");
