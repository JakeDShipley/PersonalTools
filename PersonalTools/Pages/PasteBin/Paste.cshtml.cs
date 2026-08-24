using Microsoft.AspNetCore.Mvc;
using PersonalTools.Pages.Shared;
using PersonalTools.Security;

namespace PersonalTools.Pages.PasteBin;

public sealed class PasteModel : RoleRestrictedPageModel
{
    [BindProperty(SupportsGet = true)]
    public string ShortCode { get; set; } = string.Empty;

    public IActionResult OnGet() => IsUserAllowedHere(AppRole.Admin) ? Page() : RedirectWhenUserIsNotAllowed();
}
