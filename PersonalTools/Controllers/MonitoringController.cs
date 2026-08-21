using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.Monitoring;
using PersonalTools.Entities.Monitoring;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/monitoring")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MonitoringController : ControllerBase
{
    private readonly ILogsViewerFuncs _logs;

    public MonitoringController(ILogsViewerFuncs logs)
    {
        _logs = logs;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <param name="minimumLevel"></param>
    /// <param name="search"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("logs")]
    public async Task<ActionResult<ApplicationLogResult>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string minimumLevel = "Information",
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _logs.GetLogs(page, pageSize, minimumLevel, search, cancellationToken));
}
