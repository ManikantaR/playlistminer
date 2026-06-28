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
}
