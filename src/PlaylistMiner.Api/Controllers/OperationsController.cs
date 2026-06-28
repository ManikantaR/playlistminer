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
        var workerHealthy = ageSeconds <= 30;

        var latest = await db.PipelineRuns
            .AsNoTracking()
            .Where(r => r.RunId != "worker-heartbeat")
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        var activeRunStalled = false;
        string? activeRunPhase = null;

        if (latest is not null && (latest.Status == "in_progress" || latest.Status == "pending"))
        {
            activeRunPhase = latest.Phase;
            var stallThreshold = configuration.GetValue<int>("Pipeline:StallThresholdSeconds", 300);
            activeRunStalled = (DateTime.UtcNow - latest.UpdatedAt).TotalSeconds > stallThreshold;
        }

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
}
