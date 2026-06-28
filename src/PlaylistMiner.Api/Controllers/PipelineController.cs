using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/pipeline")]
public class PipelineController(PlaylistMinerDbContext db) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<PipelineRunDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatusAsync(CancellationToken ct = default)
    {
        var latest = await db.PipelineRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (latest is null)
        {
            return new OkObjectResult(new { }) { StatusCode = StatusCodes.Status200OK };
        }

        return Ok(MapToDto(latest));
    }

    [HttpGet("history")]
    [ProducesResponseType<List<PipelineRunDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryAsync(CancellationToken ct = default)
    {
        var runs = await db.PipelineRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(50)
            .Select(r => MapToDto(r))
            .ToListAsync(ct);

        return Ok(runs);
    }

    [HttpGet("history/{runId}")]
    [ProducesResponseType<PipelineRunDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRunDetailAsync(string runId, CancellationToken ct = default)
    {
        var run = await db.PipelineRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RunId == runId, ct);

        if (run is null)
        {
            return NotFound(new { message = $"Pipeline run with ID '{runId}' not found." });
        }

        return Ok(MapToDto(run));
    }

    [HttpGet("events")]
    [ProducesResponseType<List<PipelineEventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventsAsync([FromQuery] string runId, CancellationToken ct = default)
    {
        var events = await db.PipelineEvents
            .AsNoTracking()
            .Where(e => e.RunId == runId)
            .OrderBy(e => e.OccurredAt)
            .Select(e => MapEventToDto(e))
            .ToListAsync(ct);

        return Ok(events);
    }

    private static PipelineRunDto MapToDto(PipelineRun run) =>
        new(
            run.RunId,
            run.PipelineType,
            run.Status,
            run.Phase,
            run.StartedAt,
            run.UpdatedAt,
            run.CompletedAt,
            run.CurrentMessage,
            run.Error,
            run.PlaylistsDiscovered,
            run.PlaylistsProcessed,
            run.PlaylistItemsFetched,
            run.UniqueVideoIdsIdentified,
            run.VideoMetadataBatchesTotal,
            run.VideoMetadataBatchesCompleted,
            run.VideosUpserted,
            run.PlaylistVideoLinksWritten,
            run.VideosArchived,
            run.VideosDeferred,
            run.ErrorsCount,
            run.VideosPendingTagging,
            run.VideosProcessed,
            run.VideosTagged,
            run.VideosSkipped,
            run.RuleBasedHits,
            run.TfidfHits,
            run.OllamaHits
        );

    private static PipelineEventDto MapEventToDto(PipelineEvent e) =>
        new(
            e.Id,
            e.RunId,
            e.OccurredAt,
            e.Level,
            e.Phase,
            e.Message,
            e.PayloadJson
        );
}
