using ITCafe.Api.Dtos.Messenger;

namespace ITCafe.Api.Services.Contracts;

public interface IMessengerService
{
    Task<bool> UserIsMemberAsync(Guid conversationId, string userId);

    Task<IReadOnlyList<ChatConversationListItemDto>> ListConversationsAsync(string currentUserId);

    Task<ChatConversationDetailDto?> GetConversationAsync(Guid conversationId, string currentUserId);

    Task<Guid> EnsureDirectConversationAsync(string currentUserId, string otherUserId);

    Task<Guid> CreateGroupConversationAsync(string currentUserId, string title, IReadOnlyList<string> memberUserIds);

    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        string currentUserId,
        Guid conversationId,
        Guid? beforeMessageId,
        int take);

    Task<ChatMessageDto> PostMessageAsync(
        string currentUserId,
        Guid conversationId,
        string body,
        string? attachmentUrl,
        string? attachmentMimeType,
        string? attachmentFileName);

    Task<ChatAttachmentUploadResultDto> UploadAttachmentAsync(
        string currentUserId,
        Guid conversationId,
        Stream fileStream,
        string fileName,
        string contentType,
        long contentLength);

    Task DeleteMessageAsync(string currentUserId, Guid conversationId, Guid messageId);

    Task<ChatMessageDto> ToggleMessageReactionAsync(string currentUserId, Guid conversationId, Guid messageId, string emoji);

    Task MarkConversationAsReadAsync(Guid conversationId, string userId);

    Task<ChatConversationDetailDto?> UpdateGroupAsync(
        string currentUserId,
        Guid conversationId,
        string? title,
        IReadOnlyList<string>? addMemberUserIds,
        IReadOnlyList<string>? removeMemberUserIds);

    Task<Guid> EnsureDepartmentChannelAsync(string departmentSlug, string currentUserId);

    Task<Guid> EnsureTicketChatAsync(int ticketId, string currentUserId);

    Task<IReadOnlyList<ChatMessageSearchResultDto>> SearchMessagesAsync(string currentUserId, string q);
}
