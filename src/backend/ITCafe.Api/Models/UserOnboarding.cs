using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

[BsonIgnoreExtraElements]
public class UserOnboarding
{
    public string UserId { get; set; } = string.Empty;
    public List<string> CompletedSteps { get; set; } = new();
    public bool IsCompleted { get; set; }
    public DateTime FirstLoginAt { get; set; } = DateTime.UtcNow;
}
