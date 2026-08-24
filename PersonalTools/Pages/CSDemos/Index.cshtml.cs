using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes;
using PersonalTools.Pages.Shared;
using PersonalTools.Security;

namespace PersonalTools.Pages.CSDemos;

public sealed class IndexModel : RoleRestrictedPageModel
{
    private readonly IAuthFuncs _auth;

    public IndexModel(IAuthFuncs auth)
    {
        _auth = auth;
    }

    public string? LinkedSteamId { get; private set; }

    public async Task<IActionResult> OnGet()
    {
        if (!IsUserAllowedHere(AppRole.Admin))
        {
            return RedirectWhenUserIsNotAllowed();
        }
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
        {
            return RedirectWhenUserIsNotAllowed();
        }

        LinkedSteamId = (await _auth.GetUser(userId))?.SteamId;
        return Page();
    }
}
