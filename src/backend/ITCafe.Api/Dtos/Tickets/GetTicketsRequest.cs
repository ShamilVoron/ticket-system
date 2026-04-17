namespace ITCafe.Api.Dtos.Tickets;

public record GetTicketsRequest(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? SortKey = "date",
    string? SortOrder = "desc",
    string[]? Statuses = null,
    string[]? Departments = null,
    string[]? Assignees = null,
    string[]? ClientNames = null
);
