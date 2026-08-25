-- Feature 3: auto trade-up recipes (specific-skin only - the case+rarity variant was considered
-- and dropped before implementation). New tables/columns, recipe CRUD procs, and the two new
-- upgrade progressions (recipe slots, holding capacity). The held-row-exclusion changes to
-- sp_case_opening_history_get / sp_case_opening_inventory_capacity_get / sp_case_opening_trade_up_execute
-- live in a separate migration (2026-08-25-case-opening-auto-trade-up-inventory-exclusion.sql) so a
-- regression there is easy to isolate from this recipe CRUD work.

CREATE TABLE IF NOT EXISTS CaseOpeningTradeUpRecipes (
    RecipeId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    TargetCaseKey VARCHAR(80) NOT NULL,
    TargetSourceItemId VARCHAR(160) NOT NULL,
    TargetItemName VARCHAR(255) NOT NULL,
    TargetMarketHashName VARCHAR(300) NOT NULL,
    TargetImageUrl VARCHAR(2048) NOT NULL,
    TargetRarityKey VARCHAR(30) NOT NULL,
    TargetRarityName VARCHAR(80) NOT NULL,
    TargetRarityColor CHAR(7) NOT NULL,
    -- The rarity all ten inputs must share - one rung below TargetRarityKey on the trade-up ladder.
    -- Stored rather than recomputed so recipe matching never needs the ladder in SQL.
    TargetInputRarityKey VARCHAR(30) NOT NULL,
    TargetStatTrak TINYINT(1) NOT NULL DEFAULT 0,
    -- Empty array means "any wear counts as a match" - the roll itself is unaffected either way.
    TargetWears JSON NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedUtc DATETIME(6) NOT NULL,
    UpdatedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (RecipeId),
    KEY IX_CaseOpeningTradeUpRecipes_User (UserId, IsActive),
    CONSTRAINT FK_CaseOpeningTradeUpRecipes_Users FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
) COLLATE='utf8mb4_unicode_ci' ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CaseOpeningTradeUpRecipeHoldings (
    HoldingId CHAR(36) NOT NULL,
    RecipeId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    -- A held item is still a normal CaseOpeningHistory row (reusing all existing item-rendering
    -- code) - it is just hidden from inventory by a join until this row is deleted (collected).
    OpeningId CHAR(36) NOT NULL,
    IsMatch TINYINT(1) NOT NULL,
    CreatedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (HoldingId),
    UNIQUE KEY UX_CaseOpeningTradeUpRecipeHoldings_Opening (OpeningId),
    KEY IX_CaseOpeningTradeUpRecipeHoldings_Recipe (RecipeId),
    KEY IX_CaseOpeningTradeUpRecipeHoldings_User (UserId),
    CONSTRAINT FK_CaseOpeningTradeUpRecipeHoldings_Recipes FOREIGN KEY (RecipeId) REFERENCES CaseOpeningTradeUpRecipes (RecipeId) ON DELETE CASCADE,
    CONSTRAINT FK_CaseOpeningTradeUpRecipeHoldings_Users FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE,
    CONSTRAINT FK_CaseOpeningTradeUpRecipeHoldings_History FOREIGN KEY (OpeningId) REFERENCES CaseOpeningHistory (OpeningId) ON DELETE CASCADE
) COLLATE='utf8mb4_unicode_ci' ENGINE=InnoDB;

ALTER TABLE CaseOpeningUserInventoryUpgrades
    ADD COLUMN IF NOT EXISTS TradeUpRecipeSlots INT NOT NULL DEFAULT 5 AFTER AutoBuyRuleSlots,
    ADD COLUMN IF NOT EXISTS TradeUpRecipeHoldingCapacity INT NOT NULL DEFAULT 1 AFTER TradeUpRecipeSlots;

ALTER TABLE CaseOpeningGameSettings
    ADD COLUMN IF NOT EXISTS TradeUpRecipeCostStars INT NOT NULL DEFAULT 750 AFTER MaximumStorageContainers;

