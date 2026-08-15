using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes;

namespace PersonalTools.Pages.Account;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IndexModel : PageModel
{
    private readonly IAuthFuncs _auth;

    public IndexModel(IAuthFuncs auth) => _auth = auth;

    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;

    public async Task OnGet()
    {
        Guid userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _auth.GetUser(userId);
        DisplayName = user?.DisplayName ?? User.Identity?.Name ?? "Account";
        Email = user?.Email ?? User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        Initials = BuildInitials(DisplayName);
    }

    private static string BuildInitials(string value)
    {
        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }
}
