using ITCafe.Api.Data;
using ITCafe.Api.Dtos.Tickets;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Services.Implementations;

public class SlaService : ISlaService
{
    private readonly AppDbContext _context;

    public SlaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SlaInfoDto?> GetTicketSlaAsync(int ticketId)
    {
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) return null;

        // Try to resolve client category from company if available
        var clientCategory = "*";
        var client = await _context.Clients.FindAsync(ticket.ClientId);
        if (client != null)
        {
            var company = await _context.Companies.FindAsync(client.CompanyId);
            if (company != null && !string.IsNullOrWhiteSpace(company.ExternalCode))
            {
                clientCategory = company.ExternalCode;
            }
        }

        return await CalculateSlaAsync(ticket.Priority, ticket.RequestType, ticket.Department, clientCategory, ticket.CreatedAt);
    }

    public async Task<SlaInfoDto?> CalculateSlaAsync(string priority, string requestType, string department, string clientCategory, DateTime createdAt)
    {
        var policies = await _context.SlaPolicies
            .Where(p => p.IsActive)
            .ToListAsync();

        var policy = MatchPolicy(policies, priority, requestType, department, clientCategory);
        if (policy == null) return null;

        var reactionDeadline = createdAt.AddMinutes(policy.ReactionMinutes);
        var resolutionDeadline = createdAt.AddMinutes(policy.ResolutionMinutes);
        var now = DateTime.UtcNow;

        return new SlaInfoDto(
            ReactionMinutes: policy.ReactionMinutes,
            ResolutionMinutes: policy.ResolutionMinutes,
            ReactionDeadline: reactionDeadline,
            ResolutionDeadline: resolutionDeadline,
            IsReactionBreached: now > reactionDeadline,
            IsResolutionBreached: now > resolutionDeadline,
            RemainingReactionMinutes: now > reactionDeadline ? 0 : (int)(reactionDeadline - now).TotalMinutes,
            RemainingResolutionMinutes: now > resolutionDeadline ? 0 : (int)(resolutionDeadline - now).TotalMinutes
        );
    }

    private static SlaPolicy? MatchPolicy(List<SlaPolicy> policies, string priority, string requestType, string department, string clientCategory)
    {
        var candidates = policies
            .Where(p =>
                (p.Priority == "*" || p.Priority == priority) &&
                (p.RequestType == "*" || string.Equals(p.RequestType, requestType, StringComparison.OrdinalIgnoreCase)) &&
                (p.Department == "*" || string.Equals(p.Department, department, StringComparison.OrdinalIgnoreCase)) &&
                (p.ClientCategory == "*" || string.Equals(p.ClientCategory, clientCategory, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!candidates.Any()) return null;

        // Prefer the most specific match (fewest wildcards)
        return candidates
            .OrderBy(p =>
                (p.Priority == "*" ? 1 : 0) +
                (p.RequestType == "*" ? 1 : 0) +
                (p.Department == "*" ? 1 : 0) +
                (p.ClientCategory == "*" ? 1 : 0))
            .ThenBy(p => p.Id)
            .FirstOrDefault();
    }
}
