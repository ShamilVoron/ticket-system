using System.ComponentModel.DataAnnotations;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace ITCafe.Api.Models;

public static class CoordinatorBriefFormatter
{
    public static string ToProblemText(CoordinatorBriefPayload p, Func<string, string>? resolveUserId = null)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(p.InternalCoordinatorBanner))
            lines.Add(p.InternalCoordinatorBanner.Trim().ToUpperInvariant());

        void Block(string title, string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return;
            lines.Add(title);
            foreach (var part in body.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                lines.Add(part);
            lines.Add(string.Empty);
        }

        Block("МИНСК / РЕГИОН", p.Region);
        Block("ТЕЛЕФОН МЕНЕДЖЕРА / ЧБР / ДИРЕКТОРА", p.ContactPhones);
        Block("ЧЕЙ ТАСК", p.TaskOwnerNote);
        Block("С КЕМ СОГЛАСОВАН ТАСК", p.AgreedWith);
        Block("ПРЕДМЕТ", p.SubjectEquipment);
        Block("ВВОДНЫЕ", p.IntroContext);
        Block("ЧТО С СОБОЙ БРАТЬ РАЗЪЕЗДНОМУ", p.BringList);
        Block("ДОПОМ", p.AdditionalWork);
        Block("СРОК (СРОЧНОСТЬ, ВРЕМЯ — В Т.Ч. ОБЯЗАТЕЛЬНОЕ «К 10» КАПСОМ)", p.UrgencyDeadline);

        while (lines.Count > 0 && string.IsNullOrEmpty(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        return string.Join("\n", lines);
    }
}
