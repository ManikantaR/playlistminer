using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/oauth")]
public class OAuthController(
    ITokenProvider tokenProvider,
    PlaylistMinerDbContext db,
    ILogger<OAuthController> logger) : ControllerBase
{
    [HttpGet("connect")]
    public IActionResult Connect()
    {
        var url = tokenProvider.GetAuthorizationUrl();
        return Ok(new { url });
    }

    [HttpGet("callback")]
    public async Task<IActionResult> CallbackAsync(
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("OAuth denied: {Error}", error);
            return Redirect("/settings?error=denied");
        }

        if (string.IsNullOrEmpty(code))
            return BadRequest(new { message = "Missing authorization code." });

        try
        {
            await tokenProvider.ExchangeCodeAsync(code, ct);
            logger.LogInformation("YouTube OAuth connected successfully.");
            return Redirect("/settings?connected=true");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OAuth code exchange failed.");
            return Redirect("/settings?error=exchange_failed");
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatusAsync(CancellationToken ct)
    {
        var connected = await tokenProvider.IsConnectedAsync(ct);
        return Ok(new { connected });
    }

    [HttpDelete("disconnect")]
    public async Task<IActionResult> DisconnectAsync(CancellationToken ct)
    {
        var setting = await db.Settings
            .FirstOrDefaultAsync(s => s.Key == "oauth.refresh_token", ct);

        if (setting is not null)
        {
            db.Settings.Remove(setting);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("YouTube OAuth disconnected.");
        }

        return NoContent();
    }
}
