using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/agent")]
public class AgentController(IAgentProcessService agentProcessService) : ControllerBase
{
    [HttpPost("process-now")]
    [ProducesResponseType<AgentProcessResultDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessNowAsync(CancellationToken ct = default)
    {
        var result = await agentProcessService.ProcessNowAsync(ct);
        return Ok(result);
    }
}
