-- CS2 Case Simulator: "reset as new player" for the variable-tweak modal's testing tools.
-- Wipes the calling user's own Stars/XP/upgrades/unlocked cases/bots/history/collection back to
-- a brand-new-account state (only the starter Kilowatt case stays unlocked). Non-destructive to
-- run (adds a procedure only) - the procedure itself is destructive to whichever account calls it,
-- by design.

USE PersonalTools;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_reset_dev//
CREATE PROCEDURE sp_case_opening_reset_dev(IN p_user_id CHAR(36))
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    DELETE FROM CaseOpeningBots WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningBotServers WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningCollection WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningHistory WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningUnlockedCases WHERE UserId = p_user_id;
    INSERT INTO CaseOpeningUnlockedCases (UserId, CaseKey, UnlockedUtc) VALUES (p_user_id, 'kilowatt', UTC_TIMESTAMP());

    INSERT INTO CaseOpeningProgress (UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, UpdatedUtc)
    VALUES (p_user_id, 0, 0, 0, 0, UTC_TIMESTAMP())
    ON DUPLICATE KEY UPDATE
        Stars = 0,
        Xp = 0,
        SkipAnimationUnlocked = 0,
        MultiOpenLevel = 0,
        UpdatedUtc = UTC_TIMESTAMP();

    COMMIT;
END//

DELIMITER ;