INSERT INTO CaseOpeningUpgradeDefinitions (UpgradeKey,Name,Description,Category,CostStars,RequiredLevel,SortOrder,IsActive) VALUES
('trade-up-slots-10','Recipe slots: 10','Raise your active auto trade-up recipe limit to 10.','trade-up-slots',1500,6,400,1),
('trade-up-slots-15','Recipe slots: 15','Raise your active auto trade-up recipe limit to 15.','trade-up-slots',3000,10,410,1),
('trade-up-slots-20','Recipe slots: 20','Raise your active auto trade-up recipe limit to 20.','trade-up-slots',5000,14,420,1),
('trade-up-holding-5','Holding capacity: 5','Raise how many finished auto trade-up skins you can hold at once to 5.','trade-up-holding',1200,6,430,1),
('trade-up-holding-10','Holding capacity: 10','Raise how many finished auto trade-up skins you can hold at once to 10.','trade-up-holding',2500,10,440,1),
('trade-up-holding-20','Holding capacity: 20','Raise how many finished auto trade-up skins you can hold at once to 20.','trade-up-holding',4500,14,450,1)
ON DUPLICATE KEY UPDATE Name=VALUES(Name),Description=VALUES(Description),Category=VALUES(Category),CostStars=VALUES(CostStars),RequiredLevel=VALUES(RequiredLevel),SortOrder=VALUES(SortOrder),IsActive=VALUES(IsActive);

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_game_settings_get//
CREATE PROCEDURE sp_case_opening_game_settings_get()
BEGIN
    SELECT XpPerCaseOpen,SkipAnimationCostStars,SkipAnimationXpRequirement,MultiOpenCostStars,MultiOpenXpRequirement,
        OpenSpeedUpgradeBaseCostStars,OpenSpeedUpgradeCostIncrementStars,OpenSpeedUpgradeXpRequirement,MaximumOpenSpeedLevel,
        MaximumMultiOpenLevel,MaximumOpenQuantity,BotOpeningIntervalSeconds,BotServerBaseCostStars,BotServerCostIncrementStars,
        BotBaseCostStars,BotCostGrowthRate,StorageContainerBaseCostStars,StorageContainerCostIncrementStars,StorageContainerSlots,
        MaximumStorageContainers,TradeUpRecipeCostStars
    FROM CaseOpeningGameSettings WHERE Id=1;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_game_settings_set//
CREATE PROCEDURE sp_case_opening_game_settings_set(
    IN p_xp_per_case_open INT,IN p_skip_animation_cost_stars INT,IN p_skip_animation_xp_requirement INT,
    IN p_multi_open_cost_stars INT,IN p_multi_open_xp_requirement INT,IN p_open_speed_upgrade_base_cost_stars INT,
    IN p_open_speed_upgrade_cost_increment_stars INT,IN p_open_speed_upgrade_xp_requirement INT,
    IN p_maximum_open_speed_level TINYINT UNSIGNED,IN p_maximum_multi_open_level TINYINT UNSIGNED,
    IN p_maximum_open_quantity TINYINT UNSIGNED,IN p_bot_opening_interval_seconds INT,IN p_bot_server_base_cost_stars INT,
    IN p_bot_server_cost_increment_stars INT,IN p_bot_base_cost_stars INT,IN p_bot_cost_growth_rate DECIMAL(5,3),
    IN p_storage_container_base_cost_stars INT,IN p_storage_container_cost_increment_stars INT,
    IN p_storage_container_slots INT,IN p_maximum_storage_containers INT,IN p_trade_up_recipe_cost_stars INT)
