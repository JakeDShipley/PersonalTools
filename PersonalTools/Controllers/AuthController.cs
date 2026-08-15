using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes;

namespace PersonalTools.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthFuncs _auth;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthFuncs auth, ILogger<AuthController> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
                return BadRequest(new ApiResponse(false, "Enter your email address and password."));

            var user = await _auth.Authenticate(request.Email, request.Password);
            if (user is null) return Unauthorized(new ApiResponse(false, "Email or password is incorrect."));

            var session = await _auth.CreateSession(user.UserId, request.RememberMe, Request.Headers.UserAgent.ToString());
            Claim[] claims = [new(ClaimTypes.NameIdentifier, user.UserId.ToString("D")), new(ClaimTypes.Name, user.DisplayName), new(ClaimTypes.Email, user.Email), new("session_id", session.SessionId.ToString("D"))];
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = request.RememberMe, ExpiresUtc = session.ExpiresUtc });
            return Ok(new ApiResponse(true, "Signed in."));
        }
        catch (Exception exception)
        {
            // Do not expose database or authentication details to the browser.
            try
            {
                _logger.LogError(exception, "Sign-in could not be completed for {Email}.", request.Email?.Trim());
            }
            catch
            {
                // Authentication must never fail because an optional log provider is unavailable.
            }
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse(false, "Sign-in could not be completed. Please try again."));
        }
    }
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse>> Logout() { if (Guid.TryParse(User.FindFirstValue("session_id"), out Guid sessionId)) await _auth.DeleteSession(sessionId); await HttpContext.SignOutAsync(); return Ok(new ApiResponse(true, "Signed out.")); }
}

public sealed record LoginRequest(string? Email, string? Password, bool RememberMe);
