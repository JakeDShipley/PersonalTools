using MySqlConnector;
using PersonalTools.Entities;
using System.Text.Json;

namespace PersonalTools.Data;

public interface IQuickLinksData
{
    Task<List<QuickLink>> GetQuickLinks(Guid userId);
    Task<Guid> CreateQuickLink(Guid userId, Guid quickLinkId, string title, string url, string? iconClass);
    Task UpdateQuickLink(Guid userId, Guid quickLinkId, string title, string url, string? iconClass);
    Task DeleteQuickLink(Guid userId, Guid quickLinkId);
    Task UpdateOrder(Guid userId, IReadOnlyList<Guid> quickLinkIds, CancellationToken cancellationToken = default);
}

public sealed class QuickLinksData : IQuickLinksData
{
    private readonly IMariaDbDataAccess _database;
    public QuickLinksData(IMariaDbDataAccess database) => _database = database;
    public Task<List<QuickLink>> GetQuickLinks(Guid userId) => _database.GetBulkDataSP("sp_quick_links_get", Map, Parameters(("p_user_id", userId)));
    public async Task<Guid> CreateQuickLink(Guid userId, Guid quickLinkId, string title, string url, string? iconClass) { await _database.ExecuteSP("sp_quick_links_create", Parameters(("p_user_id", userId), ("p_quick_link_id", quickLinkId), ("p_title", title), ("p_url", url), ("p_icon_class", iconClass ?? string.Empty))); return quickLinkId; }
    public async Task UpdateQuickLink(Guid userId, Guid quickLinkId, string title, string url, string? iconClass) => await _database.ExecuteSP("sp_quick_links_update", Parameters(("p_user_id", userId), ("p_quick_link_id", quickLinkId), ("p_title", title), ("p_url", url), ("p_icon_class", iconClass ?? string.Empty)));
    public async Task DeleteQuickLink(Guid userId, Guid quickLinkId) => await _database.ExecuteSP("sp_quick_links_delete", Parameters(("p_user_id", userId), ("p_quick_link_id", quickLinkId)));
    public Task UpdateOrder(Guid userId, IReadOnlyList<Guid> quickLinkIds, CancellationToken cancellationToken = default) =>
        _database.ExecuteSP("sp_quick_links_set_order_bulk", Parameters(("p_user_id", userId), ("p_quick_link_ids", JsonSerializer.Serialize(quickLinkIds))), cancellationToken);
    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value is Guid id ? id.ToString("D") : value.Value)).ToArray();
    private static QuickLink Map(MySqlDataReader reader) => new() { QuickLinkId = reader.GetGuid("QuickLinkId"), Title = reader.GetString("Title"), Url = reader.GetString("Url"), IconClass = reader.IsDBNull(reader.GetOrdinal("IconClass")) ? null : reader.GetString("IconClass"), SortOrder = reader.GetInt32("SortOrder"), UpdatedUtc = reader.GetDateTime("UpdatedUtc") };
}
