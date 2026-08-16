-- PersonalTools database update: 16 August 2026
--
-- This is non-destructive. It adds the procedure used by the dashboard calendar
-- to load only the signed-in user's matches within the visible date range.
-- Run this against the existing PersonalTools database in HeidiSQL.

USE PersonalTools;

DROP PROCEDURE IF EXISTS sp_cs_matches_get_range;

DELIMITER $$

CREATE PROCEDURE sp_cs_matches_get_range
(
    IN p_user_id CHAR(36),
    IN p_start_utc DATETIME,
    IN p_end_utc DATETIME
)
BEGIN
    -- The UserId predicate is deliberately inside the procedure so calendar
    -- requests can never return another account's saved match history.
    SELECT
        MatchId,
        StartSide,
        MapName,
        GameType,
        TeamScore,
        OpponentScore,
        OvertimeCount,
        LeetifyMatchId,
        CreatedUtc,
        UpdatedUtc
    FROM CSMatches
    WHERE UserId = p_user_id
      AND CreatedUtc >= p_start_utc
      AND CreatedUtc < p_end_utc
    ORDER BY CreatedUtc DESC;
END$$

DELIMITER ;
