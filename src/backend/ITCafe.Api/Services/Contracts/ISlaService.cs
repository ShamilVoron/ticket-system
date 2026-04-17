using ITCafe.Api.Dtos.Tickets;

namespace ITCafe.Api.Services.Contracts;

public interface ISlaService
{
    Task<SlaInfoDto?> GetTicketSlaAsync(int ticketId);
    Task<SlaInfoDto?> CalculateSlaAsync(string priority, string requestType, string department, string clientCategory, DateTime createdAt);
}
