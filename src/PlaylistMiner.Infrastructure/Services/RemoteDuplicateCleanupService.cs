using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public sealed class RemoteDuplicateCleanupService(
    PlaylistMinerDbContext db,
    IYouTubeApiClient youTubeApiClient,
    IQuotaTracker quotaTracker,
    IPipelineRunTracker tracker,
    ILogger<RemoteDuplicateCleanupService> logger) : IRemoteDuplicateCleanupService
{
    public async Task<List<RemoteDuplicateCleanupItemDto>> BuildPlanAsync(CancellationToken ct = default)
    {
        var placements = await LoadPlacementsAsync(ct);
        var unresolvedLosers = GetUnresolvedLoserPlacements(placements);

        if (unresolvedLosers.Count > 0)
        {
            await HydrateMissingPlaylistItemIdsAsync(unresolvedLosers, ct);
            placements = await LoadPlacementsAsync(ct);
        }

        var plan = placements
            .GroupBy(p => new { p.VideoId, p.YouTubeId, p.Title })
            .Where(g => g.Select(p => p.PlaylistId).Distinct().Count() > 1)
            .Select(g =>
            {
                var winner = g
                    .OrderBy(p => p.IsInbox)
                    .ThenBy(p => p.PlaylistId)
                    .First();

                var losers = g
                    .Where(p => p.PlaylistId != winner.PlaylistId)
                    .OrderBy(p => p.PlaylistName)
                    .Select(p => new RemoteDuplicateRemovalTargetDto(
                        p.PlaylistId,
                        p.PlaylistName,
                        p.PlaylistItemId))
                    .ToList();

                var hasUnresolved = losers.Any(p => string.IsNullOrWhiteSpace(p.PlaylistItemId));

                return new RemoteDuplicateCleanupItemDto(
                    g.Key.VideoId,
                    g.Key.YouTubeId,
                    g.Key.Title,
                    winner.PlaylistId,
                    winner.PlaylistName,
                    hasUnresolved,
                    losers);
            })
            .OrderBy(p => p.Title)
            .ToList();

        logger.LogInformation("Built remote duplicate cleanup plan with {Count} duplicate videos.", plan.Count);
        return plan;
    }

    private async Task<List<PlaylistPlacement>> LoadPlacementsAsync(CancellationToken ct)
    {
        return await db.PlaylistVideos
            .AsNoTracking()
            .Select(pv => new PlaylistPlacement(
                pv.VideoId,
                pv.Video.YouTubeId,
                pv.Video.Title,
                pv.PlaylistId,
                pv.Playlist.YouTubeId,
                pv.Playlist.Name,
                pv.Playlist.IsInbox,
                pv.PlaylistItemId))
            .ToListAsync(ct);
    }

    private static List<PlaylistPlacement> GetUnresolvedLoserPlacements(List<PlaylistPlacement> placements)
    {
        return placements
            .GroupBy(p => new { p.VideoId, p.YouTubeId, p.Title })
            .Where(g => g.Select(p => p.PlaylistId).Distinct().Count() > 1)
            .SelectMany(g =>
            {
                var winnerPlaylistId = g
                    .OrderBy(p => p.IsInbox)
                    .ThenBy(p => p.PlaylistId)
                    .Select(p => p.PlaylistId)
                    .First();

                return g.Where(p => p.PlaylistId != winnerPlaylistId && string.IsNullOrWhiteSpace(p.PlaylistItemId));
            })
            .ToList();
    }

    private async Task HydrateMissingPlaylistItemIdsAsync(
        List<PlaylistPlacement> unresolvedLosers,
        CancellationToken ct)
    {
        foreach (var playlistGroup in unresolvedLosers.GroupBy(p => new { p.PlaylistId, p.PlaylistYouTubeId }))
        {
            if (await quotaTracker.IsQuotaExhaustedAsync(ct))
            {
                logger.LogWarning(
                    "Skipping playlist item id hydration for playlist {PlaylistId} because YouTube quota is exhausted.",
                    playlistGroup.Key.PlaylistId);
                break;
            }

            try
            {
                var playlistItems = await youTubeApiClient.GetPlaylistItemsAsync(playlistGroup.Key.PlaylistYouTubeId, ct);
                var remoteItemIdByVideoYouTubeId = playlistItems
                    .GroupBy(item => item.VideoId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(item => item.Position).First().PlaylistItemId);

                var videoIds = playlistGroup.Select(p => p.VideoId).Distinct().ToList();
                var localLinks = await db.PlaylistVideos
                    .Where(pv => pv.PlaylistId == playlistGroup.Key.PlaylistId && videoIds.Contains(pv.VideoId))
                    .Include(pv => pv.Video)
                    .ToListAsync(ct);

                var changed = false;
                foreach (var link in localLinks.Where(link => string.IsNullOrWhiteSpace(link.PlaylistItemId)))
                {
                    if (!remoteItemIdByVideoYouTubeId.TryGetValue(link.Video.YouTubeId, out var playlistItemId))
                    {
                        continue;
                    }

                    link.PlaylistItemId = playlistItemId;
                    changed = true;
                }

                if (changed)
                {
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (QuotaExhaustedException)
            {
                await quotaTracker.RecordQuotaExhaustedAsync(ct);
                logger.LogWarning(
                    "YouTube quota exhausted while hydrating missing playlist item ids for playlist {PlaylistId}.",
                    playlistGroup.Key.PlaylistId);
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed hydrating playlist item ids for playlist {PlaylistId}. Unresolved removals will remain in the plan.",
                    playlistGroup.Key.PlaylistId);
            }
        }
    }

    public async Task<RemoteDuplicateCleanupResultDto> ExecuteAsync(
        IEnumerable<RemoteDuplicateCleanupItemDto> plan,
        CancellationToken ct = default)
    {
        var items = plan.ToList();
        var runId = await tracker.StartRunAsync("remote-duplicate-cleanup", ct);
        var planned = items.Sum(i => i.LoserPlaylists.Count);
        var executed = 0;
        var skipped = 0;
        var deferred = 0;
        var errors = new List<string>();

        try
        {
            await tracker.UpdateRunAsync(runId, _ => { }, phase: "executing", message: "Executing remote duplicate cleanup plan...", ct: ct);

            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                var item = items[itemIndex];

                for (var loserIndex = 0; loserIndex < item.LoserPlaylists.Count; loserIndex++)
                {
                    var loser = item.LoserPlaylists[loserIndex];

                    if (await quotaTracker.IsQuotaExhaustedAsync(ct))
                    {
                        deferred += item.LoserPlaylists.Count - loserIndex;
                        deferred += items.Skip(itemIndex + 1).Sum(i => i.LoserPlaylists.Count);
                        const string quotaMessage = "YouTube API quota exhausted. Deferred remaining remote cleanup removals.";
                        errors.Add(quotaMessage);
                        await tracker.DeferRunAsync(runId, quotaMessage, r =>
                        {
                            r.VideosProcessed = executed;
                            r.VideosDeferred = deferred;
                            r.ErrorsCount = errors.Count;
                        }, ct);
                        return new RemoteDuplicateCleanupResultDto(items.Count, planned, executed, skipped, deferred, errors, runId);
                    }

                    var validation = await ValidateRemovalTargetAsync(item, loser, ct);
                    if (!validation.CanExecute)
                    {
                        skipped++;
                        errors.Add(validation.Message);
                        await tracker.LogEventAsync(runId, "warning", "executing", validation.Message, ct: ct);
                        continue;
                    }

                    try
                    {
                        await youTubeApiClient.RemoveVideoFromPlaylistAsync(
                            validation.PlaylistYouTubeId!,
                            validation.PlaylistItemId!,
                            ct);

                        var localLink = await db.PlaylistVideos
                            .FirstOrDefaultAsync(pv => pv.PlaylistId == loser.PlaylistId && pv.VideoId == item.VideoId, ct);
                        if (localLink is not null)
                        {
                            db.PlaylistVideos.Remove(localLink);
                            await db.SaveChangesAsync(ct);
                        }

                        executed++;
                        await tracker.UpdateRunAsync(runId, r =>
                        {
                            r.VideosProcessed = executed;
                        }, message: $"Removed duplicate video \"{item.Title}\" from playlist \"{loser.PlaylistName}\".", ct: ct);
                    }
                    catch (QuotaExhaustedException)
                    {
                        await quotaTracker.RecordQuotaExhaustedAsync(ct);
                        deferred += item.LoserPlaylists.Count - loserIndex;
                        deferred += items.Skip(itemIndex + 1).Sum(i => i.LoserPlaylists.Count);
                        const string quotaMessage = "YouTube API quota exhausted during remote cleanup. Deferred remaining removals.";
                        errors.Add(quotaMessage);
                        await tracker.DeferRunAsync(runId, quotaMessage, r =>
                        {
                            r.VideosProcessed = executed;
                            r.VideosDeferred = deferred;
                            r.ErrorsCount = errors.Count;
                        }, ct);
                        return new RemoteDuplicateCleanupResultDto(items.Count, planned, executed, skipped, deferred, errors, runId);
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        var failureMessage = $"Failed removing video {item.YouTubeId} from playlist {loser.PlaylistName}: {ex.Message}";
                        errors.Add(failureMessage);
                        logger.LogWarning(ex, "Remote duplicate cleanup removal failed for video {VideoId} playlist {PlaylistId}.", item.VideoId, loser.PlaylistId);
                        await tracker.LogEventAsync(runId, "error", "executing", failureMessage, ct: ct);
                    }
                }
            }

            await tracker.CompleteRunAsync(runId, r =>
            {
                r.VideosProcessed = executed;
                r.VideosSkipped = skipped;
                r.ErrorsCount = errors.Count;
            }, ct);

            return new RemoteDuplicateCleanupResultDto(items.Count, planned, executed, skipped, deferred, errors, runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Remote duplicate cleanup execution failed.");
            errors.Add(ex.Message);
            await tracker.FailRunAsync(runId, ex.Message, r =>
            {
                r.VideosProcessed = executed;
                r.VideosSkipped = skipped;
                r.ErrorsCount = errors.Count;
            }, ct);
            throw;
        }
    }

    private async Task<(bool CanExecute, string Message, string? PlaylistYouTubeId, string? PlaylistItemId)> ValidateRemovalTargetAsync(
        RemoteDuplicateCleanupItemDto item,
        RemoteDuplicateRemovalTargetDto loser,
        CancellationToken ct)
    {
        var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.Id == loser.PlaylistId, ct);
        if (playlist is null)
        {
            return (false,
                $"Skipping removal for video {item.YouTubeId}: playlist {loser.PlaylistId} no longer exists locally.",
                null,
                null);
        }

        var currentLinks = await db.PlaylistVideos
            .Where(pv => pv.VideoId == item.VideoId)
            .ToListAsync(ct);

        var winnerLink = currentLinks.FirstOrDefault(pv => pv.PlaylistId == item.WinnerPlaylistId);
        if (winnerLink is null)
        {
            return (false,
                $"Skipping removal for video {item.YouTubeId}: winner playlist {item.WinnerPlaylistName} no longer owns the video locally.",
                null,
                null);
        }

        var loserLink = currentLinks.FirstOrDefault(pv => pv.PlaylistId == loser.PlaylistId);
        if (loserLink is null)
        {
            return (false,
                $"Skipping removal for video {item.YouTubeId} from playlist {loser.PlaylistName}: link is already gone locally.",
                null,
                null);
        }

        if (string.IsNullOrWhiteSpace(loserLink.PlaylistItemId))
        {
            return (false,
                $"Skipping removal for video {item.YouTubeId} from playlist {loser.PlaylistName}: missing playlist item id.",
                null,
                null);
        }

        return (true, string.Empty, playlist.YouTubeId, loserLink.PlaylistItemId);
    }

    private sealed record PlaylistPlacement(
        int VideoId,
        string YouTubeId,
        string Title,
        int PlaylistId,
        string PlaylistYouTubeId,
        string PlaylistName,
        bool IsInbox,
        string? PlaylistItemId);
}
