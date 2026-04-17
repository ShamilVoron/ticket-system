using System.ComponentModel.DataAnnotations;

namespace ITCafe.Api.Models;

public class SlaPolicy
{
    public int Id { get; set; }
    public string Priority { get; set; } = "*";
    public string RequestType { get; set; } = "*";
    public string Department { get; set; } = "*";
    public string ClientCategory { get; set; } = "*";
    public int ReactionMinutes { get; set; }
    public int ResolutionMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}