BEGIN
    UPDATE CaseOpeningGameSettings SET
        XpPerCaseOpen=p_xp_per_case_open,SkipAnimationCostStars=p_skip_animation_cost_stars,
        SkipAnimationXpRequirement=p_skip_animation_xp_requirement,MultiOpenCostStars=p_multi_open_cost_stars,
        MultiOpenXpRequirement=p_multi_open_xp_requirement,OpenSpeedUpgradeBaseCostStars=p_open_speed_upgrade_base_cost_stars,
        OpenSpeedUpgradeCostIncrementStars=p_open_speed_upgrade_cost_increment_stars,
        OpenSpeedUpgradeXpRequirement=p_open_speed_upgrade_xp_requirement,MaximumOpenSpeedLevel=p_maximum_open_speed_level,
        MaximumMultiOpenLevel=p_maximum_multi_open_level,MaximumOpenQuantity=p_maximum_open_quantity,
        BotOpeningIntervalSeconds=p_bot_opening_interval_seconds,BotServerBaseCostStars=p_bot_server_base_cost_stars,
        BotServerCostIncrementStars=p_bot_server_cost_increment_stars,BotBaseCostStars=p_bot_base_cost_stars,
        BotCostGrowthRate=p_bot_cost_growth_rate,StorageContainerBaseCostStars=p_storage_container_base_cost_stars,
        StorageContainerCostIncrementStars=p_storage_container_cost_increment_stars,
        StorageContainerSlots=p_storage_container_slots,MaximumStorageContainers=p_maximum_storage_containers,
        TradeUpRecipeCostStars=p_trade_up_recipe_cost_stars,UpdatedUtc=UTC_TIMESTAMP()
    WHERE Id=1;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrade_definitions_get//
CREATE PROCEDURE sp_case_opening_upgrade_definitions_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades(UserId,UpdatedUtc) VALUES(p_user_id,UTC_TIMESTAMP(6));
    SELECT d.UpgradeKey,d.Name,d.Description,d.Category,d.CostStars,d.RequiredLevel,d.SortOrder,
        CASE d.UpgradeKey
            WHEN 'bulk-sell-200' THEN u.BulkSellLimit>=200
            WHEN 'bulk-sell-300' THEN u.BulkSellLimit>=300
            WHEN 'bulk-sell-400' THEN u.BulkSellLimit>=400
            WHEN 'bulk-sell-500' THEN u.BulkSellLimit>=500
            WHEN 'auto-sell-covert' THEN u.AutoSellCovertUnlocked
            WHEN 'auto-sell-classified' THEN u.AutoSellClassifiedUnlocked
            WHEN 'auto-sell-restricted' THEN u.AutoSellRestrictedUnlocked
            WHEN 'auto-sell-mil-spec' THEN u.AutoSellMilSpecUnlocked
            WHEN 'inventory-slots-250' THEN u.BonusInventorySlots>=250
            WHEN 'inventory-slots-500' THEN u.BonusInventorySlots>=750
            WHEN 'inventory-slots-1000' THEN u.BonusInventorySlots>=1750
            WHEN 'auto-buy-unlock' THEN u.AutoBuyUnlocked
            WHEN 'auto-buy-slots-5' THEN u.AutoBuyRuleSlots>=5
            WHEN 'auto-buy-slots-10' THEN u.AutoBuyRuleSlots>=10
            WHEN 'trade-up-slots-10' THEN u.TradeUpRecipeSlots>=10
            WHEN 'trade-up-slots-15' THEN u.TradeUpRecipeSlots>=15
            WHEN 'trade-up-slots-20' THEN u.TradeUpRecipeSlots>=20
            WHEN 'trade-up-holding-5' THEN u.TradeUpRecipeHoldingCapacity>=5
            WHEN 'trade-up-holding-10' THEN u.TradeUpRecipeHoldingCapacity>=10
            WHEN 'trade-up-holding-20' THEN u.TradeUpRecipeHoldingCapacity>=20
            ELSE 0
        END IsUnlocked
    FROM CaseOpeningUpgradeDefinitions d
    CROSS JOIN CaseOpeningUserInventoryUpgrades u
    WHERE u.UserId=p_user_id AND d.IsActive=1
    ORDER BY d.SortOrder;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_inventory_upgrade_unlock//
