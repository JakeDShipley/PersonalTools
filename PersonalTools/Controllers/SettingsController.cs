using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes;
using PersonalTools.Entities;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly IAppSettingsFuncs _settings;
    public SettingsController(IAppSettingsFuncs settings) => _settings = settings;
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public async Task<ActionResult<List<AppSettingView>>> Get(CancellationToken cancellationToken) => Ok(await _settings.Get(UserId, cancellationToken));
    [HttpPut]
    public async Task<ActionResult<ApiResponse>> Set([FromBody] AppSettingRequest request, CancellationToken cancellationToken)
    {
        try { await _settings.Set(UserId, request.Key, request.Value, cancellationToken); return Ok(new ApiResponse(true, "Setting saved.")); }
        catch (InvalidOperationException exception) { return BadRequest(new ApiResponse(false, exception.Message)); }
    }
}
public sealed record AppSettingRequest(AppSettingKey Key, string? Value);
