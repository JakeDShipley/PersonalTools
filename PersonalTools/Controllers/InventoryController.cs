using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes;
using PersonalTools.Entities;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ISteamInventoryFuncs _inventory;
    public InventoryController(ISteamInventoryFuncs inventory) => _inventory = inventory;
    [HttpGet("cs2")] public async Task<ActionResult<SteamInventoryResult>> GetCs2([FromQuery] string profile, CancellationToken cancellationToken) => string.IsNullOrWhiteSpace(profile) ? BadRequest(new ApiResponse(false, "Enter a Steam profile.")) : Ok(await _inventory.GetCs2Inventory(profile));
}
