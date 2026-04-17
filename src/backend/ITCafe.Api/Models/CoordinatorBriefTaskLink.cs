using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

/// <summary>Элемент «ссылка + номер таска» для поиска в TG с разных доменов.</summary>
public class CoordinatorBriefTaskLink
{
    public string? Url { get; set; }
    public string? Number { get; set; }
    public string? Comment { get; set; }
}
