namespace PersonalTools.Data.CaseOpening;

public interface ICS2ItemPriceData
{
    Task<decimal?> GetEstimatedPrice(string marketHashName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deliberately returns no price until a dependable market provider is configured. Both case
/// openings and the inventory viewer can later share a replacement implementation of this interface.
/// </summary>
public sealed class NullCS2ItemPriceData : ICS2ItemPriceData
{
    public Task<decimal?> GetEstimatedPrice(string marketHashName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<decimal?>(null);
    }
}
