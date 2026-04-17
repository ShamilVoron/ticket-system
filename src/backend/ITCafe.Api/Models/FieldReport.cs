using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

public class FieldReport
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string EngineerName { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; } = DateTime.UtcNow;

    // Тип действия: "Взяли в ремонт" / "Вернули из ремонта" / "Поставили подменку" / etc.
    public string ActionType { get; set; } = string.Empty;

    // Оборудование
    public string EquipmentType { get; set; } = string.Empty;  // Принтер, Моноблок, Киоск...
    public string EquipmentSerial { get; set; } = string.Empty; // SN (опционально)
    public string EquipmentStatus { get; set; } = string.Empty; // Работает / Не работает / Требует диагностики

    // Что сделано — свободный текст
    public string WorkDone { get; set; } = string.Empty;

    // Куда передали (при ремонте)
    public string TransferredTo { get; set; } = string.Empty;
}
