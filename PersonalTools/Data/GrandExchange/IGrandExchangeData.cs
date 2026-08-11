using PersonalTools.Entities.GrandExchange;

namespace PersonalTools.Data.GrandExchange
{
    public interface IGrandExchangeData
    {
        Task<GrandExchangeLookupResultObj> SearchItems(string searchTerm);
    }
}
