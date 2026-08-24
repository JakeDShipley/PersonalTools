-- CS2 Case Simulator: auto-buy rules. Non-destructive - safe to re-run.
--
-- A rule watches one case's owned quantity; once it drops below a threshold, the existing
-- case-purchase flow (PurchaseCaseOpeningCases / sp_case_opening_cases_purchase) buys more,
-- so tiered pricing and capacity checks apply exactly as they would to a manual purchase.
--
-- sp_case_opening_upgrade_definitions_get and sp_case_opening_inventory_upgrade_unlock are
-- redefined below with the AutoBuy branches added on top of their full current bodies (bulk-sell,
-- auto-sell, inventory-slots) - not a partial copy. See this session's notes on why: this merge has
-- same-day migration files that redefined these same procs with genuinely different, conflicting
-- bodies, so "whichever file looks newest" isn't a safe way to extend them.

USE PersonalTools;

DELIMITER //

CREATE TABLE IF NOT EXISTS CaseOpeningAutoBuyRules
(
    UserId CHAR(36) NOT NULL,
    CaseKey VARCHAR(80) NOT NULL,
    ThresholdQuantity INT UNSIGNED NOT NULL DEFAULT 0,
    PurchaseQuantity INT UNSIGNED NOT NULL DEFAULT 1,
    IsEnabled TINYINT(1) NOT NULL DEFAULT 1,
    CreatedUtc DATETIME(6) NOT NULL,
    UpdatedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (UserId, CaseKey),
    CONSTRAINT FK_CaseOpeningAutoBuyRules_Users FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB//

ALTER TABLE CaseOpeningUserInventoryUpgrades
    ADD COLUMN IF NOT EXISTS AutoBuyUnlocked TINYINT(1) NOT NULL DEFAULT 0 AFTER BonusInventorySlots,
    ADD COLUMN IF NOT EXISTS AutoBuyRuleSlots INT UNSIGNED NOT NULL DEFAULT 3 AFTER AutoBuyUnlocked//

INSERT INTO CaseOpeningUpgradeDefinitions
(
    UpgradeKey,
    Name,
    Description,
    Category,
    CostStars,
    RequiredLevel,
    SortOrder,
    IsActive
)
VALUES
('auto-buy-unlock', 'Auto-buy', 'Unlock automatic case restocking: set a threshold and quantity per case, and the Shop buys more for you.', 'automation', 800, 4, 300, 1),
('auto-buy-slots-5', 'Auto-buy: 5 rules', 'Raise the number of active auto-buy rules to 5.', 'automation', 2000, 8, 310, 1),
('auto-buy-slots-10', 'Auto-buy: 10 rules', 'Raise the number of active auto-buy rules to 10.', 'automation', 4000, 15, 320, 1)
ON DUPLICATE KEY UPDATE
    Name = VALUES(Name),
    Description = VALUES(Description),
    Category = VALUES(Category),
    CostStars = VALUES(CostStars),
    RequiredLevel = VALUES(RequiredLevel),
    SortOrder = VALUES(SortOrder),
    IsActive = VALUES(IsActive)//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrade_definitions_get//
CREATE PROCEDURE sp_case_opening_upgrade_definitions_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades (UserId, UpdatedUtc)
    VALUES (p_user_id, UTC_TIMESTAMP(6));

    SELECT
        d.UpgradeKey,
        d.Name,
        d.Description,
        d.Category,
        d.CostStars,
        d.RequiredLevel,
        d.SortOrder,
        CASE d.UpgradeKey
            WHEN 'bulk-sell-200' THEN u.BulkSellLimit >= 200
            WHEN 'bulk-sell-300' THEN u.BulkSellLimit >= 300
            WHEN 'bulk-sell-400' THEN u.BulkSellLimit >= 400
            WHEN 'bulk-sell-500' THEN u.BulkSellLimit >= 500
            WHEN 'auto-sell-covert' THEN u.AutoSellCovertUnlocked
            WHEN 'auto-sell-classified' THEN u.AutoSellClassifiedUnlocked
            WHEN 'auto-sell-restricted' THEN u.AutoSellRestrictedUnlocked
            WHEN 'auto-sell-mil-spec' THEN u.AutoSellMilSpecUnlocked
            WHEN 'inventory-slots-250' THEN u.BonusInventorySlots >= 250
            WHEN 'inventory-slots-500' THEN u.BonusInventorySlots >= 750
            WHEN 'inventory-slots-1000' THEN u.BonusInventorySlots >= 1750
            WHEN 'auto-buy-unlock' THEN u.AutoBuyUnlocked
            WHEN 'auto-buy-slots-5' THEN u.AutoBuyRuleSlots >= 5
            WHEN 'auto-buy-slots-10' THEN u.AutoBuyRuleSlots >= 10
            ELSE 0
        END AS IsUnlocked
    FROM CaseOpeningUpgradeDefinitions d
    CROSS JOIN CaseOpeningUserInventoryUpgrades u
    WHERE u.UserId = p_user_id
      AND d.IsActive = 1
    ORDER BY d.SortOrder, d.UpgradeKey;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_inventory_upgrade_unlock//
CREATE PROCEDURE sp_case_opening_inventory_upgrade_unlock(IN p_user_id CHAR(36), IN p_upgrade_key VARCHAR(50), IN p_cost INT)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades (UserId, UpdatedUtc)
    VALUES (p_user_id, UTC_TIMESTAMP(6));

    UPDATE CaseOpeningProgress
    SET
        Stars = Stars - p_cost,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost;

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'There are not enough Stars to purchase this upgrade.';
    END IF;

    UPDATE CaseOpeningUserInventoryUpgrades
    SET
        BulkSellLimit = CASE p_upgrade_key
            WHEN 'bulk-sell-200' THEN GREATEST(BulkSellLimit, 200)
            WHEN 'bulk-sell-300' THEN GREATEST(BulkSellLimit, 300)
            WHEN 'bulk-sell-400' THEN GREATEST(BulkSellLimit, 400)
            WHEN 'bulk-sell-500' THEN GREATEST(BulkSellLimit, 500)
            ELSE BulkSellLimit
        END,
        BonusInventorySlots = CASE p_upgrade_key
            WHEN 'inventory-slots-250' THEN GREATEST(BonusInventorySlots, 250)
            WHEN 'inventory-slots-500' THEN GREATEST(BonusInventorySlots, 750)
            WHEN 'inventory-slots-1000' THEN GREATEST(BonusInventorySlots, 1750)
            ELSE BonusInventorySlots
        END,
        AutoSellCovertUnlocked = IF(p_upgrade_key = 'auto-sell-covert', 1, AutoSellCovertUnlocked),
        AutoSellClassifiedUnlocked = IF(p_upgrade_key = 'auto-sell-classified', 1, AutoSellClassifiedUnlocked),
        AutoSellRestrictedUnlocked = IF(p_upgrade_key = 'auto-sell-restricted', 1, AutoSellRestrictedUnlocked),
        AutoSellMilSpecUnlocked = IF(p_upgrade_key = 'auto-sell-mil-spec', 1, AutoSellMilSpecUnlocked),
        AutoBuyUnlocked = IF(p_upgrade_key = 'auto-buy-unlock', 1, AutoBuyUnlocked),
        AutoBuyRuleSlots = CASE p_upgrade_key
            WHEN 'auto-buy-slots-5' THEN GREATEST(AutoBuyRuleSlots, 5)
            WHEN 'auto-buy-slots-10' THEN GREATEST(AutoBuyRuleSlots, 10)
            ELSE AutoBuyRuleSlots
        END,
        UpdatedUtc = UTC_TIMESTAMP(6)
    WHERE UserId = p_user_id;

    COMMIT;
END//

-- ---------- Auto-buy rule CRUD ----------

DROP PROCEDURE IF EXISTS sp_case_opening_auto_buy_rules_get//
CREATE PROCEDURE sp_case_opening_auto_buy_rules_get(IN p_user_id CHAR(36))
BEGIN
    SELECT CaseKey, ThresholdQuantity, PurchaseQuantity, IsEnabled, CreatedUtc, UpdatedUtc
    FROM CaseOpeningAutoBuyRules
    WHERE UserId = p_user_id
    ORDER BY CreatedUtc, CaseKey;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_auto_buy_rule_set//
CREATE PROCEDURE sp_case_opening_auto_buy_rule_set
(
    IN p_user_id CHAR(36),
    IN p_case_key VARCHAR(80),
    IN p_threshold_quantity INT UNSIGNED,
    IN p_purchase_quantity INT UNSIGNED,
    IN p_is_enabled TINYINT(1),
    IN p_rule_slot_cap INT UNSIGNED
)
BEGIN
    DECLARE v_already_enabled INT DEFAULT 0;
    DECLARE v_active_count INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    SELECT COUNT(*) INTO v_already_enabled
    FROM CaseOpeningAutoBuyRules
    WHERE UserId = p_user_id AND CaseKey = p_case_key AND IsEnabled = 1;

    -- Only re-check the cap when this save would newly turn a rule on - editing or disabling an
    -- already-active rule, or re-saving one that's already enabled, never needs the room it's
    -- already using.
    IF p_is_enabled = 1 AND v_already_enabled = 0 THEN
        SELECT COUNT(*) INTO v_active_count
        FROM CaseOpeningAutoBuyRules
        WHERE UserId = p_user_id AND IsEnabled = 1;

        IF v_active_count >= p_rule_slot_cap THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'You have reached your active auto-buy rule limit.';
        END IF;
    END IF;

    INSERT INTO CaseOpeningAutoBuyRules
    (
        UserId, CaseKey, ThresholdQuantity, PurchaseQuantity, IsEnabled, CreatedUtc, UpdatedUtc
    )
    VALUES
    (
        p_user_id, p_case_key, p_threshold_quantity, p_purchase_quantity, p_is_enabled, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
    )
    ON DUPLICATE KEY UPDATE
        ThresholdQuantity = VALUES(ThresholdQuantity),
        PurchaseQuantity = VALUES(PurchaseQuantity),
        IsEnabled = VALUES(IsEnabled),
        UpdatedUtc = UTC_TIMESTAMP(6);

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_auto_buy_rule_delete//
CREATE PROCEDURE sp_case_opening_auto_buy_rule_delete(IN p_user_id CHAR(36), IN p_case_key VARCHAR(80))
BEGIN
    DELETE FROM CaseOpeningAutoBuyRules
    WHERE UserId = p_user_id AND CaseKey = p_case_key;
END//

-- ---------- Reset as new player: also clear auto-buy rules ----------
-- Rebuilt from the current live body (added in the ownership/capacity migration) plus a
-- CaseOpeningAutoBuyRules cleanup - not from an older, incomplete copy of this proc.

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
