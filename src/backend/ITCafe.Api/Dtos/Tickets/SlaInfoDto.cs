namespace ITCafe.Api.Dtos.Tickets;

public record SlaInfoDto(
    int ReactionMinutes,
    int ResolutionMinutes,
    DateTime? ReactionDeadline,
    DateTime? ResolutionDeadline,
    bool IsReactionBreached,
    bool IsResolutionBreached,
    int? RemainingReactionMinutes,
    int? RemainingResolutionMinutes
);
