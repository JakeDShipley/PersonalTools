using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes;

namespace PersonalTools.Controllers;

/// <summary>
/// Browser-facing Steam OpenID endpoints. The endpoint names intentionally match the original
/// Program.cs routes so existing settings and inventory links continue to work unchanged.
/// </summary>
[Authorize]
[ApiController]
[Route("auth/steam")]
public sealed class SteamOpenIdController(ISteamOpenIdFuncs steamOpenIdFuncs) : ControllerBase
{
    private const string StateCookieName = "PersonalTools.SteamLinkState";
    private readonly ISteamOpenIdFuncs _steamOpenIdFuncs = steamOpenIdFuncs;

    [HttpGet("link")]
    public IActionResult Link()
    {
        string state = _steamOpenIdFuncs.CreateState();
        Uri requestBaseUri = new($"{Request.Scheme}://{Request.Host}/");

        Response.Cookies.Append(StateCookieName, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            IsEssential = true,
        });

        return Redirect(_steamOpenIdFuncs.CreateSignInUri(requestBaseUri, state).ToString());
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? state, CancellationToken cancellationToken)
    {
        string savedState = Request.Cookies[StateCookieName] ?? string.Empty;

        // Delete it before provider verification. A callback token is single-use even if Steam
        // responds slowly or a browser retries the callback URL.
        Response.Cookies.Delete(StateCookieName, new CookieOptions
        {
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
        });

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
        {
            return Challenge();
        }

        Dictionary<string, string> providerParameters = Request.Query
            .Where(item => item.Key.StartsWith("openid.", StringComparison.Ordinal))
            .ToDictionary(item => item.Key, item => item.Value.ToString());
        providerParameters["openid.mode"] = "check_authentication";

        bool linked = await _steamOpenIdFuncs.CompleteLink(
            userId,
            state ?? string.Empty,
            savedState,
            providerParameters,
            cancellationToken);

        return linked
            ? LocalRedirect("/Settings")
            : BadRequest("Steam linking could not be verified. Please try again.");
    }
}
