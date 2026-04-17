using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

// Equipment tabs: replacement_fund | client_equipment | tools_supplies
// EquipmentType:  monoblok | printer | minipc | notebook | psu | ssd_belfood | printer_belfood | tool | supply
// FundStatus:     active | pj | decommission  (used for replacement_fund items)
public class Equipment
{
    public int Id { get; set; }
    public string Category { get; set; } = "our"; // legacy — kept for backward compat
    public string Tab { get; set; } = "replacement_fund";
    public string EquipmentType { get; set; } = "monoblok";
    public string FundStatus { get; set; } = "active"; // active | pj | decommission
    public string Name { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Defect { get; set; } = string.Empty;
    public string Processor { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string DiskInfo { get; set; } = string.Empty;
    public string OsInfo { get; set; } = string.Empty;
    public string Interfaces { get; set; } = string.Empty;
    public string Completeness { get; set; } = string.Empty;
    public string Faults { get; set; } = string.Empty;
    public string InstallPosition { get; set; } = string.Empty;
    public string PowerSpecs { get; set; } = string.Empty;
    public string IssuedTo { get; set; } = string.Empty; // for tools: who it's issued to
    public DateTime? PurchaseDate { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
