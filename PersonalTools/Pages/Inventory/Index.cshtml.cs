using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes;

namespace PersonalTools.Pages.Inventory;

public sealed class IndexModel : PageModel
{
    private readonly IAuthFuncs _auth;

    public IndexModel(IAuthFuncs auth) => _auth = auth;

    [BindProperty(SupportsGet = true)]
    public string Profile { get; set; } = string.Empty;

    public string? LinkedSteamId { get; private set; }

    public async Task OnGet()
    {
        if (Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out Guid userId))
            LinkedSteamId = (await _auth.GetUser(userId))?.SteamId;
    }
}
