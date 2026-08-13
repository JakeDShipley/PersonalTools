using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes;

namespace PersonalTools.Pages;
[AllowAnonymous]
public class SetupModel : PageModel
{
    private readonly IAuthFuncs _auth;
    public SetupModel(IAuthFuncs auth) => _auth = auth;
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string DisplayName { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;
    public string ErrorMessage { get; private set; } = string.Empty;
    public async Task<IActionResult> OnGet() => await _auth.HasUsers() ? RedirectToPage("/Login") : Page();
    public async Task<IActionResult> OnPostAsync() { if (Password != ConfirmPassword) { ErrorMessage = "Passwords do not match."; return Page(); } try { await _auth.CreateOwner(Email, DisplayName, Password); return RedirectToPage("/Login"); } catch (Exception ex) { ErrorMessage = ex.Message; return Page(); } }
}
