using MySqlConnector;
using System.Text.Json;

namespace PersonalTools.Data;

public interface IDashboardWidgetOrderData
{
    Task<List<string>> GetOrder(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateOrder(Guid userId, IReadOnlyList<string> widgetKeys, CancellationToken cancellationToken = default);
}

public sealed class DashboardWidgetOrderData : IDashboardWidgetOrderData
{
    private readonly IMariaDbDataAccess _database;
    public DashboardWidgetOrderData(IMariaDbDataAccess database) => _database = database;

    public Task<List<string>> GetOrder(Guid userId, CancellationToken cancellationToken = default) =>
        _database.GetBulkDataSP("sp_dashboard_widget_order_get", reader => reader.GetString("WidgetKey"), Parameters(("p_user_id", userId)), cancellationToken);

    public Task UpdateOrder(Guid userId, IReadOnlyList<string> widgetKeys, CancellationToken cancellationToken = default) =>
        _database.ExecuteSP("sp_dashboard_widget_order_set_bulk", Parameters(("p_user_id", userId), ("p_widget_keys", JsonSerializer.Serialize(widgetKeys))), cancellationToken);

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) =>
        values.Select(value => new MySqlParameter(value.Name, value.Value is Guid id ? id.ToString("D") : value.Value)).ToArray();
}
