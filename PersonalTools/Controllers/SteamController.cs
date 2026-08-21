using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PersonalTools.Classes;
using PersonalTools.Entities;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/steam")]
public sealed class SteamController : ControllerBase
{
    private readonly ISteamInventoryFuncs _steam;
    private readonly IAuthFuncs _auth;
    private readonly ILogger<SteamController> _logger;

    public SteamController(ISteamInventoryFuncs steam, IAuthFuncs auth, ILogger<SteamController> logger)
    {
        _steam = steam;
        _auth = auth;
        _logger = logger;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("lookup")]
    public async Task<ActionResult<SteamProfileLookupResult>> Lookup([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new ApiResponse(false, "Enter a Steam profile URL, custom profile URL, name, or 64-bit Steam ID."));
        }

        try
        {
            return Ok(await _steam.LookupProfile(query, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
    }

    [HttpPut("link")]
    public async Task<ActionResult<ApiResponse>> Link([FromBody] SteamLinkRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProfileReference))
        {
            return BadRequest(new ApiResponse(false, "Find a Steam profile before linking it."));
        }

        try
        {
            SteamProfileLookupResult profile = await _steam.LookupProfile(request.ProfileReference, cancellationToken);
            await _auth.LinkSteam(UserId, profile.SteamId64);
            return Ok(new ApiResponse(true, $"Steam account linked as {profile.DisplayName}."));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Steam account linking failed for user {UserId}.", UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "The Steam account could not be linked. Please try again."));
        }
    }
}

public sealed record SteamLinkRequest(string? ProfileReference);
