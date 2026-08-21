using PersonalTools.Data;
using PersonalTools.Entities.Dashboard;
using Mapster;

namespace PersonalTools.Classes.Dashboard;

public interface IDashboardWeatherFuncs
{
    Task<List<DashboardWeatherLocation>> GetLocations(Guid userId, CancellationToken cancellationToken = default);
    Task CreateLocation(Guid userId, string displayName, decimal latitude, decimal longitude, CancellationToken cancellationToken = default);
    Task DeleteLocation(Guid userId, Guid weatherLocationId, CancellationToken cancellationToken = default);
}

public sealed class DashboardWeatherFuncs : IDashboardWeatherFuncs
{
    private const int MaximumLocations = 12;
    private readonly IDashboardWeatherData _data;
    public DashboardWeatherFuncs(IDashboardWeatherData data) => _data = data;

    /// <summary>
    /// Keeps the API contract independent from the MariaDB row shape.
    /// </summary>
    public async Task<List<DashboardWeatherLocation>> GetLocations(Guid userId, CancellationToken cancellationToken = default) =>
        (await _data.GetLocations(userId, cancellationToken)).Adapt<List<DashboardWeatherLocation>>();

    public async Task CreateLocation(Guid userId, string displayName, decimal latitude, decimal longitude, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100 || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new InvalidOperationException("Choose a valid weather location.");
        // Query the Data layer directly here because only the count is needed for the limit;
        // this avoids allocating response models during a write validation path.
        if ((await _data.GetLocations(userId, cancellationToken)).Count >= MaximumLocations)
            throw new InvalidOperationException($"You can save up to {MaximumLocations} weather locations.");

        await _data.CreateLocation(userId, new DashboardWeatherLocation { WeatherLocationId = Guid.NewGuid(), DisplayName = displayName.Trim(), Latitude = latitude, Longitude = longitude }, cancellationToken);
    }

    public Task DeleteLocation(Guid userId, Guid weatherLocationId, CancellationToken cancellationToken = default)
    {
        if (weatherLocationId == Guid.Empty) throw new InvalidOperationException("The weather location was invalid.");
        return _data.DeleteLocation(userId, weatherLocationId, cancellationToken);
    }
}
