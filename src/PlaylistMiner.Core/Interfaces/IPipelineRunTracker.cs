using System;
using System.Threading;
using System.Threading.Tasks;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.Interfaces;

public interface IPipelineRunTracker
{
    Task<string> StartRunAsync(string pipelineType, CancellationToken ct = default);
    Task UpdateRunAsync(string runId, Action<PipelineRun> updateAction, string? phase = null, string? message = null, CancellationToken ct = default);
    Task CompleteRunAsync(string runId, Action<PipelineRun>? updateAction = null, CancellationToken ct = default);
    Task FailRunAsync(string runId, string error, Action<PipelineRun>? updateAction = null, CancellationToken ct = default);
    Task DeferRunAsync(string runId, string error, Action<PipelineRun>? updateAction = null, CancellationToken ct = default);
    Task LogEventAsync(string runId, string level, string phase, string message, string? payloadJson = null, CancellationToken ct = default);
    Task RecordWorkerHeartbeatAsync(string? workerInstance = null, string? hostEnvironment = null, string? activeJobType = null, CancellationToken ct = default);
    Task<DateTime?> GetWorkerLastHeartbeatAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks any in-progress pipeline run (and its matching sync log) as failed when it has
    /// not reported progress within <paramref name="threshold"/>. Prevents a crashed or
    /// abandoned run from showing "in progress" forever. Returns the number of runs reaped.
    /// </summary>
    Task<int> ReapStaleRunsAsync(TimeSpan threshold, CancellationToken ct = default);
}
