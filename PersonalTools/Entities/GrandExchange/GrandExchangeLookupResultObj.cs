namespace PersonalTools.Entities.GrandExchange
{
    /// <summary>
    /// Contains the items found for a lookup and a safe message when the live service is unavailable.
    /// </summary>
    public class GrandExchangeLookupResultObj
    {
        public List<GrandExchangeItemObj> Items { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
