using System;

namespace PlaylistMiner.Core.Models;

public class PipelineEvent
{
    public int Id { get; set; }
    public required string RunId { get; set; }
    public DateTime OccurredAt { get; set; }
    public required string Level { get; set; } // "info" | "warning" | "error"
    public required string Phase { get; set; }
    public required string Message { get; set; }
    public string? PayloadJson { get; set; }
}
