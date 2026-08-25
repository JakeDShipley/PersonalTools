-- Redesign: holding capacity is per-recipe, not a shared account-wide pool. Each recipe gets its
-- own HoldingCapacity (default 1, matching the original "hold 1 created skin per recipe" spec) and
-- its own repeatable +1 purchase (sp_case_opening_trade_up_recipe_holding_upgrade, mirroring the
-- recipe-slot upgrade), capped at 20 per recipe. The account-wide TradeUpRecipeHoldingCapacity
-- column and the trade-up-holding-5/10/20 upgrade definitions are removed entirely.

ALTER TABLE CaseOpeningTradeUpRecipes
    ADD COLUMN IF NOT EXISTS HoldingCapacity INT UNSIGNED NOT NULL DEFAULT 1 AFTER TargetWears;

ALTER TABLE CaseOpeningUserInventoryUpgrades
    DROP COLUMN IF EXISTS TradeUpRecipeHoldingCapacity;

DELETE FROM CaseOpeningUpgradeDefinitions WHERE UpgradeKey IN ('trade-up-holding-5','trade-up-holding-10','trade-up-holding-20');

DELIMITER //

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
            WHEN 'trade-up-unlock' THEN u.TradeUpRecipesUnlocked
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
        TradeUpRecipesUnlocked=IF(p_upgrade_key='trade-up-unlock',1,TradeUpRecipesUnlocked),
        TradeUpRecipeSlots=CASE p_upgrade_key
            WHEN 'trade-up-unlock' THEN GREATEST(TradeUpRecipeSlots,1) ELSE TradeUpRecipeSlots END,
        UpdatedUtc=UTC_TIMESTAMP(6)
    WHERE UserId=p_user_id;
    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_recipes_get//
CREATE PROCEDURE sp_case_opening_trade_up_recipes_get(IN p_user_id CHAR(36))
BEGIN
    SELECT r.RecipeId,r.TargetCaseKey,r.TargetSourceItemId,r.TargetItemName,r.TargetMarketHashName,r.TargetImageUrl,
        r.TargetRarityKey,r.TargetRarityName,r.TargetRarityColor,r.TargetInputRarityKey,r.TargetStatTrak,r.TargetWears,
        r.HoldingCapacity,r.IsActive,r.CreatedUtc,r.UpdatedUtc,
        (SELECT COUNT(*) FROM CaseOpeningTradeUpRecipeHoldings h WHERE h.RecipeId=r.RecipeId) AS HeldCount,
        (SELECT COUNT(*) FROM CaseOpeningHistory hist
            LEFT JOIN CaseOpeningTradeUpRecipeHoldings h2 ON h2.OpeningId=hist.OpeningId
            WHERE hist.UserId=p_user_id AND hist.CaseKey=r.TargetCaseKey AND hist.RarityKey=r.TargetInputRarityKey
                AND hist.IsRareSpecial=0 AND hist.IsStatTrak=r.TargetStatTrak AND h2.HoldingId IS NULL) AS EligibleInputCount
    FROM CaseOpeningTradeUpRecipes r
    WHERE r.UserId=p_user_id
    ORDER BY r.CreatedUtc,r.RecipeId;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_recipe_holding_upgrade//
CREATE PROCEDURE sp_case_opening_trade_up_recipe_holding_upgrade(IN p_user_id CHAR(36),IN p_recipe_id CHAR(36),IN p_cost INT,IN p_maximum_capacity INT)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;
    UPDATE CaseOpeningTradeUpRecipes SET HoldingCapacity=HoldingCapacity+1,UpdatedUtc=UTC_TIMESTAMP(6)
    WHERE RecipeId=p_recipe_id AND UserId=p_user_id AND HoldingCapacity<p_maximum_capacity;
    IF ROW_COUNT()=0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='This recipe''s holding capacity is already at maximum, or the recipe could not be found.';
    END IF;
    UPDATE CaseOpeningProgress SET Stars=Stars-p_cost,UpdatedUtc=UTC_TIMESTAMP() WHERE UserId=p_user_id AND Stars>=p_cost;
    IF ROW_COUNT()=0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='There are not enough Stars for this upgrade.';
    END IF;
    COMMIT;
END//

DELIMITER ;
