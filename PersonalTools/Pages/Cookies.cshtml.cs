using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PersonalTools.Pages;

[AllowAnonymous]
public class CookiesModel : PageModel { public void OnGet() { } }
