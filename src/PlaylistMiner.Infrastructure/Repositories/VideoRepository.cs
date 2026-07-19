using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Repositories;

public class VideoRepository(PlaylistMinerDbContext db) : IVideoRepository
{
    public async Task<PagedResult<VideoDto>> GetAllAsync(VideoFilter filter, CancellationToken ct = default)
    {
        var query = db.Videos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search;

            if (db.Database.IsNpgsql())
            {
                var videoIds = await db.Database
                    .SqlQueryRaw<int>(
                        """
                        SELECT v.id AS "Value"
                        FROM videos v
                        WHERE similarity(v.title, {0}) > 0.1
                           OR to_tsvector('english', v.title || ' ' || v.description)
                              @@ plainto_tsquery('english', {0})
                           OR v.title ILIKE '%' || {0} || '%'
                           OR v.description ILIKE '%' || {0} || '%'
                        ORDER BY similarity(v.title, {0}) DESC,
                                 ts_rank(to_tsvector('english', v.title || ' ' || v.description),
                                         plainto_tsquery('english', {0})) DESC
                        """,
                        search)
                    .ToListAsync(ct);

                query = query.Where(v => videoIds.Contains(v.Id));
            }
            else
            {
                var lowerSearch = search.ToLowerInvariant();
                query = query.Where(v =>
                    EF.Functions.ILike(v.Title, $"%{lowerSearch}%") ||
                    EF.Functions.ILike(v.Description, $"%{lowerSearch}%"));
            }
        }

        if (filter.Status.HasValue)
            query = query.Where(v => v.Status == filter.Status.Value);

        if (filter.PlaylistId.HasValue)
            query = query.Where(v => v.PlaylistVideos.Any(pv => pv.PlaylistId == filter.PlaylistId.Value));

        if (filter.Tags is { Length: > 0 })
        {
            foreach (var tagId in filter.Tags)
            {
                var tid = tagId;
                query = query.Where(v => v.VideoTags.Any(vt => vt.TagId == tid));
            }
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(v => new VideoDto(
                v.Id,
                v.YouTubeId,
                v.Title,
                v.ChannelName,
                v.ThumbnailUrl,
                v.Duration,
                v.PublishedAt,
                v.Status,
                v.VideoTags.Select(vt => new TagSuggestionDto(
                    vt.TagId,
                    vt.Tag.Name,
                    vt.Source,
                    vt.Confidence,
                    vt.Provider,
                    vt.ProviderModel)).ToList()))
            .ToListAsync(ct);

        return new PagedResult<VideoDto>(items, totalCount, filter.Page, filter.PageSize, totalPages);
    }

    public async Task<VideoDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Videos
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new VideoDetailDto(
                v.Id,
                v.YouTubeId,
                v.Title,
                v.Description,
                v.ChannelName,
                v.ChannelId,
                v.ThumbnailUrl,
                v.Duration,
                v.PublishedAt,
                v.Status,
                v.VideoTags.Select(vt => new TagSuggestionDto(
                    vt.TagId,
                    vt.Tag.Name,
                    vt.Source,
                    vt.Confidence,
                    vt.Provider,
                    vt.ProviderModel)).ToList(),
                v.PlaylistVideos.Select(pv => pv.Playlist.Name).ToList()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Video> UpsertAsync(Video video, CancellationToken ct = default)
    {
        var existing = await db.Videos
            .FirstOrDefaultAsync(v => v.YouTubeId == video.YouTubeId, ct);

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            video.CreatedAt = now;
            video.UpdatedAt = now;
            db.Videos.Add(video);
        }
        else
        {
            existing.Title = video.Title;
            existing.Description = video.Description;
            existing.ChannelName = video.ChannelName;
            existing.ChannelId = video.ChannelId;
            existing.ThumbnailUrl = video.ThumbnailUrl;
            existing.Duration = video.Duration;
            existing.Status = video.Status;
            existing.UpdatedAt = now;
            video = existing;
        }

        await db.SaveChangesAsync(ct);
        return video;
    }

    public async Task UpdateStatusAsync(int id, VideoStatus status, CancellationToken ct = default)
    {
        await db.Videos
            .Where(v => v.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Status, status)
                .SetProperty(v => v.UpdatedAt, DateTime.UtcNow), ct);
    }
}
