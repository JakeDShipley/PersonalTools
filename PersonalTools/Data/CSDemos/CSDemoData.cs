using MySqlConnector;
using PersonalTools.Entities.CSDemos;
using System.Text.Json;

namespace PersonalTools.Data.CSDemos;

public interface ICSDemoData
{
    Task<List<CSDemoDbModel>> GetDemos(Guid userId, string steam64Id, CancellationToken cancellationToken = default);
    Task SyncDemoCatalogue(Guid userId, string steam64Id, IReadOnlyList<CSDemoDbModel> demos, CancellationToken cancellationToken = default);
}

public sealed class CSDemoData : ICSDemoData
{
    private readonly IMariaDbDataAccess _database;

    public CSDemoData(IMariaDbDataAccess database)
    {
        _database = database;
    }

    /// <summary>
    /// User scope is always passed through to the procedure. A cached lookup from one account
    /// must never expose another user's search history or a source link they previously loaded.
    /// </summary>
    public Task<List<CSDemoDbModel>> GetDemos(Guid userId, string steam64Id, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP(
            "sp_cs_demo_catalog_get",
            ReadModel,
            Parameters(("p_user_id", userId), ("p_steam64_id", steam64Id)),
            cancellationToken);
    }

    /// <summary>
    /// The entire external response is written in one stored-procedure call. This avoids a
    /// database round trip per match while still marking old, expired source links unavailable.
    /// </summary>
    public Task SyncDemoCatalogue(
        Guid userId,
        string steam64Id,
        IReadOnlyList<CSDemoDbModel> demos,
        CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_cs_demo_catalog_refresh",
            Parameters(
                ("p_user_id", userId),
                ("p_steam64_id", steam64Id),
                ("p_demos", JsonSerializer.Serialize(demos.Select(demo => new
                {
                    DemoId = demo.DemoId,
                    demo.LeetifyMatchId,
                    demo.MapName,
                    demo.GameType,
                    demo.TeamScore,
                    demo.OpponentScore,
                    demo.IsWin,
                    demo.ReplayUrl,
                    demo.IsAvailable,
                    // MariaDB's JSON table datetime conversion is most reliable with this
                    // culture-invariant database representation rather than an ISO offset.
                    PlayedAtUtc = demo.PlayedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")
                })))),
            cancellationToken);
    }

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values)
    {
        return values.Select(value => new MySqlParameter(
            value.Name,
            value.Value is Guid id ? id.ToString("D") : value.Value)).ToArray();
    }

    // This is the permitted low-level materialiser for MariaDB rows; API mapping happens in Funcs.
    private static CSDemoDbModel ReadModel(MySqlDataReader reader)
    {
        return new CSDemoDbModel
        {
            DemoId = reader.GetGuid("DemoId"),
            Steam64Id = reader.GetString("Steam64Id"),
            LeetifyMatchId = reader.GetString("LeetifyMatchId"),
            MapName = reader.GetString("MapName"),
            GameType = reader.GetString("GameType"),
            TeamScore = reader.GetInt32("TeamScore"),
            OpponentScore = reader.GetInt32("OpponentScore"),
            IsWin = reader.GetBoolean("IsWin"),
            ReplayUrl = reader.IsDBNull(reader.GetOrdinal("ReplayUrl")) ? string.Empty : reader.GetString("ReplayUrl"),
            IsAvailable = reader.GetBoolean("IsAvailable"),
            PlayedAtUtc = reader.GetDateTime("PlayedAtUtc"),
            RefreshedUtc = reader.GetDateTime("RefreshedUtc")
        };
    }
}
