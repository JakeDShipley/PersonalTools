using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes;

namespace PersonalTools.Pages;
[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IAuthFuncs _auth;
    public LoginModel(IAuthFuncs auth) => _auth = auth;
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public bool RememberMe { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string ErrorMessage { get; private set; } = string.Empty;
    public async Task<IActionResult> OnGet() => !await _auth.HasUsers() ? RedirectToPage("/Setup") : User.Identity?.IsAuthenticated == true ? LocalRedirect("/") : Page();
    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _auth.Authenticate(Email, Password);
        if (user is null) { ErrorMessage = "Email or password is incorrect."; return Page(); }
        var session = await _auth.CreateSession(user.UserId, RememberMe, Request.Headers.UserAgent.ToString());
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()), new Claim(ClaimTypes.Name, user.DisplayName), new Claim(ClaimTypes.Email, user.Email), new Claim("session_id", session.SessionId) };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { IsPersistent = RememberMe, ExpiresUtc = session.ExpiresUtc });
        return LocalRedirect(!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
    }
    public async Task<IActionResult> OnPostSignOutAsync()
    {
        string? sessionId = User.FindFirstValue("session_id");
        if (!string.IsNullOrWhiteSpace(sessionId)) await _auth.DeleteSession(sessionId);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage();
    }
}
