namespace PlaylistMiner.Core.Models;

public class SyncRequest
{
    public int Id { get; set; }
    public required string Type { get; set; }   // "full" | "inbox"
    public required string Status { get; set; } // "pending" | "processing" | "completed" | "failed"
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
}
