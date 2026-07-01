using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/operations")]
public class OperationsController(
    PlaylistMinerDbContext db,
    ITokenProvider tokenProvider,
    IQuotaTracker quotaTracker,
    IOllamaCategorizer ollamaCategorizer,
    IPipelineRunTracker pipelineRunTracker,
    IPlaylistOrganizer playlistOrganizer,
    IRemoteDuplicateCleanupService remoteDuplicateCleanupService,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType<OperationsHealthDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealthAsync(CancellationToken ct = default)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        var oauthConnected = await tokenProvider.IsConnectedAsync(ct);
        var quotaStatus = await quotaTracker.GetStatusAsync(ct);
        var ollamaReachable = await ollamaCategorizer.IsAvailableAsync(ct);
        
        var heartbeat = await pipelineRunTracker.GetWorkerLastHeartbeatAsync(ct);
        var ageSeconds = heartbeat.HasValue ? (int)(DateTime.UtcNow - heartbeat.Value).TotalSeconds : int.MaxValue;

        var latest = await db.PipelineRuns
            .AsNoTracking()
            .Where(r => r.RunId != "worker-heartbeat")
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        var activeRunStalled = false;
        var activeRunProgressing = false;
        string? activeRunPhase = null;

        if (latest is not null && (latest.Status == "in_progress" || latest.Status == "pending"))
        {
            activeRunPhase = latest.Phase;
            var stallThreshold = configuration.GetValue<int>("Pipeline:StallThresholdSeconds", 300);
            activeRunStalled = (DateTime.UtcNow - latest.UpdatedAt).TotalSeconds > stallThreshold;
            activeRunProgressing = !activeRunStalled;
        }

        // The worker thread is busy (and not updating its idle heartbeat) while it runs a long
        // sync, so a fresh, advancing run is itself proof of life. Treat the worker as healthy
        // when the heartbeat is recent OR an active run is still making progress.
        var workerHealthy = ageSeconds <= 30 || activeRunProgressing;

        return Ok(new OperationsHealthDto
        {
            ApiHealthy = true,
            DbHealthy = dbOk,
            WorkerHealthy = workerHealthy,
            WorkerHeartbeatAgeSeconds = ageSeconds == int.MaxValue ? -1 : ageSeconds,
            OauthConnected = oauthConnected,
            QuotaExhausted = quotaStatus.IsExhausted,
            OllamaReachable = ollamaReachable,
            ActiveRunStalled = activeRunStalled,
            ActiveRunPhase = activeRunPhase
        });
    }

    [HttpGet("duplicates")]
    [ProducesResponseType<List<DuplicateReviewDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDuplicatesAsync(CancellationToken ct = default)
    {
        var duplicates = await playlistOrganizer.GetDuplicateReviewAsync(ct);
        return Ok(duplicates);
    }

    [HttpPost("duplicates/plan-remote-cleanup")]
    [ProducesResponseType<List<RemoteDuplicateCleanupItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PlanRemoteCleanupAsync(CancellationToken ct = default)
    {
        var plan = await remoteDuplicateCleanupService.BuildPlanAsync(ct);
        return Ok(plan);
    }

    [HttpPost("duplicates/execute-remote-cleanup")]
    [ProducesResponseType<RemoteDuplicateCleanupResultDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExecuteRemoteCleanupAsync(
        [FromBody] List<RemoteDuplicateCleanupItemDto> plan,
        CancellationToken ct = default)
    {
        if (plan.Any(item => item.HasUnresolvedRemovals))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Remote cleanup plan has unresolved removals.",
                Detail = "Resolve missing playlist item ids before executing remote cleanup."
            });
        }

        var result = await remoteDuplicateCleanupService.ExecuteAsync(plan, ct);
        return Ok(result);
    }
}
