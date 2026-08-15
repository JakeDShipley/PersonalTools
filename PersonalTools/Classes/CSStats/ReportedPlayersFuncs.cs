using PersonalTools.Data.CSStats;

namespace PersonalTools.Classes.CSStats;

public interface IReportedPlayersFuncs
{
    Task<int> GetReportCount(string steam64Id, CancellationToken cancellationToken = default);
    Task<bool> ReportPlayer(Guid userId, string steam64Id, CancellationToken cancellationToken = default);
}

public sealed class ReportedPlayersFuncs : IReportedPlayersFuncs
{
    private readonly IReportedPlayersData _data;
    public ReportedPlayersFuncs(IReportedPlayersData data) => _data = data;

    public Task<int> GetReportCount(string steam64Id, CancellationToken cancellationToken = default) => _data.GetReportCount(ValidateSteamId(steam64Id), cancellationToken);

    public Task<bool> ReportPlayer(Guid userId, string steam64Id, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new InvalidOperationException("Your session could not be verified.");
        return _data.CreateReport(userId, ValidateSteamId(steam64Id), cancellationToken);
    }

    private static string ValidateSteamId(string steam64Id)
    {
        string value = steam64Id?.Trim() ?? string.Empty;
        if (value.Length != 17 || !value.All(char.IsDigit)) throw new InvalidOperationException("The Steam identifier was invalid.");
        return value;
    }
}
