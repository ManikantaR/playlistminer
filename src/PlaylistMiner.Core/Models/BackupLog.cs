namespace PlaylistMiner.Core.Models;

public class BackupLog
{
    public int Id { get; set; }
    public required string Filename { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string Status { get; set; } // "success" | "failed"
    public string? Error { get; set; }
}
