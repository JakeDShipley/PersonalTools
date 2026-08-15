using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.CSMatches;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/csmatches")]
public sealed class CSMatchesController : ControllerBase
{
    private readonly ILeetifyFuncs _leetify;
    private readonly ICSMatchFuncs _matches;
    private readonly ICSMatchReferenceData _referenceData;

    public CSMatchesController(ILeetifyFuncs leetify, ICSMatchFuncs matches, ICSMatchReferenceData referenceData)
    {
        _leetify = leetify;
        _matches = matches;
        _referenceData = referenceData;
    }

    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("leetify")]
    public async Task<ActionResult<List<CSMatchLeetifyPreviewObj>>> GetLeetifyMatches([FromQuery] string? profileId)
    {
        try
        {
            return Ok(await _leetify.GetAvailableMatches(UserId, profileId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
    }

    [HttpPost("leetify/import")]
    public async Task<ActionResult<ApiResponse>> ImportLeetifyMatches([FromQuery] string? profileId, [FromBody] List<string> leetifyMatchIds)
    {
        if (leetifyMatchIds is null || leetifyMatchIds.Count == 0)
        {
            return BadRequest(new ApiResponse(false, "Select at least one match to import."));
        }

        try
        {
            List<CSMatchObj> batch = await _leetify.BuildImportBatch(UserId, profileId, leetifyMatchIds);
            await _matches.ImportMatches(UserId, profileId, batch);
            return Ok(new ApiResponse(true, $"{batch.Count} match{(batch.Count == 1 ? "" : "es")} imported."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
    }

    [HttpGet("stats")]
    public async Task<ActionResult<CSMatchStatsObj>> GetStats([FromQuery] string? profileId, [FromQuery] string[]? gameTypes, [FromQuery] bool activeDutyOnly = false)
    {
        List<string>? maps = activeDutyOnly ? await _referenceData.GetActiveDutyPool() : null;
        return Ok(await _matches.GetStats(UserId, profileId, gameTypes, maps));
    }
}
