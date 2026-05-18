namespace PlaylistMiner.Core.Models;

public class PlaylistVideo
{
    public int PlaylistId { get; set; }
    public int VideoId { get; set; }
    public int Position { get; set; }
    public string? PlaylistItemId { get; set; }
    public DateTime AddedAt { get; set; }

    public Playlist Playlist { get; set; } = null!;
    public Video Video { get; set; } = null!;
}
