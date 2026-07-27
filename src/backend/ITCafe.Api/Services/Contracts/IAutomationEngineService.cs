using ITCafe.Api.Models;

namespace ITCafe.Api.Services.Contracts;

public interface IAutomationEngineService
{
    Task EvaluateTicketCreatedAsync(Ticket ticket, CancellationToken ct = default);
    Task EvaluateTriggerAsync(string trigger, Ticket ticket, CancellationToken ct = default);
    Task RunPeriodicAsync(CancellationToken ct = default);
}
