USE PersonalTools;

-- Phase 2B depends on the progression tables and achievement definitions from
-- 2026-08-23-case-opening-progression-foundation.sql.
DELIMITER //

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

    -- This account lock stops two concurrent requests from crediting one achievement twice.
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
