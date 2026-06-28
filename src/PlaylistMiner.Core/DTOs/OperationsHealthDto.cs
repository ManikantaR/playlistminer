namespace PlaylistMiner.Core.DTOs;

public class OperationsHealthDto
{
    public required bool ApiHealthy { get; set; }
    public required bool DbHealthy { get; set; }
    public required bool WorkerHealthy { get; set; }
    public required int WorkerHeartbeatAgeSeconds { get; set; }
    public required bool OauthConnected { get; set; }
    public required bool QuotaExhausted { get; set; }
    public required bool OllamaReachable { get; set; }
    public required bool ActiveRunStalled { get; set; }
    public string? ActiveRunPhase { get; set; }
}
