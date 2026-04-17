namespace ITCafe.Api.Dtos.Messenger;

public record CreateGroupChatRequest(string Title, IReadOnlyList<string> MemberUserIds);
