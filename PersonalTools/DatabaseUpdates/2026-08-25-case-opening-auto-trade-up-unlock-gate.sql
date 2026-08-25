-- Redesign, before this ever went live: Auto trade-up now needs an explicit "unlock" purchase in
-- the Upgrades tab (mirroring Auto-buy's auto-buy-unlock) instead of starting with 5 free recipe
-- slots and 1 free holding slot. The recipe-slots ladder (trade-up-slots-10/15/20) now requires
-- trade-up-unlock as its prerequisite; so does the first holding-capacity tier (trade-up-holding-5).
-- Since nobody has used this feature yet, this includes a one-time reset of everyone's trade-up
-- state back to locked rather than a careful migration of "already earned" values.

ALTER TABLE CaseOpeningUserInventoryUpgrades
    ADD COLUMN IF NOT EXISTS TradeUpRecipesUnlocked TINYINT(1) NOT NULL DEFAULT 0 AFTER AutoBuyRuleSlots;

ALTER TABLE CaseOpeningUserInventoryUpgrades
    MODIFY COLUMN TradeUpRecipeSlots INT UNSIGNED NOT NULL DEFAULT 0,
    MODIFY COLUMN TradeUpRecipeHoldingCapacity INT UNSIGNED NOT NULL DEFAULT 0;

UPDATE CaseOpeningUserInventoryUpgrades
SET TradeUpRecipesUnlocked = 0, TradeUpRecipeSlots = 0, TradeUpRecipeHoldingCapacity = 0;

INSERT INTO CaseOpeningUpgradeDefinitions (UpgradeKey,Name,Description,Category,CostStars,RequiredLevel,SortOrder,IsActive) VALUES
('trade-up-unlock','Auto trade-up','Unlock automatic Trade Up Contracts: target a specific skin, and once ten matching inputs are ready the contract fires on its own.','trade-up-unlock',1000,5,395,1)
ON DUPLICATE KEY UPDATE Name=VALUES(Name),Description=VALUES(Description),Category=VALUES(Category),CostStars=VALUES(CostStars),RequiredLevel=VALUES(RequiredLevel),SortOrder=VALUES(SortOrder),IsActive=VALUES(IsActive);

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
        TradeUpRecipesUnlocked=IF(p_upgrade_key='trade-up-unlock',1,TradeUpRecipesUnlocked),
        TradeUpRecipeSlots=CASE p_upgrade_key
            WHEN 'trade-up-unlock' THEN GREATEST(TradeUpRecipeSlots,5)
            WHEN 'trade-up-slots-10' THEN GREATEST(TradeUpRecipeSlots,10) WHEN 'trade-up-slots-15' THEN GREATEST(TradeUpRecipeSlots,15)
            WHEN 'trade-up-slots-20' THEN GREATEST(TradeUpRecipeSlots,20) ELSE TradeUpRecipeSlots END,
        TradeUpRecipeHoldingCapacity=CASE p_upgrade_key
            WHEN 'trade-up-unlock' THEN GREATEST(TradeUpRecipeHoldingCapacity,1)
            WHEN 'trade-up-holding-5' THEN GREATEST(TradeUpRecipeHoldingCapacity,5) WHEN 'trade-up-holding-10' THEN GREATEST(TradeUpRecipeHoldingCapacity,10)
            WHEN 'trade-up-holding-20' THEN GREATEST(TradeUpRecipeHoldingCapacity,20) ELSE TradeUpRecipeHoldingCapacity END,
        UpdatedUtc=UTC_TIMESTAMP(6)
    WHERE UserId=p_user_id;
    COMMIT;
END//

DELIMITER ;
