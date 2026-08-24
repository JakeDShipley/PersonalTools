-- CS2 Case Simulator: rebalance the opening-speed upgrade.
-- Non-destructive - safe to re-run.
--
-- Skip animation (instant reveal, no wait at all) is a bigger deal than any of the partial speed
-- tiers, so it made no sense for it to be independently purchasable for LESS than an early speed
-- tier. It's now folded into the speed track as its 5th and final level instead of a separate
-- purchase - reaching max opening speed grants it automatically. Entry cost is also lowered.

USE PersonalTools;

DELIMITER //

UPDATE CaseOpeningGameSettings
SET OpenSpeedUpgradeBaseCostStars = 100,
    OpenSpeedUpgradeCostIncrementStars = 100,
    MaximumOpenSpeedLevel = 5
WHERE Id = 1//

-- Multi-open needs to keep coming after every speed tier - the max speed level just grew from 4
-- to 5, so bump this from 5 to 6 to stay strictly above it. Guarded so a value you already
-- customised isn't clobbered.
UPDATE CaseOpeningGameSettings SET MultiOpenXpRequirement = 6 WHERE Id = 1 AND MultiOpenXpRequirement = 5//

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
        MultiOpenLevel = CASE
            WHEN p_upgrade_key = 'multi-open' THEN MultiOpenLevel + 1
            ELSE MultiOpenLevel
        END,
        OpenSpeedLevel = CASE
            WHEN p_upgrade_key = 'open-speed' THEN OpenSpeedLevel + 1
            ELSE OpenSpeedLevel
        END,
        -- No longer independently purchasable - reaching the final speed level grants it.
        SkipAnimationUnlocked = CASE
            WHEN p_upgrade_key = 'open-speed' AND OpenSpeedLevel + 1 >= p_max_open_speed_level THEN 1
            ELSE SkipAnimationUnlocked
        END,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost
      AND
      (
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

DELIMITER ;
