using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Api.Models;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/playlists")]
public class PlaylistsController(
    IPlaylistRepository playlistRepository,
    IPlaylistOrganizer playlistOrganizer,
    IPlaylistRestoreService playlistRestoreService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<PlaylistDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct = default)
    {
        var playlists = await playlistRepository.GetAllAsync(ct);
        return Ok(playlists);
    }

    [HttpPost]
    [ProducesResponseType<PlaylistDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreatePlaylistRequest request,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var playlist = await playlistRepository.CreateAsync(new Playlist
        {
            YouTubeId = Guid.NewGuid().ToString("N")[..32], // "N" format GUID = 32 hex chars
            Name = request.Title,
            Description = request.Description,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);

        var dto = new PlaylistDto(playlist.YouTubeId, playlist.Name, playlist.Description, playlist.IsInbox, 0, playlist.Id);
        return Created($"/api/playlists/{playlist.Id}", dto);
    }

    [HttpPost("{id:int}/set-inbox")]
    [HttpPost("{id:int}/inbox")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetInboxAsync(int id, CancellationToken ct = default)
    {
        try
        {
            await playlistRepository.SetInboxAsync(id, ct);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Playlist not found.",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("consolidate")]
    [ProducesResponseType<List<PlaylistDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConsolidateAsync(CancellationToken ct = default)
    {
        var result = await playlistOrganizer.ConsolidateAsync(ct);
        return Ok(result);
    }

    [HttpPost("{targetPlaylistId:int}/restore-sample")]
    [ProducesResponseType<PlaylistRestoreResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreSampleAsync(
        int targetPlaylistId,
        [FromBody] RestorePlaylistSampleRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await playlistRestoreService.RestoreSampleAsync(
                request.SourcePlaylistId,
                targetPlaylistId,
                request.MaxCount,
                ct);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid restore request.",
                Detail = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Playlist not found.",
                Detail = ex.Message
            });
        }
        catch (QuotaExhaustedException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "YouTube quota exhausted.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{targetPlaylistId:int}/restore-status")]
    [ProducesResponseType<PlaylistRestoreStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRestoreStatusAsync(
        int targetPlaylistId,
        [FromQuery] int sourcePlaylistId,
        CancellationToken ct = default)
    {
        try
        {
            var status = await playlistRestoreService.GetStatusAsync(sourcePlaylistId, targetPlaylistId, ct);
            return Ok(status);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Playlist not found.",
                Detail = ex.Message
            });
        }
    }
}
