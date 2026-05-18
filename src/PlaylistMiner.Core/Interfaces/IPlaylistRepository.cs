using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.Interfaces;

public interface IPlaylistRepository
{
    Task<List<PlaylistDto>> GetAllAsync(CancellationToken ct = default);
    Task<Playlist> CreateAsync(Playlist playlist, CancellationToken ct = default);
    Task SetInboxAsync(int playlistId, CancellationToken ct = default);
    Task<Playlist?> GetInboxAsync(CancellationToken ct = default);
}
