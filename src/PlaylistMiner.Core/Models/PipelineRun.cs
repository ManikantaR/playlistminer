using System;

namespace PlaylistMiner.Core.Models;

public class PipelineRun
{
    public int Id { get; set; }
    public required string RunId { get; set; }
    public required string PipelineType { get; set; }
    public required string Status { get; set; }
    public required string Phase { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CurrentMessage { get; set; }
    public string? Error { get; set; }
    public string? WorkerInstance { get; set; }
    public string? HostEnvironment { get; set; }
    public string? ActiveJobType { get; set; }

    // Sync counters
    public int PlaylistsDiscovered { get; set; }
    public int PlaylistsProcessed { get; set; }
    public int PlaylistItemsFetched { get; set; }
    public int UniqueVideoIdsIdentified { get; set; }
    public int VideoMetadataBatchesTotal { get; set; }
    public int VideoMetadataBatchesCompleted { get; set; }
    public int VideosUpserted { get; set; }
    public int PlaylistVideoLinksWritten { get; set; }
    public int VideosArchived { get; set; }
    public int VideosDeferred { get; set; }
    public int ErrorsCount { get; set; }

    // Categorization counters
    public int VideosPendingTagging { get; set; }
    public int VideosProcessed { get; set; }
    public int VideosTagged { get; set; }
    public int VideosSkipped { get; set; }
    public int RuleBasedHits { get; set; }
    public int TfidfHits { get; set; }
    public int OllamaHits { get; set; }
}
