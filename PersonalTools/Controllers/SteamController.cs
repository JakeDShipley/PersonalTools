using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes;
using PersonalTools.Entities;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/steam")]
public sealed class SteamController : ControllerBase
{
    private readonly ISteamInventoryFuncs _steam;
    public SteamController(ISteamInventoryFuncs steam) => _steam = steam;

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
}
