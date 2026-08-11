using PersonalTools.Data.GrandExchange;
using PersonalTools.Entities.GrandExchange;

namespace PersonalTools.Classes.GrandExchange
{
    public class GrandExchangeFuncs : IGrandExchangeFuncs
    {
        private readonly IGrandExchangeData _grandExchangeData;

        public GrandExchangeFuncs(IGrandExchangeData grandExchangeData)
        {
            _grandExchangeData = grandExchangeData;
        }

        public async Task<GrandExchangeLookupResultObj> SearchItems(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new GrandExchangeLookupResultObj();

            return await _grandExchangeData.SearchItems(searchTerm.Trim());
        }
    }
}
