using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/sync")]
public class SyncController(
    ISyncService syncService,
    PlaylistMinerDbContext db,
    ILogger<SyncController> logger) : ControllerBase
{
    [HttpPost("trigger")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult TriggerAsync(CancellationToken ct = default)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await syncService.FullSyncAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background sync failed.");
            }
        }, ct);

        return Accepted(new { message = "Sync triggered." });
    }

    [HttpGet("status")]
    [ProducesResponseType<SyncLog>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatusAsync(CancellationToken ct = default)
    {
        var latest = await db.SyncLogs
            .AsNoTracking()
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        // Explicitly wrap to avoid ASP.NET Core returning 204 No Content for null values
        return new OkObjectResult(latest ?? (object)new { }) { StatusCode = StatusCodes.Status200OK };
    }

    [HttpGet("history")]
    [ProducesResponseType<List<SyncLog>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryAsync(CancellationToken ct = default)
    {
        var logs = await db.SyncLogs
            .AsNoTracking()
            .OrderByDescending(s => s.StartedAt)
            .Take(50)
            .ToListAsync(ct);

        return Ok(logs);
    }
}
