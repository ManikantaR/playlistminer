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
    IPipelineRunTracker tracker,
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

        var runId = await tracker.StartRunAsync("sync", ct);

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
            await tracker.UpdateRunAsync(runId, _ => {}, phase: "fetching_playlists", message: "Fetching user playlists from YouTube API...", ct: ct);
            var apiPlaylists = await youTubeApiClient.GetUserPlaylistsAsync(ct);
            await tracker.UpdateRunAsync(runId, r => {
                r.PlaylistsDiscovered = apiPlaylists.Count;
            }, message: $"Discovered {apiPlaylists.Count} playlists.", ct: ct);

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
            await tracker.UpdateRunAsync(runId, _ => {}, phase: "fetching_playlist_items", message: "Fetching playlist items from YouTube API...", ct: ct);
            var allVideoIds = new HashSet<string>();
            var playlistItems = new Dictionary<string, List<PlaylistItemDto>>();

            foreach (var playlist in playlistsToSync)
            {
                var items = await youTubeApiClient.GetPlaylistItemsAsync(playlist.YouTubeId, ct);
                playlistItems[playlist.YouTubeId] = items;
                foreach (var item in items)
                    allVideoIds.Add(item.VideoId);

                await tracker.UpdateRunAsync(runId, r => {
                    r.PlaylistsProcessed++;
                    r.PlaylistItemsFetched += items.Count;
                }, message: $"Fetched {items.Count} items from playlist {playlist.Name} (YouTube ID: {playlist.YouTubeId}).", ct: ct);
            }

            await tracker.UpdateRunAsync(runId, r => {
                r.UniqueVideoIdsIdentified = allVideoIds.Count;
            }, message: $"Identified {allVideoIds.Count} unique video IDs to fetch metadata for.", ct: ct);

            // 5. Batch-fetch video metadata
            await tracker.UpdateRunAsync(runId, _ => {}, phase: "hydrating_video_metadata", message: "Hydrating video metadata in batches of 50...", ct: ct);
            List<VideoMetadataDto> videoMetadata;
            try
            {
                var metadataList = new List<VideoMetadataDto>();
                var batches = allVideoIds.Chunk(50).ToList();
                await tracker.UpdateRunAsync(runId, r => {
                    r.VideoMetadataBatchesTotal = batches.Count;
                    r.VideoMetadataBatchesCompleted = 0;
                }, ct: ct);

                foreach (var batch in batches)
                {
                    var batchMetadata = await youTubeApiClient.GetVideoMetadataAsync(batch, ct);
                    metadataList.AddRange(batchMetadata);
                    await tracker.UpdateRunAsync(runId, r => {
                        r.VideoMetadataBatchesCompleted++;
                    }, message: $"Completed metadata batch {metadataList.Count / 50 + (metadataList.Count % 50 > 0 ? 1 : 0)} of {batches.Count}.", ct: ct);
                }
                videoMetadata = metadataList;
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

                await tracker.DeferRunAsync(runId, ex.Message, r => {
                    r.VideosDeferred = deferredCount;
                    r.ErrorsCount++;
                }, ct: ct);

                return new SyncResult(0, 0, errors, deferredCount);
            }

            // 6. Upsert videos
            await tracker.UpdateRunAsync(runId, _ => {}, phase: "upserting_videos", message: "Upserting video records...", ct: ct);
            var metadataByYouTubeId = videoMetadata.ToDictionary(v => v.YouTubeId);

            foreach (var meta in videoMetadata)
            {
                await UpsertVideoAsync(meta, ct);
                videosProcessed++;
                if (videosProcessed % 10 == 0 || videosProcessed == videoMetadata.Count)
                {
                    await tracker.UpdateRunAsync(runId, r => {
                        r.VideosUpserted = videosProcessed;
                        r.VideosProcessed = videosProcessed;
                    }, message: $"Upserted {videosProcessed} of {videoMetadata.Count} videos.", ct: ct);
                }
            }

            // 7. Mark videos not in API response as Archived (if they were Active)
            int archivedCount = 0;
            if (!syncInboxOnly)
            {
                var activeIdSet = metadataByYouTubeId.Keys.ToHashSet();
                var toArchive = await db.Videos
                    .Where(v => v.Status == VideoStatus.Active && !activeIdSet.Contains(v.YouTubeId))
                    .ToListAsync(ct);
                archivedCount = toArchive.Count;

                await MarkMissingVideosArchivedAsync(metadataByYouTubeId.Keys, ct);
            }

            // 8. Upsert PlaylistVideo associations
            await tracker.UpdateRunAsync(runId, _ => {}, phase: "linking_playlist_items", message: "Linking playlist videos...", ct: ct);
            int linksWritten = 0;
            foreach (var (playlistYouTubeId, items) in playlistItems)
            {
                await UpsertPlaylistVideosAsync(playlistYouTubeId, items, metadataByYouTubeId, ct);
                linksWritten += items.Count;
            }

            await tracker.UpdateRunAsync(runId, r => {
                r.PlaylistVideoLinksWritten = linksWritten;
                r.VideosArchived = archivedCount;
            }, message: $"Linked {linksWritten} playlist items.", ct: ct);

            // 9. Complete sync log
            await tracker.UpdateRunAsync(runId, _ => {}, phase: "finalizing", message: "Finalizing sync run...", ct: ct);
            syncLog.Status = "Completed";
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.VideosProcessed = videosProcessed;
            syncLog.Errors = errors.Count > 0 ? string.Join("; ", errors) : null;
            await db.SaveChangesAsync(ct);

            await tracker.CompleteRunAsync(runId, null, ct: ct);

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

            await tracker.FailRunAsync(runId, ex.Message, r => {
                r.ErrorsCount++;
            }, ct: ct);

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
