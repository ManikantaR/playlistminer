using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class PlaylistRestoreService(
    PlaylistMinerDbContext db,
    IYouTubeApiClient youTubeApiClient,
    IQuotaTracker quotaTracker,
    ILogger<PlaylistRestoreService> logger) : IPlaylistRestoreService
{
    private const int MaxSafeBatchSize = 25;
    private const int MaxNightlyBatchSize = 500;

    public async Task<PlaylistRestoreResultDto> RestoreSampleAsync(
        int sourcePlaylistId,
        int targetPlaylistId,
        int maxCount,
        CancellationToken ct = default)
    {
        if (maxCount is < 1 or > MaxSafeBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCount),
                maxCount,
                $"Restore sample size must be between 1 and {MaxSafeBatchSize}.");
        }

        return await RestoreBatchCoreAsync(sourcePlaylistId, targetPlaylistId, maxCount, ct);
    }

    public async Task<PlaylistRestoreResultDto> RestoreBatchAsync(
        int sourcePlaylistId,
        int targetPlaylistId,
        int maxCount,
        CancellationToken ct = default)
    {
        if (maxCount is < 1 or > MaxNightlyBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCount),
                maxCount,
                $"Restore batch size must be between 1 and {MaxNightlyBatchSize}.");
        }

        return await RestoreBatchCoreAsync(sourcePlaylistId, targetPlaylistId, maxCount, ct);
    }

    private async Task<PlaylistRestoreResultDto> RestoreBatchCoreAsync(
        int sourcePlaylistId,
        int targetPlaylistId,
        int maxCount,
        CancellationToken ct)
    {
        var sourcePlaylist = await db.Playlists.FindAsync([sourcePlaylistId], ct)
            ?? throw new KeyNotFoundException($"Source playlist {sourcePlaylistId} was not found.");

        var targetPlaylist = await db.Playlists.FindAsync([targetPlaylistId], ct)
            ?? throw new KeyNotFoundException($"Target playlist {targetPlaylistId} was not found.");

        if (await quotaTracker.IsQuotaExhaustedAsync(ct))
        {
            throw new QuotaExhaustedException();
        }

        var targetVideoIds = await db.PlaylistVideos
            .AsNoTracking()
            .Where(pv => pv.PlaylistId == targetPlaylistId)
            .Select(pv => pv.VideoId)
            .ToListAsync(ct);

        var targetVideoIdSet = targetVideoIds.ToHashSet();

        var sourceItems = await db.PlaylistVideos
            .Include(pv => pv.Video)
            .Where(pv => pv.PlaylistId == sourcePlaylistId)
            .OrderBy(pv => pv.Position)
            .ThenBy(pv => pv.VideoId)
            .ToListAsync(ct);

        var skippedCount = sourceItems.Count(pv => targetVideoIdSet.Contains(pv.VideoId));

        var candidates = sourceItems
            .Where(pv => !targetVideoIdSet.Contains(pv.VideoId))
            .Take(maxCount)
            .ToList();

        var nextPosition = await db.PlaylistVideos
            .Where(pv => pv.PlaylistId == targetPlaylistId)
            .Select(pv => (int?)pv.Position)
            .MaxAsync(ct) ?? -1;
        nextPosition++;

        var added = new List<PlaylistRestoreItemDto>();

        foreach (var candidate in candidates)
        {
            logger.LogInformation(
                "Restoring video {VideoId} from playlist {SourcePlaylistId} to playlist {TargetPlaylistId}.",
                candidate.VideoId,
                sourcePlaylist.Id,
                targetPlaylist.Id);

            var targetPosition = nextPosition++;
            var playlistItemId = await youTubeApiClient.AddVideoToPlaylistAsync(
                targetPlaylist.YouTubeId,
                candidate.Video.YouTubeId,
                targetPosition,
                ct);

            db.PlaylistVideos.Remove(candidate);
            db.PlaylistVideos.Add(new PlaylistVideo
            {
                PlaylistId = targetPlaylistId,
                VideoId = candidate.VideoId,
                Position = targetPosition,
                PlaylistItemId = playlistItemId,
                AddedAt = DateTime.UtcNow
            });

            targetVideoIdSet.Add(candidate.VideoId);

            added.Add(new PlaylistRestoreItemDto(
                candidate.VideoId,
                candidate.Video.YouTubeId,
                candidate.Video.Title,
                candidate.Position,
                targetPosition,
                playlistItemId));

            await db.SaveChangesAsync(ct);
        }

        return new PlaylistRestoreResultDto(
            sourcePlaylistId,
            targetPlaylistId,
            maxCount,
            added.Count,
            skippedCount,
            added);
    }
}
