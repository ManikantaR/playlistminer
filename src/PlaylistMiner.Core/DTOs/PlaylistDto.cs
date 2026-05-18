namespace PlaylistMiner.Core.DTOs;

public record PlaylistDto(
    string YouTubeId,
    string Name,
    string? Description,
    bool IsInbox,
    int ItemCount);
