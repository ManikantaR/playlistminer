namespace PlaylistMiner.Core.DTOs;

public record OrganizePlanItemDto(
    string Action,
    int? VideoId,
    string? YouTubeId,
    string? Title,
    string? SourcePlaylistName,
    string? TargetPlaylistName,
    int? TargetPlaylistId,
    string? Topic,
    float? Confidence,
    int EstimatedQuotaCost,
    string Reason);
