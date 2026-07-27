using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITCafe.Api.Dtos;
using ITCafe.Api.Dtos.Tickets;
using ITCafe.Api.Services.Contracts;

namespace ITCafe.Api.Controllers;

/// <summary>Управление заявками (тикетами).</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ISlaService _slaService;
    private readonly ITicketAssistService _ticketAssist;

    public TicketsController(ITicketService ticketService, ISlaService slaService, ITicketAssistService ticketAssist)
    {
        _ticketService = ticketService;
        _slaService = slaService;
        _ticketAssist = ticketAssist;
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("User identifier not found");

    private string CurrentUserName() =>
        User.FindFirstValue("fullName")
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? User.Identity?.Name
        ?? "Unknown";

    private string CurrentUserRole() =>
        User.FindFirstValue(ClaimTypes.Role)
        ?? "client";

    /// <summary>Возвращает список заявок.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketDto>>> GetTickets([FromQuery] string? assignee = null)
    {
        var tickets = await _ticketService.GetTicketsAsync(assignee);
        return Ok(tickets);
    }

    /// <summary>Возвращает пагинированный список заявок с фильтрами.</summary>
    [HttpGet("paged")]
    public async Task<ActionResult<Dtos.Common.PagedResult<TicketDto>>> GetTicketsPaged([FromQuery] Dtos.Tickets.GetTicketsRequest request)
    {
        var result = await _ticketService.GetTicketsPagedAsync(request);
        return Ok(result);
    }

    /// <summary>Возвращает статистику заявок за сегодня.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<Dtos.Tickets.TicketStatsDto>> GetTicketStats()
    {
        var stats = await _ticketService.GetTicketStatsAsync();
        return Ok(stats);
    }

    /// <summary>Подсказывает тип обращения / приоритет / отдел по ключевым словам.</summary>
    [HttpPost("suggest-fields")]
    public ActionResult<SuggestFieldsResponse> SuggestFields([FromBody] SuggestFieldsRequest request)
    {
        return Ok(_ticketAssist.SuggestFields(request ?? new SuggestFieldsRequest(null, null)));
    }

    /// <summary>Возвращает заявку по идентификатору.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDto>> GetTicket(int id)
    {
        var ticket = await _ticketService.GetTicketAsync(id);
        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    /// <summary>Отмечает заявку как прочитанную.</summary>
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _ticketService.MarkTicketAsReadAsync(id, CurrentUserId());
        return Ok();
    }

    /// <summary>Обновляет статус заявки.</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateStatusRequest request)
    {
        var success = await _ticketService.UpdateStatusAsync(id, request.Status);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Обновляет исполнителя заявки.</summary>
    [HttpPatch("{id:int}/assignee")]
    public async Task<IActionResult> UpdateAssignee(int id, UpdateAssigneeRequest request)
    {
        var success = await _ticketService.UpdateAssigneeAsync(id, request.Assignee, request.Assignees);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Обновляет связи заявки.</summary>
    [HttpPatch("{id:int}/links")]
    public async Task<IActionResult> UpdateLinks(int id, UpdateLinksRequest request)
    {
        var success = await _ticketService.UpdateLinksAsync(id, request.TaskLinksJson);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Обновляет поля заявки (приоритет, отдел, тип обращения).</summary>
    [HttpPatch("{id:int}/fields")]
    public async Task<IActionResult> UpdateFields(int id, UpdateFieldsRequest request)
    {
        var success = await _ticketService.UpdateFieldsAsync(id, request.Priority, request.Department, request.RequestType);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Обновляет заголовок заявки.</summary>
    [HttpPatch("{id:int}/title")]
    public async Task<IActionResult> UpdateTitle(int id, UpdateTitleRequest request)
    {
        var success = await _ticketService.UpdateTitleAsync(id, request.Title, request.AlternativeTitle);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Обновляет описание (проблему) заявки.</summary>
    [HttpPatch("{id:int}/problem")]
    public async Task<IActionResult> UpdateProblem(int id, UpdateProblemRequest request)
    {
        var success = await _ticketService.UpdateProblemAsync(id, request.Problem);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Делегирует заявку другому исполнителю.</summary>
    [HttpPatch("{id:int}/delegate")]
    public async Task<IActionResult> Delegate(int id, DelegateRequest request)
    {
        var success = await _ticketService.DelegateAsync(id, request.DelegatedFrom, request.DelegatedTo, request.Reason);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Возвращает полевые отчёты по заявке.</summary>
    [HttpGet("{id:int}/reports")]
    public async Task<ActionResult<IEnumerable<FieldReportDto>>> GetReports(int id)
    {
        var reports = await _ticketService.GetFieldReportsAsync(id);
        return Ok(reports);
    }

    /// <summary>Возвращает SLA информацию по заявке.</summary>
    [HttpGet("{id:int}/sla")]
    public async Task<ActionResult<SlaInfoDto>> GetTicketSla(int id)
    {
        var sla = await _slaService.GetTicketSlaAsync(id);
        if (sla == null) return NotFound();
        return Ok(sla);
    }

    /// <summary>Добавляет полевой отчёт к заявке.</summary>
    [HttpPost("{id:int}/reports")]
    public async Task<ActionResult<FieldReportDto>> AddReport(int id, CreateFieldReportRequest request)
    {
        try
        {
            var report = await _ticketService.AddFieldReportAsync(id, request, CurrentUserName());
            return CreatedAtAction(nameof(GetReports), new { id }, report);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Обновляет полевой отчёт по заявке.</summary>
    [HttpPatch("{id:int}/reports/{reportId:int}")]
    public async Task<ActionResult<FieldReportDto>> UpdateReport(int id, int reportId, UpdateFieldReportRequest request)
    {
        try
        {
            var report = await _ticketService.UpdateFieldReportAsync(id, reportId, request);
            return Ok(report);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Возвращает единую ленту событий заявки (создание, комментарии, акты).</summary>
    [HttpGet("{id:int}/timeline")]
    public async Task<ActionResult<IEnumerable<TimelineItemDto>>> GetTimeline(int id)
    {
        try
        {
            var items = await _ticketService.GetTimelineAsync(id);
            return Ok(items);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Черновик ответа: локальный поиск по БЗ/похожим заявкам или OpenAI (если ai_provider=openai).</summary>
    [HttpPost("{id:int}/suggest-reply")]
    public async Task<ActionResult<SuggestReplyResponse>> SuggestReply(int id)
    {
        try
        {
            var result = await _ticketAssist.SuggestReplyAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Возвращает комментарии к заявке.</summary>
    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments(int id)
    {
        var comments = await _ticketService.GetCommentsAsync(id);
        return Ok(comments);
    }

    /// <summary>Добавляет комментарий к заявке.</summary>
    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(int id, CreateCommentRequest request)
    {
        try
        {
            var comment = await _ticketService.AddCommentAsync(id, request, CurrentUserId(), CurrentUserName(), CurrentUserRole());
            return CreatedAtAction(nameof(GetComments), new { id }, comment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Добавляет или удаляет реакцию на комментарий (toggle).</summary>
    [HttpPost("{ticketId:int}/comments/{commentId:int}/reactions")]
    public async Task<ActionResult<CommentDto>> ToggleCommentReaction(int ticketId, int commentId, ToggleReactionRequest request)
    {
        try
        {
            var comment = await _ticketService.ToggleCommentReactionAsync(
                ticketId, commentId, CurrentUserId(), CurrentUserName(), request.Emoji);
            return Ok(comment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Мигрирует заявку из внешней системы.</summary>
    [HttpPost("migrate")]
    [Authorize(Roles = "super_admin,coordinator,sysadmin,head_support,head_dev,head_engineers,head_repair,director")]
    public async Task<ActionResult<Models.Ticket>> MigrateTicket(MigrateTicketRequest request)
    {
        var ticket = await _ticketService.MigrateTicketAsync(request);
        return Ok(ticket);
    }

    /// <summary>Мигрирует ремонтную заявку из внешней системы.</summary>
    [HttpPost("migrate-repair")]
    [Authorize(Roles = "super_admin,coordinator,sysadmin,head_support,head_dev,head_engineers,head_repair,director")]
    public async Task<ActionResult<Models.Ticket>> MigrateRepairTicket(MigrateRepairTicketRequest request)
    {
        try
        {
            var ticket = await _ticketService.MigrateRepairTicketAsync(request);
            return Ok(ticket);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Создаёт новую заявку.</summary>
    [HttpPost]
    public async Task<ActionResult<Models.Ticket>> CreateTicket(CreateTicketRequest request)
    {
        try
        {
            var ticket = await _ticketService.CreateTicketAsync(request);
            return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, ticket);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
