using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes;
using PersonalTools.Entities;

namespace PersonalTools.Pages.Inventory
{
    public class IndexModel : PageModel
    {
        private readonly ISteamInventoryFuncs _inventoryFuncs;
        private readonly IAuthFuncs _auth;
        public IndexModel(ISteamInventoryFuncs inventoryFuncs, IAuthFuncs auth) { _inventoryFuncs = inventoryFuncs; _auth = auth; }
        [BindProperty(SupportsGet = true)] public string Profile { get; set; } = string.Empty;
        public SteamInventoryResult? Inventory { get; private set; }
        public string? LinkedSteamId { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;
        public async Task OnGet() { if (long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out long userId)) LinkedSteamId = (await _auth.GetUser(userId))?.SteamId; if (string.IsNullOrWhiteSpace(Profile)) return; try { Inventory = await _inventoryFuncs.GetCs2Inventory(Profile); } catch (Exception ex) { ErrorMessage = ex.Message; } }
    }
}
