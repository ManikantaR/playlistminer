namespace PlaylistMiner.Core.DTOs;

public record SyncResult(
    int VideosProcessed,
    int VideosCategorized,
    IReadOnlyList<string> Errors,
    int DeferredCount);
