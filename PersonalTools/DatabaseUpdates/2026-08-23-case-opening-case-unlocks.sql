USE PersonalTools;

CREATE TABLE IF NOT EXISTS CaseOpeningUnlockedCases
(
    UserId CHAR(36) NOT NULL,
    CaseKey VARCHAR(80) NOT NULL,
    UnlockedUtc DATETIME NOT NULL,
    PRIMARY KEY (UserId, CaseKey),
    CONSTRAINT FK_CaseOpeningUnlockedCases_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_unlocked_cases_get//
CREATE PROCEDURE sp_case_opening_unlocked_cases_get(IN p_user_id CHAR(36))
BEGIN
    -- Every user keeps Kilowatt permanently. This insert also upgrades existing users cleanly.
    INSERT IGNORE INTO CaseOpeningUnlockedCases (UserId, CaseKey, UnlockedUtc)
    VALUES (p_user_id, 'kilowatt', UTC_TIMESTAMP());

    SELECT CaseKey
    FROM CaseOpeningUnlockedCases
    WHERE UserId = p_user_id
    ORDER BY UnlockedUtc, CaseKey;
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
        UpdatedUtc
    )
    VALUES
    (
        p_user_id,
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

    SELECT UserId, Stars, SkipAnimationUnlocked, MultiOpenUnlocked
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DELIMITER ;
