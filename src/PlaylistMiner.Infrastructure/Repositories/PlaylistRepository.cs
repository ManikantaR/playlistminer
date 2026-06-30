using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Repositories;

public class PlaylistRepository(PlaylistMinerDbContext db) : IPlaylistRepository
{
    public async Task<List<PlaylistDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Playlists
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PlaylistDto(
                p.YouTubeId,
                p.Name,
                p.Description,
                p.IsInbox,
                p.PlaylistVideos.Count,
                p.Id))
            .ToListAsync(ct);
    }

    public async Task<Playlist> CreateAsync(Playlist playlist, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        playlist.CreatedAt = now;
        playlist.UpdatedAt = now;
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);
        return playlist;
    }

    public async Task SetInboxAsync(int playlistId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var exists = await db.Playlists.AnyAsync(p => p.Id == playlistId, ct);
        if (!exists)
        {
            throw new KeyNotFoundException($"Playlist with id {playlistId} was not found.");
        }

        await db.Playlists
            .Where(p => p.IsInbox)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsInbox, false), ct);

        await db.Playlists
            .Where(p => p.Id == playlistId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsInbox, true), ct);

        await transaction.CommitAsync(ct);
    }

    public async Task<Playlist?> GetInboxAsync(CancellationToken ct = default)
    {
        return await db.Playlists
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsInbox, ct);
    }
}
