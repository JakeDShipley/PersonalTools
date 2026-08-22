using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PersonalTools.Pages.PasteBin;

public sealed class PasteModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string ShortCode { get; set; } = string.Empty;

    public void OnGet() { }
}
