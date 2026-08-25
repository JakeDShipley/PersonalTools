-- Redesign: buying "the first upgrade" was jumping recipe slots straight to 5 (via trade-up-unlock)
-- and further jumping in blocks of 5 (trade-up-slots-10/15/20). Recipe slots now behave like the
-- bot speed upgrade instead: a repeatable +1 purchase (sp_case_opening_trade_up_recipe_slot_upgrade),
-- capped at 20, with cost scaling per slot already owned. trade-up-unlock now grants exactly 1 slot
-- (not 5). The discrete trade-up-slots-10/15/20 upgrade definitions are removed - nobody has used
-- this feature yet, so this includes a one-time reset of everyone's recipe slot count rather than
-- trying to convert already-purchased tiers into slot counts.

DELETE FROM CaseOpeningUpgradeDefinitions WHERE UpgradeKey IN ('trade-up-slots-10','trade-up-slots-15','trade-up-slots-20');

UPDATE CaseOpeningUserInventoryUpgrades
SET TradeUpRecipeSlots = IF(TradeUpRecipesUnlocked = 1, 1, 0);

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
        TradeUpRecipesUnlocked=IF(p_upgrade_key='trade-up-unlock',1,TradeUpRecipesUnlocked),
        TradeUpRecipeSlots=CASE p_upgrade_key
            WHEN 'trade-up-unlock' THEN GREATEST(TradeUpRecipeSlots,1) ELSE TradeUpRecipeSlots END,
        TradeUpRecipeHoldingCapacity=CASE p_upgrade_key
            WHEN 'trade-up-unlock' THEN GREATEST(TradeUpRecipeHoldingCapacity,1)
            WHEN 'trade-up-holding-5' THEN GREATEST(TradeUpRecipeHoldingCapacity,5) WHEN 'trade-up-holding-10' THEN GREATEST(TradeUpRecipeHoldingCapacity,10)
            WHEN 'trade-up-holding-20' THEN GREATEST(TradeUpRecipeHoldingCapacity,20) ELSE TradeUpRecipeHoldingCapacity END,
        UpdatedUtc=UTC_TIMESTAMP(6)
    WHERE UserId=p_user_id;
    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_recipe_slot_upgrade//
CREATE PROCEDURE sp_case_opening_trade_up_recipe_slot_upgrade(IN p_user_id CHAR(36),IN p_cost INT,IN p_maximum_slots INT)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;
    UPDATE CaseOpeningUserInventoryUpgrades SET TradeUpRecipeSlots=TradeUpRecipeSlots+1,UpdatedUtc=UTC_TIMESTAMP(6)
    WHERE UserId=p_user_id AND TradeUpRecipesUnlocked=1 AND TradeUpRecipeSlots<p_maximum_slots;
    IF ROW_COUNT()=0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Auto trade-up recipe slots are already at maximum, or Auto trade-up is not unlocked.';
    END IF;
    UPDATE CaseOpeningProgress SET Stars=Stars-p_cost,UpdatedUtc=UTC_TIMESTAMP() WHERE UserId=p_user_id AND Stars>=p_cost;
    IF ROW_COUNT()=0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='There are not enough Stars for this upgrade.';
    END IF;
    COMMIT;
END//

DELIMITER ;
