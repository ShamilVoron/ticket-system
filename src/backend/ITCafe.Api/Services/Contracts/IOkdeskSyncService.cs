using ITCafe.Api.Models;

namespace ITCafe.Api.Services.Contracts;

public interface IOkdeskSyncService
{
    Task<bool> IsEnabledAsync();
    Task<bool> TestConnectionAsync();
    Task SyncTicketAsync(Ticket ticket);
    Task SyncTicketCommentAsync(Ticket ticket, TicketComment comment, string? authorUserId);
    /// <summary>Bulk import companies (and open issues when API allows) from Okdesk.</summary>
    Task<OkdeskImportResult> ImportAsync(CancellationToken cancellationToken = default);
}

public record OkdeskImportResult(
    int CompaniesFetched,
    int CompaniesUpserted,
    int IssuesFetched,
    int IssuesUpserted,
    string? Warning);
