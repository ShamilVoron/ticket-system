namespace ITCafe.Api.Models;

public class Spreadsheet
{
    public int Id { get; set; }
    /// <summary>0 — локальная таблица в ITCafe, 1 — встроенная Google Таблица (только ссылка + iframe).</summary>
    public int SourceKind { get; set; }
    /// <summary>Идентификатор из URL (.../spreadsheets/d/THIS_ID/...).</summary>
    public string GoogleSheetId { get; set; } = string.Empty;
    public string Name { get; set; } = "Без названия";
    public int Rows { get; set; } = 10;
    public int Cols { get; set; } = 5;
    /// <summary>JSON: sparse map "row,col" → {value,bold,color,bgColor,type}</summary>
    public string CellsJson { get; set; } = "{}";
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
