using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes;
using PersonalTools.Entities;
using PersonalTools.Classes.CSMatches;
using PersonalTools.Entities.CSMatches;
using PersonalTools.Classes.Tracker;
using PersonalTools.Classes.PasteBin;
using PersonalTools.Pages.Shared;
using PersonalTools.Security;

namespace PersonalTools.Pages.Settings;

public sealed class IndexModel : RoleRestrictedPageModel
{
    private readonly IAuthFuncs _auth;
    private readonly ICSMatchReferenceData _referenceData;
    private readonly IMapPoolSuggestionFuncs _mapPoolSuggestion;
    private readonly IAppSettingsFuncs _settings;
    private readonly ITrackerFuncs _tracker;
    private readonly IPasteBinFuncs _pasteBin;

    public IndexModel(
        IAuthFuncs auth,
        ICSMatchReferenceData referenceData,
        IMapPoolSuggestionFuncs mapPoolSuggestion,
        IAppSettingsFuncs settings,
        ITrackerFuncs tracker,
        IPasteBinFuncs pasteBin)
    {
        _auth = auth;
        _referenceData = referenceData;
        _mapPoolSuggestion = mapPoolSuggestion;
        _settings = settings;
        _tracker = tracker;
        _pasteBin = pasteBin;
    }

    public string? LinkedSteamId { get; private set; }
    public List<CSMapObj> AllMaps { get; private set; } = [];
    public List<string> ActiveDutyPool { get; private set; } = [];
    public List<string>? PendingMapPoolSuggestion { get; private set; }
    public List<AppSettingView> Settings { get; private set; } = [];
    public int TrackerAutoCloseAfterDays { get; private set; }
    public int PasteBinMaximumUploadSizeMb { get; private set; } = 50;

    [BindProperty(SupportsGet = true)]
    public bool SteamRequired { get; set; }

    [BindProperty]
    public List<string> SelectedActiveDutyMaps { get; set; } = [];

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsUserAllowedHere()
    {
        return base.IsUserAllowedHere(AppRole.Admin);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsUserAllowedHere())
        {
            return RedirectWhenUserIsNotAllowed();
        }

        LinkedSteamId = (await _auth.GetUser(UserId))?.SteamId;
        AllMaps = await _referenceData.GetMaps();
        ActiveDutyPool = await _referenceData.GetActiveDutyPool();
        PendingMapPoolSuggestion = await _mapPoolSuggestion.GetPendingSuggestion();
        Settings = await _settings.Get(UserId);
        TrackerAutoCloseAfterDays = (await _tracker.GetSettings()).AutoCloseAfterDays;
        PasteBinMaximumUploadSizeMb = (await _pasteBin.GetPasteBinSettings()).MaximumUploadSizeMb;
        return Page();
    }

    public async Task<IActionResult> OnPostUnlinkSteam()
    {
        if (!IsUserAllowedHere())
        {
            return RedirectWhenUserIsNotAllowed();
        }

        await _auth.UnlinkSteam(UserId);
        TempData["SuccessMessage"] = "Steam account unlinked.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateActiveDutyPool()
    {
        if (!IsUserAllowedHere())
        {
            return RedirectWhenUserIsNotAllowed();
        }

        await _referenceData.SetActiveDutyPool(SelectedActiveDutyMaps);
        TempData["SuccessMessage"] = "Active Duty map pool updated.";
        return RedirectToPage();
    }
}
