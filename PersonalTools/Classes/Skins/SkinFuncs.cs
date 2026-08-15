using PersonalTools.Data;
using PersonalTools.Data.Skins;
using PersonalTools.Entities.Skins;

namespace PersonalTools.Classes.Skins;

public interface ISkinFuncs
{
    Task<List<SkinObj>> GetSkins(Guid userId, CancellationToken cancellationToken = default);
    Task CreateSkin(Guid userId, SkinObj skin, CancellationToken cancellationToken = default);
    Task UpdateSkin(Guid userId, SkinObj skin, CancellationToken cancellationToken = default);
    Task DeleteSkin(Guid userId, Guid skinId, CancellationToken cancellationToken = default);
    Task<int> RefreshCs2SkinData();
}

public sealed class SkinFuncs : ISkinFuncs
{
    private readonly ITrackedSkinsData _data;
    private readonly ICs2SkinData _catalogue;

    public SkinFuncs(ITrackedSkinsData data, ICs2SkinData catalogue)
    {
        _data = data;
        _catalogue = catalogue;
    }

    public Task<List<SkinObj>> GetSkins(Guid userId, CancellationToken cancellationToken = default) =>
        _data.GetSkins(userId, cancellationToken);

    public Task CreateSkin(Guid userId, SkinObj skin, CancellationToken cancellationToken = default)
    {
        NormaliseAndValidate(skin, requireId: false);
        skin.SkinId = Guid.NewGuid();
        return _data.CreateSkin(userId, skin, cancellationToken);
    }

    public Task UpdateSkin(Guid userId, SkinObj skin, CancellationToken cancellationToken = default)
    {
        NormaliseAndValidate(skin, requireId: true);
        return _data.UpdateSkin(userId, skin, cancellationToken);
    }

    public Task DeleteSkin(Guid userId, Guid skinId, CancellationToken cancellationToken = default)
    {
        if (skinId == Guid.Empty) throw new InvalidOperationException("The tracked skin identifier was invalid.");
        return _data.DeleteSkin(userId, skinId, cancellationToken);
    }

    public async Task<int> RefreshCs2SkinData()
    {
        List<Cs2ApiSkinObj> apiSkins = await _catalogue.GetApiSkins();
        List<Cs2LocalSkinObj> localSkins = apiSkins
            .Where(skin => !string.IsNullOrWhiteSpace(skin.MarketHashName))
            .Select(skin => new Cs2LocalSkinObj
            {
                Name = skin.Name,
                Weapon = skin.Weapon?.Name ?? string.Empty,
                Exterior = skin.Wear?.Name ?? string.Empty,
                MarketHashName = skin.MarketHashName,
                Image = skin.Image
            })
            .OrderBy(skin => skin.MarketHashName)
            .ToList();
        await _catalogue.SaveLocalSkins(localSkins);
        return localSkins.Count;
    }

    private static void NormaliseAndValidate(SkinObj skin, bool requireId)
    {
        if (requireId && skin.SkinId == Guid.Empty) throw new InvalidOperationException("The tracked skin identifier was invalid.");
        skin.Name = skin.Name?.Trim() ?? string.Empty;
        skin.Weapon = skin.Weapon?.Trim() ?? string.Empty;
        skin.Exterior = skin.Exterior?.Trim() ?? string.Empty;
        skin.MarketHashName = skin.MarketHashName?.Trim() ?? string.Empty;
        skin.ExternalImageUrl = skin.ExternalImageUrl?.Trim() ?? string.Empty;
        skin.Notes = skin.Notes?.Trim() ?? string.Empty;
        if (skin.Name.Length is < 1 or > 200 || skin.Weapon.Length > 100 || skin.Exterior.Length > 100 || skin.MarketHashName.Length > 255 || skin.ExternalImageUrl.Length > 2048 || skin.Notes.Length > 20_000)
            throw new InvalidOperationException("One or more tracked skin fields were invalid or too long.");
        if (skin.PurchasePrice < 0 || skin.CurrentPrice < 0) throw new InvalidOperationException("Prices cannot be negative.");
    }
}
