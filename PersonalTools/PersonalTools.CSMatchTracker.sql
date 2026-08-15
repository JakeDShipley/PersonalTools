USE PersonalTools;

CREATE TABLE IF NOT EXISTS CSMatches (
    MatchId CHAR(36) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    StartSide VARCHAR(2) NOT NULL,
    MapName VARCHAR(100) NOT NULL,
    GameType VARCHAR(100) NOT NULL,
    TeamScore INT NOT NULL,
    OpponentScore INT NOT NULL,
    OvertimeCount INT NOT NULL DEFAULT 0,
    LeetifyMatchId VARCHAR(100) NULL,
    PlayedUtc DATETIME NOT NULL,
    CreatedUtc DATETIME NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (MatchId),
    KEY IX_CSMatches_UserId_PlayedUtc (UserId, PlayedUtc),
    UNIQUE KEY UX_CSMatches_UserId_LeetifyMatchId (UserId, LeetifyMatchId),
    CONSTRAINT FK_CSMatches_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_cs_matches_get$$
CREATE PROCEDURE sp_cs_matches_get(IN p_user_id BIGINT)
SELECT MatchId, StartSide, MapName, GameType, TeamScore, OpponentScore, OvertimeCount, LeetifyMatchId, PlayedUtc AS Created, UpdatedUtc AS Updated
FROM CSMatches
WHERE UserId = p_user_id
ORDER BY PlayedUtc DESC, CreatedUtc DESC$$

DROP PROCEDURE IF EXISTS sp_cs_matches_get_range$$
CREATE PROCEDURE sp_cs_matches_get_range(IN p_user_id BIGINT, IN p_start_utc DATETIME, IN p_end_utc DATETIME)
SELECT MatchId, StartSide, MapName, GameType, TeamScore, OpponentScore, OvertimeCount, LeetifyMatchId, PlayedUtc AS Created, UpdatedUtc AS Updated
FROM CSMatches
WHERE UserId = p_user_id AND PlayedUtc >= p_start_utc AND PlayedUtc < p_end_utc
ORDER BY PlayedUtc DESC$$

DROP PROCEDURE IF EXISTS sp_cs_matches_create$$
CREATE PROCEDURE sp_cs_matches_create(IN p_user_id BIGINT, IN p_match_id CHAR(36), IN p_start_side VARCHAR(2), IN p_map_name VARCHAR(100), IN p_game_type VARCHAR(100), IN p_team_score INT, IN p_opponent_score INT, IN p_overtime_count INT, IN p_leetify_match_id VARCHAR(100), IN p_played_utc DATETIME)
BEGIN
    INSERT IGNORE INTO CSMatches(MatchId, UserId, StartSide, MapName, GameType, TeamScore, OpponentScore, OvertimeCount, LeetifyMatchId, PlayedUtc, CreatedUtc, UpdatedUtc)
    VALUES(p_match_id, p_user_id, p_start_side, p_map_name, p_game_type, p_team_score, p_opponent_score, p_overtime_count, NULLIF(p_leetify_match_id, ''), p_played_utc, UTC_TIMESTAMP(), UTC_TIMESTAMP());
END$$

DROP PROCEDURE IF EXISTS sp_cs_matches_update$$
CREATE PROCEDURE sp_cs_matches_update(IN p_user_id BIGINT, IN p_match_id CHAR(36), IN p_start_side VARCHAR(2), IN p_map_name VARCHAR(100), IN p_game_type VARCHAR(100), IN p_team_score INT, IN p_opponent_score INT, IN p_overtime_count INT)
UPDATE CSMatches
SET StartSide=p_start_side, MapName=p_map_name, GameType=p_game_type, TeamScore=p_team_score, OpponentScore=p_opponent_score, OvertimeCount=p_overtime_count, UpdatedUtc=UTC_TIMESTAMP()
WHERE MatchId=p_match_id AND UserId=p_user_id$$

DROP PROCEDURE IF EXISTS sp_cs_matches_delete$$
CREATE PROCEDURE sp_cs_matches_delete(IN p_user_id BIGINT, IN p_match_id CHAR(36))
DELETE FROM CSMatches WHERE MatchId=p_match_id AND UserId=p_user_id$$

DROP PROCEDURE IF EXISTS sp_cs_matches_delete_all$$
CREATE PROCEDURE sp_cs_matches_delete_all(IN p_user_id BIGINT)
DELETE FROM CSMatches WHERE UserId=p_user_id$$

DELIMITER ;
