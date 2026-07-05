using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.Interfaces;

public interface IPlaylistOrganizer
{
    Task<Playlist> EnsureManagedPlaylistAsync(string topic, CancellationToken ct = default);
    Task MoveVideoAsync(int videoId, int sourcePlaylistId, int targetPlaylistId, CancellationToken ct = default);
    Task UndoMoveAsync(int undoLogId, CancellationToken ct = default);
    Task<List<PlaylistDto>> ConsolidateAsync(CancellationToken ct = default);
    Task<List<DuplicateReviewDto>> GetDuplicateReviewAsync(CancellationToken ct = default);
}
