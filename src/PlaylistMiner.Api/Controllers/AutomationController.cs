using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/automation")]
public class AutomationController(IAutomationPolicyService automationPolicyService) : ControllerBase
{
    [HttpGet("policy")]
    [ProducesResponseType<AutomationPolicyDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicyAsync(CancellationToken ct = default)
    {
        var policy = await automationPolicyService.GetPolicyAsync(ct);
        return Ok(policy);
    }

    [HttpPut("policy")]
    [ProducesResponseType<AutomationPolicyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePolicyAsync(
        [FromBody] UpdateAutomationPolicyRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var policy = await automationPolicyService.UpdatePolicyAsync(request, ct);
            return Ok(policy);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid automation policy.",
                Detail = ex.Message
            });
        }
    }
}
