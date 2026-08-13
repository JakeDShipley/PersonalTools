using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.Settings;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly ISettingsFuncs _settings;
    public SettingsController(ISettingsFuncs settings) => _settings = settings;
    [HttpDelete("local-data")] public async Task<ActionResult<ApiResponse>> DeleteLocalData() { await _settings.DeleteAllData(); return Ok(new ApiResponse(true, "All local data deleted.")); }
}
