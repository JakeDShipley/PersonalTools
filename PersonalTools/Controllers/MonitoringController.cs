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

    public MonitoringController(IServerMonitorFuncs server, IDatabaseMonitorFuncs database)
    {
        _server = server;
        _database = database;
    }

    [HttpGet("server")]
    public async Task<ActionResult<ServerMonitorSnapshot>> GetServer() => Ok(await _server.GetSnapshot());

    [HttpGet("database")]
    public async Task<ActionResult<DatabaseMonitorSnapshot>> GetDatabase(CancellationToken cancellationToken) =>
        Ok(await _database.GetSnapshot(cancellationToken));
}
