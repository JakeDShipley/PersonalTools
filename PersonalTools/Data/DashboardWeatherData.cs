using MySqlConnector;
using PersonalTools.Entities.Dashboard;

namespace PersonalTools.Data;

public interface IDashboardWeatherData
{
    Task<List<DashboardWeatherLocationDbModel>> GetLocations(Guid userId, CancellationToken cancellationToken = default);
    Task CreateLocation(Guid userId, DashboardWeatherLocation location, CancellationToken cancellationToken = default);
    Task DeleteLocation(Guid userId, Guid weatherLocationId, CancellationToken cancellationToken = default);
}

public sealed class DashboardWeatherData : IDashboardWeatherData
{
    private readonly IMariaDbDataAccess _database;
    public DashboardWeatherData(IMariaDbDataAccess database) => _database = database;

    /// <summary>
    /// Reads the requesting user's saved locations only; weather data itself is fetched in the
    /// browser so this call remains a small, indexed persistence lookup.
    /// </summary>
    public Task<List<DashboardWeatherLocationDbModel>> GetLocations(Guid userId, CancellationToken cancellationToken = default) =>
        _database.GetBulkDataSP("sp_dashboard_weather_locations_get", ReadDbModel, Parameters(("p_user_id", userId)), cancellationToken);

    public Task CreateLocation(Guid userId, DashboardWeatherLocation location, CancellationToken cancellationToken = default) =>
        _database.ExecuteSP("sp_dashboard_weather_locations_create", Parameters(("p_user_id", userId), ("p_weather_location_id", location.WeatherLocationId.ToString("D")), ("p_display_name", location.DisplayName), ("p_latitude", location.Latitude), ("p_longitude", location.Longitude)), cancellationToken);

    public Task DeleteLocation(Guid userId, Guid weatherLocationId, CancellationToken cancellationToken = default) =>
        _database.ExecuteSP("sp_dashboard_weather_locations_delete", Parameters(("p_user_id", userId), ("p_weather_location_id", weatherLocationId.ToString("D"))), cancellationToken);

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value is Guid id ? id.ToString("D") : value.Value)).ToArray();
    /// <summary>
    /// MySqlConnector's typed materialiser for the stored-procedure result. The Funcs layer
    /// is responsible for mapping this database transport model to the public response model.
    /// </summary>
    private static DashboardWeatherLocationDbModel ReadDbModel(MySqlDataReader reader) => new()
    {
        WeatherLocationId = reader.GetGuid("WeatherLocationId"), DisplayName = reader.GetString("DisplayName"), Latitude = reader.GetDecimal("Latitude"), Longitude = reader.GetDecimal("Longitude"), CreatedUtc = reader.GetDateTime("CreatedUtc")
    };
}
