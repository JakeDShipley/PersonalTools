using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.Dashboard;
using PersonalTools.Entities.Dashboard;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardFuncs _dashboard;
    public DashboardController(IDashboardFuncs dashboard) => _dashboard = dashboard;
    [HttpGet("tools")] public ActionResult<List<DashboardToolObj>> GetTools() => Ok(_dashboard.GetDashboardTools());
}
