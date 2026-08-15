using MySqlConnector;
using PersonalTools.Entities.Skins;

namespace PersonalTools.Data;

public interface ITrackedSkinsData
{
    Task<List<SkinObj>> GetSkins(long userId, CancellationToken cancellationToken = default);
    Task CreateSkin(long userId, SkinObj skin, CancellationToken cancellationToken = default);
    Task UpdateSkin(long userId, SkinObj skin, CancellationToken cancellationToken = default);
    Task DeleteSkin(long userId, Guid skinId, CancellationToken cancellationToken = default);
}

public sealed class TrackedSkinsData : ITrackedSkinsData
{
    private readonly IMariaDbDataAccess _database;
    public TrackedSkinsData(IMariaDbDataAccess database) => _database = database;

    public Task<List<SkinObj>> GetSkins(long userId, CancellationToken cancellationToken = default) =>
        _database.GetBulkDataSP("sp_tracked_skins_get", Map, Parameters(("p_user_id", userId)), cancellationToken);

    public async Task CreateSkin(long userId, SkinObj skin, CancellationToken cancellationToken = default) =>
        await _database.ExecuteSP("sp_tracked_skins_create", WriteParameters(userId, skin), cancellationToken);

    public async Task UpdateSkin(long userId, SkinObj skin, CancellationToken cancellationToken = default) =>
        await _database.ExecuteSP("sp_tracked_skins_update", WriteParameters(userId, skin), cancellationToken);

    public async Task DeleteSkin(long userId, Guid skinId, CancellationToken cancellationToken = default) =>
        await _database.ExecuteSP("sp_tracked_skins_delete", Parameters(("p_user_id", userId), ("p_skin_id", skinId.ToString("D"))), cancellationToken);

    private static MySqlParameter[] WriteParameters(long userId, SkinObj skin) => Parameters(
        ("p_user_id", userId),
        ("p_skin_id", skin.SkinId.ToString("D")),
        ("p_name", skin.Name),
        ("p_weapon", skin.Weapon),
        ("p_exterior", skin.Exterior),
        ("p_market_hash_name", skin.MarketHashName),
        ("p_external_image_url", skin.ExternalImageUrl),
        ("p_purchase_price", skin.PurchasePrice),
        ("p_current_price", skin.CurrentPrice ?? (object)DBNull.Value),
        ("p_purchase_date", skin.PurchaseDate?.Date ?? (object)DBNull.Value),
        ("p_notes", skin.Notes));

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) =>
        values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();

    private static SkinObj Map(MySqlDataReader reader) => new()
    {
        SkinId = reader.GetGuid("SkinId"),
        Name = reader.GetString("Name"),
        Weapon = reader.GetString("Weapon"),
        Exterior = reader.GetString("Exterior"),
        MarketHashName = reader.GetString("MarketHashName"),
        ExternalImageUrl = reader.GetString("ExternalImageUrl"),
        PurchasePrice = reader.GetDecimal("PurchasePrice"),
        CurrentPrice = reader.IsDBNull(reader.GetOrdinal("CurrentPrice")) ? null : reader.GetDecimal("CurrentPrice"),
        PurchaseDate = reader.IsDBNull(reader.GetOrdinal("PurchaseDate")) ? null : reader.GetDateTime("PurchaseDate"),
        Notes = reader.GetString("Notes"),
        Created = reader.GetDateTime("CreatedUtc"),
        Updated = reader.GetDateTime("UpdatedUtc")
    };
}
