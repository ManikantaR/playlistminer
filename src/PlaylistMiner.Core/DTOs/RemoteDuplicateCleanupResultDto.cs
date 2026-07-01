namespace PlaylistMiner.Core.DTOs;

public record RemoteDuplicateCleanupResultDto(
    int VideosExamined,
    int RemovalsPlanned,
    int RemovalsExecuted,
    int RemovalsSkipped,
    int DeferredCount,
    List<string> Errors,
    string? RunId = null);
