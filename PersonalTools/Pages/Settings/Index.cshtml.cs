using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes.Settings;

namespace PersonalTools.Pages.Settings
{
    public class IndexModel : PageModel
    {
        private readonly ISettingsFuncs _settingsFuncs;

        public IndexModel(ISettingsFuncs settingsFuncs)
        {
            _settingsFuncs = settingsFuncs;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostDeleteAllData()
        {
            await _settingsFuncs.DeleteAllData();

            TempData["SuccessMessage"] = "All data deleted successfully.";

            return RedirectToPage();
        }
    }
}