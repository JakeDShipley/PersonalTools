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

        public IndexModel(ICSMatchFuncs matchFuncs, ICSMatchReferenceData referenceData, IWebHostEnvironment env, IAuthFuncs auth, IMapPoolSuggestionFuncs mapPoolSuggestion)
        {
            _matchFuncs = matchFuncs;
            _referenceData = referenceData;
            _env = env;
            _auth = auth;
            _mapPoolSuggestion = mapPoolSuggestion;
        }

        private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public List<CSMatchObj> Matches { get; set; } = new();
        public List<CSMapObj> Maps { get; set; } = new();
        public List<string> GameTypes { get; set; } = new();
        public CSMatchStatsObj Stats { get; set; } = new();
        public bool HasLinkedSteamId { get; set; }
        public bool MapPoolSuggestionPending { get; set; }

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
            Matches = await _matchFuncs.GetMatches();
            Maps = await _referenceData.GetMaps();
            GameTypes = await _referenceData.GetGameTypes();
            Stats = await _matchFuncs.GetStats();
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

            await _matchFuncs.CreateMatch(match);

            TempData["SuccessMessage"] = "Match added successfully.";
            return RedirectToPage();
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

            await _matchFuncs.UpdateMatch(MatchId, match);

            TempData["SuccessMessage"] = "Match updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDelete(string matchId)
        {
            if (!string.IsNullOrWhiteSpace(matchId))
            {
                await _matchFuncs.DeleteMatch(matchId);
                TempData["SuccessMessage"] = "Match deleted successfully.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAllMatches()
        {
            await _matchFuncs.DeleteAllMatches();

            TempData["SuccessMessage"] = "All matches deleted successfully.";
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
            return RedirectToPage();
        }
    }
}