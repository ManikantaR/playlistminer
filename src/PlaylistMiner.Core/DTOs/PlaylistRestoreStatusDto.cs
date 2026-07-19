namespace PlaylistMiner.Core.DTOs;

public record PlaylistRestoreStatusDto(
    int SourcePlaylistId,
    int TargetPlaylistId,
    string SourcePlaylistName,
    string TargetPlaylistName,
    int SourceTotalCount,
    int TargetTotalCount,
    int AlreadyPresentCount,
    int RemainingCount);
