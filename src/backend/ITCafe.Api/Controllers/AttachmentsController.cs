using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITCafe.Api.Data;
using ITCafe.Api.Helpers;
using ITCafe.Api.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using ITCafe.Api.Services;

namespace ITCafe.Api.Controllers;

/// <summary>Управление вложениями заявки.</summary>
[Authorize]
[ApiController]
[Route("api/tickets/{ticketId:int}/attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly TicketRealtimeBroadcaster _realtime;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf",
        ".docx", ".xlsx", ".zip", ".txt", ".json", ".csv", ".mp4"
    };

    public AttachmentsController(AppDbContext context, IWebHostEnvironment env, TicketRealtimeBroadcaster realtime, IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _env = env;
        _realtime = realtime;
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
    }

    private string? CurrentUserId() =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

    private string? CurrentUserRole() =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value;

    private bool IsStaff() => !string.Equals(CurrentUserRole(), "client", StringComparison.OrdinalIgnoreCase);

    private static List<string> TicketNotifyRecipients(Ticket t, string? excludeUserId)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in (t.Assignee ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!string.IsNullOrWhiteSpace(a)) set.Add(a.Trim());
        if (!string.IsNullOrEmpty(t.CreatedByUserId)) set.Add(t.CreatedByUserId);
        if (!string.IsNullOrEmpty(excludeUserId)) set.Remove(excludeUserId);
        return set.ToList();
    }

    /// <summary>Публичный URL файла: только путь, чтобы браузер ходил на тот же хост, что и фронт (прокси /uploads).</summary>
    private async Task<bool> HasPermissionAsync(string permissionKey)
    {
        var uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid)) return false;
        var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == uid);
        if (emp == null) return false;
        try
        {
            var perms = JsonSerializer.Deserialize<Dictionary<string, bool>>(emp.PermissionsJson);
            if (perms != null && perms.TryGetValue(permissionKey, out var val)) return val;
        }
        catch { }
        return false;
    }

    private async Task EnsureTicketInteractionAccessAsync(Ticket ticket)
    {
        if (IsStaff())
        {
            var uid = CurrentUserId();
            if (!string.IsNullOrEmpty(ticket.CreatedByUserId) && ticket.CreatedByUserId == uid) return;
            if (!string.IsNullOrEmpty(ticket.Assignee))
            {
                var assignees = ticket.Assignee.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (assignees.Contains(uid)) return;
            }
            if (await HasPermissionAsync("ticketInteractForeign")) return;
        }

        var userId = CurrentUserId();
        if (!string.IsNullOrEmpty(ticket.CreatedByUserId) && ticket.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("You are not authorized to interact with this ticket.");
    }

    private static string PublicFileUrl(string relativePath) =>
        "/" + relativePath.Replace("\\", "/");

    private void EnsureTicketAccess(Ticket ticket)
    {
        if (IsStaff()) return;
        var uid = CurrentUserId();
        if (!string.IsNullOrEmpty(ticket.CreatedByUserId) && ticket.CreatedByUserId != uid)
            throw new UnauthorizedAccessException("You are not authorized to access this ticket.");
    }

    public record AttachmentDto(int Id, int TicketId, int? CommentId, int? SubtaskId, string FileName, string Url, string ContentType, long FileSizeBytes, string UploadedBy, DateTime UploadedAt, int? OkdeskId);

    /// <summary>Возвращает вложения заявки.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AttachmentDto>>> GetAll(int ticketId)
    {
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) return NotFound("Ticket not found");
        await EnsureTicketInteractionAccessAsync(ticket);

        var rows = await _context.TicketAttachments
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.UploadedAt)
            .ToListAsync();
        return rows.Select(a => new AttachmentDto(a.Id, a.TicketId, a.CommentId, a.SubtaskId, a.FileName,
            PublicFileUrl(a.FilePath), a.ContentType, a.FileSizeBytes, a.UploadedBy, a.UploadedAt, a.OkdeskId))
            .ToList();
    }

    /// <summary>Загружает файл во вложения заявки.</summary>
    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> Upload(int ticketId, IFormFile file, [FromForm] string uploadedBy = "", [FromForm] int? commentId = null, [FromForm] int? subtaskId = null, [FromForm] int? okdeskId = null)
    {
        if (file == null || file.Length == 0) return BadRequest("No file");
        if (file.Length > 50 * 1024 * 1024) return BadRequest("File exceeds 50 MB limit");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            return BadRequest($"Unsupported file type: {ext}. Allowed: {string.Join(", ", AllowedExtensions)}");

        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) return NotFound("Ticket not found");
        EnsureTicketAccess(ticket);

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;
        if (!await FileSignatureValidator.IsValidAsync(ms, ext))
            return BadRequest("File content does not match the declared extension.");
        ms.Position = 0;

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "tickets", ticketId.ToString());
        Directory.CreateDirectory(uploadsDir);

        var safeFileName = Path.GetFileName(file.FileName);
        var uniqueName   = $"{Guid.NewGuid():N}_{safeFileName}";
        var fullPath     = Path.Combine(uploadsDir, uniqueName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await ms.CopyToAsync(stream);

        var relativePath = Path.Combine("uploads", "tickets", ticketId.ToString(), uniqueName);

        var attachment = new TicketAttachment
        {
            TicketId      = ticketId,
            CommentId     = commentId,
            SubtaskId     = subtaskId,
            FileName      = safeFileName,
            FilePath      = relativePath,
            ContentType   = file.ContentType,
            FileSizeBytes = file.Length,
            UploadedBy    = uploadedBy,
            OkdeskId      = okdeskId
        };
        _context.TicketAttachments.Add(attachment);
        await _context.SaveChangesAsync();
        var uid = CurrentUserId();
        await _realtime.NotifyTicketChangedAsync(
            ticketId,
            "attachment",
            uid,
            $"Вложение к заявке #{ticketId}: {safeFileName}",
            TicketNotifyRecipients(ticket, uid));

        return new AttachmentDto(attachment.Id, ticketId, commentId, subtaskId, safeFileName,
            PublicFileUrl(relativePath),
            file.ContentType, file.Length, uploadedBy, attachment.UploadedAt, okdeskId);
    }

    // Migration endpoint: upload a file from an external URL (used by the migration script)
    /// <summary>Загружает вложение по внешней ссылке.</summary>
    [HttpPost("from-url")]
    [Authorize(Roles = "super_admin,coordinator,sysadmin")]
    public async Task<ActionResult<AttachmentDto>> UploadFromUrl(int ticketId, [FromBody] UploadFromUrlRequest req)
    {
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) return NotFound("Ticket not found");

        if (string.IsNullOrWhiteSpace(req.FileUrl) || !Uri.TryCreate(req.FileUrl, UriKind.Absolute, out var uri))
            return BadRequest("Invalid URL");

        if (!IsUrlAllowed(uri))
            return BadRequest("URL is not allowed.");

        // Check idempotency: if OkdeskId already exists, return existing record
        if (req.OkdeskId.HasValue)
        {
            var existing = await _context.TicketAttachments.FirstOrDefaultAsync(a => a.OkdeskId == req.OkdeskId);
            if (existing != null)
            {
                return new AttachmentDto(existing.Id, existing.TicketId, existing.CommentId, existing.SubtaskId, existing.FileName,
                    PublicFileUrl(existing.FilePath), existing.ContentType, existing.FileSizeBytes, existing.UploadedBy, existing.UploadedAt, existing.OkdeskId);
            }
        }

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        var bytes = await http.GetByteArrayAsync(req.FileUrl);

        var safeFileName = Path.GetFileName(req.FileName ?? "attachment");
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "attachment";

        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest($"Unsupported file type: {ext}. Allowed: {string.Join(", ", AllowedExtensions)}");

        const long MaxFileSize = 50L * 1024 * 1024;
        if (bytes.Length > MaxFileSize)
            return BadRequest("File exceeds 50 MB limit.");

        await using var urlMs = new MemoryStream(bytes);
        if (!await FileSignatureValidator.IsValidAsync(urlMs, ext))
            return BadRequest("Downloaded file content does not match the declared extension.");

        var contentType = req.ContentType ?? "application/octet-stream";

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "tickets", ticketId.ToString());
        Directory.CreateDirectory(uploadsDir);

        var uniqueName = $"{Guid.NewGuid():N}_{safeFileName}";
        var fullPath   = Path.Combine(uploadsDir, uniqueName);
        var resolvedPath = Path.GetFullPath(fullPath);
        var resolvedDir  = Path.GetFullPath(uploadsDir);
        if (!resolvedPath.StartsWith(resolvedDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return BadRequest("Invalid file name.");

        await System.IO.File.WriteAllBytesAsync(fullPath, bytes);

        var relativePath = Path.Combine("uploads", "tickets", ticketId.ToString(), uniqueName);
        var attachment = new TicketAttachment
        {
            TicketId      = ticketId,
            CommentId     = req.CommentId,
            FileName      = safeFileName,
            FilePath      = relativePath,
            ContentType   = contentType,
            FileSizeBytes = bytes.Length,
            UploadedBy    = req.UploadedBy ?? "migration",
            OkdeskId      = req.OkdeskId
        };
        _context.TicketAttachments.Add(attachment);
        await _context.SaveChangesAsync();
        var uidM = CurrentUserId();
        await _realtime.NotifyTicketChangedAsync(
            ticketId,
            "attachment",
            uidM,
            $"Вложение к заявке #{ticketId}: {safeFileName}",
            TicketNotifyRecipients(ticket, uidM));

        return new AttachmentDto(attachment.Id, ticketId, req.CommentId, null, safeFileName,
            PublicFileUrl(relativePath), contentType, bytes.Length, attachment.UploadedBy, attachment.UploadedAt, req.OkdeskId);
    }

    private static bool IsUrlAllowed(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IPAddress.TryParse(uri.Host, out var ip))
        {
            if (IPAddress.IsLoopback(ip)) return false;
            var bytes = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10) return false;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return false;
            // 127.0.0.0/8
            if (bytes[0] == 127) return false;
            // 169.254.169.254 (metadata)
            if (bytes[0] == 169 && bytes[1] == 254) return false;
        }

        return true;
    }

    public record UploadFromUrlRequest(string FileUrl, string? FileName, string? ContentType, string? UploadedBy, int? CommentId, int? OkdeskId);
}
