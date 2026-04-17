using System.Text.Json;
using ITCafe.Api.Dtos;

namespace ITCafe.Api.Helpers;

public static class ReactionJsonParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static List<ReactionDto> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<ReactionDto>>(json, Options);
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string Serialize(IReadOnlyList<ReactionDto> reactions)
    {
        return JsonSerializer.Serialize(reactions, Options);
    }
}
