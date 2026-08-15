using MySqlConnector;

namespace PersonalTools.Data.CSStats;

public interface IReportedPlayersData
{
    Task<int> GetReportCount(string steam64Id, CancellationToken cancellationToken = default);
    Task<bool> CreateReport(Guid userId, string steam64Id, CancellationToken cancellationToken = default);
}

public sealed class ReportedPlayersData : IReportedPlayersData
{
    private readonly IMariaDbDataAccess _database;
    public ReportedPlayersData(IMariaDbDataAccess database) => _database = database;

    public Task<int> GetReportCount(string steam64Id, CancellationToken cancellationToken = default) =>
        _database.GetScalarSP<int>("sp_cs_player_reports_count", Parameters(("p_steam64_id", steam64Id)), cancellationToken);

    public async Task<bool> CreateReport(Guid userId, string steam64Id, CancellationToken cancellationToken = default) =>
        await _database.GetScalarSP<int>("sp_cs_player_reports_create", Parameters(("p_report_id", Guid.NewGuid().ToString("D")), ("p_user_id", userId.ToString("D")), ("p_steam64_id", steam64Id)), cancellationToken) == 1;

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) =>
        values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();
}
