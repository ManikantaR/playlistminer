using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class PipelineRunTracker(PlaylistMinerDbContext db) : IPipelineRunTracker
{
    public async Task<string> StartRunAsync(string pipelineType, CancellationToken ct = default)
    {
        var runId = Guid.NewGuid().ToString();
        var run = new PipelineRun
        {
            RunId = runId,
            PipelineType = pipelineType,
            Status = "in_progress",
            Phase = "starting",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CurrentMessage = "Initializing run..."
        };

        db.PipelineRuns.Add(run);
        await db.SaveChangesAsync(ct);

        await LogEventAsync(runId, "info", "starting", "Pipeline run started.", null, ct);

        return runId;
    }

    public async Task UpdateRunAsync(
        string runId,
        Action<PipelineRun> updateAction,
        string? phase = null,
        string? message = null,
        CancellationToken ct = default)
    {
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == runId, ct);
        if (run is null) return;

        updateAction(run);

        bool changed = false;
        if (phase is not null && run.Phase != phase)
        {
            run.Phase = phase;
            changed = true;
        }
        if (message is not null && run.CurrentMessage != message)
        {
            run.CurrentMessage = message;
            changed = true;
        }

        run.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (changed)
        {
            await LogEventAsync(runId, "info", run.Phase, run.CurrentMessage ?? $"Phase: {run.Phase}", null, ct);
        }
    }

    public async Task CompleteRunAsync(
        string runId,
        Action<PipelineRun>? updateAction = null,
        CancellationToken ct = default)
    {
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == runId, ct);
        if (run is null) return;

        updateAction?.Invoke(run);
        run.Status = "completed";
        run.Phase = "completed";
        run.CompletedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;
        run.CurrentMessage = "Run completed successfully.";

        await db.SaveChangesAsync(ct);

        await LogEventAsync(runId, "info", "completed", "Pipeline run completed successfully.", null, ct);
    }

    public async Task FailRunAsync(
        string runId,
        string error,
        Action<PipelineRun>? updateAction = null,
        CancellationToken ct = default)
    {
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == runId, ct);
        if (run is null) return;

        updateAction?.Invoke(run);
        run.Status = "failed";
        run.Phase = "failed";
        run.Error = error;
        run.CompletedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;
        run.CurrentMessage = $"Run failed: {error}";

        await db.SaveChangesAsync(ct);

        await LogEventAsync(runId, "error", "failed", $"Pipeline run failed: {error}", null, ct);
    }

    public async Task DeferRunAsync(
        string runId,
        string error,
        Action<PipelineRun>? updateAction = null,
        CancellationToken ct = default)
    {
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == runId, ct);
        if (run is null) return;

        updateAction?.Invoke(run);
        run.Status = "deferred";
        run.Phase = "deferred";
        run.Error = error;
        run.CompletedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;
        run.CurrentMessage = $"Run deferred: {error}";

        await db.SaveChangesAsync(ct);

        await LogEventAsync(runId, "warning", "deferred", $"Pipeline run deferred: {error}", null, ct);
    }

    public async Task LogEventAsync(
        string runId,
        string level,
        string phase,
        string message,
        string? payloadJson = null,
        CancellationToken ct = default)
    {
        var pipelineEvent = new PipelineEvent
        {
            RunId = runId,
            OccurredAt = DateTime.UtcNow,
            Level = level,
            Phase = phase,
            Message = message,
            PayloadJson = payloadJson
        };

        db.PipelineEvents.Add(pipelineEvent);
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordWorkerHeartbeatAsync(
        string? workerInstance = null,
        string? hostEnvironment = null,
        string? activeJobType = null,
        CancellationToken ct = default)
    {
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == "worker-heartbeat", ct);
        var now = DateTime.UtcNow;
        if (run == null)
        {
            run = new PipelineRun
            {
                RunId = "worker-heartbeat",
                PipelineType = "worker",
                Status = "active",
                Phase = "heartbeat",
                StartedAt = now,
                UpdatedAt = now,
                WorkerInstance = workerInstance,
                HostEnvironment = hostEnvironment,
                ActiveJobType = activeJobType
            };
            db.PipelineRuns.Add(run);
        }
        else
        {
            run.UpdatedAt = now;
            run.WorkerInstance = workerInstance;
            run.HostEnvironment = hostEnvironment;
            run.ActiveJobType = activeJobType;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<DateTime?> GetWorkerLastHeartbeatAsync(CancellationToken ct = default)
    {
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == "worker-heartbeat", ct);
        return run?.UpdatedAt;
    }

    public async Task<int> ReapStaleRunsAsync(TimeSpan threshold, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - threshold;

        // The heartbeat row (run_id = "worker-heartbeat") is a long-lived marker, not a run —
        // never reap it. Only actual work runs left "in_progress" past the cutoff are stale.
        var staleRuns = await db.PipelineRuns
            .Where(r => r.Status == "in_progress"
                        && r.RunId != "worker-heartbeat"
                        && r.UpdatedAt < cutoff)
            .ToListAsync(ct);

        if (staleRuns.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        foreach (var run in staleRuns)
        {
            var message = $"Run stalled — no progress for over {(int)threshold.TotalMinutes} minutes. Marked failed by stale-run reaper.";
            run.Status = "failed";
            run.Phase = "stalled";
            run.Error = message;
            run.CompletedAt = now;
            run.UpdatedAt = now;
            run.CurrentMessage = message;

            db.PipelineEvents.Add(new PipelineEvent
            {
                RunId = run.RunId,
                OccurredAt = now,
                Level = "error",
                Phase = "stalled",
                Message = message,
                PayloadJson = null
            });
        }

        // Also close out any matching SyncLog rows still showing InProgress.
        var staleLogs = await db.SyncLogs
            .Where(s => s.Status == "InProgress" && s.StartedAt < cutoff)
            .ToListAsync(ct);
        foreach (var log in staleLogs)
        {
            log.Status = "Failed";
            log.CompletedAt = now;
            log.Errors = "Run stalled — no progress within threshold; marked failed by stale-run reaper.";
        }

        await db.SaveChangesAsync(ct);
        return staleRuns.Count;
    }
}
