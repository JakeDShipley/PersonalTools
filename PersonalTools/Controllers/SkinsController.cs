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
    private readonly ISkinFuncs _skinFuncs;
    private readonly ICs2SkinData _skinData;
    public SkinsController(ISkinFuncs skinFuncs, ICs2SkinData skinData) { _skinFuncs = skinFuncs; _skinData = skinData; }
    [HttpGet] public async Task<ActionResult<List<SkinObj>>> Get() => Ok(await _skinFuncs.GetSkins());
    [HttpGet("search")] public async Task<IActionResult> Search([FromQuery] string term) => Ok((await _skinData.SearchLocalSkins(term)).Select(s => new { id = s.MarketHashName, text = s.MarketHashName, skin = new { s.Name, s.Weapon, s.Exterior, s.MarketHashName, s.Image } }));
    [HttpPost] public async Task<ActionResult<ApiResponse>> Create([FromBody] SkinObj skin) { if (string.IsNullOrWhiteSpace(skin.Name)) return BadRequest(new ApiResponse(false, "Select a skin.")); await _skinFuncs.CreateSkin(skin); return Ok(new ApiResponse(true, "Skin added.")); }
    [HttpPut("{skinId}")] public async Task<ActionResult<ApiResponse>> Update(string skinId, [FromBody] SkinObj skin) { if (skinId != skin.SkinId || string.IsNullOrWhiteSpace(skin.Name)) return BadRequest(new ApiResponse(false, "Invalid skin update.")); await _skinFuncs.UpdateSkin(skin); return Ok(new ApiResponse(true, "Skin updated.")); }
    [HttpDelete("{skinId}")] public async Task<ActionResult<ApiResponse>> Delete(string skinId) { await _skinFuncs.DeleteSkin(skinId); return Ok(new ApiResponse(true, "Skin deleted.")); }
    [HttpPost("refresh-catalogue")] public async Task<ActionResult<ApiResponse>> Refresh() { int count = await _skinFuncs.RefreshCs2SkinData(); return Ok(new ApiResponse(count > 0, count > 0 ? $"{count} skins loaded." : "No skin data was loaded.")); }
}
