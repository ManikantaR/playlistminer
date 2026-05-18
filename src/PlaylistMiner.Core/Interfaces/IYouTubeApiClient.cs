using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IYouTubeApiClient
{
    Task<List<PlaylistDto>> GetUserPlaylistsAsync(CancellationToken ct = default);
    Task<List<PlaylistItemDto>> GetPlaylistItemsAsync(string playlistId, CancellationToken ct = default);
    Task<List<VideoMetadataDto>> GetVideoMetadataAsync(IEnumerable<string> videoIds, CancellationToken ct = default);
    Task AddVideoToPlaylistAsync(string playlistId, string videoId, CancellationToken ct = default);
    Task RemoveVideoFromPlaylistAsync(string playlistId, string playlistItemId, CancellationToken ct = default);
    Task<PlaylistDto> CreatePlaylistAsync(string title, string description, CancellationToken ct = default);
}
