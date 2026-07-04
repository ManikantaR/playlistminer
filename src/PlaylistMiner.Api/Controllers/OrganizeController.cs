using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/organize")]
public class OrganizeController(IOrganizePlannerService organizePlannerService) : ControllerBase
{
    [HttpPost("plan")]
    [ProducesResponseType<OrganizePlanDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> BuildPlanAsync(CancellationToken ct = default)
    {
        var plan = await organizePlannerService.BuildPlanAsync(ct);
        return Ok(plan);
    }
}
