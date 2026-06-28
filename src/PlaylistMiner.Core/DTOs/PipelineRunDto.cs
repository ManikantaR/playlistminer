using System;

namespace PlaylistMiner.Core.DTOs;

public record PipelineRunDto(
    string RunId,
    string PipelineType,
    string Status,
    string Phase,
    DateTime StartedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    string? CurrentMessage,
    string? Error,

    // Sync counters
    int PlaylistsDiscovered,
    int PlaylistsProcessed,
    int PlaylistItemsFetched,
    int UniqueVideoIdsIdentified,
    int VideoMetadataBatchesTotal,
    int VideoMetadataBatchesCompleted,
    int VideosUpserted,
    int PlaylistVideoLinksWritten,
    int VideosArchived,
    int VideosDeferred,
    int ErrorsCount,

    // Categorization counters
    int VideosPendingTagging,
    int VideosProcessed,
    int VideosTagged,
    int VideosSkipped,
    int RuleBasedHits,
    int TfidfHits,
    int OllamaHits,
    bool IsStalled = false
);
