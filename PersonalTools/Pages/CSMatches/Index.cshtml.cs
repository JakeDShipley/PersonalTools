using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes;
using PersonalTools.Classes.CSMatches;
using PersonalTools.Entities.CSMatches;
namespace PersonalTools.Pages.CSMatches
{
    public class IndexModel : PageModel
    {
        private readonly ICSMatchFuncs _matchFuncs;
        private readonly ICSMatchReferenceData _referenceData;
        private readonly IWebHostEnvironment _env;
        private readonly IAuthFuncs _auth;
        private readonly IMapPoolSuggestionFuncs _mapPoolSuggestion;
        private readonly IMatchProfileFuncs _profileFuncs;

        public IndexModel(ICSMatchFuncs matchFuncs, ICSMatchReferenceData referenceData, IWebHostEnvironment env, IAuthFuncs auth, IMapPoolSuggestionFuncs mapPoolSuggestion, IMatchProfileFuncs profileFuncs)
        {
            _matchFuncs = matchFuncs;
            _referenceData = referenceData;
            _env = env;
            _auth = auth;
            _mapPoolSuggestion = mapPoolSuggestion;
            _profileFuncs = profileFuncs;
        }

        private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public List<CSMatchObj> Matches { get; set; } = new();
        public List<CSMapObj> Maps { get; set; } = new();
        public List<string> GameTypes { get; set; } = new();
        public CSMatchStatsObj Stats { get; set; } = new();
        public bool HasLinkedSteamId { get; set; }
        public bool MapPoolSuggestionPending { get; set; }
        public List<MatchProfileObj> Profiles { get; set; } = new();
        public MatchProfileObj? ActiveProfile { get; set; }

        private string? _profileId;

