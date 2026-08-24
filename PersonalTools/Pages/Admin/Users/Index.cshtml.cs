using Microsoft.AspNetCore.Mvc;
using PersonalTools.Pages.Shared;
using PersonalTools.Security;

namespace PersonalTools.Pages.Admin.Users;

public sealed class IndexModel : RoleRestrictedPageModel
{
    public IActionResult OnGet()
    {
        if (!IsUserAllowedHere(AppRole.Admin))
        {
            return RedirectWhenUserIsNotAllowed();
        }

        return Page();
    }
}
