using MySqlConnector;
using PersonalTools.Entities.CaseOpening;
using System.Text.Json;

namespace PersonalTools.Data.CaseOpening;

public interface ICaseOpeningData
{
    Task<List<CaseOpeningHistoryDbModel>> GetCaseOpeningHistory(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningCollectionDbModel>> GetCaseOpeningCollection(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressDbModel> GetCaseOpeningProgress(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningOwnedCaseDbModel>> GetCaseOpeningOwnedCases(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningInventoryCapacityDbModel> GetCaseOpeningInventoryCapacity(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningPlayerStatsDbModel> GetCaseOpeningPlayerStats(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningAchievementDbModel>> GetCaseOpeningAchievements(Guid userId, CancellationToken cancellationToken = default);
    Task EvaluateCaseOpeningAchievements(Guid userId, CancellationToken cancellationToken = default);
    Task RecordCaseOpeningPlayerActivity(Guid userId, int casesOpened, int skinsObtained, int tradeUpsCompleted, int unlocksEarned, CancellationToken cancellationToken = default);
    Task RecordCaseOpeningLogin(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ClaimCaseOpeningLevelReward(Guid userId, int level, int starsAwarded, CancellationToken cancellationToken = default);
    Task<bool> RecordCompletedCaseOpeningCollection(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task<bool> RecordCompletedCaseOpeningRarity(Guid userId, string caseKey, string rarityKey, CancellationToken cancellationToken = default);
    Task<List<string>> GetCaseOpeningUnlockedCases(Guid userId, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressDbModel?> UnlockCaseOpeningCase(Guid userId, string caseKey, int cost, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressDbModel?> UnlockCaseOpeningUpgrade(Guid userId, string upgradeKey, int cost, int maximumMultiOpenLevel, int maximumOpenSpeedLevel, CancellationToken cancellationToken = default);
    Task<CaseOpeningSellResultDbModel?> SellCaseOpeningInventory(Guid userId, List<Guid> openingIds, int starsAwarded, CancellationToken cancellationToken = default);
    Task<CaseOpeningInventoryUpgradeDbModel> GetCaseOpeningInventoryUpgrades(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningUpgradeDefinitionObj>> GetCaseOpeningUpgradeDefinitions(Guid userId, CancellationToken cancellationToken = default);
    Task UnlockCaseOpeningInventoryUpgrade(Guid userId, string upgradeKey, int cost, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningAutoBuyRuleDbModel>> GetCaseOpeningAutoBuyRules(Guid userId, CancellationToken cancellationToken = default);
    Task SetCaseOpeningAutoBuyRule(Guid userId, string caseKey, int thresholdQuantity, int purchaseQuantity, bool isEnabled, int ruleSlotCap, CancellationToken cancellationToken = default);
    Task DeleteCaseOpeningAutoBuyRule(Guid userId, string caseKey, CancellationToken cancellationToken = default);
    Task SetCaseOpeningAutoSellPreference(Guid userId, string rarityKey, bool enabled, bool preserveStatTrak, CancellationToken cancellationToken = default);
    Task ExecuteCaseOpeningTradeUp(
        Guid userId,
        CaseOpeningTradeUpDbModel tradeUp,
        List<Guid> openingIds,
        CaseOpeningHistoryDbModel output,
        Guid? recipeId,
        bool? isMatch,
        CancellationToken cancellationToken = default);
    Task<List<CaseOpeningTradeUpRecipeObj>> GetCaseOpeningTradeUpRecipes(Guid userId, CancellationToken cancellationToken = default);
    Task CreateCaseOpeningTradeUpRecipe(Guid userId, CaseOpeningTradeUpRecipeDbModel recipe, int cost, int recipeSlotCap, CancellationToken cancellationToken = default);
    Task SetCaseOpeningTradeUpRecipeActive(Guid userId, Guid recipeId, bool isActive, int recipeSlotCap, CancellationToken cancellationToken = default);
    Task DeleteCaseOpeningTradeUpRecipe(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningTradeUpHoldingObj>> GetCaseOpeningTradeUpHoldings(Guid userId, CancellationToken cancellationToken = default);
    Task CollectCaseOpeningTradeUpHolding(Guid userId, Guid holdingId, CancellationToken cancellationToken = default);
    Task UpgradeCaseOpeningTradeUpRecipeSlots(Guid userId, int cost, int maximumSlots, CancellationToken cancellationToken = default);
    Task UpgradeCaseOpeningTradeUpRecipeHolding(Guid userId, Guid recipeId, int cost, int maximumCapacity, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningBotServerDbModel>> GetCaseOpeningBotServers(Guid userId, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningBotDbModel>> GetCaseOpeningBots(Guid userId, CancellationToken cancellationToken = default);
    Task PurchaseCaseOpeningBotServer(Guid userId, Guid serverId, int cost, CancellationToken cancellationToken = default);
    Task PurchaseCaseOpeningBot(Guid userId, Guid serverId, Guid botId, int cost, CancellationToken cancellationToken = default);
    Task UpgradeCaseOpeningBotServer(Guid userId, Guid serverId, int cost, int maximumLevel, CancellationToken cancellationToken = default);
    Task<bool> ClaimCaseOpeningBotCycle(Guid userId, Guid botId, CancellationToken cancellationToken = default);
    Task<bool> CaseOpeningConditionExists(Guid userId, string sourceItemId, decimal floatValue, int patternSeed, CancellationToken cancellationToken = default);
    Task<bool> CaseOpeningCollectionItemExists(Guid userId, string caseKey, string sourceItemId, CancellationToken cancellationToken = default);
    Task SaveCaseOpening(Guid userId, CaseOpeningHistoryDbModel opening, CancellationToken cancellationToken = default);
    Task<CaseOpeningStatisticsDbModel> GetCaseOpeningStatistics(Guid userId, string caseKey, string targetRarityKey, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressDbModel?> AddCaseOpeningXp(Guid userId, int xpDelta, CancellationToken cancellationToken = default);
    Task<CaseOpeningGameSettingsObj> GetGameSettings(CancellationToken cancellationToken = default);
    Task SetGameSettings(CaseOpeningGameSettingsObj settings, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningCaseSettingsObj>> GetCaseSettings(CancellationToken cancellationToken = default);
    Task SetCaseSettings(string caseKey, int unlockCostStars, int purchaseCostStars, int xpRequirement, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningXpByRarityObj>> GetXpByRarity(CancellationToken cancellationToken = default);
    Task SetXpByRarity(string rarityKey, int xpAwarded, CancellationToken cancellationToken = default);
    Task<List<CaseOpeningUpgradeDefinitionObj>> GetInventoryUpgradeSettings(CancellationToken cancellationToken = default);
    Task SetInventoryUpgradeSettings(string upgradeKey, int costStars, int requiredLevel, CancellationToken cancellationToken = default);
    Task<CaseOpeningCasePurchaseResultObj?> PurchaseCaseOpeningCases(Guid userId, string caseKey, int quantity, int purchaseCostStars, CancellationToken cancellationToken = default);
    Task<CaseOpeningStoragePurchaseResultObj?> PurchaseCaseOpeningStorageContainer(Guid userId, Guid storageContainerId, int cost, int slots, int maximumContainers, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressDbModel?> SetCaseOpeningProgressDev(Guid userId, int stars, int xp, CancellationToken cancellationToken = default);
    Task<CaseOpeningProgressDbModel?> SetCaseOpeningUpgradesDev(Guid userId, bool skipAnimationUnlocked, int multiOpenLevel, int openSpeedLevel, CancellationToken cancellationToken = default);
    Task SetCaseOpeningCaseUnlockDev(Guid userId, string caseKey, bool unlock, CancellationToken cancellationToken = default);
    Task ResetCaseOpeningProgressDev(Guid userId, CancellationToken cancellationToken = default);
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

    public Task<List<CaseOpeningOwnedCaseDbModel>> GetCaseOpeningOwnedCases(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP(
            "sp_case_opening_owned_cases_get",
            reader => new CaseOpeningOwnedCaseDbModel
            {
                CaseKey = reader.GetString("CaseKey"),
                Quantity = reader.GetInt32("Quantity")
            },
            Parameters(("p_user_id", userId)),
            cancellationToken);
    }

    public async Task<CaseOpeningInventoryCapacityDbModel> GetCaseOpeningInventoryCapacity(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _database.GetDataSP(
            "sp_case_opening_inventory_capacity_get",
            ReadInventoryCapacity,
            Parameters(("p_user_id", userId)),
            cancellationToken) ?? new CaseOpeningInventoryCapacityDbModel
        {
            BaseCapacity = 1000,
            TotalCapacity = 1000,
            AvailableSlots = 1000
        };
    }

    public Task<CaseOpeningProgressDbModel?> UnlockCaseOpeningUpgrade(
        Guid userId,
        string upgradeKey,
        int cost,
        int maximumMultiOpenLevel,
        int maximumOpenSpeedLevel,
        CancellationToken cancellationToken = default)
    {
        return _database.GetDataSP(
            "sp_case_opening_upgrade_unlock",
            ReadProgress,
            Parameters(
                ("p_user_id", userId),
                ("p_upgrade_key", upgradeKey),
                ("p_cost", cost),
                ("p_max_multi_open_level", maximumMultiOpenLevel),
                ("p_max_open_speed_level", maximumOpenSpeedLevel)),
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

    public async Task<CaseOpeningPlayerStatsDbModel> GetCaseOpeningPlayerStats(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _database.GetDataSP(
            "sp_case_opening_player_stats_get",
            ReadPlayerStats,
            Parameters(("p_user_id", userId)),
            cancellationToken) ?? new CaseOpeningPlayerStatsDbModel { UserId = userId };
    }

    public Task<List<CaseOpeningAchievementDbModel>> GetCaseOpeningAchievements(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP(
            "sp_case_opening_achievements_get",
            ReadAchievement,
            Parameters(("p_user_id", userId)),
            cancellationToken);
    }

    public Task EvaluateCaseOpeningAchievements(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_achievements_evaluate",
            Parameters(("p_user_id", userId)),
            cancellationToken);
    }

    public Task RecordCaseOpeningPlayerActivity(
        Guid userId,
        int casesOpened,
        int skinsObtained,
        int tradeUpsCompleted,
        int unlocksEarned,
        CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_player_stats_add",
            Parameters(
                ("p_user_id", userId),
                ("p_cases_opened", casesOpened),
                ("p_skins_obtained", skinsObtained),
                ("p_trade_ups_completed", tradeUpsCompleted),
                ("p_unlocks_earned", unlocksEarned)),
            cancellationToken);
    }

    public Task RecordCaseOpeningLogin(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_login_record",
            Parameters(("p_user_id", userId)),
            cancellationToken);
    }

    public async Task<bool> ClaimCaseOpeningLevelReward(
        Guid userId,
        int level,
        int starsAwarded,
        CancellationToken cancellationToken = default)
    {
        int claimed = await _database.GetScalarSP<int>(
            "sp_case_opening_level_reward_claim",
            Parameters(("p_user_id", userId), ("p_level", level), ("p_stars_awarded", starsAwarded)),
            cancellationToken);

        return claimed == 1;
    }

    public async Task<bool> RecordCompletedCaseOpeningCollection(Guid userId, string caseKey, CancellationToken cancellationToken = default)
    {
        int recorded = await _database.GetScalarSP<int>(
            "sp_case_opening_collection_completion_record",
            Parameters(("p_user_id", userId), ("p_case_key", caseKey)),
            cancellationToken);

        return recorded == 1;
    }

    public async Task<bool> RecordCompletedCaseOpeningRarity(
        Guid userId,
        string caseKey,
        string rarityKey,
        CancellationToken cancellationToken = default)
    {
        int recorded = await _database.GetScalarSP<int>(
            "sp_case_opening_collection_rarity_completion_record",
            Parameters(("p_user_id", userId), ("p_case_key", caseKey), ("p_rarity_key", rarityKey)),
            cancellationToken);

        return recorded == 1;
    }

    /// <summary>
    /// Saves a completed contract as one database transaction. The procedure validates that all
    /// input rows still belong to this user before it removes them and adds the server-selected output.
    /// </summary>
    public Task ExecuteCaseOpeningTradeUp(
        Guid userId,
        CaseOpeningTradeUpDbModel tradeUp,
        List<Guid> openingIds,
        CaseOpeningHistoryDbModel output,
        Guid? recipeId,
        bool? isMatch,
        CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_trade_up_execute",
            Parameters(
                ("p_user_id", userId),
                ("p_trade_up_id", tradeUp.TradeUpId),
                ("p_opening_ids", JsonSerializer.Serialize(openingIds)),
                ("p_input_rarity_key", tradeUp.InputRarityKey),
                ("p_output_rarity_key", tradeUp.OutputRarityKey),
                ("p_output_opening_id", output.OpeningId),
                ("p_output_case_key", output.CaseKey),
                ("p_output_source_item_id", output.SourceItemId),
                ("p_output_item_name", output.Name),
                ("p_output_market_hash_name", output.MarketHashName),
                ("p_output_image_url", output.ImageUrl),
                ("p_output_description", output.Description),
                ("p_output_weapon_name", output.WeaponName),
                ("p_output_pattern_name", output.PatternName),
                ("p_output_paint_index", output.PaintIndex),
                ("p_output_phase", output.Phase),
                ("p_output_rarity_name", output.RarityName),
                ("p_output_rarity_color", output.RarityColor),
                ("p_output_wear", output.Wear),
                ("p_output_is_stat_trak", output.IsStatTrak),
                ("p_output_is_rare_special", output.IsRareSpecial),
                ("p_output_supports_stat_trak", output.SupportsStatTrak),
                ("p_output_min_float", output.MinFloat ?? (object)DBNull.Value),
                ("p_output_max_float", output.MaxFloat ?? (object)DBNull.Value),
                ("p_output_float_value", output.FloatValue ?? (object)DBNull.Value),
                ("p_output_pattern_seed", output.PatternSeed ?? (object)DBNull.Value),
                ("p_output_estimated_price", output.EstimatedPrice ?? (object)DBNull.Value),
                ("p_average_input_float", tradeUp.AverageInputFloat),
                ("p_recipe_id", recipeId.HasValue ? recipeId.Value : (object)DBNull.Value),
                ("p_is_match", isMatch.HasValue ? isMatch.Value : (object)DBNull.Value)),
            cancellationToken);
    }

    public Task<List<CaseOpeningTradeUpRecipeObj>> GetCaseOpeningTradeUpRecipes(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP("sp_case_opening_trade_up_recipes_get", ReadTradeUpRecipe,
            Parameters(("p_user_id", userId)), cancellationToken);
    }

    public Task CreateCaseOpeningTradeUpRecipe(Guid userId, CaseOpeningTradeUpRecipeDbModel recipe, int cost, int recipeSlotCap, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_trade_up_recipe_create",
            Parameters(
                ("p_user_id", userId),
                ("p_recipe_id", recipe.RecipeId),
                ("p_target_case_key", recipe.TargetCaseKey),
                ("p_target_source_item_id", recipe.TargetSourceItemId),
                ("p_target_item_name", recipe.TargetItemName),
                ("p_target_market_hash_name", recipe.TargetMarketHashName),
                ("p_target_image_url", recipe.TargetImageUrl),
                ("p_target_rarity_key", recipe.TargetRarityKey),
                ("p_target_rarity_name", recipe.TargetRarityName),
                ("p_target_rarity_color", recipe.TargetRarityColor),
                ("p_target_input_rarity_key", recipe.TargetInputRarityKey),
                ("p_target_stat_trak", recipe.TargetStatTrak),
                ("p_target_wears", JsonSerializer.Serialize(recipe.TargetWears)),
                ("p_cost", cost),
                ("p_recipe_slot_cap", recipeSlotCap)),
            cancellationToken);
    }

    public Task SetCaseOpeningTradeUpRecipeActive(Guid userId, Guid recipeId, bool isActive, int recipeSlotCap, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_trade_up_recipe_set_active",
            Parameters(("p_user_id", userId), ("p_recipe_id", recipeId), ("p_is_active", isActive), ("p_recipe_slot_cap", recipeSlotCap)),
            cancellationToken);
    }

    public Task DeleteCaseOpeningTradeUpRecipe(Guid userId, Guid recipeId, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_trade_up_recipe_delete",
            Parameters(("p_user_id", userId), ("p_recipe_id", recipeId)),
            cancellationToken);
    }

    public Task<List<CaseOpeningTradeUpHoldingObj>> GetCaseOpeningTradeUpHoldings(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP("sp_case_opening_trade_up_holdings_get", ReadTradeUpHolding,
            Parameters(("p_user_id", userId)), cancellationToken);
    }

    public Task CollectCaseOpeningTradeUpHolding(Guid userId, Guid holdingId, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_trade_up_holding_collect",
            Parameters(("p_user_id", userId), ("p_holding_id", holdingId)),
            cancellationToken);
    }

    public Task UpgradeCaseOpeningTradeUpRecipeSlots(Guid userId, int cost, int maximumSlots, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_trade_up_recipe_slot_upgrade",
            Parameters(("p_user_id", userId), ("p_cost", cost), ("p_maximum_slots", maximumSlots)),
            cancellationToken);
    }

    public Task UpgradeCaseOpeningTradeUpRecipeHolding(Guid userId, Guid recipeId, int cost, int maximumCapacity, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_trade_up_recipe_holding_upgrade",
            Parameters(("p_user_id", userId), ("p_recipe_id", recipeId), ("p_cost", cost), ("p_maximum_capacity", maximumCapacity)),
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

    public async Task<bool> CaseOpeningCollectionItemExists(Guid userId, string caseKey, string sourceItemId, CancellationToken cancellationToken = default)
    {
        int count = await _database.GetScalarSP<int>("sp_case_opening_collection_item_exists",
            Parameters(("p_user_id", userId), ("p_case_key", caseKey), ("p_source_item_id", sourceItemId)), cancellationToken);
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

    public Task<CaseOpeningProgressDbModel?> AddCaseOpeningXp(Guid userId, int xpDelta, CancellationToken cancellationToken = default)
    {
        return _database.GetDataSP(
            "sp_case_opening_xp_add",
            ReadProgress,
            Parameters(("p_user_id", userId), ("p_xp_delta", xpDelta)),
            cancellationToken);
    }

    public async Task<CaseOpeningGameSettingsObj> GetGameSettings(CancellationToken cancellationToken = default)
    {
        return await _database.GetDataSP(
            "sp_case_opening_game_settings_get",
            ReadGameSettings,
            cancellationToken: cancellationToken) ?? new CaseOpeningGameSettingsObj();
    }

    public Task SetGameSettings(CaseOpeningGameSettingsObj settings, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_game_settings_set",
            Parameters(
                ("p_xp_per_case_open", settings.XpPerCaseOpen),
                ("p_skip_animation_cost_stars", settings.SkipAnimationCostStars),
                ("p_skip_animation_xp_requirement", settings.SkipAnimationXpRequirement),
                ("p_multi_open_cost_stars", settings.MultiOpenCostStars),
                ("p_multi_open_xp_requirement", settings.MultiOpenXpRequirement),
                ("p_open_speed_upgrade_base_cost_stars", settings.OpenSpeedUpgradeBaseCostStars),
                ("p_open_speed_upgrade_cost_increment_stars", settings.OpenSpeedUpgradeCostIncrementStars),
                ("p_open_speed_upgrade_xp_requirement", settings.OpenSpeedUpgradeXpRequirement),
                ("p_maximum_open_speed_level", settings.MaximumOpenSpeedLevel),
                ("p_maximum_multi_open_level", settings.MaximumMultiOpenLevel),
                ("p_maximum_open_quantity", settings.MaximumOpenQuantity),
                ("p_bot_opening_interval_seconds", settings.BotOpeningIntervalSeconds),
                ("p_bot_server_base_cost_stars", settings.BotServerBaseCostStars),
                ("p_bot_server_cost_increment_stars", settings.BotServerCostIncrementStars),
                ("p_bot_base_cost_stars", settings.BotBaseCostStars),
                ("p_bot_cost_growth_rate", settings.BotCostGrowthRate),
                ("p_storage_container_base_cost_stars", settings.StorageContainerBaseCostStars),
                ("p_storage_container_cost_increment_stars", settings.StorageContainerCostIncrementStars),
                ("p_storage_container_slots", settings.StorageContainerSlots),
                ("p_maximum_storage_containers", settings.MaximumStorageContainers),
                ("p_trade_up_recipe_cost_stars", settings.TradeUpRecipeCostStars)),
            cancellationToken);
    }

    public Task<List<CaseOpeningCaseSettingsObj>> GetCaseSettings(CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP("sp_case_opening_case_settings_get_all", ReadCaseSettings, cancellationToken: cancellationToken);
    }

    public Task SetCaseSettings(string caseKey, int unlockCostStars, int purchaseCostStars, int xpRequirement, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_case_settings_set",
            Parameters(("p_case_key", caseKey), ("p_unlock_cost_stars", unlockCostStars), ("p_purchase_cost_stars", purchaseCostStars), ("p_xp_requirement", xpRequirement)),
            cancellationToken);
    }

    public Task UpgradeCaseOpeningBotServer(Guid userId, Guid serverId, int cost, int maximumLevel, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP("sp_case_opening_bot_server_speed_upgrade",
            Parameters(("p_user_id", userId), ("p_server_id", serverId), ("p_cost", cost), ("p_maximum_level", maximumLevel)), cancellationToken);
    }

    public async Task<CaseOpeningInventoryUpgradeDbModel> GetCaseOpeningInventoryUpgrades(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _database.GetDataSP("sp_case_opening_inventory_upgrades_get", ReadInventoryUpgrades,
            Parameters(("p_user_id", userId)), cancellationToken) ?? new CaseOpeningInventoryUpgradeDbModel { UserId = userId };
    }

    public Task<List<CaseOpeningUpgradeDefinitionObj>> GetCaseOpeningUpgradeDefinitions(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP("sp_case_opening_upgrade_definitions_get", reader => new CaseOpeningUpgradeDefinitionObj
        {
            UpgradeKey = reader.GetString("UpgradeKey"), Name = reader.GetString("Name"), Description = reader.GetString("Description"),
            Category = reader.GetString("Category"), CostStars = reader.GetInt32("CostStars"), RequiredLevel = reader.GetInt32("RequiredLevel"),
            SortOrder = reader.GetInt32("SortOrder"), IsUnlocked = reader.GetBoolean("IsUnlocked")
        }, Parameters(("p_user_id", userId)), cancellationToken);
    }

    public Task UnlockCaseOpeningInventoryUpgrade(Guid userId, string upgradeKey, int cost, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP("sp_case_opening_inventory_upgrade_unlock",
            Parameters(("p_user_id", userId), ("p_upgrade_key", upgradeKey), ("p_cost", cost)), cancellationToken);
    }

    public Task<List<CaseOpeningAutoBuyRuleDbModel>> GetCaseOpeningAutoBuyRules(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP("sp_case_opening_auto_buy_rules_get", ReadAutoBuyRule,
            Parameters(("p_user_id", userId)), cancellationToken);
    }

    public Task SetCaseOpeningAutoBuyRule(
        Guid userId,
        string caseKey,
        int thresholdQuantity,
        int purchaseQuantity,
        bool isEnabled,
        int ruleSlotCap,
        CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_auto_buy_rule_set",
            Parameters(
                ("p_user_id", userId),
                ("p_case_key", caseKey),
                ("p_threshold_quantity", thresholdQuantity),
                ("p_purchase_quantity", purchaseQuantity),
                ("p_is_enabled", isEnabled),
                ("p_rule_slot_cap", ruleSlotCap)),
            cancellationToken);
    }

    public Task DeleteCaseOpeningAutoBuyRule(Guid userId, string caseKey, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_auto_buy_rule_delete",
            Parameters(("p_user_id", userId), ("p_case_key", caseKey)),
            cancellationToken);
    }

    public Task<List<CaseOpeningUpgradeDefinitionObj>> GetInventoryUpgradeSettings(CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP("sp_case_opening_upgrade_settings_get", ReadUpgradeDefinition, cancellationToken: cancellationToken);
    }

    public Task SetInventoryUpgradeSettings(string upgradeKey, int costStars, int requiredLevel, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP("sp_case_opening_upgrade_settings_set",
            Parameters(("p_upgrade_key", upgradeKey), ("p_cost_stars", costStars), ("p_required_level", requiredLevel)), cancellationToken);
    }

    public Task SetCaseOpeningAutoSellPreference(Guid userId, string rarityKey, bool enabled, bool preserveStatTrak, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP("sp_case_opening_auto_sell_set",
            Parameters(("p_user_id", userId), ("p_rarity_key", rarityKey), ("p_enabled", enabled), ("p_preserve_stat_trak", preserveStatTrak)), cancellationToken);
    }

    public Task<List<CaseOpeningXpByRarityObj>> GetXpByRarity(CancellationToken cancellationToken = default)
    {
        return _database.GetBulkDataSP("sp_case_opening_xp_by_rarity_get_all", ReadXpByRarity, cancellationToken: cancellationToken);
    }

    public Task SetXpByRarity(string rarityKey, int xpAwarded, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_xp_by_rarity_set",
            Parameters(("p_rarity_key", rarityKey), ("p_xp_awarded", xpAwarded)),
            cancellationToken);
    }

    public Task<CaseOpeningCasePurchaseResultObj?> PurchaseCaseOpeningCases(Guid userId, string caseKey, int quantity, int purchaseCostStars, CancellationToken cancellationToken = default)
    {
        return _database.GetDataSP("sp_case_opening_cases_purchase", reader => new CaseOpeningCasePurchaseResultObj
        {
            CaseKey = reader.GetString("CaseKey"), PurchasedQuantity = reader.GetInt32("PurchasedQuantity"), OwnedQuantity = reader.GetInt32("OwnedQuantity"),
            StarsSpent = reader.GetInt32("StarsSpent"), StarsBalance = reader.GetInt32("StarsBalance")
        }, Parameters(("p_user_id", userId), ("p_case_key", caseKey), ("p_quantity", quantity), ("p_purchase_cost_stars", purchaseCostStars)), cancellationToken);
    }

    public Task<CaseOpeningStoragePurchaseResultObj?> PurchaseCaseOpeningStorageContainer(Guid userId, Guid storageContainerId, int cost, int slots, int maximumContainers, CancellationToken cancellationToken = default)
    {
        return _database.GetDataSP("sp_case_opening_storage_container_purchase", reader => new CaseOpeningStoragePurchaseResultObj
        {
            StorageContainerCount = reader.GetInt32("StorageContainerCount"), AddedSlots = reader.GetInt32("AddedSlots"), TotalCapacity = reader.GetInt32("TotalCapacity"),
            StarsSpent = reader.GetInt32("StarsSpent"), StarsBalance = reader.GetInt32("StarsBalance")
        }, Parameters(("p_user_id", userId), ("p_storage_container_id", storageContainerId), ("p_cost", cost), ("p_slots", slots), ("p_maximum_containers", maximumContainers)), cancellationToken);
    }

    public Task<CaseOpeningProgressDbModel?> SetCaseOpeningProgressDev(Guid userId, int stars, int xp, CancellationToken cancellationToken = default)
    {
        return _database.GetDataSP(
            "sp_case_opening_progress_dev_set",
            ReadProgress,
            Parameters(("p_user_id", userId), ("p_stars", stars), ("p_xp", xp)),
            cancellationToken);
    }

    public Task<CaseOpeningProgressDbModel?> SetCaseOpeningUpgradesDev(Guid userId, bool skipAnimationUnlocked, int multiOpenLevel, int openSpeedLevel, CancellationToken cancellationToken = default)
    {
        return _database.GetDataSP(
            "sp_case_opening_upgrades_dev_set",
            ReadProgress,
            Parameters(
                ("p_user_id", userId),
                ("p_skip_animation_unlocked", skipAnimationUnlocked),
                ("p_multi_open_level", multiOpenLevel),
                ("p_open_speed_level", openSpeedLevel)),
            cancellationToken);
    }

    public Task SetCaseOpeningCaseUnlockDev(Guid userId, string caseKey, bool unlock, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_case_unlock_dev_set",
            Parameters(("p_user_id", userId), ("p_case_key", caseKey), ("p_unlock", unlock)),
            cancellationToken);
    }

    public Task ResetCaseOpeningProgressDev(Guid userId, CancellationToken cancellationToken = default)
    {
        return _database.ExecuteSP(
            "sp_case_opening_reset_dev",
            Parameters(("p_user_id", userId)),
            cancellationToken);
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
            Xp = reader.GetInt32("Xp"),
            SkipAnimationUnlocked = reader.GetBoolean("SkipAnimationUnlocked"),
            MultiOpenLevel = reader.GetInt32("MultiOpenLevel"),
            OpenSpeedLevel = reader.GetInt32("OpenSpeedLevel")
        };
    }

    private static CaseOpeningPlayerStatsDbModel ReadPlayerStats(MySqlDataReader reader)
    {
        return new CaseOpeningPlayerStatsDbModel
        {
            UserId = reader.GetGuid("UserId"),
            TotalCasesOpened = reader.GetInt32("TotalCasesOpened"),
            TotalSkinsObtained = reader.GetInt32("TotalSkinsObtained"),
            TotalTradeUpsCompleted = reader.GetInt32("TotalTradeUpsCompleted"),
            TotalUnlocks = reader.GetInt32("TotalUnlocks"),
            TotalLoginDays = reader.GetInt32("TotalLoginDays"),
            CurrentLoginStreak = reader.GetInt32("CurrentLoginStreak"),
            LongestLoginStreak = reader.GetInt32("LongestLoginStreak"),
            CompletedCollections = reader.GetInt32("CompletedCollections"),
            CompletedRaritySets = reader.GetInt32("CompletedRaritySets"),
            HighestRewardedLevel = reader.GetInt32("HighestRewardedLevel"),
            LastLoginUtcDate = reader.IsDBNull(reader.GetOrdinal("LastLoginUtcDate"))
                ? null
                : DateOnly.FromDateTime(reader.GetDateTime("LastLoginUtcDate"))
        };
    }

    private static CaseOpeningAchievementDbModel ReadAchievement(MySqlDataReader reader)
    {
        return new CaseOpeningAchievementDbModel
        {
            AchievementKey = reader.GetString("AchievementKey"),
            Name = reader.GetString("Name"),
            Description = reader.GetString("Description"),
            MetricKey = reader.GetString("MetricKey"),
            TargetValue = reader.GetInt32("TargetValue"),
            RewardStars = reader.GetInt32("RewardStars"),
            SortOrder = reader.GetInt32("SortOrder"),
            IsUnlocked = reader.GetBoolean("IsUnlocked"),
            UnlockedUtc = reader.IsDBNull(reader.GetOrdinal("UnlockedUtc"))
                ? null
                : DateTime.SpecifyKind(reader.GetDateTime("UnlockedUtc"), DateTimeKind.Utc)
        };
    }

    private static CaseOpeningInventoryCapacityDbModel ReadInventoryCapacity(MySqlDataReader reader)
    {
        return new CaseOpeningInventoryCapacityDbModel
        {
            SkinSlots = reader.GetInt32("SkinSlots"),
            CaseSlots = reader.GetInt32("CaseSlots"),
            UsedSlots = reader.GetInt32("UsedSlots"),
            BaseCapacity = reader.GetInt32("BaseCapacity"),
            StorageContainerCount = reader.GetInt32("StorageContainerCount"),
            StorageSlots = reader.GetInt32("StorageSlots"),
            UpgradeSlots = reader.GetInt32("UpgradeSlots"),
            TotalCapacity = reader.GetInt32("TotalCapacity"),
            AvailableSlots = reader.GetInt32("AvailableSlots")
        };
    }

    private static CaseOpeningGameSettingsObj ReadGameSettings(MySqlDataReader reader)
    {
        return new CaseOpeningGameSettingsObj
        {
            XpPerCaseOpen = reader.GetInt32("XpPerCaseOpen"),
            SkipAnimationCostStars = reader.GetInt32("SkipAnimationCostStars"),
            SkipAnimationXpRequirement = reader.GetInt32("SkipAnimationXpRequirement"),
            MultiOpenCostStars = reader.GetInt32("MultiOpenCostStars"),
            MultiOpenXpRequirement = reader.GetInt32("MultiOpenXpRequirement"),
            OpenSpeedUpgradeBaseCostStars = reader.GetInt32("OpenSpeedUpgradeBaseCostStars"),
            OpenSpeedUpgradeCostIncrementStars = reader.GetInt32("OpenSpeedUpgradeCostIncrementStars"),
            OpenSpeedUpgradeXpRequirement = reader.GetInt32("OpenSpeedUpgradeXpRequirement"),
            MaximumOpenSpeedLevel = reader.GetInt32("MaximumOpenSpeedLevel"),
            MaximumMultiOpenLevel = reader.GetInt32("MaximumMultiOpenLevel"),
            MaximumOpenQuantity = reader.GetInt32("MaximumOpenQuantity"),
            BotOpeningIntervalSeconds = reader.GetInt32("BotOpeningIntervalSeconds"),
            BotServerBaseCostStars = reader.GetInt32("BotServerBaseCostStars"),
            BotServerCostIncrementStars = reader.GetInt32("BotServerCostIncrementStars"),
            BotBaseCostStars = reader.GetInt32("BotBaseCostStars"),
            BotCostGrowthRate = reader.GetDecimal("BotCostGrowthRate"),
            StorageContainerBaseCostStars = reader.GetInt32("StorageContainerBaseCostStars"),
            StorageContainerCostIncrementStars = reader.GetInt32("StorageContainerCostIncrementStars"),
            StorageContainerSlots = reader.GetInt32("StorageContainerSlots"),
            MaximumStorageContainers = reader.GetInt32("MaximumStorageContainers"),
            TradeUpRecipeCostStars = reader.GetInt32("TradeUpRecipeCostStars")
        };
    }

    private static CaseOpeningCaseSettingsObj ReadCaseSettings(MySqlDataReader reader)
    {
        return new CaseOpeningCaseSettingsObj
        {
            CaseKey = reader.GetString("CaseKey"),
            UnlockCostStars = reader.GetInt32("UnlockCostStars"),
            PurchaseCostStars = reader.GetInt32("PurchaseCostStars"),
            XpRequirement = reader.GetInt32("XpRequirement")
        };
    }

    private static CaseOpeningUpgradeDefinitionObj ReadUpgradeDefinition(MySqlDataReader reader)
    {
        return new CaseOpeningUpgradeDefinitionObj
        {
            UpgradeKey = reader.GetString("UpgradeKey"),
            Name = reader.GetString("Name"),
            Description = reader.GetString("Description"),
            Category = reader.GetString("Category"),
            CostStars = reader.GetInt32("CostStars"),
            RequiredLevel = reader.GetInt32("RequiredLevel"),
            SortOrder = reader.GetInt32("SortOrder"),
            IsUnlocked = reader.GetBoolean("IsUnlocked")
        };
    }

    private static CaseOpeningXpByRarityObj ReadXpByRarity(MySqlDataReader reader)
    {
        return new CaseOpeningXpByRarityObj
        {
            RarityKey = reader.GetString("RarityKey"),
            XpAwarded = reader.GetInt32("XpAwarded")
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
            CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime("CreatedUtc"), DateTimeKind.Utc),
            SpeedLevel = reader.GetInt32("SpeedLevel")
        };
    }

    private static CaseOpeningInventoryUpgradeDbModel ReadInventoryUpgrades(MySqlDataReader reader)
    {
        return new CaseOpeningInventoryUpgradeDbModel
        {
            UserId = reader.GetGuid("UserId"), BulkSellLimit = reader.GetInt32("BulkSellLimit"),
            BonusInventorySlots = reader.GetInt32("BonusInventorySlots"),
            AutoBuyUnlocked = reader.GetBoolean("AutoBuyUnlocked"), AutoBuyRuleSlots = reader.GetInt32("AutoBuyRuleSlots"),
            TradeUpRecipesUnlocked = reader.GetBoolean("TradeUpRecipesUnlocked"),
            TradeUpRecipeSlots = reader.GetInt32("TradeUpRecipeSlots"),
            AutoSellCovertUnlocked = reader.GetBoolean("AutoSellCovertUnlocked"), AutoSellCovertEnabled = reader.GetBoolean("AutoSellCovertEnabled"),
            AutoSellClassifiedUnlocked = reader.GetBoolean("AutoSellClassifiedUnlocked"), AutoSellClassifiedEnabled = reader.GetBoolean("AutoSellClassifiedEnabled"),
            AutoSellRestrictedUnlocked = reader.GetBoolean("AutoSellRestrictedUnlocked"), AutoSellRestrictedEnabled = reader.GetBoolean("AutoSellRestrictedEnabled"),
            AutoSellMilSpecUnlocked = reader.GetBoolean("AutoSellMilSpecUnlocked"), AutoSellMilSpecEnabled = reader.GetBoolean("AutoSellMilSpecEnabled"),
            PreserveStatTrak = reader.GetBoolean("PreserveStatTrak")
        };
    }

    private static CaseOpeningAutoBuyRuleDbModel ReadAutoBuyRule(MySqlDataReader reader)
    {
        return new CaseOpeningAutoBuyRuleDbModel
        {
            CaseKey = reader.GetString("CaseKey"),
            ThresholdQuantity = reader.GetInt32("ThresholdQuantity"),
            PurchaseQuantity = reader.GetInt32("PurchaseQuantity"),
            IsEnabled = reader.GetBoolean("IsEnabled"),
            CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime("CreatedUtc"), DateTimeKind.Utc),
            UpdatedUtc = DateTime.SpecifyKind(reader.GetDateTime("UpdatedUtc"), DateTimeKind.Utc)
        };
    }

    private static CaseOpeningTradeUpRecipeObj ReadTradeUpRecipe(MySqlDataReader reader)
    {
        return new CaseOpeningTradeUpRecipeObj
        {
            RecipeId = reader.GetGuid("RecipeId"),
            TargetCaseKey = reader.GetString("TargetCaseKey"),
            TargetSourceItemId = reader.GetString("TargetSourceItemId"),
            TargetItemName = reader.GetString("TargetItemName"),
            TargetMarketHashName = reader.GetString("TargetMarketHashName"),
            TargetImageUrl = reader.GetString("TargetImageUrl"),
            TargetRarityKey = reader.GetString("TargetRarityKey"),
            TargetRarityName = reader.GetString("TargetRarityName"),
            TargetRarityColor = reader.GetString("TargetRarityColor"),
            TargetInputRarityKey = reader.GetString("TargetInputRarityKey"),
            TargetStatTrak = reader.GetBoolean("TargetStatTrak"),
            TargetWears = JsonSerializer.Deserialize<List<string>>(reader.GetString("TargetWears")) ?? [],
            IsActive = reader.GetBoolean("IsActive"),
            HoldingCapacity = reader.GetInt32("HoldingCapacity"),
            CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime("CreatedUtc"), DateTimeKind.Utc),
            UpdatedUtc = DateTime.SpecifyKind(reader.GetDateTime("UpdatedUtc"), DateTimeKind.Utc),
            HeldCount = reader.GetInt32("HeldCount"),
            EligibleInputCount = reader.GetInt32("EligibleInputCount")
        };
    }

    private static CaseOpeningTradeUpHoldingObj ReadTradeUpHolding(MySqlDataReader reader)
    {
        return new CaseOpeningTradeUpHoldingObj
        {
            HoldingId = reader.GetGuid("HoldingId"),
            RecipeId = reader.GetGuid("RecipeId"),
            IsMatch = reader.GetBoolean("IsMatch"),
            TargetItemName = reader.GetString("TargetItemName"),
            OpeningId = reader.GetGuid("OpeningId"),
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
            OpenedUtc = DateTime.SpecifyKind(reader.GetDateTime("OpenedUtc"), DateTimeKind.Utc)
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
