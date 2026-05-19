using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuotaController(IQuotaTracker quotaTracker) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatusAsync(CancellationToken ct)
    {
        var status = await quotaTracker.GetStatusAsync(ct);
        return Ok(status);
    }
}
