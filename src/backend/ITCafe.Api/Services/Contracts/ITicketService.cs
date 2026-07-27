using ITCafe.Api.Dtos.Tickets;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace ITCafe.Api.Services.Contracts;

public interface ITicketService
{
    Task<IEnumerable<TicketDto>> GetTicketsAsync(string? assignee = null);
    Task<Dtos.Common.PagedResult<TicketDto>> GetTicketsPagedAsync(Dtos.Tickets.GetTicketsRequest request);
    Task<Dtos.Tickets.TicketStatsDto> GetTicketStatsAsync();
    Task<TicketDto?> GetTicketAsync(int id);
    Task<bool> UpdateStatusAsync(int id, string status);
    Task<bool> UpdateAssigneeAsync(int id, string assignee, string[]? assignees);
    Task<bool> UpdateLinksAsync(int id, string taskLinksJson);
    Task<bool> UpdateTitleAsync(int id, string? title, string? alternativeTitle);
    Task<bool> UpdateProblemAsync(int id, string problem);
    Task<bool> DelegateAsync(int id, string delegatedFrom, string delegatedTo, string reason);
    Task<bool> UpdateFieldsAsync(int id, string? priority, string? department, string? requestType);
    Task<IEnumerable<FieldReportDto>> GetFieldReportsAsync(int id);
    Task<FieldReportDto> AddFieldReportAsync(int id, CreateFieldReportRequest request, string currentUserName);
    Task<FieldReportDto> UpdateFieldReportAsync(int ticketId, int reportId, UpdateFieldReportRequest request);
    Task<IEnumerable<CommentDto>> GetCommentsAsync(int id);
    Task<IEnumerable<TimelineItemDto>> GetTimelineAsync(int id);
    Task<CommentDto> AddCommentAsync(int id, CreateCommentRequest request, string currentUserId, string currentUserName, string currentUserRole);
    Task<CommentDto> ToggleCommentReactionAsync(int ticketId, int commentId, string userId, string userName, string emoji);
    Task MarkTicketAsReadAsync(int ticketId, string userId);
    Task<Ticket> MigrateTicketAsync(MigrateTicketRequest request);
    Task<Ticket> MigrateRepairTicketAsync(MigrateRepairTicketRequest request);
    Task<Ticket> CreateTicketAsync(CreateTicketRequest request);
}
