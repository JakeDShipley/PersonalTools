-- CS2 Case Simulator: case-opening speed upgrade (1.5x/2x/2.5x/3x), sitting before Skip Animation
-- and Multi-open in the progression. Non-destructive - safe to re-run.
--
-- Several procs touched here were redefined more than once across the recent upstream merge's
-- same-day migration files (in at least one case with genuinely conflicting bodies - see the
-- session notes). Every proc below has been rebuilt from its actual current live body (verified by
-- checking every file that defines it, not just the newest-looking one), not copied from whichever
-- file happened to look newest.

USE PersonalTools;

DELIMITER //

ALTER TABLE CaseOpeningProgress
    ADD COLUMN IF NOT EXISTS OpenSpeedLevel TINYINT UNSIGNED NOT NULL DEFAULT 0 AFTER MultiOpenLevel//

ALTER TABLE CaseOpeningGameSettings
    ADD COLUMN IF NOT EXISTS OpenSpeedUpgradeBaseCostStars INT NOT NULL DEFAULT 400 AFTER MultiOpenXpRequirement,
    ADD COLUMN IF NOT EXISTS OpenSpeedUpgradeCostIncrementStars INT NOT NULL DEFAULT 400 AFTER OpenSpeedUpgradeBaseCostStars,
    ADD COLUMN IF NOT EXISTS OpenSpeedUpgradeXpRequirement INT NOT NULL DEFAULT 1 AFTER OpenSpeedUpgradeCostIncrementStars,
    ADD COLUMN IF NOT EXISTS MaximumOpenSpeedLevel TINYINT UNSIGNED NOT NULL DEFAULT 4 AFTER OpenSpeedUpgradeXpRequirement//

-- Speed tiers require level 1/2/3/4 (OpenSpeedUpgradeXpRequirement + current level). Bump
-- Skip Animation/Multi-open to require level 5 - strictly above every speed tier - so speed
-- upgrades are reachable first. Guarded so a value you already customised via the tweak modal
-- isn't clobbered.
UPDATE CaseOpeningGameSettings SET SkipAnimationXpRequirement = 5 WHERE Id = 1 AND SkipAnimationXpRequirement = 0//
UPDATE CaseOpeningGameSettings SET MultiOpenXpRequirement = 5 WHERE Id = 1 AND MultiOpenXpRequirement = 0//

-- ---------- CaseOpeningProgress readers/writers: OpenSpeedLevel added to every SELECT/INSERT ----------

