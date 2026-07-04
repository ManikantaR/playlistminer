using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public sealed class OperationsObservabilityService(
    PlaylistMinerDbContext db,
    IConfiguration configuration,
    TimeProvider? timeProvider = null) : IOperationsObservabilityService
{
    private const int DefaultMoveBudget = 80;
    private static readonly TimeZoneInfo PacificTz = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<OperationsActivityFeedDto> GetActivityAsync(int limit, int offset, CancellationToken ct = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 100);
        var normalizedOffset = Math.Max(0, offset);

        var query =
            from pipelineEvent in db.PipelineEvents.AsNoTracking()
            join pipelineRun in db.PipelineRuns.AsNoTracking()
                on pipelineEvent.RunId equals pipelineRun.RunId into runGroup
            from pipelineRun in runGroup.DefaultIfEmpty()
            where pipelineEvent.RunId != "worker-heartbeat"
            orderby pipelineEvent.OccurredAt descending, pipelineEvent.Id descending
            select new OperationsActivityItemDto(
                pipelineEvent.Id,
                pipelineEvent.RunId,
                pipelineRun != null ? pipelineRun.PipelineType : "unknown",
                GetPipelineLabel(pipelineRun != null ? pipelineRun.PipelineType : null),
                pipelineRun != null ? pipelineRun.Status : "unknown",
                pipelineEvent.Level,
                pipelineEvent.Phase,
                pipelineEvent.Message,
                pipelineEvent.OccurredAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToListAsync(ct);

        return new OperationsActivityFeedDto(
            items,
            normalizedLimit,
            normalizedOffset,
            totalCount,
            normalizedOffset + items.Count < totalCount);
    }

    public async Task<OperationsQuotaDto> GetMoveBudgetAsync(CancellationToken ct = default)
    {
        var moveBudget = configuration.GetValue<int?>("Organize:DailyMoveBudget") ?? DefaultMoveBudget;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var nowPacific = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, PacificTz);
        var dayStartPacific = new DateTime(nowPacific.Year, nowPacific.Month, nowPacific.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var dayEndPacific = dayStartPacific.AddDays(1);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartPacific, PacificTz);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayEndPacific, PacificTz);

        var movesUsedToday = await db.PipelineRuns
            .AsNoTracking()
            .Where(run => IsMoveBudgetRun(run.PipelineType)
                          && run.StartedAt >= dayStartUtc
                          && run.StartedAt < dayEndUtc)
            .SumAsync(run => run.VideosProcessed, ct);

        var unitsRemaining = Math.Max(0, moveBudget - movesUsedToday);
        var isBlocked = unitsRemaining == 0;
        var message = isBlocked
            ? "Daily move budget exhausted."
            : "Move budget available.";

        return new OperationsQuotaDto(
            movesUsedToday,
            moveBudget,
            dayEndUtc,
            unitsRemaining,
            isBlocked,
            message);
    }

    private static bool IsMoveBudgetRun(string pipelineType)
        => pipelineType is "remote-duplicate-cleanup" or "organize-execute" or "organize-execution";

    private static string GetPipelineLabel(string? pipelineType)
        => pipelineType switch
        {
            "sync" => "Sync Job",
            "remote-duplicate-cleanup" => "Remote Cleanup",
            "organize-execute" => "Organize Execute",
            "organize-execution" => "Organize Execute",
            _ => "Organize Activity"
        };
}
