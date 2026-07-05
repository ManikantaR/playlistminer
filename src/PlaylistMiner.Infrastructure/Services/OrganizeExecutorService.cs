using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public sealed class OrganizeExecutorService(
    PlaylistMinerDbContext db,
    IOrganizePlannerService planner,
    IPlaylistOrganizer playlistOrganizer,
    IOperationsObservabilityService operationsObservabilityService,
    IPipelineRunTracker tracker,
    IConfiguration configuration,
    ILogger<OrganizeExecutorService> logger) : IOrganizeExecutorService
{
    private const int DefaultBatchSize = 20;

    public async Task<OrganizeExecutionResultDto> ExecuteAsync(CancellationToken ct = default)
    {
        var plan = await planner.BuildPlanAsync(ct);
        var moveItems = plan.Items
            .Where(item => string.Equals(item.Action, "move", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (moveItems.Count == 0)
        {
            return new OrganizeExecutionResultDto(plan.VideosExamined, 0, 0, 0, 0, [], null);
        }

        var quota = await operationsObservabilityService.GetMoveBudgetAsync(ct);
        if (quota.IsBlocked || quota.UnitsRemaining <= 0)
        {
            return new OrganizeExecutionResultDto(
                plan.VideosExamined,
                0,
                0,
                0,
                moveItems.Count,
                [quota.Message],
                null);
        }

        var batchSize = Math.Max(1, configuration.GetValue<int?>("Organize:ExecutionBatchSize") ?? DefaultBatchSize);
        var executableCount = Math.Min(moveItems.Count, Math.Min(batchSize, quota.UnitsRemaining));
        var executableMoves = moveItems.Take(executableCount).ToList();
        var deferredCount = moveItems.Count - executableMoves.Count;
        var runId = await tracker.StartRunAsync("organize-execute", ct);
        var errors = new List<string>();
        var executed = 0;
        var skipped = 0;

        try
        {
            await tracker.UpdateRunAsync(
                runId,
                run =>
                {
                    run.VideosPendingTagging = executableMoves.Count;
                    run.VideosDeferred = deferredCount;
                },
                phase: "executing",
                message: "Executing organize batch...",
                ct: ct);

            var inbox = await db.Playlists
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IsInbox, ct);

            if (inbox is null)
            {
                const string missingInbox = "No inbox playlist is configured. Cannot execute organize batch.";
                errors.Add(missingInbox);
                await tracker.FailRunAsync(runId, missingInbox, ct: ct);
                return new OrganizeExecutionResultDto(plan.VideosExamined, executableMoves.Count, 0, 0, moveItems.Count, errors, runId);
            }

            for (var index = 0; index < executableMoves.Count; index++)
            {
                var item = executableMoves[index];

                try
                {
                    var targetPlaylistId = item.TargetPlaylistId
                        ?? (await playlistOrganizer.EnsureManagedPlaylistAsync(item.Topic ?? item.TargetPlaylistName ?? throw new InvalidOperationException("Move item is missing a topic."), ct)).Id;

                    await playlistOrganizer.MoveVideoAsync(item.VideoId ?? throw new InvalidOperationException("Move item is missing a video id."), inbox.Id, targetPlaylistId, ct);
                    executed++;

                    await tracker.UpdateRunAsync(
                        runId,
                        run =>
                        {
                            run.VideosProcessed = executed;
                            run.VideosSkipped = skipped;
                            run.VideosDeferred = deferredCount;
                        },
                        message: $"Moved \"{item.Title}\" to \"{item.TargetPlaylistName ?? item.Topic}\".",
                        ct: ct);
                }
                catch (QuotaExhaustedException)
                {
                    var remaining = executableMoves.Count - index;
                    deferredCount += remaining;
                    const string quotaMessage = "YouTube API quota exhausted during organize execution. Deferred remaining moves.";
                    errors.Add(quotaMessage);

                    await tracker.DeferRunAsync(runId, quotaMessage, run =>
                    {
                        run.VideosProcessed = executed;
                        run.VideosSkipped = skipped;
                        run.VideosDeferred = deferredCount;
                        run.ErrorsCount = errors.Count;
                    }, ct);

                    return new OrganizeExecutionResultDto(plan.VideosExamined, executableMoves.Count, executed, skipped, deferredCount, errors, runId);
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add(ex.Message);
                    logger.LogWarning(ex, "Organize execution skipped video {VideoId}.", item.VideoId);

                    await tracker.LogEventAsync(
                        runId,
                        "warning",
                        "executing",
                        $"Skipping move for video {item.YouTubeId ?? item.VideoId?.ToString()}: {ex.Message}",
                        ct: ct);
                }
            }

            await tracker.CompleteRunAsync(runId, run =>
            {
                run.VideosProcessed = executed;
                run.VideosSkipped = skipped;
                run.VideosDeferred = deferredCount;
                run.ErrorsCount = errors.Count;
            }, ct);

            return new OrganizeExecutionResultDto(plan.VideosExamined, executableMoves.Count, executed, skipped, deferredCount, errors, runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Organize execution failed.");
            await tracker.FailRunAsync(runId, ex.Message, run =>
            {
                run.VideosProcessed = executed;
                run.VideosSkipped = skipped;
                run.VideosDeferred = deferredCount;
                run.ErrorsCount = errors.Count + 1;
            }, ct);
            throw;
        }
    }
}
