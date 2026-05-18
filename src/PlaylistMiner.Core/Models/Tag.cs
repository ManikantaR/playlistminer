namespace PlaylistMiner.Core.Models;

public class Tag
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<VideoTag> VideoTags { get; set; } = [];
    public ICollection<TagRule> TagRules { get; set; } = [];
}
