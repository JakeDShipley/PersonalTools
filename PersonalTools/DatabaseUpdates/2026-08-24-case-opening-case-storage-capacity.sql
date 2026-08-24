USE PersonalTools;

-- Cases now share the same inventory capacity as opened skins. These permanent upgrades give
-- players another progression route without replacing the existing storage-container system.
ALTER TABLE CaseOpeningUserInventoryUpgrades
    ADD COLUMN IF NOT EXISTS BonusInventorySlots INT UNSIGNED NOT NULL DEFAULT 0 AFTER BulkSellLimit;

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
('inventory-slots-250', 'Compact shelving', 'Add 250 permanent slots for cases and skins.', 'capacity', 750, 5, 200, 1),
('inventory-slots-500', 'Reinforced racks', 'Add another 500 permanent slots for cases and skins.', 'capacity', 2000, 12, 210, 1),
('inventory-slots-1000', 'Armory extension', 'Add another 1,000 permanent slots for cases and skins.', 'capacity', 5000, 25, 220, 1)
ON DUPLICATE KEY UPDATE
    Name = VALUES(Name),
    Description = VALUES(Description),
    Category = VALUES(Category),
    CostStars = VALUES(CostStars),
    RequiredLevel = VALUES(RequiredLevel),
    SortOrder = VALUES(SortOrder),
    IsActive = VALUES(IsActive);

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_inventory_upgrades_get//
CREATE PROCEDURE sp_case_opening_inventory_upgrades_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades (UserId, UpdatedUtc)
    VALUES (p_user_id, UTC_TIMESTAMP(6));

    SELECT *
    FROM CaseOpeningUserInventoryUpgrades
    WHERE UserId = p_user_id;
END//

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
        UpdatedUtc = UTC_TIMESTAMP(6)
    WHERE UserId = p_user_id;

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_inventory_capacity_get//
CREATE PROCEDURE sp_case_opening_inventory_capacity_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningInventoryCapacity (UserId, BaseCapacity, UpdatedUtc)
    VALUES (p_user_id, 1000, UTC_TIMESTAMP(6));

    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades (UserId, UpdatedUtc)
    VALUES (p_user_id, UTC_TIMESTAMP(6));

    SELECT
        (SELECT COUNT(*) FROM CaseOpeningHistory WHERE UserId = p_user_id) AS SkinSlots,
        (SELECT COALESCE(SUM(Quantity), 0) FROM CaseOpeningOwnedCases WHERE UserId = p_user_id) AS CaseSlots,
        (SELECT COUNT(*) FROM CaseOpeningHistory WHERE UserId = p_user_id)
            + (SELECT COALESCE(SUM(Quantity), 0) FROM CaseOpeningOwnedCases WHERE UserId = p_user_id) AS UsedSlots,
        c.BaseCapacity,
        (SELECT COUNT(*) FROM CaseOpeningStorageContainers WHERE UserId = p_user_id) AS StorageContainerCount,
        (SELECT COALESCE(SUM(AddedSlots), 0) FROM CaseOpeningStorageContainers WHERE UserId = p_user_id) AS StorageSlots,
        u.BonusInventorySlots AS UpgradeSlots,
        c.BaseCapacity
            + (SELECT COALESCE(SUM(AddedSlots), 0) FROM CaseOpeningStorageContainers WHERE UserId = p_user_id)
            + u.BonusInventorySlots AS TotalCapacity,
        GREATEST(
            c.BaseCapacity
                + (SELECT COALESCE(SUM(AddedSlots), 0) FROM CaseOpeningStorageContainers WHERE UserId = p_user_id)
                + u.BonusInventorySlots
                - (SELECT COUNT(*) FROM CaseOpeningHistory WHERE UserId = p_user_id)
                - (SELECT COALESCE(SUM(Quantity), 0) FROM CaseOpeningOwnedCases WHERE UserId = p_user_id),
            0
        ) AS AvailableSlots
    FROM CaseOpeningInventoryCapacity c
    INNER JOIN CaseOpeningUserInventoryUpgrades u ON u.UserId = c.UserId
    WHERE c.UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_cases_purchase//
