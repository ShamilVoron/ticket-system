namespace ITCafe.Api.Models;

public class AutomationRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    /// <summary>ticket_created | sla_80 | sla_breach | status_resolved | vip_email_domain</summary>
    public string Trigger { get; set; } = "";
    public string ConditionsJson { get; set; } = "{}";
    /// <summary>JSON array: [{type, params}]</summary>
    public string ActionsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
