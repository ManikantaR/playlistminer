using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.Interfaces;

public interface IVideoRepository
{
    Task<PagedResult<VideoDto>> GetAllAsync(VideoFilter filter, CancellationToken ct = default);
    Task<VideoDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Video> UpsertAsync(Video video, CancellationToken ct = default);
    Task UpdateStatusAsync(int id, VideoStatus status, CancellationToken ct = default);
}
