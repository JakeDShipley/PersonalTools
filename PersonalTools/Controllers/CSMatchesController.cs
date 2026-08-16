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
    private readonly ILogger<CSMatchesController> _logger;

    public CSMatchesController(ILeetifyFuncs leetify, ICSMatchFuncs matches, ICSMatchReferenceData referenceData, ILogger<CSMatchesController> logger)
    {
        _leetify = leetify;
        _matches = matches;
        _referenceData = referenceData;
        _logger = logger;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<CSMatchListItemObj>>> GetMatches([FromQuery] string? profileId)
    {
        List<CSMatchObj> matches = await _matches.GetMatches(UserId, profileId);
        List<CSMapObj> maps = await _referenceData.GetMaps();
        Dictionary<string, string> mapImages = maps.ToDictionary(m => m.Name, m => m.ImagePath);

        List<CSMatchListItemObj> items = matches.Select(match =>
        {
            bool isWin = match.TeamScore > match.OpponentScore;
            bool isOvertime = (match.TeamScore + match.OpponentScore) > 24;

            return new CSMatchListItemObj
            {
                MatchId = match.MatchId,
                StartSide = match.StartSide,
                MapName = match.MapName,
                MapImagePath = mapImages.TryGetValue(match.MapName, out string? imagePath) ? imagePath : null,
                GameType = match.GameType,
                GameTypeLogoPath = GameTypeAssets.LogoPath(match.GameType),
                TeamScore = match.TeamScore,
                OpponentScore = match.OpponentScore,
                OvertimeCount = match.OvertimeCount,
                IsWin = isWin,
                IsOvertime = isOvertime,
                CreatedIso = match.Created.ToString("o"),
                CreatedDisplay = match.Created.ToString("dd MMM"),
                CreatedDisplayFull = match.Created.ToString("dd MMM yyyy HH:mm")
            };
        }).ToList();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> CreateMatch([FromQuery] string? profileId, [FromBody] CSMatchRequest request)
    {
        try
        {
            await _matches.CreateMatch(UserId, profileId, request.ToMatchObj());
            return Ok(new ApiResponse(true, "Match added."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
    }

    [HttpPut("{matchId}")]
    public async Task<ActionResult<ApiResponse>> UpdateMatch(string matchId, [FromBody] CSMatchRequest request)
    {
        try
        {
            await _matches.UpdateMatch(UserId, matchId, request.ToMatchObj());
            return Ok(new ApiResponse(true, "Match updated."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
    }

    [HttpDelete("{matchId}")]
    public async Task<ActionResult<ApiResponse>> DeleteMatch(string matchId)
    {
        await _matches.DeleteMatch(UserId, matchId);
        return Ok(new ApiResponse(true, "Match deleted."));
    }

    [HttpDelete]
    public async Task<ActionResult<ApiResponse>> DeleteAllMatches([FromQuery] string? profileId)
    {
        await _matches.DeleteAllMatches(UserId, profileId);
        return Ok(new ApiResponse(true, "All matches deleted."));
    }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Leetify match lookup failed for user {UserId} and profile {ProfileId}.", UserId, profileId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "Leetify matches could not be loaded. Please try again."));
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Leetify match import failed for user {UserId} and profile {ProfileId}.", UserId, profileId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "The selected matches could not be imported. Please try again."));
        }
    }

    [HttpGet("stats")]
    public async Task<ActionResult<CSMatchStatsObj>> GetStats([FromQuery] string? profileId, [FromQuery] string[]? gameTypes, [FromQuery] bool activeDutyOnly = false)
    {
        List<string>? maps = activeDutyOnly ? await _referenceData.GetActiveDutyPool() : null;
        return Ok(await _matches.GetStats(UserId, profileId, gameTypes, maps));
    }
}

public sealed record CSMatchRequest(string StartSide, string MapName, string GameType, int TeamScore, int OpponentScore, int? OvertimeCount)
{
    public CSMatchObj ToMatchObj() => new()
    {
        StartSide = StartSide,
        MapName = MapName,
        GameType = GameType,
        TeamScore = TeamScore,
        OpponentScore = OpponentScore,
        OvertimeCount = (TeamScore + OpponentScore) > 24 ? (OvertimeCount ?? 0) : 0
    };
}
