using ITCafe.Api.Dtos.Tickets;

namespace ITCafe.Api.Services.Contracts;

public interface ITicketAssistService
{
    SuggestFieldsResponse SuggestFields(SuggestFieldsRequest request);
    Task<SuggestReplyResponse> SuggestReplyAsync(int ticketId);
}
