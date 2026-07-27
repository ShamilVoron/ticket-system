using System.Security.Claims;
using ITCafe.Api.Dtos;
using ITCafe.Api.Dtos.Messenger;
using ITCafe.Api.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ITCafe.Api.Controllers;

/// <summary>Внутренний мессенджер (только сотрудники).</summary>
[Authorize(Policy = "StaffOnly")]
[ApiController]
[Route("api/[controller]")]
public class MessengerController : ControllerBase
{
    private readonly IMessengerService _messenger;

    public MessengerController(IMessengerService messenger)
    {
        _messenger = messenger;
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("User identifier not found");

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ChatConversationListItemDto>>> ListConversations()
    {
        var list = await _messenger.ListConversationsAsync(CurrentUserId());
        return Ok(list);
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<ChatConversationDetailDto>> GetConversation(Guid id)
    {
        var detail = await _messenger.GetConversationAsync(id, CurrentUserId());
        if (detail == null) return NotFound();
        return Ok(detail);
    }

    [HttpPost("conversations/direct")]
    [EnableRateLimiting("messenger_write")]
    public async Task<ActionResult<object>> EnsureDirect([FromBody] CreateDirectChatRequest request)
    {
        var id = await _messenger.EnsureDirectConversationAsync(CurrentUserId(), request.OtherUserId);
        return Ok(new { id });
    }

    [HttpPost("conversations/group")]
    [EnableRateLimiting("messenger_write")]
    public async Task<ActionResult<object>> CreateGroup([FromBody] CreateGroupChatRequest request)
    {
        var id = await _messenger.CreateGroupConversationAsync(
            CurrentUserId(),
            request.Title,
            request.MemberUserIds);
        return Ok(new { id });
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetMessages(
        Guid id,
        [FromQuery] string? before = null,
        [FromQuery] int take = 80)
    {
        Guid? beforeId = null;
        if (!string.IsNullOrWhiteSpace(before))
        {
            if (!Guid.TryParse(before, out var parsed))
                return BadRequest("Invalid before message id.");
            beforeId = parsed;
        }

        var messages = await _messenger.GetMessagesAsync(CurrentUserId(), id, beforeId, take);
        return Ok(messages);
    }

    [HttpPost("conversations/{id:guid}/messages")]
    [EnableRateLimiting("messenger_write")]
    public async Task<ActionResult<ChatMessageDto>> PostMessage(
        Guid id,
        [FromBody] PostChatMessageRequest request)
    {
        var dto = await _messenger.PostMessageAsync(
            CurrentUserId(),
            id,
            request.Body,
            request.AttachmentUrl,
            request.AttachmentMimeType,
            request.AttachmentFileName);
        return Ok(dto);
    }

    [HttpPost("conversations/{id:guid}/attachments")]
    [EnableRateLimiting("messenger_write")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<ChatAttachmentUploadResultDto>> UploadAttachment(Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File is required.");

        await using var stream = file.OpenReadStream();
        var result = await _messenger.UploadAttachmentAsync(
            CurrentUserId(),
            id,
            stream,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            file.Length);
        return Ok(result);
    }

    [HttpDelete("conversations/{id:guid}/messages/{messageId:guid}")]
    [EnableRateLimiting("messenger_write")]
    public async Task<IActionResult> DeleteMessage(Guid id, Guid messageId)
    {
        await _messenger.DeleteMessageAsync(CurrentUserId(), id, messageId);
        return Ok();
    }

    [HttpPost("conversations/{id:guid}/messages/{messageId:guid}/reactions")]
    [EnableRateLimiting("messenger_write")]
    public async Task<ActionResult<ChatMessageDto>> ToggleReaction(
        Guid id,
        Guid messageId,
        [FromBody] ToggleReactionRequest request)
    {
        var dto = await _messenger.ToggleMessageReactionAsync(
            CurrentUserId(),
            id,
            messageId,
            request.Emoji);
        return Ok(dto);
    }

    [HttpPatch("conversations/{id:guid}/group")]
    [EnableRateLimiting("messenger_write")]
    public async Task<ActionResult<ChatConversationDetailDto>> UpdateGroup(
        Guid id,
        [FromBody] UpdateGroupChatRequest request)
    {
        var detail = await _messenger.UpdateGroupAsync(
            CurrentUserId(),
            id,
            request.Title,
            request.AddMemberUserIds,
            request.RemoveMemberUserIds);
        if (detail == null) return NotFound();
        return Ok(detail);
    }

    [HttpPost("conversations/{id:guid}/read")]
    [EnableRateLimiting("messenger_write")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _messenger.MarkConversationAsReadAsync(id, CurrentUserId());
        return Ok();
    }

    [HttpPost("channels/department")]
    [EnableRateLimiting("messenger_write")]
    public async Task<ActionResult<object>> EnsureDepartmentChannel([FromBody] EnsureDepartmentChannelRequest request)
    {
        try
        {
            var id = await _messenger.EnsureDepartmentChannelAsync(request.DepartmentSlug, CurrentUserId());
            return Ok(new { id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("conversations/ticket/{ticketId:int}")]
    [EnableRateLimiting("messenger_write")]
    public async Task<ActionResult<object>> EnsureTicketChat(int ticketId)
    {
        try
        {
            var id = await _messenger.EnsureTicketChatAsync(ticketId, CurrentUserId());
            return Ok(new { id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Заявка #{ticketId} не найдена." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageSearchResultDto>>> Search([FromQuery] string q)
    {
        var results = await _messenger.SearchMessagesAsync(CurrentUserId(), q ?? string.Empty);
        return Ok(results);
    }
}
