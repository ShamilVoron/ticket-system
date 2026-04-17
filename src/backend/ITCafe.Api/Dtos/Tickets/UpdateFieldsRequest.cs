namespace ITCafe.Api.Dtos.Tickets;

public record UpdateFieldsRequest(string? Priority = null, string? Department = null, string? RequestType = null);
