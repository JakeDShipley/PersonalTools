-- CS2 Case Simulator: two fixes/additions.
-- Non-destructive - safe to re-run.
--
-- 1) sp_case_opening_bot_cycle_claim had a HARDCODED 12-second cooldown, ignoring
--    CaseOpeningGameSettings.BotOpeningIntervalSeconds (which the variable-tweak modal lets you
--    edit). If that setting is ever changed away from 12, the client fires bot-open requests on
--    the configured interval but the server keeps rejecting them as "still cooling down" using the
--    old hardcoded value - explaining bots skipping cycles inconsistently. Now reads the real
--    configured interval.
--
-- 2) New CaseOpeningXpByRarity table: XP awarded per rarity tier, replacing the flat
--    XpPerCaseOpen for actual case opens (XpPerCaseOpen stays as the fallback for any rarity key
--    not found in this table). Seeded with an increasing 5/10/15/20/25 scale across the existing
--    paired-tier structure already used for sale values (mil-spec/high-grade, restricted/remarkable,
--    classified/exotic, covert, rare-special).

USE PersonalTools;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_bot_cycle_claim//
CREATE PROCEDURE sp_case_opening_bot_cycle_claim(IN p_user_id CHAR(36), IN p_bot_id CHAR(36))
BEGIN
    DECLARE v_interval_seconds INT DEFAULT 12;

    SELECT BotOpeningIntervalSeconds INTO v_interval_seconds
    FROM CaseOpeningGameSettings
    WHERE Id = 1;

    UPDATE CaseOpeningBots
    SET LastOpenedUtc = UTC_TIMESTAMP(6)
    WHERE BotId = p_bot_id
      AND UserId = p_user_id
      AND (LastOpenedUtc IS NULL OR LastOpenedUtc <= DATE_SUB(UTC_TIMESTAMP(6), INTERVAL v_interval_seconds SECOND));

    SELECT ROW_COUNT();
END//

CREATE TABLE IF NOT EXISTS CaseOpeningXpByRarity
(
    RarityKey VARCHAR(30) NOT NULL,
    XpAwarded INT NOT NULL DEFAULT 5,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (RarityKey)
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB//

INSERT IGNORE INTO CaseOpeningXpByRarity (RarityKey, XpAwarded, UpdatedUtc) VALUES
    ('mil-spec', 5, UTC_TIMESTAMP()),
    ('high-grade', 5, UTC_TIMESTAMP()),
    ('restricted', 10, UTC_TIMESTAMP()),
    ('remarkable', 10, UTC_TIMESTAMP()),
    ('classified', 15, UTC_TIMESTAMP()),
    ('exotic', 15, UTC_TIMESTAMP()),
    ('covert', 20, UTC_TIMESTAMP()),
    ('rare-special', 25, UTC_TIMESTAMP())//

DROP PROCEDURE IF EXISTS sp_case_opening_xp_by_rarity_get_all//
CREATE PROCEDURE sp_case_opening_xp_by_rarity_get_all()
BEGIN
    SELECT RarityKey, XpAwarded
    FROM CaseOpeningXpByRarity
    ORDER BY XpAwarded, RarityKey;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_xp_by_rarity_set//
CREATE PROCEDURE sp_case_opening_xp_by_rarity_set
(
    IN p_rarity_key VARCHAR(30),
    IN p_xp_awarded INT
)
BEGIN
    INSERT INTO CaseOpeningXpByRarity (RarityKey, XpAwarded, UpdatedUtc)
    VALUES (p_rarity_key, p_xp_awarded, UTC_TIMESTAMP())
    ON DUPLICATE KEY UPDATE
        XpAwarded = VALUES(XpAwarded),
        UpdatedUtc = UTC_TIMESTAMP();
END//

DELIMITER ;
