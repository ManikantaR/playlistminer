using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IPlaylistOrganizer
{
    Task MoveVideoAsync(int videoId, int sourcePlaylistId, int targetPlaylistId, CancellationToken ct = default);
    Task UndoMoveAsync(int undoLogId, CancellationToken ct = default);
    Task<List<PlaylistDto>> ConsolidateAsync(CancellationToken ct = default);
    Task<List<DuplicateReviewDto>> GetDuplicateReviewAsync(CancellationToken ct = default);
}
