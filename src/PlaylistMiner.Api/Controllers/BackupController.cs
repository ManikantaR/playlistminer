using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/backup")]
public class BackupController(IBackupService backupService) : ControllerBase
{
    [HttpPost("trigger")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult Trigger()
    {
        _ = backupService.TriggerBackupAsync(HttpContext.RequestAborted);
        return Accepted();
    }

    [HttpGet("history")]
    [ProducesResponseType<List<BackupInfo>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
        => Ok(await backupService.ListBackupsAsync(ct));

    [HttpGet("download/{filename}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string filename, CancellationToken ct)
    {
        try
        {
            var stream = await backupService.GetBackupStreamAsync(filename, ct);
            return File(stream, "application/sql", filename);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}
