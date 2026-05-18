using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/import")]
public class ImportController(IImportService importService) : ControllerBase
{
    [HttpPost("takeout")]
    [ProducesResponseType<ImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadTakeoutAsync(
        [FromForm] IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "No file provided." });

        await using var stream = file.OpenReadStream();
        var result = await importService.ImportTakeoutAsync(stream, ct);

        if (result.Errors > 0 && result.VideosFound == 0)
            return BadRequest(new ProblemDetails { Title = "Invalid CSV format." });

        return Ok(result);
    }

    [HttpGet("history")]
    [ProducesResponseType<List<ImportBatch>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryAsync(CancellationToken ct = default)
    {
        var batches = await importService.GetHistoryAsync(ct);
        return Ok(batches);
    }
}
