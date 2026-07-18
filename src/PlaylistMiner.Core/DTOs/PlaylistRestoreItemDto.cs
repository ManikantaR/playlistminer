namespace PlaylistMiner.Core.DTOs;

public record PlaylistRestoreItemDto(
    int VideoId,
    string YouTubeId,
    string Title,
    int SourcePosition,
    int TargetPosition,
    string PlaylistItemId);
