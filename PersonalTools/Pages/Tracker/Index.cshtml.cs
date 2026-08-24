using Microsoft.AspNetCore.Mvc;
using PersonalTools.Pages.Shared;
using PersonalTools.Security;

namespace PersonalTools.Pages.Tracker
{
    public class IndexModel : RoleRestrictedPageModel
    {
        public IActionResult OnGet() => IsUserAllowedHere(AppRole.Admin) ? Page() : RedirectWhenUserIsNotAllowed();
    }
}
