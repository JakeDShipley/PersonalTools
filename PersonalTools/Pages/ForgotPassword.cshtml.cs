using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace PersonalTools.Pages;
[AllowAnonymous]
public class ForgotPasswordModel : PageModel { public void OnGet() { } }
