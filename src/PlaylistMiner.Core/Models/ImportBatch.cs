namespace PlaylistMiner.Core.Models;

public class ImportBatch
{
    public int Id { get; set; }
    public required string Source { get; set; }
    public required string Filename { get; set; }
    public int TotalVideos { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime ImportedAt { get; set; }
}
