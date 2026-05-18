using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.Interfaces;

public interface IUndoRepository
{
    Task<List<UndoLogDto>> GetPendingAsync(CancellationToken ct = default);
    Task<UndoLog> CreateAsync(UndoLog entry, CancellationToken ct = default);
    Task MarkUndoneAsync(int id, CancellationToken ct = default);
    Task CleanupExpiredAsync(CancellationToken ct = default);
}
