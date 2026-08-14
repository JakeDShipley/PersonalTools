using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes;
using PersonalTools.Classes.CSMatches;
using PersonalTools.Classes.Settings;
using PersonalTools.Entities.CSMatches;

namespace PersonalTools.Pages.Settings
{
    public class IndexModel : PageModel
    {
        private readonly ISettingsFuncs _settingsFuncs;
        private readonly IAuthFuncs _auth;
        private readonly ICSMatchReferenceData _referenceData;
        private readonly IMapPoolSuggestionFuncs _mapPoolSuggestion;

        public IndexModel(ISettingsFuncs settingsFuncs, IAuthFuncs auth, ICSMatchReferenceData referenceData, IMapPoolSuggestionFuncs mapPoolSuggestion)
        {
            _settingsFuncs = settingsFuncs;
            _auth = auth;
            _referenceData = referenceData;
            _mapPoolSuggestion = mapPoolSuggestion;
        }

        public string? LinkedSteamId { get; set; }
        public List<CSMapObj> AllMaps { get; set; } = new();
        public List<string> ActiveDutyPool { get; set; } = new();
        public List<string>? PendingMapPoolSuggestion { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool SteamRequired { get; set; }

        [BindProperty]
        public List<string> SelectedActiveDutyMaps { get; set; } = new();

        private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task OnGet()
        {
            LinkedSteamId = (await _auth.GetUser(UserId))?.SteamId;
            AllMaps = await _referenceData.GetMaps();
            ActiveDutyPool = await _referenceData.GetActiveDutyPool();
            PendingMapPoolSuggestion = await _mapPoolSuggestion.GetPendingSuggestion();
        }

        public async Task<IActionResult> OnPostUnlinkSteam()
        {
            await _auth.UnlinkSteam(UserId);

            TempData["SuccessMessage"] = "Steam account unlinked.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateActiveDutyPool()
        {
            await _referenceData.SetActiveDutyPool(SelectedActiveDutyMaps);

            TempData["SuccessMessage"] = "Active Duty map pool updated.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAllData()
        {
            await _settingsFuncs.DeleteAllData();

            TempData["SuccessMessage"] = "All data deleted successfully.";

            return RedirectToPage();
        }
    }
}
