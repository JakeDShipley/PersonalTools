using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Security;

namespace PersonalTools.Pages.Shared;

/// <summary>
/// Shared base for Razor pages with a role requirement. Tool pages that every signed-in user can
/// use continue to rely on the application's authenticated fallback policy.
/// </summary>
public abstract class RoleRestrictedPageModel : PageModel
{
    protected bool IsUserAllowedHere(params AppRole[] allowedRoles)
    {
        return User.IsUserAllowedHere(allowedRoles);
    }

    protected IActionResult RedirectWhenUserIsNotAllowed()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return RedirectToPage("/Login", new { ReturnUrl = Request.Path.Value });
    }
}
