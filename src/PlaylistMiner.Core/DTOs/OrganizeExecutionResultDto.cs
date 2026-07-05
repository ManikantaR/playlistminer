namespace PlaylistMiner.Core.DTOs;

public record OrganizeExecutionResultDto(
    int VideosExamined,
    int MovesPlanned,
    int MovesExecuted,
    int MovesSkipped,
    int DeferredCount,
    List<string> Errors,
    string? RunId);
