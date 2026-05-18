namespace PlaylistMiner.Core.Models;

public class Setting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}
