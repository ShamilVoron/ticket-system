using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Локальные / Google-таблицы.</summary>
[Authorize(Roles = StaffRoles.All)]
[ApiController]
[Route("api/[controller]")]
public class SpreadsheetsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppDbContext _db;

    public SpreadsheetsController(AppDbContext db)
    {
        _db = db;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        var list = await _db.Spreadsheets.AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        var userIds = list.Select(s => s.CreatedByUserId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var names = await _db.UserAccounts.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName);

        var result = list.Select(s => MapListItem(s, names.GetValueOrDefault(s.CreatedByUserId)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> GetById(int id)
    {
        var s = await _db.Spreadsheets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();

        var name = await ResolveCreatorNameAsync(s.CreatedByUserId);
        return Ok(MapDetail(s, name));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateSpreadsheetRequest body)
    {
        var now = DateTime.UtcNow;
        var googleId = ExtractGoogleSheetId(body.GoogleSheetUrlOrId)
            ?? body.GoogleSheetId?.Trim()
            ?? string.Empty;

        var sourceKind = !string.IsNullOrEmpty(googleId) ? 1 : (body.SourceKind ?? 0);

        var entity = new Spreadsheet
        {
            Name = string.IsNullOrWhiteSpace(body.Name) ? "Без названия" : body.Name.Trim(),
            SourceKind = sourceKind,
            GoogleSheetId = googleId,
            Rows = body.Rows is > 0 ? body.Rows.Value : 20,
            Cols = body.Cols is > 0 ? body.Cols.Value : 10,
            CellsJson = "{}",
            CreatedByUserId = CurrentUserId ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Spreadsheets.Add(entity);
        await _db.SaveChangesAsync();

        var creatorName = await ResolveCreatorNameAsync(entity.CreatedByUserId);
        return Ok(MapDetail(entity, creatorName));
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<object>> UpdateMeta(int id, [FromBody] UpdateSpreadsheetMetaRequest body)
    {
        var entity = await _db.Spreadsheets.FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return NotFound();

        if (body.Name != null)
            entity.Name = string.IsNullOrWhiteSpace(body.Name) ? entity.Name : body.Name.Trim();
        if (body.Rows is > 0)
            entity.Rows = body.Rows.Value;
        if (body.Cols is > 0)
            entity.Cols = body.Cols.Value;
        if (body.SourceKind.HasValue)
            entity.SourceKind = body.SourceKind.Value;
        if (body.GoogleSheetId != null)
            entity.GoogleSheetId = ExtractGoogleSheetId(body.GoogleSheetId) ?? body.GoogleSheetId.Trim();
        if (body.GoogleSheetUrlOrId != null)
            entity.GoogleSheetId = ExtractGoogleSheetId(body.GoogleSheetUrlOrId) ?? entity.GoogleSheetId;

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var creatorName = await ResolveCreatorNameAsync(entity.CreatedByUserId);
        return Ok(MapDetail(entity, creatorName));
    }

    [HttpPatch("{id:int}/cells")]
    public async Task<IActionResult> PatchCells(int id, [FromBody] PatchCellsRequest body)
    {
        var entity = await _db.Spreadsheets.FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return NotFound();

        Dictionary<string, JsonElement> map;
        try
        {
            map = string.IsNullOrWhiteSpace(entity.CellsJson)
                ? new Dictionary<string, JsonElement>()
                : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entity.CellsJson, JsonOpts)
                  ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            map = new Dictionary<string, JsonElement>();
        }

        if (body.Replace == true && body.Cells != null)
        {
            entity.CellsJson = JsonSerializer.Serialize(body.Cells);
        }
        else if (body.CellsJson != null)
        {
            entity.CellsJson = body.CellsJson;
        }
        else if (body.Patches != null)
        {
            foreach (var patch in body.Patches)
            {
                if (string.IsNullOrWhiteSpace(patch.Key)) continue;
                if (patch.Cell.ValueKind == JsonValueKind.Undefined || patch.Cell.ValueKind == JsonValueKind.Null)
                    map.Remove(patch.Key);
                else
                    map[patch.Key] = patch.Cell.Clone();
            }
            entity.CellsJson = JsonSerializer.Serialize(map);
        }
        else if (body.Cells != null)
        {
            foreach (var kv in body.Cells)
                map[kv.Key] = kv.Value.Clone();
            entity.CellsJson = JsonSerializer.Serialize(map);
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Spreadsheets.FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return NotFound();
        _db.Spreadsheets.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Импорт xlsx (ClosedXML) — заполняет CellsJson; иначе stub ok.</summary>
    [HttpPost("{id:int}/import")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<object>> Import(int id, IFormFile? file)
    {
        var entity = await _db.Spreadsheets.FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return NotFound();

        if (file == null || file.Length == 0)
            return Ok(new { ok = true, imported = false, message = "No file provided." });

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (ext is ".xlsx" or ".xlsm")
        {
            try
            {
                await using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var sheet = workbook.Worksheets.First();
                var used = sheet.RangeUsed();
                var cells = new Dictionary<string, object>();
                var maxRow = 0;
                var maxCol = 0;

                if (used != null)
                {
                    foreach (var cell in used.CellsUsed())
                    {
                        var r = cell.Address.RowNumber - 1;
                        var c = cell.Address.ColumnNumber - 1;
                        maxRow = Math.Max(maxRow, r + 1);
                        maxCol = Math.Max(maxCol, c + 1);
                        var val = cell.GetFormattedString();
                        if (!string.IsNullOrEmpty(val))
                            cells[$"{r},{c}"] = new { value = val };
                    }
                }

                if (maxRow > 0) entity.Rows = Math.Max(entity.Rows, maxRow);
                if (maxCol > 0) entity.Cols = Math.Max(entity.Cols, maxCol);
                entity.CellsJson = JsonSerializer.Serialize(cells);
                entity.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                return Ok(new { ok = true, imported = true, cells = cells.Count, rows = entity.Rows, cols = entity.Cols });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Failed to parse xlsx: " + ex.Message });
            }
        }

        // Non-xlsx: acknowledge upload without parsing
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, imported = false, message = "File received; only .xlsx is parsed." });
    }

    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id)
    {
        var entity = await _db.Spreadsheets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return NotFound();

        Dictionary<string, JsonElement>? map = null;
        try
        {
            map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entity.CellsJson ?? "{}", JsonOpts);
        }
        catch { /* empty */ }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        if (map != null)
        {
            foreach (var (key, cell) in map)
            {
                var parts = key.Split(',');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], out var r) || !int.TryParse(parts[1], out var c)) continue;
                var value = cell.ValueKind == JsonValueKind.Object && cell.TryGetProperty("value", out var v)
                    ? v.ToString()
                    : cell.ToString();
                sheet.Cell(r + 1, c + 1).Value = value;
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        var fileName = $"{SanitizeFileName(entity.Name)}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private async Task<string?> ResolveCreatorNameAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        return await _db.UserAccounts.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync();
    }

    private static object MapListItem(Spreadsheet s, string? createdByName) => new
    {
        s.Id,
        s.Name,
        s.SourceKind,
        s.GoogleSheetId,
        s.Rows,
        s.Cols,
        s.CreatedByUserId,
        createdByName = createdByName ?? s.CreatedByUserId,
        s.CreatedAt,
        s.UpdatedAt,
    };

    private static object MapDetail(Spreadsheet s, string? createdByName)
    {
        object cells;
        try
        {
            cells = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(s.CellsJson ?? "{}", JsonOpts)
                    ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            cells = new Dictionary<string, object>();
        }

        return new
        {
            s.Id,
            s.Name,
            s.SourceKind,
            s.GoogleSheetId,
            s.Rows,
            s.Cols,
            cells,
            cellsJson = s.CellsJson,
            s.CreatedByUserId,
            createdByName = createdByName ?? s.CreatedByUserId,
            s.CreatedAt,
            s.UpdatedAt,
        };
    }

    private static string? ExtractGoogleSheetId(string? urlOrId)
    {
        if (string.IsNullOrWhiteSpace(urlOrId)) return null;
        var s = urlOrId.Trim();
        var m = Regex.Match(s, @"/spreadsheets/d/([a-zA-Z0-9-_]+)");
        if (m.Success) return m.Groups[1].Value;
        if (Regex.IsMatch(s, @"^[a-zA-Z0-9-_]+$") && s.Length >= 20)
            return s;
        return null;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "spreadsheet" : cleaned;
    }

    public record CreateSpreadsheetRequest(
        string? Name,
        int? SourceKind,
        string? GoogleSheetId,
        string? GoogleSheetUrlOrId,
        int? Rows,
        int? Cols);

    public record UpdateSpreadsheetMetaRequest(
        string? Name,
        int? Rows,
        int? Cols,
        int? SourceKind,
        string? GoogleSheetId,
        string? GoogleSheetUrlOrId);

    public class PatchCellsRequest
    {
        public List<CellPatch>? Patches { get; set; }
        public Dictionary<string, JsonElement>? Cells { get; set; }
        public string? CellsJson { get; set; }
        public bool? Replace { get; set; }
    }

    public class CellPatch
    {
        public string Key { get; set; } = string.Empty;
        public JsonElement Cell { get; set; }
    }
}
