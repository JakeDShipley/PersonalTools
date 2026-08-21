namespace PersonalTools.Entities.Dashboard;

public sealed class DashboardWeatherLocation
{
    public Guid WeatherLocationId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public DateTime CreatedUtc { get; init; }
}
