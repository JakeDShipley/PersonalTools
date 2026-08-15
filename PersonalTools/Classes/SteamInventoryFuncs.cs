using Microsoft.Extensions.Caching.Memory;
using PersonalTools.Data;
using PersonalTools.Entities;

namespace PersonalTools.Classes;

public interface ISteamInventoryFuncs
{
    Task<SteamInventoryResult> GetCs2Inventory(string profileReference, CancellationToken cancellationToken = default);
}

public sealed class SteamInventoryFuncs : ISteamInventoryFuncs
{
    private readonly ISteamInventoryData _data;
    private readonly IMemoryCache _cache;

    public SteamInventoryFuncs(ISteamInventoryData data, IMemoryCache cache)
    {
        _data = data;
        _cache = cache;
    }

    public async Task<SteamInventoryResult> GetCs2Inventory(string profileReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileReference) || profileReference.Trim().Length > 300)
            throw new InvalidOperationException("Enter a Steam profile URL, custom profile URL, or 64-bit Steam ID.");

        string steamId = await _data.ResolveSteamId(profileReference.Trim(), cancellationToken);
        SteamInventoryResult? cached = await _cache.GetOrCreateAsync($"steam-cs2-{steamId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _data.LoadCs2Inventory(steamId, cancellationToken);
        });
        return cached ?? new SteamInventoryResult { SteamId = steamId };
    }
}
