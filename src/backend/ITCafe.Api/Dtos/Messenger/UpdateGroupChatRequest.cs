namespace ITCafe.Api.Dtos.Messenger;

public record UpdateGroupChatRequest(
    string? Title = null,
    IReadOnlyList<string>? AddMemberUserIds = null,
    IReadOnlyList<string>? RemoveMemberUserIds = null);
