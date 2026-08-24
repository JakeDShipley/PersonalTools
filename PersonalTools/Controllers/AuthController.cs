using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalTools.Classes;
using PersonalTools.Classes.CaseOpening;
using PersonalTools.Entities;
using PersonalTools.Security;

namespace PersonalTools.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthFuncs _auth;
    private readonly ICaseOpeningFuncs _caseOpening;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthFuncs auth, ICaseOpeningFuncs caseOpening, ILogger<AuthController> logger)
    {
        _auth = auth;
        _caseOpening = caseOpening;
        _logger = logger;
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
                return BadRequest(new LoginResponse(false, "Enter your email address and password.", string.Empty));

            AuthenticationResult authentication = await _auth.Authenticate(request.Email, request.Password);

            if (authentication.IsLockedOut)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new LoginResponse(
                    false,
                    "Too many incorrect sign-in attempts. Try again in a few minutes or ask an administrator to unlock the account.",
                    string.Empty));
            }

            AppUser? user = authentication.User;
            if (user is null)
            {
                return Unauthorized(new LoginResponse(false, "Email or password is incorrect.", string.Empty));
            }

            var session = await _auth.CreateSession(user.UserId, request.RememberMe, Request.Headers.UserAgent.ToString());
            Claim[] claims =
            [
                new(ClaimTypes.NameIdentifier, user.UserId.ToString("D")),
                new(ClaimTypes.Name, user.DisplayName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.ToString()),
                new("session_id", session.SessionId.ToString("D")),
            ];
            AuthenticationProperties properties = new()
            {
                IsPersistent = request.RememberMe,
                ExpiresUtc = session.ExpiresUtc,
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                properties);

            try
            {
                await _caseOpening.RecordCaseOpeningLogin(user.UserId);
            }
            catch (Exception exception)
            {
                // Progression is a secondary feature. A valid account must still be able to sign
                // in if its optional daily activity record cannot be written.
                _logger.LogWarning(exception, "Case-opening login activity could not be recorded for {UserId}.", user.UserId);
            }

            // The launch screen can welcome the just-authenticated account without another
            // round-trip. This is a display-only value and never includes session information.
            return Ok(new LoginResponse(true, "Signed in.", user.DisplayName));
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
            return StatusCode(StatusCodes.Status500InternalServerError, new LoginResponse(false, "Sign-in could not be completed. Please try again.", string.Empty));
        }
    }
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse>> Logout()
    {
        if (Guid.TryParse(User.FindFirstValue("session_id"), out Guid sessionId))
        {
            // Invalidate the server-side session before clearing the browser cookie so a copied
            // authentication ticket cannot stay valid until its natural expiry.
            await _auth.DeleteSession(sessionId);
        }

        await HttpContext.SignOutAsync();
        return Ok(new ApiResponse(true, "Signed out."));
    }
}

public sealed record LoginRequest(string? Email, string? Password, bool RememberMe);
public sealed record LoginResponse(bool Success, string Message, string DisplayName);
