using MySqlConnector;
using PersonalTools.Entities.CaseOpening;
using System.Text.Json;

namespace PersonalTools.Data.CaseOpening;

public interface ICaseOpeningData
{
    Task<List<CaseOpeningHistoryDbModel>> GetCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningCollectionDbModel>> GetCaseOpeningCollection(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressDbModel> GetCaseOpeningProgress(Guid userId, CancellationToken cancellationToken = default);
    Task<List<string>> GetCaseOpeningUnlockedCases(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressDbModel?> UnlockCaseOpeningCase(Guid userId, string caseKey, int cost, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressDbModel?> UnlockCaseOpeningUpgrade(Guid userId, string upgradeKey, int cost, CancellationToken cancellationToken = default);
    Task<CaseOpeningSellResultDbModel?> SellCaseOpeningInventory(Guid userId, List<Guid> openingIds, int starsAwarded, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningBotServerDbModel>> GetCaseOpeningBotServers(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningBotDbModel>> GetCaseOpeningBots(Guid userId, CancellationToken cancellationToken = default);
    Task PurchaseCaseOpeningBotServer(Guid userId, Guid serverId, int cost, CancellationToken cancellationToken = default);
    Task PurchaseCaseOpeningBot(Guid userId, Guid serverId, Guid botId, int cost, CancellationToken cancellationToken = default);
    Task<bool> ClaimCaseOpeningBotCycle(Guid userId, Guid botId, CancellationToken cancellationToken = default);
    Task<bool> CaseOpeningConditionExists(Guid userId, string sourceItemId, decimal floatValue, int patternSeed, CancellationToken cancellationToken = default);
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

    public Task<List<CaseOpeningCollectionDbModel>> GetCaseOpeningCollection(
        Guid userId,
        string caseKey,
        CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP(
            "sp_case_opening_collection_get",
            ReadCollection,
            Parameters(("p_user_id", userId), ("p_case_key", caseKey)),
            cancellationToken);
    }

    public async Task<CaseOpeningProgressDbModel> GetCaseOpeningProgress(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _database.GetDataSP(
            "sp_case_opening_progress_get",
            ReadProgress,
            Parameters(("p_user_id", userId)),
            cancellationToken) ?? new CaseOpeningProgressDbModel { UserId = userId };
    }

    public Task<CaseOpeningProgressDbModel?> UnlockCaseOpeningUpgrade(
        Guid userId,
        string upgradeKey,
        int cost,
        CancellationToken cancellationToken = default)
    {
        return _database.GetDataSP(
            "sp_case_opening_upgrade_unlock",
            ReadProgress,
            Parameters(("p_user_id", userId), ("p_upgrade_key", upgradeKey), ("p_cost", cost)),
            cancellationToken);
    }

    public Task<List<string>> GetCaseOpeningUnlockedCases(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP(
            "sp_case_opening_unlocked_cases_get",
            reader => reader.GetString("CaseKey"),
            Parameters(("p_user_id", userId)),
            cancellationToken);
    }

    public Task<CaseOpeningProgressDbModel?> UnlockCaseOpeningCase(
        Guid userId,
        string caseKey,
        int cost,
        CancellationToken cancellationToken = default)
    {
        return _database.GetDataSP(
            "sp_case_opening_case_unlock",
            ReadProgress,
            Parameters(("p_user_id", userId), ("p_case_key", caseKey), ("p_cost", cost)),
            cancellationToken);
    }

    public Task<CaseOpeningSellResultDbModel?> SellCaseOpeningInventory(
        Guid userId,
        List<Guid> openingIds,
        int starsAwarded,
        CancellationToken cancellationToken = default)
    {
        return _database.GetDataSP(
            "sp_case_opening_inventory_sell",
            ReadSellResult,
            Parameters(
                ("p_user_id", userId),
                ("p_opening_ids", JsonSerializer.Serialize(openingIds)),
                ("p_item_count", openingIds.Count),
                ("p_stars_awarded", starsAwarded)),
            cancellationToken);
    }

    public Task<List<CaseOpeningBotServerDbModel>> GetCaseOpeningBotServers(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP(
            "sp_case_opening_bot_servers_get",
            ReadBotServer,
            Parameters(("p_user_id", userId)),
            cancellationToken);
    }

    public Task<List<CaseOpeningBotDbModel>> GetCaseOpeningBots(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP(
            "sp_case_opening_bots_get",
            ReadBot,
            Parameters(("p_user_id", userId)),
            cancellationToken);
    }

    public Task PurchaseCaseOpeningBotServer(Guid userId, Guid serverId, int cost, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_bot_server_purchase",
            Parameters(("p_user_id", userId), ("p_server_id", serverId), ("p_cost", cost)),
            cancellationToken);
    }

    public Task PurchaseCaseOpeningBot(Guid userId, Guid serverId, Guid botId, int cost, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_bot_purchase",
            Parameters(
                ("p_user_id", userId),
                ("p_server_id", serverId),
                ("p_bot_id", botId),
                ("p_cost", cost)),
            cancellationToken);
    }

    public async Task<bool> ClaimCaseOpeningBotCycle(Guid userId, Guid botId, CancellationToken cancellationToken = default)
    {
        int claimed = await _database.GetScalarSP<int>(
            "sp_case_opening_bot_cycle_claim",
            Parameters(("p_user_id", userId), ("p_bot_id", botId)),
            cancellationToken);

        return claimed == 1;
    }

    /// <summary>
    /// Checks the complete float and pattern pairing for this user and skin. Wear names are broad
    /// bands and can repeat, but an exact simulated item condition should belong to one opening.
    /// </summary>
    public async Task<bool> CaseOpeningConditionExists(
        Guid userId,
        string sourceItemId,
        decimal floatValue,
        int patternSeed,
        CancellationToken cancellationToken = default)
    {
        int count = await _database.GetScalarSP<int>(
            "sp_case_opening_condition_exists",
            Parameters(
                ("p_user_id", userId),
                ("p_source_item_id", sourceItemId),
                ("p_float_value", floatValue),
                ("p_pattern_seed", patternSeed)),
            cancellationToken);

        return count > 0;
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

    private static CaseOpeningProgressDbModel ReadProgress(MySqlDataReader reader)
    {
        return new CaseOpeningProgressDbModel
        {
            UserId = reader.GetGuid("UserId"),
            Stars = reader.GetInt32("Stars"),
            SkipAnimationUnlocked = reader.GetBoolean("SkipAnimationUnlocked"),
            MultiOpenLevel = reader.GetInt32("MultiOpenLevel")
        };
    }

    private static CaseOpeningCollectionDbModel ReadCollection(MySqlDataReader reader)
    {
        return new CaseOpeningCollectionDbModel
        {
            CollectionId = reader.GetGuid("CollectionId"),
            UserId = reader.GetGuid("UserId"),
            CaseKey = reader.GetString("CaseKey"),
            SourceItemId = reader.GetString("SourceItemId"),
            FirstObtainedUtc = DateTime.SpecifyKind(reader.GetDateTime("FirstObtainedUtc"), DateTimeKind.Utc)
        };
    }

    private static CaseOpeningBotServerDbModel ReadBotServer(MySqlDataReader reader)
    {
        return new CaseOpeningBotServerDbModel
        {
            ServerId = reader.GetGuid("ServerId"),
            UserId = reader.GetGuid("UserId"),
            CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime("CreatedUtc"), DateTimeKind.Utc)
        };
    }

    private static CaseOpeningBotDbModel ReadBot(MySqlDataReader reader)
    {
        return new CaseOpeningBotDbModel
        {
            BotId = reader.GetGuid("BotId"),
            ServerId = reader.GetGuid("ServerId"),
            UserId = reader.GetGuid("UserId"),
            CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime("CreatedUtc"), DateTimeKind.Utc),
            LastOpenedUtc = reader.IsDBNull(reader.GetOrdinal("LastOpenedUtc"))
                ? null
                : DateTime.SpecifyKind(reader.GetDateTime("LastOpenedUtc"), DateTimeKind.Utc)
        };
    }

    private static CaseOpeningSellResultDbModel ReadSellResult(MySqlDataReader reader)
    {
        return new CaseOpeningSellResultDbModel
        {
            StarsAwarded = reader.GetInt32("StarsAwarded"),
            StarsBalance = reader.GetInt32("StarsBalance"),
            SoldItemCount = reader.GetInt32("SoldItemCount")
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