CREATE PROCEDURE sp_case_opening_cases_purchase(
    IN p_user_id CHAR(36),
    IN p_case_key VARCHAR(80),
    IN p_quantity INT,
    IN p_purchase_cost_stars INT
)
BEGIN
    DECLARE v_total_cost INT DEFAULT 0;
    DECLARE v_base_capacity INT DEFAULT 1000;
    DECLARE v_storage_slots INT DEFAULT 0;
    DECLARE v_upgrade_slots INT DEFAULT 0;
    DECLARE v_skin_slots INT DEFAULT 0;
    DECLARE v_case_slots INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET v_total_cost = p_quantity * p_purchase_cost_stars;
    START TRANSACTION;

    IF p_quantity < 1 OR p_quantity > 500 OR p_purchase_cost_stars < 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Buy between 1 and 500 cases at a time.';
    END IF;

    IF NOT EXISTS
    (
        SELECT 1
        FROM CaseOpeningUnlockedCases
        WHERE UserId = p_user_id
          AND CaseKey = p_case_key
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Unlock this case before buying copies from the Shop.';
    END IF;

    INSERT IGNORE INTO CaseOpeningInventoryCapacity (UserId, BaseCapacity, UpdatedUtc)
    VALUES (p_user_id, 1000, UTC_TIMESTAMP(6));

    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades (UserId, UpdatedUtc)
    VALUES (p_user_id, UTC_TIMESTAMP(6));

    -- Lock the account capacity row before checking space. This prevents two simultaneous
    -- purchases from both claiming the same final slots.
    SELECT BaseCapacity
    INTO v_base_capacity
    FROM CaseOpeningInventoryCapacity
    WHERE UserId = p_user_id
    FOR UPDATE;

    SELECT COALESCE(SUM(AddedSlots), 0)
    INTO v_storage_slots
    FROM CaseOpeningStorageContainers
    WHERE UserId = p_user_id;

    SELECT BonusInventorySlots
    INTO v_upgrade_slots
    FROM CaseOpeningUserInventoryUpgrades
    WHERE UserId = p_user_id;

    SELECT COUNT(*)
    INTO v_skin_slots
    FROM CaseOpeningHistory
    WHERE UserId = p_user_id;

    SELECT COALESCE(SUM(Quantity), 0)
    INTO v_case_slots
    FROM CaseOpeningOwnedCases
    WHERE UserId = p_user_id;

    IF p_quantity > GREATEST(v_base_capacity + v_storage_slots + v_upgrade_slots - v_skin_slots - v_case_slots, 0) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'There is not enough inventory space for these cases. Sell skins or unlock more storage.';
    END IF;

    INSERT IGNORE INTO CaseOpeningProgress (UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, UpdatedUtc)
    VALUES (p_user_id, 0, 0, 0, 0, UTC_TIMESTAMP());

    UPDATE CaseOpeningProgress
    SET
        Stars = Stars - v_total_cost,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= v_total_cost;

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'There are not enough Stars to buy these cases.';
    END IF;

    INSERT INTO CaseOpeningOwnedCases (UserId, CaseKey, Quantity, UpdatedUtc)
    VALUES (p_user_id, p_case_key, p_quantity, UTC_TIMESTAMP(6))
    ON DUPLICATE KEY UPDATE
        Quantity = Quantity + VALUES(Quantity),
        UpdatedUtc = UTC_TIMESTAMP(6);

    COMMIT;

    SELECT
        p_case_key AS CaseKey,
        p_quantity AS PurchasedQuantity,
        Quantity AS OwnedQuantity,
        v_total_cost AS StarsSpent,
        (SELECT Stars FROM CaseOpeningProgress WHERE UserId = p_user_id) AS StarsBalance
    FROM CaseOpeningOwnedCases
    WHERE UserId = p_user_id
      AND CaseKey = p_case_key;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_history_create//
CREATE PROCEDURE sp_case_opening_history_create(
    IN p_user_id CHAR(36),
    IN p_opening_id CHAR(36),
    IN p_case_key VARCHAR(80),
    IN p_source_item_id VARCHAR(160),
    IN p_item_name VARCHAR(255),
    IN p_market_hash_name VARCHAR(300),
    IN p_image_url VARCHAR(2048),
    IN p_description TEXT,
    IN p_weapon_name VARCHAR(100),
    IN p_pattern_name VARCHAR(150),
    IN p_paint_index VARCHAR(20),
    IN p_phase VARCHAR(50),
    IN p_rarity_key VARCHAR(30),
    IN p_rarity_name VARCHAR(80),
    IN p_rarity_color CHAR(7),
    IN p_wear VARCHAR(40),
    IN p_is_stat_trak TINYINT(1),
    IN p_is_rare_special TINYINT(1),
    IN p_supports_stat_trak TINYINT(1),
    IN p_min_float DECIMAL(9,6),
    IN p_max_float DECIMAL(9,6),
    IN p_float_value DECIMAL(9,6),
    IN p_pattern_seed INT,
    IN p_estimated_price DECIMAL(12,2)
)
BEGIN
    DECLARE v_owned_quantity INT DEFAULT 0;
    DECLARE v_capacity_lock INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    INSERT IGNORE INTO CaseOpeningInventoryCapacity (UserId, BaseCapacity, UpdatedUtc)
    VALUES (p_user_id, 1000, UTC_TIMESTAMP(6));

    INSERT IGNORE INTO CaseOpeningOwnedCases (UserId, CaseKey, Quantity, UpdatedUtc)
    SELECT p_user_id, 'kilowatt', 25, UTC_TIMESTAMP(6)
    WHERE p_case_key = 'kilowatt';

    -- Opening exchanges one owned case slot for one skin slot. Locking the shared capacity row
    -- serialises it with Shop purchases while still allowing an opening when storage is full.
    SELECT BaseCapacity
    INTO v_capacity_lock
    FROM CaseOpeningInventoryCapacity
    WHERE UserId = p_user_id
    FOR UPDATE;

    SELECT Quantity
    INTO v_owned_quantity
    FROM CaseOpeningOwnedCases
    WHERE UserId = p_user_id
      AND CaseKey = p_case_key
    FOR UPDATE;

    IF v_owned_quantity IS NULL OR v_owned_quantity < 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'You do not own this case. Buy more cases from the Shop before opening.';
    END IF;

    UPDATE CaseOpeningOwnedCases
    SET
        Quantity = Quantity - 1,
        UpdatedUtc = UTC_TIMESTAMP(6)
    WHERE UserId = p_user_id
      AND CaseKey = p_case_key
      AND Quantity >= 1;

    INSERT INTO CaseOpeningHistory
    (
        OpeningId, UserId, CaseKey, SourceItemId, ItemName, MarketHashName, ImageUrl,
        Description, WeaponName, PatternName, PaintIndex, Phase, RarityKey, RarityName,
        RarityColor, Wear, IsStatTrak, IsRareSpecial, SupportsStatTrak, MinFloat, MaxFloat,
        FloatValue, PatternSeed, EstimatedPrice, OpenedUtc
    )
    VALUES
    (
        p_opening_id, p_user_id, p_case_key, p_source_item_id, p_item_name, p_market_hash_name,
        p_image_url, p_description, p_weapon_name, p_pattern_name, p_paint_index, p_phase,
        p_rarity_key, p_rarity_name, p_rarity_color, p_wear, p_is_stat_trak,
        p_is_rare_special, p_supports_stat_trak, p_min_float, p_max_float, p_float_value,
        p_pattern_seed, p_estimated_price, UTC_TIMESTAMP(6)
    );

    INSERT IGNORE INTO CaseOpeningCollection (CollectionId, UserId, CaseKey, SourceItemId, FirstObtainedUtc)
    VALUES (UUID(), p_user_id, p_case_key, p_source_item_id, UTC_TIMESTAMP(6));

    COMMIT;
END//

DELIMITER ;
