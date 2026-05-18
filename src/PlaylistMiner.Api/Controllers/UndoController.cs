using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/undo")]
public class UndoController(IUndoRepository undoRepository, IPlaylistOrganizer playlistOrganizer) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<UndoLogDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingAsync(CancellationToken ct = default)
    {
        var pending = await undoRepository.GetPendingAsync(ct);
        return Ok(pending);
    }

    [HttpPost("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> UndoAsync(int id, CancellationToken ct = default)
    {
        try
        {
            await playlistOrganizer.UndoMoveAsync(id, ct);
            return Ok();
        }
        catch (GoneException ex)
        {
            return StatusCode(StatusCodes.Status410Gone, new ProblemDetails { Title = ex.Message });
        }
    }
}
