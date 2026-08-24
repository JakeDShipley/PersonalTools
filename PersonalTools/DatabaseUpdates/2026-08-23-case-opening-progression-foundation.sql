USE PersonalTools;

-- Permanent simulator progression lives separately from the inventory. Inventory rows can be
-- sold, cleared or consumed by a contract without undoing a player's earned milestones.
CREATE TABLE IF NOT EXISTS CaseOpeningPlayerStats
(
    UserId CHAR(36) NOT NULL,
    TotalCasesOpened INT UNSIGNED NOT NULL DEFAULT 0,
    TotalSkinsObtained INT UNSIGNED NOT NULL DEFAULT 0,
    TotalTradeUpsCompleted INT UNSIGNED NOT NULL DEFAULT 0,
    TotalUnlocks INT UNSIGNED NOT NULL DEFAULT 0,
    TotalLoginDays INT UNSIGNED NOT NULL DEFAULT 0,
    CurrentLoginStreak INT UNSIGNED NOT NULL DEFAULT 0,
    LongestLoginStreak INT UNSIGNED NOT NULL DEFAULT 0,
    CompletedCollections INT UNSIGNED NOT NULL DEFAULT 0,
    CompletedRaritySets INT UNSIGNED NOT NULL DEFAULT 0,
    HighestRewardedLevel INT UNSIGNED NOT NULL DEFAULT 0,
    LastLoginUtcDate DATE NULL,
    UpdatedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (UserId),
    CONSTRAINT FK_CaseOpeningPlayerStats_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CaseOpeningCompletedCollections
(
    CompletionId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    CaseKey VARCHAR(80) NOT NULL,
    CompletedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (CompletionId),
    UNIQUE KEY UX_CaseOpeningCompletedCollections_User_Case (UserId, CaseKey),
    CONSTRAINT FK_CaseOpeningCompletedCollections_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CaseOpeningCompletedRarities
(
    CompletionId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    CaseKey VARCHAR(80) NOT NULL,
    RarityKey VARCHAR(30) NOT NULL,
    CompletedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (CompletionId),
    UNIQUE KEY UX_CaseOpeningCompletedRarities_User_Case_Rarity (UserId, CaseKey, RarityKey),
    CONSTRAINT FK_CaseOpeningCompletedRarities_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

-- Definitions are data rather than hard-coded browser content. Phase 2B will surface these in
-- the achievement catalogue and record one user unlock row for each completed definition.
CREATE TABLE IF NOT EXISTS CaseOpeningAchievementDefinitions
(
    AchievementKey VARCHAR(80) NOT NULL,
    Name VARCHAR(120) NOT NULL,
    Description VARCHAR(300) NOT NULL,
    MetricKey VARCHAR(60) NOT NULL,
    TargetValue INT UNSIGNED NOT NULL,
    RewardStars INT UNSIGNED NOT NULL DEFAULT 0,
    SortOrder INT UNSIGNED NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (AchievementKey)
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CaseOpeningUserAchievements
(
    UserAchievementId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    AchievementKey VARCHAR(80) NOT NULL,
    UnlockedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (UserAchievementId),
    UNIQUE KEY UX_CaseOpeningUserAchievements_User_Achievement (UserId, AchievementKey),
    KEY IX_CaseOpeningUserAchievements_User_Unlock (UserId, UnlockedUtc),
    CONSTRAINT FK_CaseOpeningUserAchievements_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE,
    CONSTRAINT FK_CaseOpeningUserAchievements_Definitions
        FOREIGN KEY (AchievementKey) REFERENCES CaseOpeningAchievementDefinitions (AchievementKey) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

INSERT INTO CaseOpeningAchievementDefinitions
(
    AchievementKey,
    Name,
    Description,
    MetricKey,
    TargetValue,
    RewardStars,
    SortOrder,
    IsActive
)
VALUES
    ('first-case', 'First case', 'Open your first case.', 'cases-opened', 1, 5, 10, 1),
    ('cases-10', 'Getting started', 'Open 10 cases.', 'cases-opened', 10, 10, 20, 1),
    ('cases-50', 'Case regular', 'Open 50 cases.', 'cases-opened', 50, 25, 30, 1),
    ('cases-100', 'Century opener', 'Open 100 cases.', 'cases-opened', 100, 50, 40, 1),
    ('cases-500', 'Case enthusiast', 'Open 500 cases.', 'cases-opened', 500, 125, 50, 1),
    ('cases-1000', 'Opening machine', 'Open 1,000 cases.', 'cases-opened', 1000, 250, 60, 1),
    ('skins-25', 'Stockpile', 'Obtain 25 skins.', 'skins-obtained', 25, 15, 70, 1),
    ('skins-100', 'Locker room', 'Obtain 100 skins.', 'skins-obtained', 100, 50, 80, 1),
    ('skins-500', 'Armory', 'Obtain 500 skins.', 'skins-obtained', 500, 150, 90, 1),
    ('first-trade-up', 'Trade up', 'Complete your first Trade Up Contract.', 'trade-ups-completed', 1, 15, 100, 1),
    ('trade-ups-10', 'Contractor', 'Complete 10 Trade Up Contracts.', 'trade-ups-completed', 10, 75, 110, 1),
    ('unlocks-1', 'First unlock', 'Unlock your first case or upgrade.', 'unlocks', 1, 10, 120, 1),
    ('unlocks-5', 'Building out', 'Unlock five cases or upgrades.', 'unlocks', 5, 40, 130, 1),
    ('unlocks-10', 'Fully equipped', 'Unlock ten cases or upgrades.', 'unlocks', 10, 100, 140, 1),
    ('login-days-7', 'Weekly check-in', 'Log in on seven different UTC days.', 'login-days', 7, 20, 150, 1),
    ('login-days-30', 'Monthly check-in', 'Log in on 30 different UTC days.', 'login-days', 30, 80, 160, 1),
    ('login-days-100', 'Dedicated opener', 'Log in on 100 different UTC days.', 'login-days', 100, 250, 170, 1),
    ('streak-3', 'Three day streak', 'Keep a three day login streak.', 'login-streak', 3, 15, 180, 1),
    ('streak-7', 'Seven day streak', 'Keep a seven day login streak.', 'login-streak', 7, 50, 190, 1),
    ('streak-30', 'Thirty day streak', 'Keep a thirty day login streak.', 'login-streak', 30, 200, 200, 1),
    ('first-collection', 'Collection complete', 'Complete a case collection.', 'collections-completed', 1, 100, 210, 1),
    ('first-rarity-set', 'Rarity complete', 'Complete a rarity set from a collection.', 'rarity-sets-completed', 1, 30, 220, 1)
ON DUPLICATE KEY UPDATE
    Name = VALUES(Name),
    Description = VALUES(Description),
    MetricKey = VALUES(MetricKey),
    TargetValue = VALUES(TargetValue),
    RewardStars = VALUES(RewardStars),
    SortOrder = VALUES(SortOrder),
    IsActive = VALUES(IsActive);

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_player_stats_get//

CREATE PROCEDURE sp_case_opening_player_stats_get(
    IN p_user_id CHAR(36)
)
BEGIN
    INSERT IGNORE INTO CaseOpeningPlayerStats
    (
        UserId,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        UTC_TIMESTAMP(6)
    );

    SELECT
        UserId,
        TotalCasesOpened,
        TotalSkinsObtained,
        TotalTradeUpsCompleted,
        TotalUnlocks,
        TotalLoginDays,
        CurrentLoginStreak,
        LongestLoginStreak,
        CompletedCollections,
        CompletedRaritySets,
        HighestRewardedLevel,
        LastLoginUtcDate
    FROM CaseOpeningPlayerStats
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_player_stats_add//

CREATE PROCEDURE sp_case_opening_player_stats_add(
    IN p_user_id CHAR(36),
    IN p_cases_opened INT,
    IN p_skins_obtained INT,
    IN p_trade_ups_completed INT,
    IN p_unlocks_earned INT
)
BEGIN
    INSERT IGNORE INTO CaseOpeningPlayerStats
    (
        UserId,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        UTC_TIMESTAMP(6)
    );

    UPDATE CaseOpeningPlayerStats
    SET
        TotalCasesOpened = TotalCasesOpened + GREATEST(0, p_cases_opened),
        TotalSkinsObtained = TotalSkinsObtained + GREATEST(0, p_skins_obtained),
        TotalTradeUpsCompleted = TotalTradeUpsCompleted + GREATEST(0, p_trade_ups_completed),
        TotalUnlocks = TotalUnlocks + GREATEST(0, p_unlocks_earned),
        UpdatedUtc = UTC_TIMESTAMP(6)
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_login_record//

CREATE PROCEDURE sp_case_opening_login_record(
    IN p_user_id CHAR(36)
)
BEGIN
    DECLARE v_last_login_date DATE DEFAULT NULL;
    DECLARE v_today DATE DEFAULT UTC_DATE();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    INSERT IGNORE INTO CaseOpeningPlayerStats
    (
        UserId,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        UTC_TIMESTAMP(6)
    );

    SELECT LastLoginUtcDate
    INTO v_last_login_date
    FROM CaseOpeningPlayerStats
    WHERE UserId = p_user_id
    FOR UPDATE;

    IF v_last_login_date IS NULL OR v_last_login_date < v_today THEN
        UPDATE CaseOpeningPlayerStats
        SET
            TotalLoginDays = TotalLoginDays + 1,
            CurrentLoginStreak = CASE
                WHEN v_last_login_date = DATE_SUB(v_today, INTERVAL 1 DAY)
                    THEN CurrentLoginStreak + 1
                ELSE 1
            END,
            LongestLoginStreak = GREATEST
            (
                LongestLoginStreak,
                CASE
                    WHEN v_last_login_date = DATE_SUB(v_today, INTERVAL 1 DAY)
                        THEN CurrentLoginStreak + 1
                    ELSE 1
                END
            ),
            LastLoginUtcDate = v_today,
            UpdatedUtc = UTC_TIMESTAMP(6)
        WHERE UserId = p_user_id;
    END IF;

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_level_reward_claim//

CREATE PROCEDURE sp_case_opening_level_reward_claim(
    IN p_user_id CHAR(36),
    IN p_level INT,
    IN p_stars_awarded INT
)
BEGIN
    DECLARE v_claimed INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    INSERT IGNORE INTO CaseOpeningPlayerStats
    (
        UserId,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        UTC_TIMESTAMP(6)
    );

    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId,
        Stars,
        Xp,
        SkipAnimationUnlocked,
        MultiOpenLevel,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        0,
        0,
        0,
        0,
        UTC_TIMESTAMP(6)
    );

    UPDATE CaseOpeningPlayerStats
    SET
        HighestRewardedLevel = p_level,
        UpdatedUtc = UTC_TIMESTAMP(6)
    WHERE UserId = p_user_id
      AND HighestRewardedLevel < p_level;

    SET v_claimed = ROW_COUNT();

    IF v_claimed = 1 THEN
        UPDATE CaseOpeningProgress
        SET
            Stars = Stars + GREATEST(0, p_stars_awarded),
            UpdatedUtc = UTC_TIMESTAMP(6)
        WHERE UserId = p_user_id;
    END IF;

    COMMIT;

    SELECT v_claimed;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_collection_completion_record//

CREATE PROCEDURE sp_case_opening_collection_completion_record(
    IN p_user_id CHAR(36),
    IN p_case_key VARCHAR(80)
)
BEGIN
    DECLARE v_recorded INT DEFAULT 0;

    INSERT IGNORE INTO CaseOpeningPlayerStats
    (
        UserId,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        UTC_TIMESTAMP(6)
    );

    INSERT IGNORE INTO CaseOpeningCompletedCollections
    (
        CompletionId,
        UserId,
        CaseKey,
        CompletedUtc
    )
    VALUES
    (
        UUID(),
        p_user_id,
        p_case_key,
        UTC_TIMESTAMP(6)
    );

    SET v_recorded = ROW_COUNT();

    IF v_recorded = 1 THEN
        UPDATE CaseOpeningPlayerStats
        SET
            CompletedCollections = CompletedCollections + 1,
            UpdatedUtc = UTC_TIMESTAMP(6)
        WHERE UserId = p_user_id;
    END IF;

    SELECT v_recorded;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_collection_rarity_completion_record//

CREATE PROCEDURE sp_case_opening_collection_rarity_completion_record(
    IN p_user_id CHAR(36),
    IN p_case_key VARCHAR(80),
    IN p_rarity_key VARCHAR(30)
)
BEGIN
    DECLARE v_recorded INT DEFAULT 0;

    INSERT IGNORE INTO CaseOpeningPlayerStats
    (
        UserId,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        UTC_TIMESTAMP(6)
    );

    INSERT IGNORE INTO CaseOpeningCompletedRarities
    (
        CompletionId,
        UserId,
        CaseKey,
        RarityKey,
        CompletedUtc
    )
    VALUES
    (
        UUID(),
        p_user_id,
        p_case_key,
        p_rarity_key,
        UTC_TIMESTAMP(6)
    );

    SET v_recorded = ROW_COUNT();

    IF v_recorded = 1 THEN
        UPDATE CaseOpeningPlayerStats
        SET
            CompletedRaritySets = CompletedRaritySets + 1,
            UpdatedUtc = UTC_TIMESTAMP(6)
        WHERE UserId = p_user_id;
    END IF;

    SELECT v_recorded;
END//

-- The existing test reset is expected to return a simulator account to a true clean state.
-- Keep permanent progression rows in that same reset scope rather than leaving stale milestones.
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

    DELETE FROM CaseOpeningUserAchievements
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningCompletedRarities
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningCompletedCollections
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningPlayerStats
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningBots
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningBotServers
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningTradeUps
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningCollection
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningHistory
    WHERE UserId = p_user_id;

    DELETE FROM CaseOpeningUnlockedCases
    WHERE UserId = p_user_id;

    INSERT INTO CaseOpeningUnlockedCases
    (
        UserId,
        CaseKey,
        UnlockedUtc
    )
    VALUES
    (
        p_user_id,
        'kilowatt',
        UTC_TIMESTAMP()
    );

    INSERT INTO CaseOpeningProgress
    (
        UserId,
        Stars,
        Xp,
        SkipAnimationUnlocked,
        MultiOpenLevel,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        0,
        0,
        0,
        0,
        UTC_TIMESTAMP()
    )
    ON DUPLICATE KEY UPDATE
        Stars = 0,
        Xp = 0,
        SkipAnimationUnlocked = 0,
        MultiOpenLevel = 0,
        UpdatedUtc = UTC_TIMESTAMP();

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_achievements_get//

CREATE PROCEDURE sp_case_opening_achievements_get(
    IN p_user_id CHAR(36)
)
BEGIN
    SELECT
        d.AchievementKey,
        d.Name,
        d.Description,
        d.MetricKey,
        d.TargetValue,
        d.RewardStars,
        d.SortOrder,
        CASE WHEN u.UserAchievementId IS NULL THEN 0 ELSE 1 END AS IsUnlocked,
        u.UnlockedUtc
    FROM CaseOpeningAchievementDefinitions d
    LEFT JOIN CaseOpeningUserAchievements u
        ON u.AchievementKey = d.AchievementKey
       AND u.UserId = p_user_id
    WHERE d.IsActive = 1
    ORDER BY d.SortOrder, d.AchievementKey;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_achievements_evaluate//

CREATE PROCEDURE sp_case_opening_achievements_evaluate(
    IN p_user_id CHAR(36)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    INSERT IGNORE INTO CaseOpeningPlayerStats
    (
        UserId,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        UTC_TIMESTAMP(6)
    );

    -- Locking the one permanent stats row serialises reward evaluation for this account. Without
    -- it, two nearly simultaneous case opens could both see the same achievement as unclaimed.
    SELECT UserId
    FROM CaseOpeningPlayerStats
    WHERE UserId = p_user_id
    FOR UPDATE;

    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId,
        Stars,
        Xp,
        SkipAnimationUnlocked,
        MultiOpenLevel,
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
        0,
        0,
        0,
        0,
        UTC_TIMESTAMP(6)
    );

    UPDATE CaseOpeningProgress p
    SET
        Stars = Stars + COALESCE
        (
            (
                SELECT SUM(d.RewardStars)
                FROM CaseOpeningAchievementDefinitions d
                INNER JOIN CaseOpeningPlayerStats s
                    ON s.UserId = p_user_id
                LEFT JOIN CaseOpeningUserAchievements u
                    ON u.UserId = p_user_id
                   AND u.AchievementKey = d.AchievementKey
                WHERE d.IsActive = 1
                  AND u.UserAchievementId IS NULL
                  AND CASE d.MetricKey
                      WHEN 'cases-opened' THEN s.TotalCasesOpened
                      WHEN 'skins-obtained' THEN s.TotalSkinsObtained
                      WHEN 'trade-ups-completed' THEN s.TotalTradeUpsCompleted
                      WHEN 'unlocks' THEN s.TotalUnlocks
                      WHEN 'login-days' THEN s.TotalLoginDays
                      WHEN 'login-streak' THEN s.CurrentLoginStreak
                      WHEN 'collections-completed' THEN s.CompletedCollections
                      WHEN 'rarity-sets-completed' THEN s.CompletedRaritySets
                      ELSE 0
                  END >= d.TargetValue
            ),
            0
        ),
        UpdatedUtc = UTC_TIMESTAMP(6)
    WHERE p.UserId = p_user_id;

    INSERT IGNORE INTO CaseOpeningUserAchievements
    (
        UserAchievementId,
        UserId,
        AchievementKey,
        UnlockedUtc
    )
    SELECT
        UUID(),
        p_user_id,
        d.AchievementKey,
        UTC_TIMESTAMP(6)
    FROM CaseOpeningAchievementDefinitions d
    INNER JOIN CaseOpeningPlayerStats s
        ON s.UserId = p_user_id
    WHERE d.IsActive = 1
      AND CASE d.MetricKey
          WHEN 'cases-opened' THEN s.TotalCasesOpened
          WHEN 'skins-obtained' THEN s.TotalSkinsObtained
          WHEN 'trade-ups-completed' THEN s.TotalTradeUpsCompleted
          WHEN 'unlocks' THEN s.TotalUnlocks
          WHEN 'login-days' THEN s.TotalLoginDays
          WHEN 'login-streak' THEN s.CurrentLoginStreak
          WHEN 'collections-completed' THEN s.CompletedCollections
          WHEN 'rarity-sets-completed' THEN s.CompletedRaritySets
          ELSE 0
      END >= d.TargetValue;

    COMMIT;
END//

DELIMITER ;
