using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

// MongoDB Model for Agent Customization
[BsonIgnoreExtraElements]
public class UserPreferences
{
    public string UserId { get; set; } = string.Empty;
    // Practical default for enterprise tool.
    public string Theme { get; set; } = "light";
    public string BackgroundUrl { get; set; } = string.Empty;
    public List<string> DashboardBlocks { get; set; } = new();
    public string AccentColor { get; set; } = "#23a836";
    public string WindowColor { get; set; } = "#ffffff";
    public string TextColor { get; set; } = "#111827";
}
