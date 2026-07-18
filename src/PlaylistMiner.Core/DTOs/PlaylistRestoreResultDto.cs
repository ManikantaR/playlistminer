namespace PlaylistMiner.Core.DTOs;

public record PlaylistRestoreResultDto(
    int SourcePlaylistId,
    int TargetPlaylistId,
    int RequestedCount,
    int AddedCount,
    int SkippedCount,
    List<PlaylistRestoreItemDto> Added);
