using PersonalTools.Data;

namespace PersonalTools.Classes.Dashboard;

public interface IDashboardWidgetOrderFuncs
{
    Task<List<string>> GetOrder(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateOrder(Guid userId, IReadOnlyList<string> widgetKeys, CancellationToken cancellationToken = default);
}

public sealed class DashboardWidgetOrderFuncs : IDashboardWidgetOrderFuncs
{
    private static readonly HashSet<string> AllowedWidgetKeys = new(StringComparer.Ordinal)
    {
        "weather", "calendar", "recent-notes"
    };

    private readonly IDashboardWidgetOrderData _data;
    public DashboardWidgetOrderFuncs(IDashboardWidgetOrderData data) => _data = data;

    public Task<List<string>> GetOrder(Guid userId, CancellationToken cancellationToken = default) =>
        _data.GetOrder(userId, cancellationToken);

    public Task UpdateOrder(Guid userId, IReadOnlyList<string> widgetKeys, CancellationToken cancellationToken = default)
    {
        List<string> distinctKeys = widgetKeys.Distinct(StringComparer.Ordinal).ToList();
        if (distinctKeys.Count != AllowedWidgetKeys.Count || distinctKeys.Any(key => !AllowedWidgetKeys.Contains(key)))
            throw new InvalidOperationException("The dashboard widget order was invalid.");

        return _data.UpdateOrder(userId, distinctKeys, cancellationToken);
    }
}
