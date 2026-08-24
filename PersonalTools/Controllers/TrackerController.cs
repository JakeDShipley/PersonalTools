using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.Tracker;
using PersonalTools.Entities.Tracker;
using PersonalTools.Security;

namespace PersonalTools.Controllers;

[Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
[ApiController]
[Route("api/tracker")]
public sealed class TrackerController : ControllerBase
{
    private readonly ITrackerFuncs _tracker;
    private readonly ILogger<TrackerController> _logger;

    public TrackerController(ITrackerFuncs tracker, ILogger<TrackerController> logger)
    {
        _tracker = tracker;
        _logger = logger;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<TrackerItemObj>>> Get()
    {
        try
        {
            return Ok(await _tracker.GetItems());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tracker items.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "Tracker items could not be loaded. Please try again."));
        }
    }

    [HttpGet("closed")]
    public async Task<ActionResult<List<TrackerItemObj>>> GetClosed()
    {
        try
        {
            return Ok(await _tracker.GetClosedItems());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load closed tracker items.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "Closed items could not be loaded. Please try again."));
        }
    }

    [HttpGet("assignees")]
    public async Task<ActionResult<List<TrackerAssigneeObj>>> GetAssignees()
    {
        try
        {
            return Ok(await _tracker.GetAssignees());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tracker assignees.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "The list of people to assign to could not be loaded. Please try again."));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> Create([FromBody] TrackerItemRequest request)
    {
        try
        {
            Guid itemId = await _tracker.CreateItem(UserId, request.Type, request.Title, request.Description, request.Area, request.AssignedToUserId, request.ShowOnDashboard);
            return Ok(new ApiResponse(true, itemId.ToString("D")));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tracker item for user {UserId}.", UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "The item could not be saved. Please try again."));
        }
    }

    [HttpPut("{itemId}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid itemId, [FromBody] TrackerItemUpdateRequest request)
    {
        try
        {
            await _tracker.UpdateItem(itemId, request.Type, request.Title, request.Description, request.Area, request.Status, request.AssignedToUserId, request.ShowOnDashboard);
            return Ok(new ApiResponse(true, "Item updated."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tracker item {ItemId}.", itemId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "The item could not be saved. Please try again."));
        }
    }

    [HttpPut("{itemId}/move")]
    public async Task<ActionResult<ApiResponse>> Move(Guid itemId, [FromBody] TrackerItemMoveRequest request)
    {
        try
        {
            await _tracker.MoveItem(itemId, request.Status, request.OrderedItemIds ?? []);
            return Ok(new ApiResponse(true, "Board order saved."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move tracker item {ItemId}.", itemId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "The board order could not be saved. Please try again."));
        }
    }

    [HttpPut("{itemId}/status")]
    public async Task<ActionResult<ApiResponse>> SetStatus(Guid itemId, [FromBody] TrackerItemStatusRequest request)
    {
        try
        {
            await _tracker.SetStatus(itemId, request.Status);
            return Ok(new ApiResponse(true, "Status updated."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set status for tracker item {ItemId}.", itemId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "The status could not be saved. Please try again."));
        }
    }

    [HttpDelete("{itemId}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid itemId)
    {
        try
        {
            await _tracker.DeleteItem(itemId);
            return Ok(new ApiResponse(true, "Item removed."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete tracker item {ItemId}.", itemId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "The item could not be removed. Please try again."));
        }
    }

    [HttpGet("settings")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<TrackerSettingsObj>> GetSettings()
    {
        try
        {
            return Ok(await _tracker.GetSettings());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tracker settings.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "Tracker settings could not be loaded. Please try again."));
        }
    }

    [HttpPut("settings")]
    [Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ApiResponse>> UpdateSettings([FromBody] TrackerSettingsRequest request)
    {
        try
        {
            await _tracker.UpdateSettings(request.AutoCloseAfterDays);
            return Ok(new ApiResponse(true, "Tracker settings saved."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tracker settings.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "Tracker settings could not be saved. Please try again."));
        }
    }
}

public sealed record TrackerItemRequest(string Type, string Title, string Description, string Area, Guid? AssignedToUserId, bool ShowOnDashboard);
public sealed record TrackerItemUpdateRequest(string Type, string Title, string Description, string Area, string Status, Guid? AssignedToUserId, bool ShowOnDashboard);
public sealed record TrackerItemMoveRequest(string Status, IReadOnlyList<Guid>? OrderedItemIds);
public sealed record TrackerItemStatusRequest(string Status);
public sealed record TrackerSettingsRequest(int AutoCloseAfterDays);
