using MySqlConnector;
using PersonalTools.Entities.CaseOpening;

namespace PersonalTools.Data.CaseOpening;

public interface ICaseOpeningData
{
    Task<List<CaseOpeningHistoryDbModel>> GetCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default);
    Task SaveCaseOpening(Guid userId, CaseOpeningHistoryDbModel opening, CancellationToken cancellationToken = default);
    Task ClearCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningStatisticsDbModel> GetCaseOpeningStatistics(Guid userId, string caseKey, string targetRarityKey, CancellationToken cancellationToken = default);
}

public sealed class CaseOpeningData : ICaseOpeningData
{
    private readonly IMariaDbDataAccess _database;

    public CaseOpeningData(IMariaDbDataAccess database)
    {
        _database = database;
    }

    public Task<List<CaseOpeningHistoryDbModel>> GetCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP(
            "sp_case_opening_history_get",
            ReadHistory,
            Parameters(("p_user_id", userId)),
            cancellationToken);
    }

    public async Task SaveCaseOpening(Guid userId, CaseOpeningHistoryDbModel opening, CancellationToken cancellationToken = default)
    {
        await _database.ExecuteSP(
            "sp_case_opening_history_create",
            Parameters(
                ("p_user_id", userId),
                ("p_opening_id", opening.OpeningId),
                ("p_case_key", opening.CaseKey),
                ("p_source_item_id", opening.SourceItemId),
                ("p_item_name", opening.Name),
                ("p_market_hash_name", opening.MarketHashName),
                ("p_image_url", opening.ImageUrl),
                ("p_description", opening.Description),
                ("p_weapon_name", opening.WeaponName),
                ("p_pattern_name", opening.PatternName),
                ("p_paint_index", opening.PaintIndex),
                ("p_phase", opening.Phase),
                ("p_rarity_key", opening.RarityKey),
                ("p_rarity_name", opening.RarityName),
                ("p_rarity_color", opening.RarityColor),
                ("p_wear", opening.Wear),
                ("p_is_stat_trak", opening.IsStatTrak),
                ("p_is_rare_special", opening.IsRareSpecial),
                ("p_supports_stat_trak", opening.SupportsStatTrak),
                ("p_min_float", opening.MinFloat ?? (object)DBNull.Value),
                ("p_max_float", opening.MaxFloat ?? (object)DBNull.Value),
                ("p_float_value", opening.FloatValue ?? (object)DBNull.Value),
                ("p_pattern_seed", opening.PatternSeed ?? (object)DBNull.Value),
                ("p_estimated_price", opening.EstimatedPrice ?? (object)DBNull.Value)),
            cancellationToken);
    }

    public async Task ClearCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default)
    {
        await _database.ExecuteSP(
            "sp_case_opening_history_clear",
            Parameters(("p_user_id", userId)),
            cancellationToken);
    }

    public async Task<CaseOpeningStatisticsDbModel> GetCaseOpeningStatistics(
        Guid userId,
        string caseKey,
        string targetRarityKey,
        CancellationToken cancellationToken = default)
    {
        return await _database.GetDataSP(
            "sp_case_opening_statistics_get",
            ReadStatistics,
            Parameters(("p_user_id", userId), ("p_case_key", caseKey), ("p_target_rarity_key", targetRarityKey)),
            cancellationToken) ?? new CaseOpeningStatisticsDbModel();
    }

    private static MySqlParameter[] Parameters(params (string Name, object Value)[] values)
    {
        return values.Select(value => new MySqlParameter(value.Name, value.Value)).ToArray();
    }

    private static CaseOpeningHistoryDbModel ReadHistory(MySqlDataReader reader)
    {
        return new CaseOpeningHistoryDbModel
        {
            OpeningId = reader.GetGuid("OpeningId"),
            UserId = reader.GetGuid("UserId"),
            CaseKey = reader.GetString("CaseKey"),
            SourceItemId = reader.GetString("SourceItemId"),
            Name = reader.GetString("ItemName"),
            MarketHashName = reader.GetString("MarketHashName"),
            ImageUrl = reader.GetString("ImageUrl"),
            Description = reader.GetString("Description"),
            WeaponName = reader.GetString("WeaponName"),
            PatternName = reader.GetString("PatternName"),
            PaintIndex = reader.GetString("PaintIndex"),
            Phase = reader.GetString("Phase"),
            RarityKey = reader.GetString("RarityKey"),
            RarityName = reader.GetString("RarityName"),
            RarityColor = reader.GetString("RarityColor"),
            Wear = reader.GetString("Wear"),
            IsStatTrak = reader.GetBoolean("IsStatTrak"),
            IsRareSpecial = reader.GetBoolean("IsRareSpecial"),
            SupportsStatTrak = reader.GetBoolean("SupportsStatTrak"),
            MinFloat = NullableDecimal(reader, "MinFloat"),
            MaxFloat = NullableDecimal(reader, "MaxFloat"),
            FloatValue = NullableDecimal(reader, "FloatValue"),
            PatternSeed = reader.IsDBNull(reader.GetOrdinal("PatternSeed")) ? null : reader.GetInt32("PatternSeed"),
            EstimatedPrice = reader.IsDBNull(reader.GetOrdinal("EstimatedPrice")) ? null : reader.GetDecimal("EstimatedPrice"),
            // MariaDB DATETIME values have no timezone marker. The column is UTC by contract, so
            // restore that information before JSON serialisation rather than letting browsers read it as local time.
            OpenedUtc = DateTime.SpecifyKind(reader.GetDateTime("OpenedUtc"), DateTimeKind.Utc)
        };
    }

    private static decimal? NullableDecimal(MySqlDataReader reader, string columnName)
    {
        return reader.IsDBNull(reader.GetOrdinal(columnName)) ? null : reader.GetDecimal(columnName);
    }

    private static CaseOpeningStatisticsDbModel ReadStatistics(MySqlDataReader reader)
    {
        return new CaseOpeningStatisticsDbModel
        {
            TotalOpenings = reader.GetInt64("TotalOpenings"),
            TargetPulls = reader.GetInt64("TargetPulls"),
            CurrentDryStreak = reader.GetInt64("CurrentDryStreak"),
            LastTargetOpenedUtc = reader.IsDBNull(reader.GetOrdinal("LastTargetOpenedUtc"))
                ? null
                : DateTime.SpecifyKind(reader.GetDateTime("LastTargetOpenedUtc"), DateTimeKind.Utc)
        };
    }
}
