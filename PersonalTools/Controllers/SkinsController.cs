using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.Skins;
using PersonalTools.Data.Skins;
using PersonalTools.Entities.Skins;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/skins")]
public sealed class SkinsController : ControllerBase
{
    private readonly ISkinFuncs _skins;
    private readonly ICs2SkinData _catalogue;

    public SkinsController(ISkinFuncs skins, ICs2SkinData catalogue)
    {
        _skins = skins;
        _catalogue = catalogue;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<SkinObj>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _skins.GetSkins(UserId, cancellationToken));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? term)
    {
        // The catalogue is static/reference data. Searching its locally cached copy keeps the
        // Select2 type-ahead fast and avoids calling an external API for every keystroke.
        return Ok((await _catalogue.SearchLocalSkins(term ?? string.Empty)).Select(skin => new
        {
            id = skin.MarketHashName,
            text = skin.MarketHashName,
            skin = new
            {
                skin.Name,
                skin.Weapon,
                skin.Exterior,
                skin.MarketHashName,
                skin.Image,
            },
        }));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> Create([FromBody] SkinObj skin, CancellationToken cancellationToken)
    {
        await _skins.CreateSkin(UserId, skin, cancellationToken);
        return Ok(new ApiResponse(true, "Skin added."));
    }

    [HttpPut("{skinId}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid skinId, [FromBody] SkinObj skin, CancellationToken cancellationToken)
    {
        if (skinId != skin.SkinId)
        {
            return BadRequest(new ApiResponse(false, "Invalid skin update."));
        }
        await _skins.UpdateSkin(UserId, skin, cancellationToken);
        return Ok(new ApiResponse(true, "Skin updated."));
    }

    [HttpDelete("{skinId}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid skinId, CancellationToken cancellationToken)
    {
        await _skins.DeleteSkin(UserId, skinId, cancellationToken);
        return Ok(new ApiResponse(true, "Skin deleted."));
    }

    [HttpPost("refresh-catalogue")]
    public async Task<ActionResult<ApiResponse>> Refresh()
    {
        int count = await _skins.RefreshCs2SkinData();
        return Ok(new ApiResponse(count > 0, count > 0 ? $"{count} skins loaded." : "No skin data was loaded."));
    }
}
