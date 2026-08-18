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
    private readonly IServerMonitorFuncs _server;
    private readonly IDatabaseMonitorFuncs _database;
    private readonly ILogsViewerFuncs _logs;

    public MonitoringController(IServerMonitorFuncs server, IDatabaseMonitorFuncs database, ILogsViewerFuncs logs)
    {
        _server = server;
        _database = database;
        _logs = logs;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="forceRefresh"></param>
    /// <returns></returns>
    [HttpGet("server")]
    public async Task<ActionResult<ServerMonitorSnapshot>> GetServer([FromQuery] bool forceRefresh = false) =>
        Ok(await _server.GetSnapshot(forceRefresh));

    /// <summary>
    /// 
    /// </summary>
    /// <param name="forceRefresh"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("database")]
    public async Task<ActionResult<DatabaseMonitorSnapshot>> GetDatabase([FromQuery] bool forceRefresh = false, CancellationToken cancellationToken = default) =>
        Ok(await _database.GetSnapshot(forceRefresh, cancellationToken));

    /// <summary>
    /// 
    /// </summary>
    /// <param name="afterId"></param>
    /// <param name="minimumLevel"></param>
    /// <param name="search"></param>
    /// <param name="take"></param>
    /// <returns></returns>
    [HttpGet("logs")]
    public ActionResult<ApplicationLogResult> GetLogs(
        [FromQuery] long afterId = 0,
        [FromQuery] string minimumLevel = "Information",
        [FromQuery] string? search = null,
        [FromQuery] int take = 200) =>
        Ok(_logs.GetLogs(afterId, minimumLevel, search, take));
}