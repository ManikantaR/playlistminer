using System;
using System.Text.Json.Serialization;

namespace PlaylistMiner.Core.DTOs;

public class DependencyHealthDto
{
    public required string Database { get; set; } // "healthy" | "unhealthy"
    [JsonPropertyName("oauthConnected")]
    public required bool OAuthConnected { get; set; }
    [JsonPropertyName("youtubeQuotaAvailable")]
    public required bool YouTubeQuotaAvailable { get; set; }
    public required bool OllamaReachable { get; set; }
    public required string WorkerStatus { get; set; } // "healthy" | "stale" | "unknown"
    public required DateTime? WorkerLastHeartbeat { get; set; }
}
