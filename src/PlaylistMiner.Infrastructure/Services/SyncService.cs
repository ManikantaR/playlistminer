using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

/// <summary>
/// Syncs playlists and videos from YouTube into the local DB.
///
/// Design: sync is <b>incremental and checkpointed</b>. Each playlist is processed as an
/// independent unit that fetches its items + video metadata, then commits videos and links in
/// bulk before moving on. This means:
///   • partial progress is always persisted and reviewable (no all-or-nothing wait),
///   • a quota wall or interruption mid-run keeps every playlist processed so far,
///   • the run reports progress after every playlist, so it never looks frozen,
///   • DB work is bulk (one query-set + one save per playlist) instead of per-item N+1.
/// A process-wide <see cref="SyncConcurrencyGate"/> ensures two syncs never write concurrently.
/// </summary>
public sealed class SyncService(
    IYouTubeApiClient youTubeApiClient,
    IQuotaTracker quotaTracker,
    PlaylistMinerDbContext db,
    IPipelineRunTracker tracker,
    ILogger<SyncService> logger,
    SyncConcurrencyGate? gate = null) : ISyncService
{
    public Task<SyncResult> FullSyncAsync(CancellationToken ct = default) => RunSyncAsync(syncInboxOnly: false, ct);

    public Task<SyncResult> SyncInboxAsync(CancellationToken ct = default) => RunSyncAsync(syncInboxOnly: true, ct);

    private async Task<SyncResult> RunSyncAsync(bool syncInboxOnly, CancellationToken ct)
    {
        // Single-flight: skip (don't queue) if another sync already holds the gate.
        if (gate is not null && !await gate.TryAcquireAsync(ct))
        {
            logger.LogInformation("Skipping {Kind} sync — another sync is already running.", syncInboxOnly ? "inbox" : "full");
            return new SyncResult(0, 0, ["Another sync is already running; skipped."], 0);
        }

        try
        {
            return await RunSyncCoreAsync(syncInboxOnly, ct);
        }
        finally
        {
            gate?.Release();
        }
    }

    private async Task<SyncResult> RunSyncCoreAsync(bool syncInboxOnly, CancellationToken ct)
    {
        var runId = await tracker.StartRunAsync("sync", ct);
        var syncType = syncInboxOnly ? "Inbox" : "Full";

        if (await quotaTracker.IsQuotaExhaustedAsync(ct))
        {
            const string quotaMsg = "YouTube API quota exhausted for today. Will resume after midnight Pacific.";
            logger.LogWarning("Skipping sync — {Message}", quotaMsg);

            db.SyncLogs.Add(new SyncLog
            {
                SyncType = syncType,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Status = "Deferred",
                VideosProcessed = 0,
                VideosCategorized = 0,
                Errors = quotaMsg
            });
            await db.SaveChangesAsync(ct);

            await tracker.DeferRunAsync(runId, quotaMsg, ct: ct);
            return new SyncResult(0, 0, [quotaMsg], 0);
        }

        var syncLog = new SyncLog
        {
            SyncType = syncType,
            StartedAt = DateTime.UtcNow,
            Status = "InProgress",
            VideosProcessed = 0,
            VideosCategorized = 0
        };
        db.SyncLogs.Add(syncLog);
        await db.SaveChangesAsync(ct);
        var syncLogId = syncLog.Id;

        var errors = new List<string>();
        var videosProcessed = 0;
        var totalLinks = 0;
        var metadataCache = new Dictionary<string, VideoMetadataDto>();
        var allActiveVideoIds = new HashSet<string>();
        var uniqueVideoIds = new HashSet<string>();

        try
        {
            // 1. Discover playlists.
            await tracker.UpdateRunAsync(runId, _ => { }, phase: "fetching_playlists",
                message: "Fetching user playlists from YouTube API...", ct: ct);
            var apiPlaylists = await youTubeApiClient.GetUserPlaylistsAsync(ct);
            await tracker.UpdateRunAsync(runId, r => r.PlaylistsDiscovered = apiPlaylists.Count,
                message: $"Discovered {apiPlaylists.Count} playlists.", ct: ct);

            // 2. Resolve which playlists to process.
            List<PlaylistDto> playlistsToSync;
            if (syncInboxOnly)
            {
                var inboxPlaylist = await db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.IsInbox, ct);
                playlistsToSync = inboxPlaylist is null
                    ? apiPlaylists.Where(p => p.IsInbox).ToList()
                    : apiPlaylists.Where(p => p.YouTubeId == inboxPlaylist.YouTubeId).ToList();
            }
            else
            {
                playlistsToSync = apiPlaylists;
                // Upsert playlist shells up-front so they appear in the UI immediately,
                // before the (slower) per-playlist video processing fills them in.
                await UpsertPlaylistsAsync(apiPlaylists, ct);
                db.ChangeTracker.Clear();
            }

            // 3. Process each playlist as an independent committed checkpoint.
            await tracker.UpdateRunAsync(runId, _ => { }, phase: "processing_playlists",
                message: "Processing playlists incrementally...", ct: ct);

            var total = playlistsToSync.Count;
            var index = 0;
            foreach (var playlist in playlistsToSync)
            {
                index++;

                var items = await youTubeApiClient.GetPlaylistItemsAsync(playlist.YouTubeId, ct);
                foreach (var item in items)
                {
                    uniqueVideoIds.Add(item.VideoId);
                    allActiveVideoIds.Add(item.VideoId);
                }

                // Fetch metadata only for video IDs not already hydrated this run (shared across playlists).
                var needed = items.Select(i => i.VideoId).Distinct().Where(id => !metadataCache.ContainsKey(id)).ToList();
                try
                {
                    var batches = needed.Chunk(50).ToList();
                    for (var b = 0; b < batches.Count; b++)
                    {
                        var batchMeta = await youTubeApiClient.GetVideoMetadataAsync(batches[b], ct);
                        foreach (var m in batchMeta)
                            metadataCache[m.YouTubeId] = m;
                        await tracker.UpdateRunAsync(runId, _ => { },
                            message: $"Completed metadata batch {b + 1} of {batches.Count}.", ct: ct);
                    }
                }
                catch (QuotaExhaustedException ex)
                {
                    // Checkpoint: every playlist before this one is already committed. Defer the rest.
                    var deferred = playlistsToSync.Skip(index - 1)
                        .Sum(p => Math.Max(p.ItemCount, p.YouTubeId == playlist.YouTubeId ? needed.Count : 1));
                    logger.LogWarning(ex, "Quota exhausted at playlist {Index}/{Total}. Deferring remainder.", index, total);
                    errors.Add(ex.Message);

                    await FinalizeSyncLogAsync(syncLogId, "PartiallyCompleted", videosProcessed, errors, ct);
                    await tracker.DeferRunAsync(runId, ex.Message, r =>
                    {
                        r.VideosDeferred = deferred;
                        r.ErrorsCount++;
                    }, ct: ct);
                    return new SyncResult(videosProcessed, 0, errors, deferred);
                }

                // Bulk upsert this playlist's videos, then its links. Each is one query-set + one save.
                var playlistMeta = items
                    .Where(i => metadataCache.ContainsKey(i.VideoId))
                    .Select(i => metadataCache[i.VideoId])
                    .GroupBy(m => m.YouTubeId)
                    .Select(g => g.First())
                    .ToList();

                videosProcessed += await UpsertVideosBulkAsync(playlistMeta, ct);
                totalLinks += await UpsertPlaylistVideosBulkAsync(playlist.YouTubeId, items, metadataCache, ct);

                await tracker.UpdateRunAsync(runId, r =>
                {
                    r.PlaylistsProcessed = index;
                    r.PlaylistItemsFetched += items.Count;
                    r.UniqueVideoIdsIdentified = uniqueVideoIds.Count;
                    r.VideosUpserted = videosProcessed;
                    r.VideosProcessed = videosProcessed;
                    r.PlaylistVideoLinksWritten = totalLinks;
                }, message: $"Processed playlist \"{playlist.Name}\" ({index}/{total}).", ct: ct);

                // Keep the change tracker small across hundreds of playlists — each playlist is
                // already committed, and per-playlist helpers re-query what they need.
                db.ChangeTracker.Clear();
            }

            // 4. Archive videos that disappeared from YouTube (full sync only, and only if we
            //    actually saw playlists — never archive everything off an empty/failed discovery).
            var archivedCount = 0;
            if (!syncInboxOnly && playlistsToSync.Count > 0)
            {
                archivedCount = await MarkMissingVideosArchivedAsync(allActiveVideoIds, ct);
                db.ChangeTracker.Clear();
            }

            // 5. Finalize.
            await tracker.UpdateRunAsync(runId, _ => { }, phase: "finalizing", message: "Finalizing sync run...", ct: ct);
            await FinalizeSyncLogAsync(syncLogId, "Completed", videosProcessed, errors, ct);
            await tracker.CompleteRunAsync(runId, r => r.VideosArchived = archivedCount, ct: ct);

            return new SyncResult(videosProcessed, 0, errors, 0);
        }
        catch (Exception ex) when (ex is not QuotaExhaustedException)
        {
            logger.LogError(ex, "Sync failed unexpectedly.");
            errors.Add(ex.Message);
            await FinalizeSyncLogAsync(syncLogId, "Failed", videosProcessed, errors, ct);
            await tracker.FailRunAsync(runId, ex.Message, r => r.ErrorsCount++, ct: ct);
            throw;
        }
    }

    private async Task FinalizeSyncLogAsync(int syncLogId, string status, int videosProcessed, List<string> errors, CancellationToken ct)
    {
        // Re-load: the change tracker is cleared between playlists, so the original entity is detached.
        var log = await db.SyncLogs.FirstOrDefaultAsync(s => s.Id == syncLogId, ct);
        if (log is null) return;

        log.Status = status;
        log.CompletedAt = DateTime.UtcNow;
        log.VideosProcessed = videosProcessed;
        log.Errors = errors.Count > 0 ? string.Join("; ", errors) : null;
        await db.SaveChangesAsync(ct);
    }

    private async Task UpsertPlaylistsAsync(List<PlaylistDto> apiPlaylists, CancellationToken ct)
    {
        var existingByYouTubeId = (await db.Playlists.ToListAsync(ct)).ToDictionary(p => p.YouTubeId);
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

    /// <summary>Bulk-upserts a batch of videos with one read + one save. Returns the count processed.</summary>
    private async Task<int> UpsertVideosBulkAsync(List<VideoMetadataDto> metas, CancellationToken ct)
    {
        if (metas.Count == 0) return 0;

        var ids = metas.Select(m => m.YouTubeId).ToList();
        var existingByYouTubeId = (await db.Videos.Where(v => ids.Contains(v.YouTubeId)).ToListAsync(ct))
            .ToDictionary(v => v.YouTubeId);
        var now = DateTime.UtcNow;

        foreach (var meta in metas)
        {
            if (existingByYouTubeId.TryGetValue(meta.YouTubeId, out var existing))
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
            else
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
        }

        await db.SaveChangesAsync(ct);
        return metas.Count;
    }

    /// <summary>Bulk-upserts playlist→video links for one playlist with one read-set + one save.</summary>
    private async Task<int> UpsertPlaylistVideosBulkAsync(
        string playlistYouTubeId,
        List<PlaylistItemDto> items,
        Dictionary<string, VideoMetadataDto> metadataCache,
        CancellationToken ct)
    {
        var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.YouTubeId == playlistYouTubeId, ct);
        if (playlist is null) return 0;

        var ytIds = items.Select(i => i.VideoId).Distinct().ToList();
        var videoIdByYouTubeId = await db.Videos
            .Where(v => ytIds.Contains(v.YouTubeId))
            .ToDictionaryAsync(v => v.YouTubeId, v => v.Id, ct);

        var linksByVideoId = (await db.PlaylistVideos.Where(pv => pv.PlaylistId == playlist.Id).ToListAsync(ct))
            .ToDictionary(pv => pv.VideoId);

        var written = 0;
        foreach (var item in items)
        {
            if (!metadataCache.ContainsKey(item.VideoId)) continue;
            if (!videoIdByYouTubeId.TryGetValue(item.VideoId, out var videoId)) continue;

            if (linksByVideoId.TryGetValue(videoId, out var existing))
            {
                existing.Position = item.Position;
            }
            else
            {
                var link = new PlaylistVideo
                {
                    PlaylistId = playlist.Id,
                    VideoId = videoId,
                    Position = item.Position,
                    AddedAt = item.AddedAt
                };
                db.PlaylistVideos.Add(link);
                linksByVideoId[videoId] = link;
            }
            written++;
        }

        await db.SaveChangesAsync(ct);
        return written;
    }

    private async Task<int> MarkMissingVideosArchivedAsync(IReadOnlySet<string> activeYouTubeIds, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var toArchive = await db.Videos
            .Where(v => v.Status == VideoStatus.Active && !activeYouTubeIds.Contains(v.YouTubeId))
            .ToListAsync(ct);

        foreach (var video in toArchive)
        {
            video.Status = VideoStatus.Archived;
            video.UpdatedAt = now;
        }

        if (toArchive.Count > 0)
            await db.SaveChangesAsync(ct);

        return toArchive.Count;
    }
}
