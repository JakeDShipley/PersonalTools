-- CS2 Case Simulator: XP/leveling system + tunable game settings (for the variable-tweak modal).
-- Non-destructive - safe to re-run.
--
-- XP curve (increasing increment, chosen so each level genuinely needs more xp than the last):
--   level N requires an additional 100*N xp beyond level N-1.
--   Cumulative xp to reach level N = 100 * N * (N+1) / 2  (level1=100, level2=300, level3=600...)
--   The formula itself lives in C# (CaseOpeningXpLevels) - nothing SQL-side needs to know it,
--   this file only adds the raw Xp counter column.
--
-- Game balance constants that used to be C# consts are moved into CaseOpeningGameSettings (one
-- singleton row) and per-case costs/XP requirements into CaseOpeningCaseSettings, so the new
-- variable-tweak modal has something real to read and write.

USE PersonalTools;

DELIMITER //

ALTER TABLE CaseOpeningProgress
    ADD COLUMN IF NOT EXISTS Xp INT NOT NULL DEFAULT 0 AFTER Stars//

CREATE TABLE IF NOT EXISTS CaseOpeningGameSettings
(
    Id TINYINT NOT NULL,
    XpPerCaseOpen INT NOT NULL DEFAULT 5,
    SkipAnimationCostStars INT NOT NULL DEFAULT 250,
    SkipAnimationXpRequirement INT NOT NULL DEFAULT 0,
    MultiOpenCostStars INT NOT NULL DEFAULT 1000,
    MultiOpenXpRequirement INT NOT NULL DEFAULT 0,
    MaximumMultiOpenLevel TINYINT UNSIGNED NOT NULL DEFAULT 4,
    MaximumOpenQuantity TINYINT UNSIGNED NOT NULL DEFAULT 5,
    BotOpeningIntervalSeconds INT NOT NULL DEFAULT 12,
    BotServerBaseCostStars INT NOT NULL DEFAULT 2500,
    BotServerCostIncrementStars INT NOT NULL DEFAULT 2500,
    BotBaseCostStars INT NOT NULL DEFAULT 600,
    BotCostGrowthRate DECIMAL(5,3) NOT NULL DEFAULT 1.550,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (Id)
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB//

INSERT IGNORE INTO CaseOpeningGameSettings (Id, UpdatedUtc) VALUES (1, UTC_TIMESTAMP())//

CREATE TABLE IF NOT EXISTS CaseOpeningCaseSettings
(
    CaseKey VARCHAR(80) NOT NULL,
    UnlockCostStars INT NOT NULL DEFAULT 0,
    XpRequirement INT NOT NULL DEFAULT 0,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (CaseKey)
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB//

-- Seeded from the unlock costs that previously lived in CaseOpeningFuncs.CaseUnlockCosts.
-- XpRequirement starts at 0 for every case, per the "keep it at 0 for now" instruction.
INSERT IGNORE INTO CaseOpeningCaseSettings (CaseKey, UnlockCostStars, XpRequirement, UpdatedUtc) VALUES
    ('kilowatt', 0, 0, UTC_TIMESTAMP()),
    ('fever', 10, 0, UTC_TIMESTAMP()),
    ('gallery', 10, 0, UTC_TIMESTAMP()),
    ('fracture', 10, 0, UTC_TIMESTAMP()),
    ('austin-2025-legends', 10, 0, UTC_TIMESTAMP()),
    ('snakebite', 15, 0, UTC_TIMESTAMP()),
    ('revolution', 15, 0, UTC_TIMESTAMP()),
    ('prisma-2', 20, 0, UTC_TIMESTAMP()),
    ('copenhagen-2024-legends', 20, 0, UTC_TIMESTAMP()),
    ('dreams-and-nightmares', 20, 0, UTC_TIMESTAMP()),
    ('recoil', 20, 0, UTC_TIMESTAMP()),
    ('prisma', 25, 0, UTC_TIMESTAMP()),
    ('paris-2023-legends', 30, 0, UTC_TIMESTAMP()),
    ('clutch', 35, 0, UTC_TIMESTAMP()),
    ('shattered-web', 40, 0, UTC_TIMESTAMP()),
    ('chroma-2', 40, 0, UTC_TIMESTAMP()),
    ('antwerp-2022-legends', 60, 0, UTC_TIMESTAMP()),
    ('broken-fang', 75, 0, UTC_TIMESTAMP()),
    ('breakout', 60, 0, UTC_TIMESTAMP()),
    ('cs20', 60, 0, UTC_TIMESTAMP()),
    ('stockholm-2021-legends', 80, 0, UTC_TIMESTAMP()),
    ('gamma-2', 80, 0, UTC_TIMESTAMP()),
    ('riptide', 100, 0, UTC_TIMESTAMP()),
    ('spectrum-2', 110, 0, UTC_TIMESTAMP()),
    ('atlanta-2017-legends', 120, 0, UTC_TIMESTAMP()),
    ('hydra', 150, 0, UTC_TIMESTAMP()),
    ('glove', 200, 0, UTC_TIMESTAMP()),
    ('esports-2013', 250, 0, UTC_TIMESTAMP()),
    ('weapon-case-3', 250, 0, UTC_TIMESTAMP()),
    ('esports-2014-summer', 275, 0, UTC_TIMESTAMP()),
    ('esports-2013-winter', 300, 0, UTC_TIMESTAMP()),
    ('weapon-case-1', 350, 0, UTC_TIMESTAMP()),
    ('weapon-case-2', 450, 0, UTC_TIMESTAMP()),
    ('cologne-2014-legends', 1200, 0, UTC_TIMESTAMP()),
    ('katowice-2014-challengers', 1000, 0, UTC_TIMESTAMP()),
    ('katowice-2014-legends', 1500, 0, UTC_TIMESTAMP()),
    ('cologne-2014-cobblestone-souvenir', 3000, 0, UTC_TIMESTAMP())//

-- Re-created with Xp added to every SELECT/INSERT list. Logic is otherwise unchanged from
-- 2026-08-23-case-opening-multi-open-levels.sql / -collection.sql.

DROP PROCEDURE IF EXISTS sp_case_opening_progress_get//
CREATE PROCEDURE sp_case_opening_progress_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrade_unlock//
CREATE PROCEDURE sp_case_opening_upgrade_unlock
(
    IN p_user_id CHAR(36),
    IN p_upgrade_key VARCHAR(30),
    IN p_cost INT,
    IN p_max_multi_open_level TINYINT UNSIGNED
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
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost
      AND
      (
          (p_upgrade_key = 'skip-animation' AND SkipAnimationUnlocked = 0)
          OR
          (p_upgrade_key = 'multi-open' AND MultiOpenLevel < p_max_multi_open_level)
      );

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The selected upgrade is fully unlocked or there are not enough Stars.';
    END IF;

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel
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
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
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

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_bot_server_purchase//
CREATE PROCEDURE sp_case_opening_bot_server_purchase
(
    IN p_user_id CHAR(36),
    IN p_server_id CHAR(36),
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
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    UPDATE CaseOpeningProgress
    SET Stars = Stars - p_cost, UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost;

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'There are not enough Stars to purchase this bot server.';
    END IF;

    INSERT INTO CaseOpeningBotServers (ServerId, UserId, CreatedUtc)
    VALUES (p_server_id, p_user_id, UTC_TIMESTAMP(6));

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_bot_purchase//
CREATE PROCEDURE sp_case_opening_bot_purchase
(
    IN p_user_id CHAR(36),
    IN p_server_id CHAR(36),
    IN p_bot_id CHAR(36),
    IN p_cost INT
)
BEGIN
    DECLARE v_bot_count INT DEFAULT 0;
    DECLARE v_server_found CHAR(36) DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    SELECT ServerId
    INTO v_server_found
    FROM CaseOpeningBotServers
    WHERE ServerId = p_server_id
      AND UserId = p_user_id
    FOR UPDATE;

    IF v_server_found IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The selected bot server could not be found.';
    END IF;

    SELECT COUNT(*)
    INTO v_bot_count
    FROM CaseOpeningBots
    WHERE ServerId = p_server_id
      AND UserId = p_user_id;

    IF v_bot_count >= 4 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'This bot server is full. Purchase another server to add a bot.';
    END IF;

    UPDATE CaseOpeningProgress
    SET Stars = Stars - p_cost, UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost;

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'There are not enough Stars to purchase this bot.';
    END IF;

    INSERT INTO CaseOpeningBots
    (
        BotId, ServerId, UserId, CreatedUtc, LastOpenedUtc
    )
    VALUES
    (
        p_bot_id, p_server_id, p_user_id, UTC_TIMESTAMP(6), NULL
    );

    COMMIT;
END//

-- ---------- New: XP awarding ----------

DROP PROCEDURE IF EXISTS sp_case_opening_xp_add//
CREATE PROCEDURE sp_case_opening_xp_add
(
    IN p_user_id CHAR(36),
    IN p_xp_delta INT
)
BEGIN
    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    UPDATE CaseOpeningProgress
    SET Xp = Xp + p_xp_delta, UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id;

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

-- ---------- New: game settings (global, shared across every account) ----------

DROP PROCEDURE IF EXISTS sp_case_opening_game_settings_get//
CREATE PROCEDURE sp_case_opening_game_settings_get()
BEGIN
    SELECT XpPerCaseOpen, SkipAnimationCostStars, SkipAnimationXpRequirement,
           MultiOpenCostStars, MultiOpenXpRequirement, MaximumMultiOpenLevel, MaximumOpenQuantity,
           BotOpeningIntervalSeconds, BotServerBaseCostStars, BotServerCostIncrementStars,
           BotBaseCostStars, BotCostGrowthRate
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
    IN p_maximum_multi_open_level TINYINT UNSIGNED,
    IN p_maximum_open_quantity TINYINT UNSIGNED,
    IN p_bot_opening_interval_seconds INT,
    IN p_bot_server_base_cost_stars INT,
    IN p_bot_server_cost_increment_stars INT,
    IN p_bot_base_cost_stars INT,
    IN p_bot_cost_growth_rate DECIMAL(5,3)
)
BEGIN
    UPDATE CaseOpeningGameSettings
    SET XpPerCaseOpen = p_xp_per_case_open,
        SkipAnimationCostStars = p_skip_animation_cost_stars,
        SkipAnimationXpRequirement = p_skip_animation_xp_requirement,
        MultiOpenCostStars = p_multi_open_cost_stars,
        MultiOpenXpRequirement = p_multi_open_xp_requirement,
        MaximumMultiOpenLevel = p_maximum_multi_open_level,
        MaximumOpenQuantity = p_maximum_open_quantity,
        BotOpeningIntervalSeconds = p_bot_opening_interval_seconds,
        BotServerBaseCostStars = p_bot_server_base_cost_stars,
        BotServerCostIncrementStars = p_bot_server_cost_increment_stars,
        BotBaseCostStars = p_bot_base_cost_stars,
        BotCostGrowthRate = p_bot_cost_growth_rate,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE Id = 1;
END//

-- ---------- New: per-case settings (unlock cost + xp requirement) ----------

DROP PROCEDURE IF EXISTS sp_case_opening_case_settings_get_all//
CREATE PROCEDURE sp_case_opening_case_settings_get_all()
BEGIN
    SELECT CaseKey, UnlockCostStars, XpRequirement
    FROM CaseOpeningCaseSettings
    ORDER BY UnlockCostStars, CaseKey;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_case_settings_set//
CREATE PROCEDURE sp_case_opening_case_settings_set
(
    IN p_case_key VARCHAR(80),
    IN p_unlock_cost_stars INT,
    IN p_xp_requirement INT
)
BEGIN
    INSERT INTO CaseOpeningCaseSettings (CaseKey, UnlockCostStars, XpRequirement, UpdatedUtc)
    VALUES (p_case_key, p_unlock_cost_stars, p_xp_requirement, UTC_TIMESTAMP())
    ON DUPLICATE KEY UPDATE
        UnlockCostStars = VALUES(UnlockCostStars),
        XpRequirement = VALUES(XpRequirement),
        UpdatedUtc = UTC_TIMESTAMP();
END//

-- ---------- New: testing overrides (your own account's progress only) ----------

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
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    UPDATE CaseOpeningProgress
    SET Stars = p_stars, Xp = p_xp, UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id;

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrades_dev_set//
CREATE PROCEDURE sp_case_opening_upgrades_dev_set
(
    IN p_user_id CHAR(36),
    IN p_skip_animation_unlocked TINYINT(1),
    IN p_multi_open_level TINYINT UNSIGNED
)
BEGIN
    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    UPDATE CaseOpeningProgress
    SET SkipAnimationUnlocked = p_skip_animation_unlocked,
        MultiOpenLevel = p_multi_open_level,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id;

    SELECT UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_case_unlock_dev_set//
CREATE PROCEDURE sp_case_opening_case_unlock_dev_set
(
    IN p_user_id CHAR(36),
    IN p_case_key VARCHAR(80),
    IN p_unlock TINYINT(1)
)
BEGIN
    IF p_unlock = 1 THEN
        INSERT IGNORE INTO CaseOpeningUnlockedCases (UserId, CaseKey, UnlockedUtc)
        VALUES (p_user_id, p_case_key, UTC_TIMESTAMP());
    ELSE
        DELETE FROM CaseOpeningUnlockedCases
        WHERE UserId = p_user_id AND CaseKey = p_case_key AND CaseKey <> 'kilowatt';
    END IF;
END//

DELIMITER ;
