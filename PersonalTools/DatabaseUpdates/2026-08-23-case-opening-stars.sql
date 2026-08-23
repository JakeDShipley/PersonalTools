USE PersonalTools;

CREATE TABLE IF NOT EXISTS CaseOpeningProgress
(
    UserId CHAR(36) NOT NULL,
    Stars INT UNSIGNED NOT NULL DEFAULT 0,
    SkipAnimationUnlocked TINYINT(1) NOT NULL DEFAULT 0,
    MultiOpenUnlocked TINYINT(1) NOT NULL DEFAULT 0,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (UserId),
    CONSTRAINT FK_CaseOpeningProgress_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

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

    SELECT UserId, Stars, SkipAnimationUnlocked, MultiOpenUnlocked
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
        MultiOpenUnlocked = CASE
            WHEN p_upgrade_key = 'multi-open' THEN 1
            ELSE MultiOpenUnlocked
        END,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost
      AND
      (
          (p_upgrade_key = 'skip-animation' AND SkipAnimationUnlocked = 0)
          OR
          (p_upgrade_key = 'multi-open' AND MultiOpenUnlocked = 0)
      );

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The selected upgrade is already unlocked or there are not enough Stars.';
    END IF;

    SELECT UserId, Stars, SkipAnimationUnlocked, MultiOpenUnlocked
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_inventory_sell//
CREATE PROCEDURE sp_case_opening_inventory_sell
(
    IN p_user_id CHAR(36),
    IN p_opening_ids JSON,
    IN p_item_count INT,
    IN p_stars_awarded INT
)
BEGIN
    DECLARE v_sold_item_count INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    SELECT
        COUNT(*)
    INTO v_sold_item_count
    FROM CaseOpeningHistory h
    INNER JOIN
    (
        SELECT DISTINCT OpeningId
        FROM JSON_TABLE
        (
            p_opening_ids,
            '$[*]' COLUMNS
            (
                OpeningId CHAR(36) PATH '$'
            )
        ) AS selectedIds
    ) selectedIds
        -- GUID values are identifiers, not natural-language text. Binary comparison avoids a
        -- legacy table collation difference from making a valid inventory sale fail.
        ON BINARY selectedIds.OpeningId = BINARY h.OpeningId
    WHERE BINARY h.UserId = BINARY p_user_id;

    IF v_sold_item_count <> p_item_count THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'One or more selected inventory items could not be sold.';
    END IF;

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

    UPDATE CaseOpeningProgress
    SET
        Stars = Stars + p_stars_awarded,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id;

    DELETE h
    FROM CaseOpeningHistory h
    INNER JOIN
    (
        SELECT DISTINCT OpeningId
        FROM JSON_TABLE
        (
            p_opening_ids,
            '$[*]' COLUMNS
            (
                OpeningId CHAR(36) PATH '$'
            )
        ) AS selectedIds
    ) selectedIds
        ON BINARY selectedIds.OpeningId = BINARY h.OpeningId
    WHERE BINARY h.UserId = BINARY p_user_id;

    COMMIT;

    SELECT
        p_stars_awarded AS StarsAwarded,
        Stars AS StarsBalance,
        v_sold_item_count AS SoldItemCount
    FROM CaseOpeningProgress
    WHERE UserId = p_user_id;
END//

DELIMITER ;
