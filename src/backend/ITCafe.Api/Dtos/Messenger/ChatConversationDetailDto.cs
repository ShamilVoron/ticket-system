namespace ITCafe.Api.Dtos.Messenger;

public record ChatMemberBriefDto(string UserId, string FullName, string? AvatarUrl);

public record ChatConversationDetailDto(
    Guid Id,
    bool IsGroup,
    string? Title,
    IReadOnlyList<ChatMemberBriefDto> Members,
    DateTime LastMessageAtUtc
);
