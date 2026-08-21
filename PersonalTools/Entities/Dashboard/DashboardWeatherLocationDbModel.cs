namespace PersonalTools.Entities.Dashboard;

/// <summary>
/// MariaDB result shape for a saved dashboard weather location.
/// It is kept separate from the response model to preserve a one-way Data-to-Funcs boundary.
/// </summary>
public sealed class DashboardWeatherLocationDbModel
{
    public Guid WeatherLocationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime CreatedUtc { get; set; }
}
