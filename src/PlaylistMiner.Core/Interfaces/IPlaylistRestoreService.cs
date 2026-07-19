using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IPlaylistRestoreService
{
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
