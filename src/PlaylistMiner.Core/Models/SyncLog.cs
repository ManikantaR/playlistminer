namespace PlaylistMiner.Core.Models;

public class SyncLog
{
    public int Id { get; set; }
    public required string SyncType { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int VideosProcessed { get; set; }
    public int VideosCategorized { get; set; }
    public string? Errors { get; set; }
    public required string Status { get; set; }
}
