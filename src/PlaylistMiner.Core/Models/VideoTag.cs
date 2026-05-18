namespace PlaylistMiner.Core.Models;

public class VideoTag
{
    public int VideoId { get; set; }
    public int TagId { get; set; }
    public TagSource Source { get; set; }
    public float? Confidence { get; set; }
    public DateTime CreatedAt { get; set; }

    public Video Video { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
