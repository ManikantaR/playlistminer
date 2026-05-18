namespace PlaylistMiner.Core.Models;

public class UndoLog
{
    public int Id { get; set; }
    public int VideoId { get; set; }
    public required string Action { get; set; }
    public int? SourcePlaylistId { get; set; }
    public int? TargetPlaylistId { get; set; }
    public DateTime PerformedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Undone { get; set; }

    public Video Video { get; set; } = null!;
    public Playlist? SourcePlaylist { get; set; }
    public Playlist? TargetPlaylist { get; set; }
}
