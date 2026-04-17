namespace ITCafe.Api.Dtos.Messenger;

public record PostChatMessageRequest(
    string Body,
    string? AttachmentUrl = null,
    string? AttachmentMimeType = null,
    string? AttachmentFileName = null);
