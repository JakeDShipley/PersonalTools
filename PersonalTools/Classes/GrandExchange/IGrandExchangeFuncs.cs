using PersonalTools.Entities.GrandExchange;

namespace PersonalTools.Classes.GrandExchange
{
    public interface IGrandExchangeFuncs
    {
        Task<GrandExchangeLookupResultObj> SearchItems(string searchTerm);
    }
}
