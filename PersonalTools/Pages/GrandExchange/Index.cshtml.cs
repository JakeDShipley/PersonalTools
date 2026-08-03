using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalTools.Classes.GrandExchange;
using PersonalTools.Entities.GrandExchange;

namespace PersonalTools.Pages.GrandExchange
{
    public class IndexModel : PageModel
    {
        private readonly IGrandExchangeFuncs _grandExchangeFuncs;

        public IndexModel(IGrandExchangeFuncs grandExchangeFuncs)
        {
            _grandExchangeFuncs = grandExchangeFuncs;
        }

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty;

        public List<GrandExchangeItemObj> Items { get; private set; } = new();
        public string ErrorMessage { get; private set; } = string.Empty;
        public bool HasSearched => !string.IsNullOrWhiteSpace(SearchTerm);

        public async Task OnGetAsync()
        {
            if (!HasSearched)
                return;

            GrandExchangeLookupResultObj result = await _grandExchangeFuncs.SearchItems(SearchTerm);

            Items = result.Items;
            ErrorMessage = result.ErrorMessage;
        }
    }
}
