using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IPlaylistRestoreService
{
    Task<PlaylistRestoreResultDto> RestoreSampleAsync(
        int sourcePlaylistId,
        int targetPlaylistId,
        int maxCount,
        CancellationToken ct = default);
}
