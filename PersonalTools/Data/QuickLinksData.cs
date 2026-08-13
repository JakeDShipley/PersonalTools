using MySqlConnector;
using PersonalTools.Entities;

namespace PersonalTools.Data;

public interface IQuickLinksData
{
    Task<List<QuickLink>> GetQuickLinks(long userId);
    Task<long> CreateQuickLink(long userId, string title, string url, string? iconClass);
    Task UpdateQuickLink(long userId, long quickLinkId, string title, string url, string? iconClass);
    Task DeleteQuickLink(long userId, long quickLinkId);
}

public sealed class QuickLinksData : IQuickLinksData
{
    private readonly IMariaDbDataAccess _database;
    public QuickLinksData(IMariaDbDataAccess database) => _database = database;
    public Task<List<QuickLink>> GetQuickLinks(long userId) => _database.GetBulkDataSP("sp_quick_links_get", Map, Parameters(("p_user_id", userId)));
    public Task<long> CreateQuickLink(long userId, string title, string url, string? iconClass) => _database.GetScalarSP<long>("sp_quick_links_create", Parameters(("p_user_id", userId), ("p_title", title), ("p_url", url), ("p_icon_class", iconClass ?? string.Empty)));
    public async Task UpdateQuickLink(long userId, long quickLinkId, string title, string url, string? iconClass) => await _database.ExecuteSP("sp_quick_links_update", Parameters(("p_user_id", userId), ("p_quick_link_id", quickLinkId), ("p_title", title), ("p_url", url), ("p_icon_class", iconClass ?? string.Empty)));
    public async Task DeleteQuickLink(long userId, long quickLinkId) => await _database.ExecuteSP("sp_quick_links_delete", Parameters(("p_user_id", userId), ("p_quick_link_id", quickLinkId)));
    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();
    private static QuickLink Map(MySqlDataReader reader) => new() { QuickLinkId = reader.GetInt64("QuickLinkId"), Title = reader.GetString("Title"), Url = reader.GetString("Url"), IconClass = reader.IsDBNull(reader.GetOrdinal("IconClass")) ? null : reader.GetString("IconClass"), UpdatedUtc = reader.GetDateTime("UpdatedUtc") };
}
