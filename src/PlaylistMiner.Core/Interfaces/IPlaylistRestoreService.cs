using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IPlaylistRestoreService
{
    Task<PlaylistRestoreStatusDto> GetStatusAsync(
        int sourcePlaylistId,
        int targetPlaylistId,
        CancellationToken ct = default);

    Task<PlaylistRestoreResultDto> RestoreSampleAsync(
        int sourcePlaylistId,
        int targetPlaylistId,
        int maxCount,
        CancellationToken ct = default);

    Task<PlaylistRestoreResultDto> RestoreBatchAsync(
        int sourcePlaylistId,
        int targetPlaylistId,
        int maxCount,
        CancellationToken ct = default);
}
