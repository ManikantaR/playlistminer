using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Api.Models;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/playlists")]
public class PlaylistsController(
    IPlaylistRepository playlistRepository,
    IPlaylistOrganizer playlistOrganizer) : ControllerBase
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
}
