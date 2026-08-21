using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes;

namespace PersonalTools.Pages.CSDemos;

public sealed class IndexModel : PageModel
{
    private readonly IAuthFuncs _auth;

    public IndexModel(IAuthFuncs auth)
    {
        _auth = auth;
    }

    public string? LinkedSteamId { get; private set; }

    public async Task OnGet()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
        {
            return;
        }

        LinkedSteamId = (await _auth.GetUser(userId))?.SteamId;
    }
}
