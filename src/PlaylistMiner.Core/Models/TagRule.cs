namespace PlaylistMiner.Core.Models;

public class TagRule
{
    public int Id { get; set; }
    public int TagId { get; set; }
    public required string Keyword { get; set; }
    public TagRuleField Field { get; set; } = TagRuleField.Both;
    public float Weight { get; set; } = 0.5f;
    public bool IsLearned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Tag Tag { get; set; } = null!;
}