CREATE PROCEDURE sp_case_opening_inventory_upgrade_unlock(IN p_user_id CHAR(36),IN p_upgrade_key VARCHAR(50),IN p_cost INT)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;
    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades(UserId,UpdatedUtc) VALUES(p_user_id,UTC_TIMESTAMP(6));
    UPDATE CaseOpeningProgress SET Stars=Stars-p_cost,UpdatedUtc=UTC_TIMESTAMP() WHERE UserId=p_user_id AND Stars>=p_cost;
    IF ROW_COUNT()=0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='There are not enough Stars to purchase this upgrade.';
    END IF;
    UPDATE CaseOpeningUserInventoryUpgrades SET
        BulkSellLimit=CASE p_upgrade_key
            WHEN 'bulk-sell-200' THEN GREATEST(BulkSellLimit,200) WHEN 'bulk-sell-300' THEN GREATEST(BulkSellLimit,300)
            WHEN 'bulk-sell-400' THEN GREATEST(BulkSellLimit,400) WHEN 'bulk-sell-500' THEN GREATEST(BulkSellLimit,500)
            ELSE BulkSellLimit END,
        BonusInventorySlots=CASE p_upgrade_key
            WHEN 'inventory-slots-250' THEN GREATEST(BonusInventorySlots,250) WHEN 'inventory-slots-500' THEN GREATEST(BonusInventorySlots,750)
            WHEN 'inventory-slots-1000' THEN GREATEST(BonusInventorySlots,1750) ELSE BonusInventorySlots END,
        AutoSellCovertUnlocked=IF(p_upgrade_key='auto-sell-covert',1,AutoSellCovertUnlocked),
        AutoSellClassifiedUnlocked=IF(p_upgrade_key='auto-sell-classified',1,AutoSellClassifiedUnlocked),
        AutoSellRestrictedUnlocked=IF(p_upgrade_key='auto-sell-restricted',1,AutoSellRestrictedUnlocked),
        AutoSellMilSpecUnlocked=IF(p_upgrade_key='auto-sell-mil-spec',1,AutoSellMilSpecUnlocked),
        AutoBuyUnlocked=IF(p_upgrade_key='auto-buy-unlock',1,AutoBuyUnlocked),
        AutoBuyRuleSlots=CASE p_upgrade_key
            WHEN 'auto-buy-slots-5' THEN GREATEST(AutoBuyRuleSlots,5) WHEN 'auto-buy-slots-10' THEN GREATEST(AutoBuyRuleSlots,10)
            ELSE AutoBuyRuleSlots END,
        TradeUpRecipeSlots=CASE p_upgrade_key
            WHEN 'trade-up-slots-10' THEN GREATEST(TradeUpRecipeSlots,10) WHEN 'trade-up-slots-15' THEN GREATEST(TradeUpRecipeSlots,15)
            WHEN 'trade-up-slots-20' THEN GREATEST(TradeUpRecipeSlots,20) ELSE TradeUpRecipeSlots END,
        TradeUpRecipeHoldingCapacity=CASE p_upgrade_key
            WHEN 'trade-up-holding-5' THEN GREATEST(TradeUpRecipeHoldingCapacity,5) WHEN 'trade-up-holding-10' THEN GREATEST(TradeUpRecipeHoldingCapacity,10)
            WHEN 'trade-up-holding-20' THEN GREATEST(TradeUpRecipeHoldingCapacity,20) ELSE TradeUpRecipeHoldingCapacity END,
        UpdatedUtc=UTC_TIMESTAMP(6)
    WHERE UserId=p_user_id;
    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_recipes_get//
CREATE PROCEDURE sp_case_opening_trade_up_recipes_get(IN p_user_id CHAR(36))
BEGIN
    SELECT r.RecipeId,r.TargetCaseKey,r.TargetSourceItemId,r.TargetItemName,r.TargetMarketHashName,r.TargetImageUrl,
        r.TargetRarityKey,r.TargetRarityName,r.TargetRarityColor,r.TargetInputRarityKey,r.TargetStatTrak,r.TargetWears,
        r.IsActive,r.CreatedUtc,r.UpdatedUtc,
        (SELECT COUNT(*) FROM CaseOpeningTradeUpRecipeHoldings h WHERE h.RecipeId=r.RecipeId) AS HeldCount,
        (SELECT COUNT(*) FROM CaseOpeningHistory hist
            LEFT JOIN CaseOpeningTradeUpRecipeHoldings h2 ON h2.OpeningId=hist.OpeningId
            WHERE hist.UserId=p_user_id AND hist.CaseKey=r.TargetCaseKey AND hist.RarityKey=r.TargetInputRarityKey
                AND hist.IsRareSpecial=0 AND hist.IsStatTrak=r.TargetStatTrak AND h2.HoldingId IS NULL) AS EligibleInputCount
    FROM CaseOpeningTradeUpRecipes r
    WHERE r.UserId=p_user_id
    ORDER BY r.CreatedUtc,r.RecipeId;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_recipe_create//
