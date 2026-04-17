namespace ITCafe.Api.Dtos.Messenger;

public record ChatAttachmentUploadResultDto(string Url, string MimeType, string FileName, long SizeBytes);
