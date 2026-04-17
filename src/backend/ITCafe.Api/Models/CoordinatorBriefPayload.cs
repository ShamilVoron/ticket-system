using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

/// <summary>Поля формы брифа координатора → текст заявки в стиле Telegram.</summary>
public class CoordinatorBriefPayload
{
    public string? InternalCoordinatorBanner { get; set; }
    public string? Region { get; set; }
    public int? ObjectId { get; set; }
    public string? ObjectCode { get; set; }
    public string? ObjectAddress { get; set; }
    public string? ObjectVenueExtra { get; set; }
    public string? LegalEntity { get; set; }
    public List<CoordinatorBriefEquipmentItem>? Equipment { get; set; }
    public string? ContactPhones { get; set; }
    public List<CoordinatorBriefTaskLink>? TaskLinks { get; set; }
    /// <summary>UserId сотрудников (как в Employees.UserId).</summary>
    public List<string>? KnowledgeableUserIds { get; set; }
    public string? TaskOwnerNote { get; set; }
    public string? AgreedWith { get; set; }
    public string? SubjectEquipment { get; set; }
    public string? IntroContext { get; set; }
    public string? BringList { get; set; }
    public string? AdditionalWork { get; set; }
    public string? UrgencyDeadline { get; set; }

    public bool HasAnyContent()
    {
        bool Any(string? s) => !string.IsNullOrWhiteSpace(s);
        if (ObjectId > 0)
            return true;
        if (Any(InternalCoordinatorBanner) || Any(Region) || Any(ObjectCode) || Any(ObjectAddress) ||
            Any(ObjectVenueExtra) || Any(LegalEntity) || Any(ContactPhones) || Any(TaskOwnerNote) ||
            Any(AgreedWith) || Any(SubjectEquipment) || Any(IntroContext) || Any(BringList) ||
            Any(AdditionalWork) || Any(UrgencyDeadline))
            return true;
        if (Equipment != null && Equipment.Any(e => e.EquipmentId > 0))
            return true;
        if (TaskLinks != null && TaskLinks.Any(l => Any(l.Url) || Any(l.Number) || Any(l.Comment)))
            return true;
        if (KnowledgeableUserIds != null && KnowledgeableUserIds.Any(Any))
            return true;
        return false;
    }
}
