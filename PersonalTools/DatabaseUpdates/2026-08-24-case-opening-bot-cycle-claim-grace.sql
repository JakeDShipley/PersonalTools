-- CS2 Case Simulator: give the bot-cycle cooldown check a small grace window.
-- Non-destructive - safe to re-run.
--
-- Firing every bot's open request in the same tick can push a bot's actual claim a second or two
-- late within its cycle (browser HTTP/1.1 connection limits queue most of them), while the next
-- cycle still fires at the original fixed interval - so that bot looks like it's "still cooling
-- down" even though nothing is wrong. The client now staggers its requests to avoid this, but this
-- grace window is a safety net against ordinary jitter regardless of client-side timing.

USE PersonalTools;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_bot_cycle_claim//
CREATE PROCEDURE sp_case_opening_bot_cycle_claim(IN p_user_id CHAR(36), IN p_bot_id CHAR(36))
BEGIN
    DECLARE v_interval_seconds INT DEFAULT 12;
    DECLARE v_grace_seconds INT DEFAULT 2;

    SELECT BotOpeningIntervalSeconds INTO v_interval_seconds
    FROM CaseOpeningGameSettings
    WHERE Id = 1;

    UPDATE CaseOpeningBots
    SET LastOpenedUtc = UTC_TIMESTAMP(6)
    WHERE BotId = p_bot_id
      AND UserId = p_user_id
      AND (LastOpenedUtc IS NULL OR LastOpenedUtc <= DATE_SUB(UTC_TIMESTAMP(6), INTERVAL GREATEST(1, v_interval_seconds - v_grace_seconds) SECOND));

    SELECT ROW_COUNT();
END//

DELIMITER ;