CREATE PROCEDURE sp_case_opening_trade_up_recipe_create(
    IN p_user_id CHAR(36),IN p_recipe_id CHAR(36),IN p_target_case_key VARCHAR(80),IN p_target_source_item_id VARCHAR(160),
    IN p_target_item_name VARCHAR(255),IN p_target_market_hash_name VARCHAR(300),IN p_target_image_url VARCHAR(2048),
    IN p_target_rarity_key VARCHAR(30),IN p_target_rarity_name VARCHAR(80),IN p_target_rarity_color CHAR(7),
    IN p_target_input_rarity_key VARCHAR(30),IN p_target_stat_trak TINYINT(1),IN p_target_wears JSON,
    IN p_cost INT,IN p_recipe_slot_cap INT UNSIGNED)
BEGIN
    DECLARE v_active_count INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;

    SELECT COUNT(*) INTO v_active_count FROM CaseOpeningTradeUpRecipes WHERE UserId=p_user_id AND IsActive=1 FOR UPDATE;
    IF v_active_count>=p_recipe_slot_cap THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='You have reached your active recipe limit.';
    END IF;

    UPDATE CaseOpeningProgress SET Stars=Stars-p_cost,UpdatedUtc=UTC_TIMESTAMP() WHERE UserId=p_user_id AND Stars>=p_cost;
    IF ROW_COUNT()=0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='There are not enough Stars to create this recipe.';
    END IF;

    INSERT INTO CaseOpeningTradeUpRecipes(RecipeId,UserId,TargetCaseKey,TargetSourceItemId,TargetItemName,TargetMarketHashName,TargetImageUrl,TargetRarityKey,TargetRarityName,TargetRarityColor,TargetInputRarityKey,TargetStatTrak,TargetWears,IsActive,CreatedUtc,UpdatedUtc)
    VALUES(p_recipe_id,p_user_id,p_target_case_key,p_target_source_item_id,p_target_item_name,p_target_market_hash_name,p_target_image_url,p_target_rarity_key,p_target_rarity_name,p_target_rarity_color,p_target_input_rarity_key,p_target_stat_trak,p_target_wears,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_recipe_set_active//
CREATE PROCEDURE sp_case_opening_trade_up_recipe_set_active(IN p_user_id CHAR(36),IN p_recipe_id CHAR(36),IN p_is_active TINYINT(1),IN p_recipe_slot_cap INT UNSIGNED)
BEGIN
    DECLARE v_active_count INT DEFAULT 0;
    DECLARE v_exists INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;

    SELECT COUNT(*) INTO v_exists FROM CaseOpeningTradeUpRecipes WHERE RecipeId=p_recipe_id AND UserId=p_user_id FOR UPDATE;
    IF v_exists=0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='That recipe could not be found.';
    END IF;

    IF p_is_active=1 THEN
        SELECT COUNT(*) INTO v_active_count FROM CaseOpeningTradeUpRecipes WHERE UserId=p_user_id AND IsActive=1 AND RecipeId<>p_recipe_id;
        IF v_active_count>=p_recipe_slot_cap THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='You have reached your active recipe limit.';
        END IF;
    END IF;

    UPDATE CaseOpeningTradeUpRecipes SET IsActive=p_is_active,UpdatedUtc=UTC_TIMESTAMP(6) WHERE RecipeId=p_recipe_id AND UserId=p_user_id;
    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_recipe_delete//
CREATE PROCEDURE sp_case_opening_trade_up_recipe_delete(IN p_user_id CHAR(36),IN p_recipe_id CHAR(36))
BEGIN
    DECLARE v_held_count INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;

    SELECT COUNT(*) INTO v_held_count FROM CaseOpeningTradeUpRecipeHoldings WHERE RecipeId=p_recipe_id AND UserId=p_user_id;
    IF v_held_count>0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Collect this recipe''s held skins before deleting it.';
    END IF;

    DELETE FROM CaseOpeningTradeUpRecipes WHERE RecipeId=p_recipe_id AND UserId=p_user_id;
    IF ROW_COUNT()=0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='That recipe could not be found.';
    END IF;
    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_holdings_get//
CREATE PROCEDURE sp_case_opening_trade_up_holdings_get(IN p_user_id CHAR(36))
BEGIN
    SELECT h.HoldingId,h.RecipeId,h.IsMatch,r.TargetItemName,
        hist.OpeningId,hist.CaseKey,hist.SourceItemId,hist.ItemName,hist.MarketHashName,hist.ImageUrl,hist.Description,
        hist.WeaponName,hist.PatternName,hist.PaintIndex,hist.Phase,hist.RarityKey,hist.RarityName,hist.RarityColor,hist.Wear,
        hist.IsStatTrak,hist.IsRareSpecial,hist.SupportsStatTrak,hist.MinFloat,hist.MaxFloat,hist.FloatValue,hist.PatternSeed,
        hist.EstimatedPrice,hist.OpenedUtc
    FROM CaseOpeningTradeUpRecipeHoldings h
    INNER JOIN CaseOpeningHistory hist ON hist.OpeningId=h.OpeningId
    INNER JOIN CaseOpeningTradeUpRecipes r ON r.RecipeId=h.RecipeId
    WHERE h.UserId=p_user_id
    ORDER BY h.CreatedUtc,h.HoldingId;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_holding_collect//
CREATE PROCEDURE sp_case_opening_trade_up_holding_collect(IN p_user_id CHAR(36),IN p_holding_id CHAR(36))
BEGIN
    DELETE FROM CaseOpeningTradeUpRecipeHoldings WHERE HoldingId=p_holding_id AND UserId=p_user_id;
    IF ROW_COUNT()=0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='That held skin could not be found.';
    END IF;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_reset_dev//
CREATE PROCEDURE sp_case_opening_reset_dev(IN p_user_id CHAR(36))
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;
    DELETE FROM CaseOpeningUserAchievements WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningCompletedRarities WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningCompletedCollections WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningPlayerStats WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningBots WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningBotServers WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningTradeUps WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningAutoBuyRules WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningTradeUpRecipeHoldings WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningTradeUpRecipes WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningUserInventoryUpgrades WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningCollection WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningHistory WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningStorageContainers WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningInventoryCapacity WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningOwnedCases WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningUnlockedCases WHERE UserId = p_user_id;

    INSERT INTO CaseOpeningUnlockedCases(UserId, CaseKey, UnlockedUtc)
    VALUES(p_user_id, 'kilowatt', UTC_TIMESTAMP());

    INSERT INTO CaseOpeningOwnedCases(UserId, CaseKey, Quantity, UpdatedUtc)
    VALUES(p_user_id, 'kilowatt', 25, UTC_TIMESTAMP(6));

    INSERT INTO CaseOpeningInventoryCapacity(UserId, BaseCapacity, UpdatedUtc)
    VALUES(p_user_id, 1000, UTC_TIMESTAMP(6));

    INSERT INTO CaseOpeningProgress(UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, OpenSpeedLevel, UpdatedUtc)
    VALUES(p_user_id, 0, 0, 0, 0, 0, UTC_TIMESTAMP())
    ON DUPLICATE KEY UPDATE
        Stars = 0,
        Xp = 0,
        SkipAnimationUnlocked = 0,
        MultiOpenLevel = 0,
        OpenSpeedLevel = 0,
        UpdatedUtc = UTC_TIMESTAMP();

    COMMIT;
END//

DELIMITER ;
