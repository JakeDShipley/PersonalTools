using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes;

namespace PersonalTools.Controllers;

[ApiController]
[Route("api/account")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AccountController : ControllerBase
{
    private readonly IAuthFuncs _auth;

    public AccountController(IAuthFuncs auth) => _auth = auth;

    [HttpPost("password")]
    public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
            return Unauthorized(new ApiResponse(false, "Your session could not be verified. Please sign in again."));

        Guid.TryParse(User.FindFirstValue("session_id"), out Guid sessionId);

        try
        {
            await _auth.ChangePassword(userId, sessionId, request.CurrentPassword, request.NewPassword, request.ConfirmPassword);
            return Ok(new ApiResponse(true, "Password changed securely. Other signed-in sessions have been ended."));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ApiResponse(false, exception.Message));
        }
    }
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}
