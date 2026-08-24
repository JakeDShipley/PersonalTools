using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes;
using PersonalTools.Entities;
using PersonalTools.Security;

namespace PersonalTools.Controllers;

[Authorize(Policy = AppAuthorizationPolicies.AdminOnly)]
[ApiController]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAuthFuncs _auth;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(IAuthFuncs auth, ILogger<AdminUsersController> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<AdminUserObj>>> GetUsers()
    {
        try
        {
            return Ok(await _auth.GetManagedUsers());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Administrator {UserId} could not load registered users.", UserId);
            return StatusCode(500, new ApiResponse(false, "Registered users could not be loaded. Please try again."));
        }
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserObj>> CreateUser([FromBody] AdminUserSaveRequest request)
    {
        try
        {
            return Ok(await _auth.CreateManagedUser(request.Email, request.DisplayName, request.Password ?? string.Empty, request.ConfirmPassword ?? string.Empty, request.Role, request.IsActive));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Administrator {UserId} could not create user {Email}.", UserId, request.Email?.Trim());
            return StatusCode(500, new ApiResponse(false, "The user account could not be created. Please try again."));
        }
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult<AdminUserObj>> UpdateUser(Guid userId, [FromBody] AdminUserSaveRequest request)
    {
        try
        {
            return Ok(await _auth.UpdateManagedUser(UserId, userId, request.Email, request.DisplayName, request.Password, request.ConfirmPassword, request.Role, request.IsActive));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Administrator {UserId} could not update user {ManagedUserId}.", UserId, userId);
            return StatusCode(500, new ApiResponse(false, "The user account could not be updated. Please try again."));
        }
    }

    [HttpPost("{userId:guid}/login-lockout/reset")]
    public async Task<ActionResult<AdminUserObj>> ResetLoginLockout(Guid userId)
    {
        try
        {
            AdminUserObj user = await _auth.ResetManagedUserLoginLockout(userId);
            _logger.LogInformation("Administrator {UserId} cleared the sign-in lockout for {ManagedUserId}.", UserId, userId);
            return Ok(user);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Administrator {UserId} could not clear the sign-in lockout for {ManagedUserId}.", UserId, userId);
            return StatusCode(500, new ApiResponse(false, "The account lockout could not be cleared. Please try again."));
        }
    }
}

public sealed record AdminUserSaveRequest(
    string Email,
    string DisplayName,
    string? Password,
    string? ConfirmPassword,
    AppRole Role,
    bool IsActive);
