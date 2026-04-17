namespace ITCafe.Api.Dtos.Tickets;

public record DelegateRequest(string DelegatedFrom, string DelegatedTo, string Reason);