DROP PROCEDURE IF EXISTS sp_case_opening_progress_get//
CREATE PROCEDURE sp_case_opening_progress_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, OpenSpeedLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, OpenSpeedLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrade_unlock//
CREATE PROCEDURE sp_case_opening_upgrade_unlock
(
    IN p_user_id CHAR(36),
    IN p_upgrade_key VARCHAR(30),
    IN p_cost INT,
    IN p_max_multi_open_level TINYINT UNSIGNED,
    IN p_max_open_speed_level TINYINT UNSIGNED
)
BEGIN
    UPDATE CaseOpeningProgress
    SET
        Stars = Stars - p_cost,
        SkipAnimationUnlocked = CASE
            WHEN p_upgrade_key = 'skip-animation' THEN 1
            ELSE SkipAnimationUnlocked
        END,
        MultiOpenLevel = CASE
            WHEN p_upgrade_key = 'multi-open' THEN MultiOpenLevel + 1
            ELSE MultiOpenLevel
        END,
        OpenSpeedLevel = CASE
            WHEN p_upgrade_key = 'open-speed' THEN OpenSpeedLevel + 1
            ELSE OpenSpeedLevel
        END,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost
      AND
      (
          (p_upgrade_key = 'skip-animation' AND SkipAnimationUnlocked = 0)
          OR
          (p_upgrade_key = 'multi-open' AND MultiOpenLevel < p_max_multi_open_level)
          OR
          (p_upgrade_key = 'open-speed' AND OpenSpeedLevel < p_max_open_speed_level)
      );

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The selected upgrade is fully unlocked or there are not enough Stars.';
    END IF;

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, OpenSpeedLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_case_unlock//
CREATE PROCEDURE sp_case_opening_case_unlock
(
    IN p_user_id CHAR(36),
    IN p_case_key VARCHAR(80),
    IN p_cost INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, OpenSpeedLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    IF EXISTS
    (
        SELECT 1
        FROM CaseOpeningUnlockedCases
        WHERE UserId = p_user_id
          AND CaseKey = p_case_key
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'This case is already unlocked.';
    END IF;

    UPDATE CaseOpeningProgress
    SET
        Stars = Stars - p_cost,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost;

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'There are not enough Stars to unlock this case.';
    END IF;

    INSERT INTO CaseOpeningUnlockedCases (UserId, CaseKey, UnlockedUtc)
    VALUES (p_user_id, p_case_key, UTC_TIMESTAMP());

    COMMIT;

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, OpenSpeedLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_xp_add//
CREATE PROCEDURE sp_case_opening_xp_add
(
    IN p_user_id CHAR(36),
    IN p_xp_delta INT
)
BEGIN
    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, OpenSpeedLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    UPDATE CaseOpeningProgress
    SET Xp = Xp + p_xp_delta, UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id;

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, OpenSpeedLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

-- ---------- Game settings (global, shared across every account) ----------

DROP PROCEDURE IF EXISTS sp_case_opening_game_settings_get//
CREATE PROCEDURE sp_case_opening_game_settings_get()
BEGIN
    SELECT XpPerCaseOpen, SkipAnimationCostStars, SkipAnimationXpRequirement,
           MultiOpenCostStars, MultiOpenXpRequirement,
           OpenSpeedUpgradeBaseCostStars, OpenSpeedUpgradeCostIncrementStars,
           OpenSpeedUpgradeXpRequirement, MaximumOpenSpeedLevel,
           MaximumMultiOpenLevel, MaximumOpenQuantity,
           BotOpeningIntervalSeconds, BotServerBaseCostStars, BotServerCostIncrementStars,
           BotBaseCostStars, BotCostGrowthRate,
           StorageContainerBaseCostStars, StorageContainerCostIncrementStars,
           StorageContainerSlots, MaximumStorageContainers
    FROM CaseOpeningGameSettings
    WHERE Id = 1;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_game_settings_set//
CREATE PROCEDURE sp_case_opening_game_settings_set
(
    IN p_xp_per_case_open INT,
    IN p_skip_animation_cost_stars INT,
    IN p_skip_animation_xp_requirement INT,
    IN p_multi_open_cost_stars INT,
    IN p_multi_open_xp_requirement INT,
    IN p_open_speed_upgrade_base_cost_stars INT,
    IN p_open_speed_upgrade_cost_increment_stars INT,
    IN p_open_speed_upgrade_xp_requirement INT,
    IN p_maximum_open_speed_level TINYINT UNSIGNED,
    IN p_maximum_multi_open_level TINYINT UNSIGNED,
    IN p_maximum_open_quantity TINYINT UNSIGNED,
    IN p_bot_opening_interval_seconds INT,
    IN p_bot_server_base_cost_stars INT,
    IN p_bot_server_cost_increment_stars INT,
    IN p_bot_base_cost_stars INT,
    IN p_bot_cost_growth_rate DECIMAL(5,3),
    IN p_storage_container_base_cost_stars INT,
    IN p_storage_container_cost_increment_stars INT,
    IN p_storage_container_slots INT,
    IN p_maximum_storage_containers INT
)
BEGIN
    UPDATE CaseOpeningGameSettings
    SET XpPerCaseOpen = p_xp_per_case_open,
        SkipAnimationCostStars = p_skip_animation_cost_stars,
        SkipAnimationXpRequirement = p_skip_animation_xp_requirement,
        MultiOpenCostStars = p_multi_open_cost_stars,
        MultiOpenXpRequirement = p_multi_open_xp_requirement,
        OpenSpeedUpgradeBaseCostStars = p_open_speed_upgrade_base_cost_stars,
        OpenSpeedUpgradeCostIncrementStars = p_open_speed_upgrade_cost_increment_stars,
        OpenSpeedUpgradeXpRequirement = p_open_speed_upgrade_xp_requirement,
        MaximumOpenSpeedLevel = p_maximum_open_speed_level,
        MaximumMultiOpenLevel = p_maximum_multi_open_level,
        MaximumOpenQuantity = p_maximum_open_quantity,
        BotOpeningIntervalSeconds = p_bot_opening_interval_seconds,
        BotServerBaseCostStars = p_bot_server_base_cost_stars,
        BotServerCostIncrementStars = p_bot_server_cost_increment_stars,
        BotBaseCostStars = p_bot_base_cost_stars,
        BotCostGrowthRate = p_bot_cost_growth_rate,
        StorageContainerBaseCostStars = p_storage_container_base_cost_stars,
        StorageContainerCostIncrementStars = p_storage_container_cost_increment_stars,
        StorageContainerSlots = p_storage_container_slots,
        MaximumStorageContainers = p_maximum_storage_containers,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE Id = 1;
END//

-- ---------- Testing overrides (your own account's progress only) ----------

DROP PROCEDURE IF EXISTS sp_case_opening_progress_dev_set//
CREATE PROCEDURE sp_case_opening_progress_dev_set
(
    IN p_user_id CHAR(36),
    IN p_stars INT,
    IN p_xp INT
)
BEGIN
    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, OpenSpeedLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    UPDATE CaseOpeningProgress
    SET Stars = p_stars, Xp = p_xp, UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id;

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, OpenSpeedLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrades_dev_set//
CREATE PROCEDURE sp_case_opening_upgrades_dev_set
(
    IN p_user_id CHAR(36),
    IN p_skip_animation_unlocked TINYINT(1),
    IN p_multi_open_level TINYINT UNSIGNED,
    IN p_open_speed_level TINYINT UNSIGNED
)
BEGIN
    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, OpenSpeedLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    UPDATE CaseOpeningProgress
    SET SkipAnimationUnlocked = p_skip_animation_unlocked,
        MultiOpenLevel = p_multi_open_level,
        OpenSpeedLevel = p_open_speed_level,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id;

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, OpenSpeedLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

-- ---------- Reset as new player: also zero OpenSpeedLevel ----------
-- Rebuilt from the current live body (2026-08-24-case-ownership-and-inventory-capacity.sql), which
-- is itself the union of every earlier reset_dev version (achievements/trade-ups/bots/ownership) -
-- not from the older, incomplete 2026-08-23 copies of this same proc.

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
