using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.Dashboard;
using PersonalTools.Entities.Dashboard;
using System.Security.Claims;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardFuncs _dashboard;
    private readonly IDashboardWidgetOrderFuncs _widgetOrder;
    private readonly IDashboardWeatherFuncs _weather;
    public DashboardController(IDashboardFuncs dashboard, IDashboardWidgetOrderFuncs widgetOrder, IDashboardWeatherFuncs weather) { _dashboard = dashboard; _widgetOrder = widgetOrder; _weather = weather; }
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("tools")] public ActionResult<List<DashboardToolObj>> GetTools() => Ok(_dashboard.GetDashboardTools());
    [HttpGet("widget-order")] public async Task<ActionResult<List<string>>> GetWidgetOrder(CancellationToken cancellationToken) => Ok(await _widgetOrder.GetOrder(UserId, cancellationToken));
    [HttpPut("widget-order")] public async Task<ActionResult<ApiResponse>> UpdateWidgetOrder([FromBody] DashboardWidgetOrderRequest request, CancellationToken cancellationToken) { await _widgetOrder.UpdateOrder(UserId, request.WidgetKeys ?? [], cancellationToken); return Ok(new ApiResponse(true, "Dashboard layout saved.")); }
    [HttpGet("weather-locations")] public async Task<ActionResult<List<DashboardWeatherLocation>>> GetWeatherLocations(CancellationToken cancellationToken) => Ok(await _weather.GetLocations(UserId, cancellationToken));
    [HttpPost("weather-locations")] public async Task<ActionResult<ApiResponse>> CreateWeatherLocation([FromBody] DashboardWeatherLocationRequest request, CancellationToken cancellationToken) { await _weather.CreateLocation(UserId, request.DisplayName, request.Latitude, request.Longitude, cancellationToken); return Ok(new ApiResponse(true, "Weather location saved.")); }
    [HttpDelete("weather-locations/{weatherLocationId:guid}")] public async Task<ActionResult<ApiResponse>> DeleteWeatherLocation(Guid weatherLocationId, CancellationToken cancellationToken) { await _weather.DeleteLocation(UserId, weatherLocationId, cancellationToken); return Ok(new ApiResponse(true, "Weather location removed.")); }
}

public sealed record DashboardWidgetOrderRequest(IReadOnlyList<string>? WidgetKeys);
public sealed record DashboardWeatherLocationRequest(string DisplayName, decimal Latitude, decimal Longitude);
