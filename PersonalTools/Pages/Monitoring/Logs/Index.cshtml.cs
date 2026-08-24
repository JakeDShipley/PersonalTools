using Microsoft.AspNetCore.Mvc;
using PersonalTools.Pages.Shared;
using PersonalTools.Security;

namespace PersonalTools.Pages.Monitoring.Logs;

public sealed class IndexModel : RoleRestrictedPageModel
{
    public IActionResult OnGet() => IsUserAllowedHere(AppRole.Admin) ? Page() : RedirectWhenUserIsNotAllowed();
}
