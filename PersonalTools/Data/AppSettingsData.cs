using MySqlConnector;
using PersonalTools.Entities;

namespace PersonalTools.Data;

public interface IAppSettingsData
{
    Task<Dictionary<AppSettingKey, string>> Get(Guid userId, CancellationToken cancellationToken = default);
    Task Set(Guid userId, AppSettingKey key, string value, CancellationToken cancellationToken = default);
}

public sealed class AppSettingsData : IAppSettingsData
{
    private readonly IMariaDbDataAccess _database;
    public AppSettingsData(IMariaDbDataAccess database) => _database = database;
    public async Task<Dictionary<AppSettingKey, string>> Get(Guid userId, CancellationToken cancellationToken = default) => (await _database.GetBulkDataSP("sp_app_settings_get", reader => (Enum.Parse<AppSettingKey>(reader.GetString("SettingKey")), reader.GetString("SettingValue")), Parameters(("p_user_id", userId.ToString("D"))), cancellationToken)).ToDictionary(item => item.Item1, item => item.Item2);
    public Task Set(Guid userId, AppSettingKey key, string value, CancellationToken cancellationToken = default) => _database.ExecuteSP("sp_app_settings_set", Parameters(("p_user_id", userId.ToString("D")), ("p_setting_key", key.ToString()), ("p_setting_value", value)), cancellationToken);
    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values) => values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();
}
