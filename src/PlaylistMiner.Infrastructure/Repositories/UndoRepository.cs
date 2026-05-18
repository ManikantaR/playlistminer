using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Repositories;

public class UndoRepository(PlaylistMinerDbContext db) : IUndoRepository
{
    public async Task<List<UndoLogDto>> GetPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await db.UndoLogs
            .AsNoTracking()
            .Where(ul => !ul.Undone && ul.ExpiresAt > now)
            .Select(ul => new UndoLogDto(
                ul.Id,
                ul.VideoId,
                ul.Video.Title,
                ul.SourcePlaylistId ?? 0,
                ul.SourcePlaylist != null ? ul.SourcePlaylist.Name : string.Empty,
                ul.TargetPlaylistId ?? 0,
                ul.TargetPlaylist != null ? ul.TargetPlaylist.Name : string.Empty,
                ul.PerformedAt,
                ul.ExpiresAt,
                ul.Undone))
            .OrderByDescending(ul => ul.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<UndoLog> CreateAsync(UndoLog entry, CancellationToken ct = default)
    {
        db.UndoLogs.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task MarkUndoneAsync(int id, CancellationToken ct = default)
    {
        await db.UndoLogs
            .Where(ul => ul.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(ul => ul.Undone, true), ct);
    }

    public async Task CleanupExpiredAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await db.UndoLogs
            .Where(ul => ul.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);
    }
}