        // An empty-string ProfileId (from "?ProfileId=" on the default tab, or an empty hidden form
        // field) must normalize to null here, not stay "" - the DB layer treats null as "the default
        // profile" and passes it through the null-safe <=> operator, which "" would silently fail to match.
        [BindProperty(SupportsGet = true)]
        public string? ProfileId
        {
            get => _profileId;
            set => _profileId = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        [BindProperty]
        public string ProfileName { get; set; } = string.Empty;

        [BindProperty]
        public string ProfileSteamId { get; set; } = string.Empty;

        [BindProperty]
        public string MatchId { get; set; } = string.Empty;

        [BindProperty]
        public string StartSide { get; set; } = string.Empty;

        [BindProperty]
        public string MapName { get; set; } = string.Empty;

        [BindProperty]
        public string GameType { get; set; } = "Premier";

        [BindProperty]
        public int TeamScore { get; set; }

        [BindProperty]
        public int OpponentScore { get; set; }

        [BindProperty]
        public int? OvertimeCount { get; set; }

        [BindProperty]
        public string NewMapName { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? NewMapImage { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        public async Task OnGet()
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString() ?? string.Empty;
            await LoadPageData();
        }

        private async Task LoadPageData()
        {
            Profiles = await _profileFuncs.GetProfiles(UserId);

            ActiveProfile = await _profileFuncs.GetProfile(UserId, ProfileId);
            if (!string.IsNullOrWhiteSpace(ProfileId) && ActiveProfile is null)
            {
                // Profile was deleted or doesn't belong to this user - fall back to the default "You" tab.
                ProfileId = null;
            }

            Matches = await _matchFuncs.GetMatches(UserId, ProfileId);
            Maps = await _referenceData.GetMaps();
            GameTypes = await _referenceData.GetGameTypes();
            Stats = await _matchFuncs.GetStats(UserId, ProfileId);
            HasLinkedSteamId = !string.IsNullOrWhiteSpace((await _auth.GetUser(UserId))?.SteamId);
            MapPoolSuggestionPending = (await _mapPoolSuggestion.GetPendingSuggestion())?.Count > 0;
        }

        public async Task<IActionResult> OnPostCreate()
        {
            if (string.IsNullOrWhiteSpace(StartSide) || string.IsNullOrWhiteSpace(MapName) || string.IsNullOrWhiteSpace(GameType))
            {
                ErrorMessage = "Please complete all fields.";
                await LoadPageData();
                return Page();
            }

            CSMatchObj match = new CSMatchObj
            {
                StartSide = StartSide,
                MapName = MapName,
                GameType = GameType,
                TeamScore = TeamScore,
                OpponentScore = OpponentScore,
                OvertimeCount = (TeamScore + OpponentScore) > 24 ? (OvertimeCount ?? 0) : 0
            };

            await _matchFuncs.CreateMatch(UserId, ProfileId, match);

            TempData["SuccessMessage"] = "Match added successfully.";
            return RedirectToPage(new { ProfileId });
        }

        public async Task<IActionResult> OnPostEdit()
        {
            if (string.IsNullOrWhiteSpace(MatchId))
            {
                ErrorMessage = "Could not find the match to update.";
                await LoadPageData();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(StartSide) || string.IsNullOrWhiteSpace(MapName) || string.IsNullOrWhiteSpace(GameType))
            {
                ErrorMessage = "Please complete all fields.";
                await LoadPageData();
                return Page();
            }

            CSMatchObj match = new CSMatchObj
            {
                StartSide = StartSide,
                MapName = MapName,
                GameType = GameType,
                TeamScore = TeamScore,
                OpponentScore = OpponentScore,
                OvertimeCount = (TeamScore + OpponentScore) > 24 ? (OvertimeCount ?? 0) : 0
            };

            await _matchFuncs.UpdateMatch(UserId, MatchId, match);

            TempData["SuccessMessage"] = "Match updated successfully.";
            return RedirectToPage(new { ProfileId });
        }

        public async Task<IActionResult> OnPostDelete(string matchId)
        {
            if (!string.IsNullOrWhiteSpace(matchId))
            {
                await _matchFuncs.DeleteMatch(UserId, matchId);
                TempData["SuccessMessage"] = "Match deleted successfully.";
            }

            return RedirectToPage(new { ProfileId });
        }

        public async Task<IActionResult> OnPostDeleteAllMatches()
        {
            await _matchFuncs.DeleteAllMatches(UserId, ProfileId);

            TempData["SuccessMessage"] = "All matches deleted successfully.";
            return RedirectToPage(new { ProfileId });
        }

        public async Task<IActionResult> OnPostCreateProfile()
        {
            try
            {
                string newProfileId = await _profileFuncs.CreateProfile(UserId, ProfileName, ProfileSteamId);
                TempData["SuccessMessage"] = "Profile added successfully.";
                return RedirectToPage(new { ProfileId = newProfileId });
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                await LoadPageData();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateProfile()
        {
            if (string.IsNullOrWhiteSpace(ProfileId))
            {
                ErrorMessage = "Could not find the profile to update.";
                await LoadPageData();
                return Page();
            }

            try
            {
                await _profileFuncs.UpdateProfile(UserId, ProfileId, ProfileName, ProfileSteamId);
                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToPage(new { ProfileId });
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                await LoadPageData();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteProfile()
        {
            if (!string.IsNullOrWhiteSpace(ProfileId))
            {
                await _profileFuncs.DeleteProfile(UserId, ProfileId);
                TempData["SuccessMessage"] = "Profile removed successfully.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddMap()
        {
            if (string.IsNullOrWhiteSpace(NewMapName) || NewMapImage == null || NewMapImage.Length == 0)
            {
                ErrorMessage = "Please provide a map name and an image.";
                await LoadPageData();
                return Page();
            }

            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
            string extension = Path.GetExtension(NewMapImage.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ErrorMessage = "Please upload a jpg, png or webp image.";
                await LoadPageData();
                return Page();
            }

            if (NewMapImage.Length > 2 * 1024 * 1024)
            {
                ErrorMessage = "Image must be smaller than 2MB.";
                await LoadPageData();
                return Page();
            }

            string fileName = $"{Guid.NewGuid()}{extension}";
            string folderPath = Path.Combine(_env.WebRootPath, "images", "cs", "maps");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, fileName);

            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                await NewMapImage.CopyToAsync(stream);
            }

            CSMapObj map = new CSMapObj
            {
                Name = NewMapName.Trim(),
                ImagePath = $"/images/cs/maps/{fileName}"
            };

            await _referenceData.AddMap(map);

            TempData["SuccessMessage"] = "Map added successfully.";
            return RedirectToPage(new { ProfileId });
        }
    }
}