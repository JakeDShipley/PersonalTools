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
    public AuthController(IAuthFuncs auth) => _auth = auth;
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _auth.Authenticate(request.Email, request.Password);
        if (user is null) return Unauthorized(new ApiResponse(false, "Email or password is incorrect."));
        var session = await _auth.CreateSession(user.UserId, request.RememberMe, Request.Headers.UserAgent.ToString());
        Claim[] claims = [new(ClaimTypes.NameIdentifier, user.UserId.ToString()), new(ClaimTypes.Name, user.DisplayName), new(ClaimTypes.Email, user.Email), new("session_id", session.SessionId)];
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = request.RememberMe, ExpiresUtc = session.ExpiresUtc });
        return Ok(new ApiResponse(true, "Signed in."));
    }
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse>> Logout() { string? sessionId = User.FindFirstValue("session_id"); if (!string.IsNullOrWhiteSpace(sessionId)) await _auth.DeleteSession(sessionId); await HttpContext.SignOutAsync(); return Ok(new ApiResponse(true, "Signed out.")); }
}

public sealed record LoginRequest(string Email, string Password, bool RememberMe);
