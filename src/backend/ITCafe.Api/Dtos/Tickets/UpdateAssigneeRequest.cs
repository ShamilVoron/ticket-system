namespace ITCafe.Api.Dtos.Tickets;

public record UpdateAssigneeRequest(string Assignee, string[]? Assignees = null);
