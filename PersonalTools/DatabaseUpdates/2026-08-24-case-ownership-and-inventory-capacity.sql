USE PersonalTools;

CREATE TABLE IF NOT EXISTS CaseOpeningOwnedCases
(
    UserId CHAR(36) NOT NULL,
    CaseKey VARCHAR(80) NOT NULL,
    Quantity INT UNSIGNED NOT NULL DEFAULT 0,
    UpdatedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (UserId, CaseKey),
    CONSTRAINT FK_CaseOpeningOwnedCases_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CaseOpeningInventoryCapacity
(
    UserId CHAR(36) NOT NULL,
    BaseCapacity INT UNSIGNED NOT NULL DEFAULT 1000,
    UpdatedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (UserId),
    CONSTRAINT FK_CaseOpeningInventoryCapacity_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CaseOpeningStorageContainers
(
    StorageContainerId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    AddedSlots INT UNSIGNED NOT NULL DEFAULT 1000,
    AcquiredUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (StorageContainerId),
    KEY IX_CaseOpeningStorageContainers_User (UserId),
    CONSTRAINT FK_CaseOpeningStorageContainers_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_owned_cases_get//

CREATE PROCEDURE sp_case_opening_owned_cases_get(
    IN p_user_id CHAR(36)
)
BEGIN
    -- The starter allocation keeps established accounts playable until the Shop phase gives
    -- users a way to buy more stock. It is insert-once, never a repeatable daily reward.
    INSERT IGNORE INTO CaseOpeningOwnedCases
    (
        UserId,
        CaseKey,
        Quantity,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        'kilowatt',
        25,
        UTC_TIMESTAMP(6)
    );

    SELECT
        CaseKey,
        Quantity
    FROM CaseOpeningOwnedCases
    WHERE UserId = p_user_id
    ORDER BY CaseKey;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_inventory_capacity_get//

CREATE PROCEDURE sp_case_opening_inventory_capacity_get(
    IN p_user_id CHAR(36)
)
BEGIN
    INSERT IGNORE INTO CaseOpeningInventoryCapacity
    (
        UserId,
        BaseCapacity,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        1000,
        UTC_TIMESTAMP(6)
    );

    SELECT
        (SELECT COUNT(*) FROM CaseOpeningHistory WHERE UserId = p_user_id) AS UsedSlots,
        c.BaseCapacity,
        (SELECT COUNT(*) FROM CaseOpeningStorageContainers WHERE UserId = p_user_id) AS StorageContainerCount,
        (SELECT COALESCE(SUM(AddedSlots), 0) FROM CaseOpeningStorageContainers WHERE UserId = p_user_id) AS StorageSlots,
        c.BaseCapacity + (SELECT COALESCE(SUM(AddedSlots), 0) FROM CaseOpeningStorageContainers WHERE UserId = p_user_id) AS TotalCapacity,
        GREATEST(c.BaseCapacity + (SELECT COALESCE(SUM(AddedSlots), 0) FROM CaseOpeningStorageContainers WHERE UserId = p_user_id) - (SELECT COUNT(*) FROM CaseOpeningHistory WHERE UserId = p_user_id), 0) AS AvailableSlots
    FROM CaseOpeningInventoryCapacity c
    WHERE c.UserId = p_user_id;
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
    DECLARE v_base_capacity INT DEFAULT 1000;
    DECLARE v_storage_slots INT DEFAULT 0;
    DECLARE v_used_slots INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    INSERT IGNORE INTO CaseOpeningInventoryCapacity
    (
        UserId,
        BaseCapacity,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        1000,
        UTC_TIMESTAMP(6)
    );

    INSERT IGNORE INTO CaseOpeningOwnedCases
    (
        UserId,
        CaseKey,
        Quantity,
        UpdatedUtc
    )
    SELECT
        p_user_id,
        'kilowatt',
        25,
        UTC_TIMESTAMP(6)
    WHERE p_case_key = 'kilowatt';

    -- Every opening locks the capacity row first. This serialises capacity checks across every
    -- case type, so two different case keys cannot both claim the final free inventory slot.
    SELECT BaseCapacity
    INTO v_base_capacity
    FROM CaseOpeningInventoryCapacity
    WHERE UserId = p_user_id
    FOR UPDATE;

    SELECT COALESCE(SUM(AddedSlots), 0)
    INTO v_storage_slots
    FROM CaseOpeningStorageContainers
    WHERE UserId = p_user_id;

    SELECT COUNT(*)
    INTO v_used_slots
    FROM CaseOpeningHistory
    WHERE UserId = p_user_id;

    IF v_used_slots >= v_base_capacity + v_storage_slots THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Your inventory is full. Sell items or add storage before opening another case.';
    END IF;

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

    INSERT IGNORE INTO CaseOpeningCollection
    (
        CollectionId,
        UserId,
        CaseKey,
        SourceItemId,
        FirstObtainedUtc
    )
    VALUES
    (
        UUID(),
        p_user_id,
        p_case_key,
        p_source_item_id,
        UTC_TIMESTAMP(6)
    );

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_reset_dev//

CREATE PROCEDURE sp_case_opening_reset_dev(
    IN p_user_id CHAR(36)
)
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

    INSERT INTO CaseOpeningProgress(UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, UpdatedUtc)
    VALUES(p_user_id, 0, 0, 0, 0, UTC_TIMESTAMP())
    ON DUPLICATE KEY UPDATE
        Stars = 0,
        Xp = 0,
        SkipAnimationUnlocked = 0,
        MultiOpenLevel = 0,
        UpdatedUtc = UTC_TIMESTAMP();

    COMMIT;
END//

DELIMITER ;
