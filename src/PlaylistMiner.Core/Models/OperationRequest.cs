namespace PlaylistMiner.Core.Models;

public class OperationRequest
{
    public int Id { get; set; }
    public required string Type { get; set; }
    public required string Status { get; set; }
    public required string CreatedBy { get; set; }
    public string? Source { get; set; }
    public string? Target { get; set; }
    public int? MaxItems { get; set; }
    public int? QuotaEstimate { get; set; }
    public DateTime? NotBefore { get; set; }
    public string? AllowedWindowStart { get; set; }
    public string? AllowedWindowEnd { get; set; }
    public string? RunId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
}
