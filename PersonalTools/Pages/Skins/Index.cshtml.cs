using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes.Skins;
using PersonalTools.Data.Skins;
using PersonalTools.Entities.Skins;
using System.Security.Claims;

namespace PersonalTools.Pages.Skins
{
    public class IndexModel : PageModel
    {
        private readonly ISkinFuncs _skinFuncs;
        private readonly ICs2SkinData _cs2SkinData;
        public IndexModel(ISkinFuncs skinFuncs, ICs2SkinData cs2SkinData) { _skinFuncs = skinFuncs; _cs2SkinData = cs2SkinData; }
        public List<SkinObj> Skins { get; set; } = new();
        [BindProperty] public SkinObj Skin { get; set; } = new();
        [BindProperty(SupportsGet = true)] public string SortBy { get; set; } = "currentPrice";
        [BindProperty(SupportsGet = true)] public string SortDirection { get; set; } = "desc";
        public decimal TotalPurchaseValue { get; set; }
        public decimal TotalCurrentValue { get; set; }
        public decimal TotalProfitLoss { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task OnGet()
        {
            Skins = await _skinFuncs.GetSkins(UserId);
            Skins = SortBy switch
            {
                "name" => SortDirection == "asc" ? Skins.OrderBy(x => x.Name).ToList() : Skins.OrderByDescending(x => x.Name).ToList(),
                "purchasePrice" => SortDirection == "asc" ? Skins.OrderBy(x => x.PurchasePrice).ToList() : Skins.OrderByDescending(x => x.PurchasePrice).ToList(),
                "currentPrice" => SortDirection == "asc" ? Skins.OrderBy(x => x.CurrentPrice ?? x.PurchasePrice).ToList() : Skins.OrderByDescending(x => x.CurrentPrice ?? x.PurchasePrice).ToList(),
                _ => Skins.OrderByDescending(x => x.CurrentPrice ?? x.PurchasePrice).ToList()
            };
            TotalPurchaseValue = Skins.Sum(x => x.PurchasePrice);
            TotalCurrentValue = Skins.Sum(x => x.CurrentPrice ?? x.PurchasePrice);
            TotalProfitLoss = TotalCurrentValue - TotalPurchaseValue;
        }

        public async Task<IActionResult> OnPostCreate()
        {
            if (string.IsNullOrWhiteSpace(Skin.Name)) { TempData["ErrorMessage"] = "Please select a skin."; return RedirectToPage(); }
            await _skinFuncs.CreateSkin(UserId, Skin); TempData["SuccessMessage"] = "Skin added successfully."; return RedirectToPage();
        }
        public async Task<IActionResult> OnPostEdit()
        {
            if (Skin.SkinId == Guid.Empty) { TempData["ErrorMessage"] = "Could not find the skin to update."; return RedirectToPage(); }
            if (string.IsNullOrWhiteSpace(Skin.Name)) { TempData["ErrorMessage"] = "Please select a skin."; return RedirectToPage(); }
            await _skinFuncs.UpdateSkin(UserId, Skin); TempData["SuccessMessage"] = "Skin updated successfully."; return RedirectToPage();
        }
        public async Task<IActionResult> OnPostDelete(Guid skinId)
        {
            if (skinId != Guid.Empty) { await _skinFuncs.DeleteSkin(UserId, skinId); TempData["SuccessMessage"] = "Skin deleted successfully."; }
            return RedirectToPage();
        }
        public async Task<IActionResult> OnPostRefreshSkinData()
        {
            try { int count = await _skinFuncs.RefreshCs2SkinData(); TempData[count <= 0 ? "ErrorMessage" : "SuccessMessage"] = count <= 0 ? "No CS2 skin data was loaded." : $"CS2 skin data refreshed. {count} skins loaded."; }
            catch (Exception ex) { TempData["ErrorMessage"] = $"Could not refresh CS2 skin data: {ex.Message}"; }
            return RedirectToPage();
        }
        public async Task<IActionResult> OnGetSkinSearch(string term)
        {
            List<Cs2LocalSkinObj> skins = await _cs2SkinData.SearchLocalSkins(term);
            return new JsonResult(skins.Select(s => new { id = s.MarketHashName, text = s.MarketHashName, skin = new { s.Name, s.Weapon, s.Exterior, s.MarketHashName, s.Image } }));
        }

        private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
