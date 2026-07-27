namespace ITCafe.Api.Models;

public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = "Default";
    public string? Slug { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
