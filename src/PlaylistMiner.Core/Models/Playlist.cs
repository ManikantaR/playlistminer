namespace PlaylistMiner.Core.Models;

public class Playlist
{
    public int Id { get; set; }
    public required string YouTubeId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsInbox { get; set; }
    public bool IsManaged { get; set; }
    public string? Topic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime SyncedAt { get; set; }

    public ICollection<PlaylistVideo> PlaylistVideos { get; set; } = [];
    public ICollection<UndoLog> SourceUndoLogs { get; set; } = [];
    public ICollection<UndoLog> TargetUndoLogs { get; set; } = [];
}
