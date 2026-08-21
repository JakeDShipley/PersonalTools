using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.CSDemos;
using PersonalTools.Entities;
using PersonalTools.Entities.CSDemos;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/cs-demos")]
public sealed class CSDemosController : ControllerBase
{
    private readonly ICSDemoFuncs _demos;
    private readonly ILogger<CSDemosController> _logger;

    public CSDemosController(ICSDemoFuncs demos, ILogger<CSDemosController> logger)
    {
        _demos = demos;
        _logger = logger;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Returns the user's cached catalogue where possible so repeat visits do not wait on
    /// Leetify. The first lookup safely fills the cache through the normal Funcs/Data flow.
    /// </summary>
    [HttpGet]
    public Task<ActionResult<CSDemoLibraryObj>> GetRecentDemos(
        [FromQuery] string profile,
        CancellationToken cancellationToken)
    {
        return Execute(profile, (reference, token) => _demos.GetRecentDemos(UserId, reference, token), cancellationToken);
    }

    /// <summary>
    /// Deliberately refreshes provider metadata and the short-lived demo URLs. This still never
    /// proxies a replay file through the server: the browser later uses the source URL directly.
    /// </summary>
    [HttpPost("refresh")]
    public Task<ActionResult<CSDemoLibraryObj>> RefreshRecentDemos(
        [FromBody] CSDemoProfileRequest request,
        CancellationToken cancellationToken)
    {
        return Execute(request.Profile, (reference, token) => _demos.RefreshRecentDemos(UserId, reference, token), cancellationToken);
    }

    private async Task<ActionResult<CSDemoLibraryObj>> Execute(
        string profile,
        Func<string, CancellationToken, Task<CSDemoLibraryObj>> action,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile) || profile.Length > 200)
        {
            return BadRequest(new ApiResponse(false, "Enter a valid Steam profile URL, custom name, or SteamID64."));
        }

        try
        {
            return Ok(await action(profile, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "CS2 demo lookup failed for user {UserId}.", UserId);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new ApiResponse(false, "Demo links could not be loaded right now. Please try again shortly."));
        }
    }
}

public sealed record CSDemoProfileRequest(string Profile);
