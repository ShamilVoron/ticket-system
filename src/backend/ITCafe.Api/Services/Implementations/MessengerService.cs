using ITCafe.Api.Data;
using ITCafe.Api.Dtos;
using ITCafe.Api.Dtos.Messenger;
using ITCafe.Api.Helpers;
using ITCafe.Api.Models;
using ITCafe.Api.Services.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Services.Implementations;

public class MessengerService(
    AppDbContext context,
    ChatRealtimeBroadcaster realtime,
    IWebHostEnvironment env,
    ILogger<MessengerService> log) : IMessengerService
{
    private const int MaxBodyLength = 8000;
    private const int PreviewLength = 140;
    private const long MaxAttachmentBytes = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf",
        ".docx", ".xlsx", ".zip", ".txt", ".csv",
    };

    private string WebRoot => env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    public Task<bool> UserIsMemberAsync(Guid conversationId, string userId) =>
        context.ChatMembers.AsNoTracking().AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId);

    public async Task<IReadOnlyList<ChatConversationListItemDto>> ListConversationsAsync(string currentUserId)
    {
        var convIds = await context.ChatMembers.AsNoTracking()
            .Where(m => m.UserId == currentUserId)
            .Select(m => m.ConversationId)
            .ToListAsync();

        if (convIds.Count == 0) return Array.Empty<ChatConversationListItemDto>();

        var convs = await context.ChatConversations.AsNoTracking()
            .Where(c => convIds.Contains(c.Id))
            .OrderByDescending(c => c.LastMessageAtUtc)
            .Take(80)
            .ToListAsync();

        var displayedConvIds = convs.Select(c => c.Id).ToList();

        var readStates = await context.UserChatReadStates.AsNoTracking()
            .Where(r => r.UserId == currentUserId && displayedConvIds.Contains(r.ConversationId))
            .ToDictionaryAsync(r => r.ConversationId, r => r.LastReadAt);

        // Batch: подсчёт непрочитанных за один запрос вместо N запросов
        var unreadCounts = new Dictionary<Guid, int>();
        foreach (var convId in displayedConvIds)
        {
            var lastRead = readStates.GetValueOrDefault(convId);
            unreadCounts[convId] = 0; // default
        }

        var unreadRows = await context.ChatMessages.AsNoTracking()
            .Where(m => displayedConvIds.Contains(m.ConversationId)
                && m.SenderUserId != currentUserId)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConvId = g.Key, Messages = g.ToList() })
            .ToListAsync();

        foreach (var row in unreadRows)
        {
            var lastRead = readStates.GetValueOrDefault(row.ConvId);
            unreadCounts[row.ConvId] = lastRead == default
                ? row.Messages.Count
                : row.Messages.Count(m => m.CreatedAtUtc > lastRead);
        }

        // Batch: последнее сообщение каждой беседы за один запрос
        var lastMessages = await context.ChatMessages.AsNoTracking()
            .Where(m => displayedConvIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.CreatedAtUtc).First())
            .ToDictionaryAsync(m => m.ConversationId);

        // Batch: peer userIds для direct-чатов
        var directConvIds = convs.Where(c => !c.IsGroup).Select(c => c.Id).ToList();
        var peerMap = new Dictionary<Guid, string>();
        if (directConvIds.Count > 0)
        {
            var peers = await context.ChatMembers.AsNoTracking()
                .Where(m => directConvIds.Contains(m.ConversationId) && m.UserId != currentUserId)
                .Select(m => new { m.ConversationId, m.UserId })
                .ToListAsync();
            foreach (var p in peers)
                peerMap.TryAdd(p.ConversationId, p.UserId);
        }

        // Batch: загружаем профили всех нужных пользователей одним запросом
        var peerUserIds = peerMap.Values.Distinct(StringComparer.Ordinal).ToList();
        var profileMap = new Dictionary<string, (string FullName, string? Avatar)>(StringComparer.Ordinal);
        if (peerUserIds.Count > 0)
        {
            var profiles = await (
                from u in context.UserAccounts.AsNoTracking()
                join e in context.Employees.AsNoTracking() on u.UserId equals e.UserId into ej
                from e in ej.DefaultIfEmpty()
                where peerUserIds.Contains(u.UserId)
                select new { u.UserId, u.FullName, Avatar = e != null ? e.AvatarUrl : string.Empty }
            ).ToListAsync();

            foreach (var p in profiles)
                profileMap[p.UserId] = (p.FullName, string.IsNullOrEmpty(p.Avatar) ? null : p.Avatar);
        }

        var result = new List<ChatConversationListItemDto>(convs.Count);
        foreach (var c in convs)
        {
            lastMessages.TryGetValue(c.Id, out var lastMsg);
            var lastPreview = lastMsg == null
                ? null
                : FormatMessagePreview(lastMsg.Body, lastMsg.AttachmentFileName, lastMsg.AttachmentMimeType);

            // Формируем элемент списка без дополнительных запросов
            string? peerId = null;
            string displayName;
            string? avatar = null;

            if (c.IsGroup)
            {
                displayName = c.Title ?? "Группа";
            }
            else
            {
                peerMap.TryGetValue(c.Id, out peerId);
                if (string.IsNullOrEmpty(peerId))
                {
                    displayName = "Диалог";
                }
                else if (profileMap.TryGetValue(peerId, out var prof))
                {
                    displayName = prof.FullName;
                    avatar = prof.Avatar;
                }
                else
                {
                    displayName = peerId;
                }
            }

            result.Add(new ChatConversationListItemDto(
                c.Id, c.IsGroup, c.Title, peerId, displayName, avatar,
                TruncatePreview(lastPreview), c.LastMessageAtUtc,
                unreadCounts.GetValueOrDefault(c.Id)));
        }

        return result;
    }

    public async Task<ChatConversationDetailDto?> GetConversationAsync(Guid conversationId, string currentUserId)
    {
        if (!await UserIsMemberAsync(conversationId, currentUserId)) return null;
        var c = await context.ChatConversations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId);
        if (c == null) return null;

        var memberIds = await context.ChatMembers.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.UserId)
            .ToListAsync();

        var members = new List<ChatMemberBriefDto>();
        foreach (var uid in memberIds)
        {
            var (name, av) = await GetProfileAsync(uid);
            members.Add(new ChatMemberBriefDto(uid, name, av));
        }

        return new ChatConversationDetailDto(c.Id, c.IsGroup, c.Title, members, c.LastMessageAtUtc);
    }

    public async Task<Guid> EnsureDirectConversationAsync(string currentUserId, string otherUserId)
    {
        if (string.Equals(currentUserId, otherUserId, StringComparison.Ordinal))
            throw new ArgumentException("Нельзя открыть диалог с самим собой.");

        if (!await IsStaffAsync(otherUserId))
            throw new ArgumentException("Можно писать только сотрудникам.");

        var existingId = await FindDirectConversationIdAsync(currentUserId, otherUserId);
        if (existingId.HasValue) return existingId.Value;

        var now = DateTime.UtcNow;
        var conv = new ChatConversation
        {
            Id = Guid.NewGuid(),
            IsGroup = false,
            Title = null,
            CreatedByUserId = currentUserId,
            CreatedAtUtc = now,
            LastMessageAtUtc = now,
            Members =
            {
                new ChatMember { UserId = currentUserId, JoinedAtUtc = now },
                new ChatMember { UserId = otherUserId, JoinedAtUtc = now },
            }
        };

        context.ChatConversations.Add(conv);
        await context.SaveChangesAsync();
        return conv.Id;
    }

    public async Task<Guid> CreateGroupConversationAsync(string currentUserId, string title, IReadOnlyList<string> memberUserIds)
    {
        var t = (title ?? string.Empty).Trim();
        if (t.Length is < 1 or > 120)
            throw new ArgumentException("Название группы: от 1 до 120 символов.");

        var ids = memberUserIds
            .Append(currentUserId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count < 2)
            throw new ArgumentException("В группе нужно минимум два участника.");

        foreach (var uid in ids)
        {
            if (!await IsStaffAsync(uid))
                throw new ArgumentException("В группу можно добавлять только сотрудников.");
        }

        var now = DateTime.UtcNow;
        var conv = new ChatConversation
        {
            Id = Guid.NewGuid(),
            IsGroup = true,
            Title = t,
            CreatedByUserId = currentUserId,
            CreatedAtUtc = now,
            LastMessageAtUtc = now,
            Members = ids.Select(uid => new ChatMember { UserId = uid, JoinedAtUtc = now }).ToList(),
        };

        context.ChatConversations.Add(conv);
        await context.SaveChangesAsync();
        return conv.Id;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        string currentUserId,
        Guid conversationId,
        Guid? beforeMessageId,
        int take)
    {
        if (!await UserIsMemberAsync(conversationId, currentUserId))
            throw new UnauthorizedAccessException();

        take = Math.Clamp(take, 1, 100);
        var q = context.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (beforeMessageId.HasValue)
        {
            var pivot = await q.Where(m => m.Id == beforeMessageId.Value).Select(m => m.CreatedAtUtc).FirstOrDefaultAsync();
            if (pivot != default)
                q = q.Where(m => m.CreatedAtUtc < pivot);
        }

        var rows = await q
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(take)
            .ToListAsync();

        rows.Reverse();
        var result = new List<ChatMessageDto>();
        foreach (var m in rows)
            result.Add(await ToMessageDtoAsync(m));

        return result;
    }

    public async Task<ChatMessageDto> PostMessageAsync(
        string currentUserId,
        Guid conversationId,
        string body,
        string? attachmentUrl,
        string? attachmentMimeType,
        string? attachmentFileName)
    {
        if (!await UserIsMemberAsync(conversationId, currentUserId))
            throw new UnauthorizedAccessException();

        var text = NormalizeChatBody(body ?? string.Empty);
        if (text.Length > MaxBodyLength)
            throw new ArgumentException($"Текст сообщения: не более {MaxBodyLength} символов.");

        string? attRel = null;
        string? attMime = string.IsNullOrWhiteSpace(attachmentMimeType) ? null : attachmentMimeType.Trim();
        string? attName = string.IsNullOrWhiteSpace(attachmentFileName) ? null : attachmentFileName.Trim();

        if (!string.IsNullOrWhiteSpace(attachmentUrl))
        {
            if (!TryResolveUploadedAttachment(WebRoot, conversationId, attachmentUrl, out attRel, out var fullPath) ||
                !System.IO.File.Exists(fullPath))
                throw new ArgumentException("Некорректное или отсутствующее вложение. Загрузите файл снова.");
            attMime ??= "application/octet-stream";
        }
        else
        {
            attMime = null;
            attName = null;
        }

        if (text.Length < 1 && string.IsNullOrEmpty(attRel))
            throw new ArgumentException("Нужен текст сообщения или вложение.");

        var conv = await context.ChatConversations.FirstAsync(c => c.Id == conversationId);
        var now = DateTime.UtcNow;
        var msg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderUserId = currentUserId,
            Body = text,
            AttachmentUrl = attRel == null ? null : PublicFileUrl(attRel),
            AttachmentMimeType = attRel == null ? null : attMime,
            AttachmentFileName = attRel == null ? null : attName,
            CreatedAtUtc = now,
        };
        context.ChatMessages.Add(msg);
        conv.LastMessageAtUtc = now;
        await context.SaveChangesAsync();

        log.LogInformation(
            "Messenger message ConversationId={ConversationId} SenderUserId={SenderUserId} BodyLength={BodyLength} HasAttachment={HasAtt}",
            conversationId, currentUserId, text.Length, attRel != null);

        var dto = await ToMessageDtoAsync(msg);

        var members = await context.ChatMembers.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.UserId)
            .ToListAsync();

        await realtime.BroadcastChatMessageAsync(conversationId, dto, members);

        var previewText = FormatMessagePreview(msg.Body, msg.AttachmentFileName, msg.AttachmentMimeType);
        var listItem = await ToListItemAsync(conv, currentUserId, TruncatePreview(previewText));
        if (listItem != null)
        {
            var sync = new ChatSidebarSyncDto(
                listItem.Id,
                listItem.IsGroup,
                listItem.Title,
                listItem.PeerUserId,
                listItem.DisplayName,
                listItem.AvatarUrl,
                listItem.LastMessagePreview,
                listItem.LastMessageAtUtc,
                currentUserId,
                "posted");
            await realtime.NotifySidebarAsync(members, sync);
        }

        return dto;
    }

    public async Task<ChatAttachmentUploadResultDto> UploadAttachmentAsync(
        string currentUserId,
        Guid conversationId,
        Stream fileStream,
        string fileName,
        string contentType,
        long contentLength)
    {
        if (!await UserIsMemberAsync(conversationId, currentUserId))
            throw new UnauthorizedAccessException();
        if (contentLength <= 0 || contentLength > MaxAttachmentBytes)
            throw new ArgumentException($"Размер файла: 1 Б — {MaxAttachmentBytes / 1024 / 1024} МБ.");

        var safeBase = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeBase))
            throw new ArgumentException("Имя файла обязательно.");

        var ext = Path.GetExtension(safeBase);
        if (!AllowedAttachmentExtensions.Contains(ext))
            throw new ArgumentException(
                $"Тип файла не разрешён ({ext}). Разрешены: {string.Join(", ", AllowedAttachmentExtensions)}.");

        await using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        ms.Position = 0;
        if (!await FileSignatureValidator.IsValidAsync(ms, ext))
            throw new ArgumentException("Содержимое файла не соответствует расширению.");
        ms.Position = 0;

        var dir = Path.Combine(WebRoot, "uploads", "chat", conversationId.ToString("N"));
        Directory.CreateDirectory(dir);

        var storedName = $"{Guid.NewGuid():N}_{safeBase}";
        var fullPath = Path.Combine(dir, storedName);
        await using (var outStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await ms.CopyToAsync(outStream);

        var relative = Path.Combine("uploads", "chat", conversationId.ToString("N"), storedName);
        var url = PublicFileUrl(replaceSeparators(relative));
        var fi = new FileInfo(fullPath);
        return new ChatAttachmentUploadResultDto(
            url,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            safeBase,
            fi.Length);
    }

    public async Task DeleteMessageAsync(string currentUserId, Guid conversationId, Guid messageId)
    {
        if (!await UserIsMemberAsync(conversationId, currentUserId))
            throw new UnauthorizedAccessException();

        var msg = await context.ChatMessages.FirstOrDefaultAsync(m =>
            m.Id == messageId && m.ConversationId == conversationId);
        if (msg == null)
            throw new KeyNotFoundException("Сообщение не найдено.");
        if (!string.Equals(msg.SenderUserId, currentUserId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Удалить можно только своё сообщение.");

        string? fileToDelete = null;
        if (!string.IsNullOrEmpty(msg.AttachmentUrl))
        {
            var rel = msg.AttachmentUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(WebRoot, rel));
            var uploadsRoot = Path.GetFullPath(Path.Combine(WebRoot, "uploads", "chat"));
            if (full.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(full))
                fileToDelete = full;
        }

        var conv = await context.ChatConversations.FirstAsync(c => c.Id == conversationId);
        var lastAt = await context.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Id != messageId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => m.CreatedAtUtc)
            .FirstOrDefaultAsync();
        if (lastAt == default)
            lastAt = conv.CreatedAtUtc;
        conv.LastMessageAtUtc = lastAt;

        context.ChatMessages.Remove(msg);
        await context.SaveChangesAsync();

        if (fileToDelete != null)
        {
            try
            {
                System.IO.File.Delete(fileToDelete);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Chat attachment delete failed {Path}", fileToDelete);
            }
        }

        var members = await context.ChatMembers.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.UserId)
            .ToListAsync();

        await realtime.BroadcastChatMessageDeletedAsync(conversationId, messageId, members);

        var lastRow = await context.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new { m.Body, m.AttachmentFileName, m.AttachmentMimeType })
            .FirstOrDefaultAsync();
        var preview = lastRow == null
            ? null
            : FormatMessagePreview(lastRow.Body, lastRow.AttachmentFileName, lastRow.AttachmentMimeType);

        var listItem = await ToListItemAsync(conv, currentUserId, TruncatePreview(preview));
        if (listItem != null)
        {
            var sync = new ChatSidebarSyncDto(
                listItem.Id,
                listItem.IsGroup,
                listItem.Title,
                listItem.PeerUserId,
                listItem.DisplayName,
                listItem.AvatarUrl,
                listItem.LastMessagePreview,
                listItem.LastMessageAtUtc,
                null,
                "deleted");
            await realtime.NotifySidebarAsync(members, sync);
        }
    }

    public async Task<ChatMessageDto> ToggleMessageReactionAsync(string currentUserId, Guid conversationId, Guid messageId, string emoji)
    {
        if (!await UserIsMemberAsync(conversationId, currentUserId))
            throw new UnauthorizedAccessException();

        var msg = await context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId)
            ?? throw new KeyNotFoundException("Сообщение не найдено.");

        var reactions = ReactionJsonParser.Parse(msg.ReactionsJson);
        var existing = reactions.FirstOrDefault(r => r.Emoji == emoji && r.UserId == currentUserId);
        if (existing != null)
        {
            reactions.Remove(existing);
        }
        else
        {
            var (name, _) = await GetProfileAsync(currentUserId);
            reactions.Add(new ReactionDto(emoji, currentUserId, name));
        }
        msg.ReactionsJson = ReactionJsonParser.Serialize(reactions);
        await context.SaveChangesAsync();

        var dto = await ToMessageDtoAsync(msg);
        var members = await context.ChatMembers.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.UserId)
            .ToListAsync();
        await realtime.BroadcastChatMessageUpdatedAsync(conversationId, dto, members);
        return dto;
    }

    public async Task<ChatConversationDetailDto?> UpdateGroupAsync(
        string currentUserId,
        Guid conversationId,
        string? title,
        IReadOnlyList<string>? addMemberUserIds,
        IReadOnlyList<string>? removeMemberUserIds)
    {
        if (!await UserIsMemberAsync(conversationId, currentUserId))
            throw new UnauthorizedAccessException();

        var conv = await context.ChatConversations
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return null;
        if (!conv.IsGroup)
            throw new InvalidOperationException("Это не групповой чат.");

        if (title != null)
        {
            var t = title.Trim();
            if (t.Length is < 1 or > 120)
                throw new ArgumentException("Название группы: от 1 до 120 символов.");
            conv.Title = t;
        }

        var toRemove = (removeMemberUserIds ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var toAdd = (addMemberUserIds ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var projected = new HashSet<string>(conv.Members.Select(m => m.UserId), StringComparer.Ordinal);
        foreach (var uid in toRemove)
            projected.Remove(uid);
        foreach (var uid in toAdd)
            projected.Add(uid);
        if (projected.Count < 2)
            throw new InvalidOperationException("В группе должно остаться минимум два участника.");

        var now = DateTime.UtcNow;
        foreach (var uid in toRemove)
        {
            var m = conv.Members.FirstOrDefault(x => x.UserId == uid);
            if (m != null)
                context.ChatMembers.Remove(m);
        }

        foreach (var uid in toAdd)
        {
            if (conv.Members.Any(m => m.UserId == uid)) continue;
            if (!await IsStaffAsync(uid))
                throw new ArgumentException("В группу можно добавлять только сотрудников.");
            conv.Members.Add(new ChatMember { UserId = uid, JoinedAtUtc = now });
        }

        await context.SaveChangesAsync();

        var detail = await GetConversationAsync(conversationId, currentUserId);
        var memberIds = await context.ChatMembers.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.UserId)
            .ToListAsync();
        if (detail != null)
            await realtime.NotifyConversationMetaAsync(memberIds, detail);

        return detail;
    }

    private async Task<Guid?> FindDirectConversationIdAsync(string userIdA, string userIdB)
    {
        return await (
                from c in context.ChatConversations.AsNoTracking()
                where !c.IsGroup
                where context.ChatMembers.Count(m => m.ConversationId == c.Id) == 2
                where context.ChatMembers.Any(m => m.ConversationId == c.Id && m.UserId == userIdA)
                where context.ChatMembers.Any(m => m.ConversationId == c.Id && m.UserId == userIdB)
                select (Guid?)c.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<bool> IsStaffAsync(string userId) =>
        await context.UserAccounts.AsNoTracking().AnyAsync(u => u.UserId == userId && u.Role != "client");

    private async Task<(string FullName, string? Avatar)> GetProfileAsync(string userId)
    {
        var row = await (
            from u in context.UserAccounts.AsNoTracking()
            join e in context.Employees.AsNoTracking() on u.UserId equals e.UserId into ej
            from e in ej.DefaultIfEmpty()
            where u.UserId == userId
            select new { u.FullName, Avatar = e != null ? e.AvatarUrl : string.Empty }).FirstOrDefaultAsync();

        return row == null
            ? (userId, null)
            : (row.FullName, string.IsNullOrEmpty(row.Avatar) ? null : row.Avatar);
    }

    private async Task<ChatMessageDto> ToMessageDtoAsync(ChatMessage m)
    {
        var (name, _) = await GetProfileAsync(m.SenderUserId);
        return new ChatMessageDto(
            m.Id,
            m.ConversationId,
            m.SenderUserId,
            name,
            m.Body,
            m.CreatedAtUtc,
            m.AttachmentUrl,
            m.AttachmentMimeType,
            m.AttachmentFileName,
            ReactionJsonParser.Parse(m.ReactionsJson));
    }

    private async Task<ChatConversationListItemDto?> ToListItemAsync(
        ChatConversation c,
        string currentUserId,
        string? lastPreview,
        int unreadCount = 0)
    {
        string? peerId = null;
        string displayName;
        string? avatar = null;

        if (c.IsGroup)
        {
            displayName = c.Title ?? "Группа";
        }
        else
        {
            peerId = await context.ChatMembers.AsNoTracking()
                .Where(m => m.ConversationId == c.Id && m.UserId != currentUserId)
                .Select(m => m.UserId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(peerId))
                displayName = "Диалог";
            else
            {
                var prof = await GetProfileAsync(peerId);
                displayName = prof.FullName;
                avatar = prof.Avatar;
            }
        }

        return new ChatConversationListItemDto(
            c.Id,
            c.IsGroup,
            c.Title,
            peerId,
            displayName,
            avatar,
            lastPreview,
            c.LastMessageAtUtc,
            unreadCount);
    }

    public async Task MarkConversationAsReadAsync(Guid conversationId, string userId)
    {
        if (!await UserIsMemberAsync(conversationId, userId)) return;
        var existing = await context.UserChatReadStates
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ConversationId == conversationId);
        if (existing != null)
        {
            existing.LastReadAt = DateTime.UtcNow;
        }
        else
        {
            context.UserChatReadStates.Add(new UserChatReadState
            {
                UserId = userId,
                ConversationId = conversationId,
                LastReadAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();
    }

    public async Task<Guid> EnsureDepartmentChannelAsync(string departmentSlug, string currentUserId)
    {
        if (!TryNormalizeDepartmentSlug(departmentSlug, out var slug, out var title))
            throw new ArgumentException("Неизвестный отдел. Допустимы: support, engineers, repair, coordinators, developers, accounting, procurement, sysadmin.");

        var existing = await context.ChatConversations
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.IsGroup && c.DepartmentSlug == slug);

        var now = DateTime.UtcNow;
        if (existing != null)
        {
            if (!existing.Members.Any(m => m.UserId == currentUserId))
            {
                existing.Members.Add(new ChatMember { UserId = currentUserId, JoinedAtUtc = now });
                await context.SaveChangesAsync();
            }
            return existing.Id;
        }

        var conv = new ChatConversation
        {
            Id = Guid.NewGuid(),
            IsGroup = true,
            Title = title,
            DepartmentSlug = slug,
            CreatedByUserId = currentUserId,
            CreatedAtUtc = now,
            LastMessageAtUtc = now,
            Members = { new ChatMember { UserId = currentUserId, JoinedAtUtc = now } },
        };
        context.ChatConversations.Add(conv);
        await context.SaveChangesAsync();
        return conv.Id;
    }

    public async Task<Guid> EnsureTicketChatAsync(int ticketId, string currentUserId)
    {
        var ticket = await context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new KeyNotFoundException($"Заявка #{ticketId} не найдена.");

        var existing = await context.ChatConversations
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.IsGroup && c.TicketId == ticketId);

        var now = DateTime.UtcNow;
        var memberIds = new HashSet<string>(StringComparer.Ordinal) { currentUserId };
        foreach (var a in SplitAssignees(ticket.Assignee))
        {
            if (await IsStaffAsync(a))
                memberIds.Add(a);
        }

        if (existing != null)
        {
            var added = false;
            foreach (var uid in memberIds)
            {
                if (existing.Members.Any(m => m.UserId == uid)) continue;
                existing.Members.Add(new ChatMember { UserId = uid, JoinedAtUtc = now });
                added = true;
            }
            if (added)
                await context.SaveChangesAsync();
            return existing.Id;
        }

        var conv = new ChatConversation
        {
            Id = Guid.NewGuid(),
            IsGroup = true,
            Title = $"Заявка #{ticketId}",
            TicketId = ticketId,
            CreatedByUserId = currentUserId,
            CreatedAtUtc = now,
            LastMessageAtUtc = now,
            Members = memberIds.Select(uid => new ChatMember { UserId = uid, JoinedAtUtc = now }).ToList(),
        };
        context.ChatConversations.Add(conv);

        var bootstrap = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conv.Id,
            SenderUserId = currentUserId,
            Body = $"Чат по заявке #{ticketId}",
            CreatedAtUtc = now,
        };
        context.ChatMessages.Add(bootstrap);
        await context.SaveChangesAsync();
        return conv.Id;
    }

    public async Task<IReadOnlyList<ChatMessageSearchResultDto>> SearchMessagesAsync(string currentUserId, string q)
    {
        var query = (q ?? string.Empty).Trim();
        if (query.Length < 1)
            return Array.Empty<ChatMessageSearchResultDto>();
        if (query.Length > 200)
            query = query[..200];

        var convIds = await context.ChatMembers.AsNoTracking()
            .Where(m => m.UserId == currentUserId)
            .Select(m => m.ConversationId)
            .ToListAsync();
        if (convIds.Count == 0)
            return Array.Empty<ChatMessageSearchResultDto>();

        var pattern = $"%{EscapeLikePattern(query)}%";
        var rows = await (
            from m in context.ChatMessages.AsNoTracking()
            where convIds.Contains(m.ConversationId)
            where EF.Functions.ILike(m.Body, pattern)
            orderby m.CreatedAtUtc descending
            select new { m.Id, m.ConversationId, m.Body, m.CreatedAtUtc, m.SenderUserId }
        ).Take(50).ToListAsync();

        var senderIds = rows.Select(r => r.SenderUserId).Distinct(StringComparer.Ordinal).ToList();
        var nameMap = new Dictionary<string, string>(StringComparer.Ordinal);
        if (senderIds.Count > 0)
        {
            var names = await context.UserAccounts.AsNoTracking()
                .Where(u => senderIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToListAsync();
            foreach (var n in names)
                nameMap[n.UserId] = n.FullName;
        }

        return rows.Select(r => new ChatMessageSearchResultDto(
            r.Id,
            r.ConversationId,
            r.Body,
            r.CreatedAtUtc,
            nameMap.GetValueOrDefault(r.SenderUserId, r.SenderUserId)
        )).ToList();
    }

    private static readonly Dictionary<string, string> DepartmentChannelTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["support"] = "#support",
        ["engineers"] = "#engineers",
        ["repair"] = "#repair",
        ["coordinators"] = "#coordinators",
        ["developers"] = "#developers",
        ["accounting"] = "#accounting",
        ["procurement"] = "#procurement",
        ["sysadmin"] = "#sysadmin",
        // aliases → canonical slug
        ["support_l1"] = "#support",
        ["support_l2"] = "#support",
        ["head_support"] = "#support",
        ["field_engineer"] = "#engineers",
        ["head_engineers"] = "#engineers",
        ["head_repair"] = "#repair",
        ["coordinator"] = "#coordinators",
        ["developer"] = "#developers",
        ["head_dev"] = "#developers",
        ["accountant"] = "#accounting",
        ["1 линия"] = "#support",
        ["2 линия"] = "#support",
        ["выездные инженеры"] = "#engineers",
        ["ремонт / сервис"] = "#repair",
        ["координатор"] = "#coordinators",
        ["разработчики"] = "#developers",
        ["бухгалтерия"] = "#accounting",
        ["закупки"] = "#procurement",
        ["системный администратор"] = "#sysadmin",
    };

    private static bool TryNormalizeDepartmentSlug(string? raw, out string slug, out string title)
    {
        slug = string.Empty;
        title = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var key = raw.Trim().TrimStart('#');
        if (!DepartmentChannelTitles.TryGetValue(key, out var channelTitle))
            return false;
        title = channelTitle;
        slug = channelTitle.TrimStart('#');
        return true;
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string[] SplitAssignees(string s) =>
        string.IsNullOrWhiteSpace(s) ? [] :
        s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string replaceSeparators(string path) =>
        path.Replace('\\', '/');

    private static string PublicFileUrl(string relativePathWithForwardSlashes) =>
        "/" + relativePathWithForwardSlashes.TrimStart('/').Replace('\\', '/');

    private static bool TryResolveUploadedAttachment(
        string webRoot,
        Guid conversationId,
        string clientUrl,
        out string relativePath,
        out string fullPath)
    {
        relativePath = string.Empty;
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(clientUrl)) return false;

        var n = clientUrl.Replace('\\', '/').Trim();
        if (!n.StartsWith('/')) n = '/' + n;
        var expectedPrefix = $"/uploads/chat/{conversationId:N}/";
        if (!n.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        var filePart = n[expectedPrefix.Length..];
        if (string.IsNullOrEmpty(filePart) || filePart.Contains("..", StringComparison.Ordinal)) return false;

        relativePath = $"uploads/chat/{conversationId:N}/{filePart}";
        fullPath = Path.GetFullPath(Path.Combine(webRoot, "uploads", "chat", conversationId.ToString("N"), filePart));
        var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", "chat"));
        return fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FormatMessagePreview(string body, string? attachmentFileName, string? attachmentMimeType)
    {
        var b = (body ?? string.Empty).Trim();
        if (b.Length > 0) return b;
        if (!string.IsNullOrEmpty(attachmentFileName))
            return "📎 " + attachmentFileName;
        if (!string.IsNullOrEmpty(attachmentMimeType) && attachmentMimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "📷 Изображение";
        return "📎 Вложение";
    }

    private static string? TruncatePreview(string? body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        body = body.ReplaceLineEndings(" ");
        return body.Length <= PreviewLength ? body : body[..PreviewLength] + "…";
    }

    private static string NormalizeChatBody(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        ReadOnlySpan<char> bidi = stackalloc char[] { '\u202a', '\u202b', '\u202c', '\u202d', '\u202e' };
        var s = raw.Replace("\0", string.Empty, StringComparison.Ordinal);
        s = s.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        foreach (var c in bidi)
            s = s.Replace(c.ToString(), string.Empty, StringComparison.Ordinal);
        return s.Trim();
    }
}
