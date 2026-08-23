USE PersonalTools;

ALTER TABLE CaseOpeningProgress
    ADD COLUMN MultiOpenLevel TINYINT UNSIGNED NOT NULL DEFAULT 0
    AFTER MultiOpenUnlocked;

-- Existing users who paid for the original all-at-once upgrade retain its full entitlement.
UPDATE CaseOpeningProgress
SET MultiOpenLevel = 4
WHERE MultiOpenUnlocked = 1;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_progress_get//
CREATE PROCEDURE sp_case_opening_progress_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId,
        Stars,
        SkipAnimationUnlocked,
        MultiOpenUnlocked,
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
    );

    SELECT UserId, Stars, SkipAnimationUnlocked, MultiOpenLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrade_unlock//
CREATE PROCEDURE sp_case_opening_upgrade_unlock
(
    IN p_user_id CHAR(36),
    IN p_upgrade_key VARCHAR(30),
    IN p_cost INT
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
          (p_upgrade_key = 'multi-open' AND MultiOpenLevel < 4)
      );

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The selected upgrade is fully unlocked or there are not enough Stars.';
    END IF;

    SELECT UserId, Stars, SkipAnimationUnlocked, MultiOpenLevel
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
        UserId,
        Stars,
        SkipAnimationUnlocked,
        MultiOpenUnlocked,
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

    SELECT UserId, Stars, SkipAnimationUnlocked, MultiOpenLevel
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DELIMITER ;
