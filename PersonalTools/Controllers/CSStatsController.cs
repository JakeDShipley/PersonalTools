using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.CSStats;
using PersonalTools.Entities.CSStats;
using System.Security.Claims;

namespace PersonalTools.Controllers;

[ApiController]
[Route("api/cs-stats")]
public sealed class CSStatsController : ControllerBase
{
    private readonly ICSStatsFuncs _stats;
    private readonly IReportedPlayersFuncs _reports;

    public CSStatsController(ICSStatsFuncs stats, IReportedPlayersFuncs reports) { _stats = stats; _reports = reports; }
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("profile")]
    public async Task<ActionResult<CSStatsProfileObj>> GetProfile([FromQuery] string profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile) || profile.Length > 200)
            return BadRequest(new { message = "Enter a valid Steam profile URL, custom name, or SteamID64." });

        try { return Ok(await _stats.GetProfile(UserId, profile, cancellationToken)); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("reports")]
    public async Task<ActionResult<ApiResponse>> ReportPlayer([FromBody] CSStatsReportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            bool created = await _reports.ReportPlayer(UserId, request.Steam64Id, cancellationToken);
            int count = await _reports.GetReportCount(request.Steam64Id, cancellationToken);
            return Ok(new CSStatsReportResponse(created, count, created ? "Report saved." : "You have already reported this player."));
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }
}

public sealed record CSStatsReportRequest(string Steam64Id);
public sealed record CSStatsReportResponse(bool Created, int ReportCount, string Message);
