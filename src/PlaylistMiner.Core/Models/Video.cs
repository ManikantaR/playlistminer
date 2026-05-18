namespace PlaylistMiner.Core.Models;

public class Video
{
    public int Id { get; set; }
    public required string YouTubeId { get; set; }
    public required string Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public required string ChannelName { get; set; }
    public required string ChannelId { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public DateTime PublishedAt { get; set; }
    public VideoStatus Status { get; set; } = VideoStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime SyncedAt { get; set; }

    public ICollection<VideoTag> VideoTags { get; set; } = [];
    public ICollection<PlaylistVideo> PlaylistVideos { get; set; } = [];
    public ICollection<UndoLog> UndoLogs { get; set; } = [];
}
