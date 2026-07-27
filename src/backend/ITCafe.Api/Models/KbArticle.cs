namespace ITCafe.Api.Models;

public class KbArticle
{
    public int Id { get; set; }
    public int? CategoryId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Tags { get; set; } = "";
    public bool IsPublished { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public KbCategory? Category { get; set; }
}
