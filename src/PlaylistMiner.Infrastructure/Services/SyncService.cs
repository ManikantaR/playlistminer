using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public sealed class SyncService(
    IYouTubeApiClient youTubeApiClient,
    IQuotaTracker quotaTracker,
    PlaylistMinerDbContext db,
    ILogger<SyncService> logger) : ISyncService
{
    public async Task<SyncResult> FullSyncAsync(CancellationToken ct = default)
    {
        return await RunSyncAsync(syncInboxOnly: false, ct);
    }

    public async Task<SyncResult> SyncInboxAsync(CancellationToken ct = default)
    {
        return await RunSyncAsync(syncInboxOnly: true, ct);
    }

    private async Task<SyncResult> RunSyncAsync(bool syncInboxOnly, CancellationToken ct)
    {
        if (await quotaTracker.IsQuotaExhaustedAsync(ct))
        {
            logger.LogWarning("Skipping sync — YouTube API quota exhausted for today.");
            return new SyncResult(0, 0, ["YouTube API quota exhausted for today. Will resume after midnight Pacific."], 0);
        }

        var syncLog = new SyncLog
        {
            SyncType = syncInboxOnly ? "Inbox" : "Full",
            StartedAt = DateTime.UtcNow,
            Status = "InProgress",
            VideosProcessed = 0,
            VideosCategorized = 0
        };
        db.SyncLogs.Add(syncLog);
        await db.SaveChangesAsync(ct);

        var errors = new List<string>();
        var videosProcessed = 0;
        var deferredCount = 0;

        try
        {
            // 1. Fetch playlists from YouTube API
            var apiPlaylists = await youTubeApiClient.GetUserPlaylistsAsync(ct);

            // 2. Determine which playlists to sync
            List<PlaylistDto> playlistsToSync;
            if (syncInboxOnly)
            {
                var inboxPlaylist = await db.Playlists
                    .Where(p => p.IsInbox)
                    .FirstOrDefaultAsync(ct);

                if (inboxPlaylist is null)
                {
                    playlistsToSync = apiPlaylists.Where(p => p.IsInbox).ToList();
                }
                else
                {
                    var inboxDto = apiPlaylists.FirstOrDefault(p => p.YouTubeId == inboxPlaylist.YouTubeId);
                    playlistsToSync = inboxDto is not null ? [inboxDto] : [];
                }
            }
            else
            {
                playlistsToSync = apiPlaylists;
            }

            // 3. Upsert playlists
            if (!syncInboxOnly)
            {
                await UpsertPlaylistsAsync(apiPlaylists, ct);
            }

            // 4. For each playlist, collect all video IDs
            var allVideoIds = new HashSet<string>();
            var playlistItems = new Dictionary<string, List<PlaylistItemDto>>();

            foreach (var playlist in playlistsToSync)
            {
                var items = await youTubeApiClient.GetPlaylistItemsAsync(playlist.YouTubeId, ct);
                playlistItems[playlist.YouTubeId] = items;
                foreach (var item in items)
                    allVideoIds.Add(item.VideoId);
            }

            // 5. Batch-fetch video metadata
            List<VideoMetadataDto> videoMetadata;
            try
            {
                videoMetadata = await youTubeApiClient.GetVideoMetadataAsync(allVideoIds, ct);
            }
            catch (QuotaExhaustedException ex)
            {
                logger.LogWarning(ex, "Quota exhausted during video metadata fetch. Deferring {Count} videos.", allVideoIds.Count);
                errors.Add(ex.Message);
                deferredCount = allVideoIds.Count;

                // Complete partial sync
                syncLog.Status = "PartiallyCompleted";
                syncLog.CompletedAt = DateTime.UtcNow;
                syncLog.VideosProcessed = 0;
                syncLog.Errors = string.Join("; ", errors);
                await db.SaveChangesAsync(ct);

                return new SyncResult(0, 0, errors, deferredCount);
            }

            // 6. Upsert videos
            var metadataByYouTubeId = videoMetadata.ToDictionary(v => v.YouTubeId);

            foreach (var meta in videoMetadata)
            {
                await UpsertVideoAsync(meta, ct);
                videosProcessed++;
            }

            // 7. Mark videos not in API response as Archived (if they were Active)
            if (!syncInboxOnly)
            {
                await MarkMissingVideosArchivedAsync(metadataByYouTubeId.Keys, ct);
            }

            // 8. Upsert PlaylistVideo associations
            foreach (var (playlistYouTubeId, items) in playlistItems)
            {
                await UpsertPlaylistVideosAsync(playlistYouTubeId, items, metadataByYouTubeId, ct);
            }

            // 9. Complete sync log
            syncLog.Status = "Completed";
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.VideosProcessed = videosProcessed;
            syncLog.Errors = errors.Count > 0 ? string.Join("; ", errors) : null;
            await db.SaveChangesAsync(ct);

            return new SyncResult(videosProcessed, 0, errors, 0);
        }
        catch (Exception ex) when (ex is not QuotaExhaustedException)
        {
            logger.LogError(ex, "Sync failed unexpectedly.");
            errors.Add(ex.Message);
            syncLog.Status = "Failed";
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.Errors = string.Join("; ", errors);
            await db.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task UpsertPlaylistsAsync(List<PlaylistDto> apiPlaylists, CancellationToken ct)
    {
        var existingPlaylists = await db.Playlists.ToListAsync(ct);
        var existingByYouTubeId = existingPlaylists.ToDictionary(p => p.YouTubeId);
        var now = DateTime.UtcNow;

        foreach (var dto in apiPlaylists)
        {
            if (existingByYouTubeId.TryGetValue(dto.YouTubeId, out var existing))
            {
                existing.Name = dto.Name;
                existing.Description = dto.Description;
                existing.UpdatedAt = now;
                existing.SyncedAt = now;
            }
            else
            {
                db.Playlists.Add(new Playlist
                {
                    YouTubeId = dto.YouTubeId,
                    Name = dto.Name,
                    Description = dto.Description,
                    IsInbox = dto.IsInbox,
                    CreatedAt = now,
                    UpdatedAt = now,
                    SyncedAt = now
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task UpsertVideoAsync(VideoMetadataDto meta, CancellationToken ct)
    {
        var existing = await db.Videos.FirstOrDefaultAsync(v => v.YouTubeId == meta.YouTubeId, ct);
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            db.Videos.Add(new Video
            {
                YouTubeId = meta.YouTubeId,
                Title = meta.Title,
                Description = meta.Description,
                ChannelName = meta.ChannelName,
                ChannelId = meta.ChannelId,
                ThumbnailUrl = meta.ThumbnailUrl,
                Duration = meta.Duration,
                PublishedAt = meta.PublishedAt,
                Status = meta.Status,
                CreatedAt = now,
                UpdatedAt = now,
                SyncedAt = now
            });
        }
        else
        {
            existing.Title = meta.Title;
            existing.Description = meta.Description;
            existing.ChannelName = meta.ChannelName;
            existing.ChannelId = meta.ChannelId;
            existing.ThumbnailUrl = meta.ThumbnailUrl;
            existing.Duration = meta.Duration;
            existing.Status = meta.Status;
            existing.UpdatedAt = now;
            existing.SyncedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task MarkMissingVideosArchivedAsync(IEnumerable<string> activeYouTubeIds, CancellationToken ct)
    {
        var activeIdSet = activeYouTubeIds.ToHashSet();
        var now = DateTime.UtcNow;

        var toArchive = await db.Videos
            .Where(v => v.Status == VideoStatus.Active && !activeIdSet.Contains(v.YouTubeId))
            .ToListAsync(ct);

        foreach (var video in toArchive)
        {
            video.Status = VideoStatus.Archived;
            video.UpdatedAt = now;
        }

        if (toArchive.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private async Task UpsertPlaylistVideosAsync(
        string playlistYouTubeId,
        List<PlaylistItemDto> items,
        Dictionary<string, VideoMetadataDto> metadataByYouTubeId,
        CancellationToken ct)
    {
        var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.YouTubeId == playlistYouTubeId, ct);
        if (playlist is null)
            return;

        foreach (var item in items)
        {
            if (!metadataByYouTubeId.ContainsKey(item.VideoId))
                continue;

            var video = await db.Videos.FirstOrDefaultAsync(v => v.YouTubeId == item.VideoId, ct);
            if (video is null)
                continue;

            var existing = await db.PlaylistVideos
                .FirstOrDefaultAsync(pv => pv.PlaylistId == playlist.Id && pv.VideoId == video.Id, ct);

            if (existing is null)
            {
                db.PlaylistVideos.Add(new PlaylistVideo
                {
                    PlaylistId = playlist.Id,
                    VideoId = video.Id,
                    Position = item.Position,
                    AddedAt = item.AddedAt
                });
            }
            else
            {
                existing.Position = item.Position;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
