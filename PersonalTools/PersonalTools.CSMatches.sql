/*
    PersonalTools - CS Match Tracker (database-backed matches + multi-profile tabs)
    ------------------------------------------------------------
    Run this entire script in a HeidiSQL query tab while connected
    to the MariaDB server.

    Requirements:
      - The PersonalTools database already exists.
      - The Users table already exists.

    This script is safe to run again:
      - Tables use CREATE TABLE IF NOT EXISTS.
      - Procedures are dropped and recreated.
      - Existing CSMatches and CSMatchProfiles rows are not deleted.
*/

USE PersonalTools;

CREATE TABLE IF NOT EXISTS CSMatchProfiles
(
    ProfileId CHAR(36) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    Name VARCHAR(100) NOT NULL,
    SteamId CHAR(17) NOT NULL,
    CreatedUtc DATETIME NOT NULL,
    PRIMARY KEY (ProfileId),
    KEY IX_CSMatchProfiles_UserId (UserId),
    CONSTRAINT FK_CSMatchProfiles_Users
        FOREIGN KEY (UserId)
        REFERENCES Users (UserId)
        ON DELETE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS CSMatches
(
    MatchId CHAR(36) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    ProfileId CHAR(36) NULL COMMENT 'NULL = the default "You" profile (uses Users.SteamId)',
    StartSide VARCHAR(2) NOT NULL,
    MapName VARCHAR(100) NOT NULL,
    GameType VARCHAR(50) NOT NULL,
    TeamScore SMALLINT UNSIGNED NOT NULL,
    OpponentScore SMALLINT UNSIGNED NOT NULL,
    OvertimeCount SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    LeetifyMatchId VARCHAR(64) NULL,
    CreatedUtc DATETIME NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (MatchId),
    KEY IX_CSMatches_UserId_ProfileId (UserId, ProfileId),
    CONSTRAINT FK_CSMatches_Users
        FOREIGN KEY (UserId)
        REFERENCES Users (UserId)
        ON DELETE CASCADE,
    CONSTRAINT FK_CSMatches_CSMatchProfiles
        FOREIGN KEY (ProfileId)
        REFERENCES CSMatchProfiles (ProfileId)
        ON DELETE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_cs_match_profiles_get$$
CREATE PROCEDURE sp_cs_match_profiles_get(IN p_user_id BIGINT UNSIGNED)
BEGIN
    SELECT
        ProfileId,
        Name,
        SteamId,
        CreatedUtc
    FROM CSMatchProfiles
    WHERE UserId = p_user_id
    ORDER BY CreatedUtc;
END$$

DROP PROCEDURE IF EXISTS sp_cs_match_profiles_create$$
CREATE PROCEDURE sp_cs_match_profiles_create
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_profile_id CHAR(36),
    IN p_name VARCHAR(100),
    IN p_steam_id CHAR(17)
)
BEGIN
    INSERT INTO CSMatchProfiles
    (
        ProfileId,
        UserId,
        Name,
        SteamId,
        CreatedUtc
    )
    VALUES
    (
        p_profile_id,
        p_user_id,
        p_name,
        p_steam_id,
        UTC_TIMESTAMP()
    );
END$$

DROP PROCEDURE IF EXISTS sp_cs_match_profiles_update$$
CREATE PROCEDURE sp_cs_match_profiles_update
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_profile_id CHAR(36),
    IN p_name VARCHAR(100),
    IN p_steam_id CHAR(17)
)
BEGIN
    UPDATE CSMatchProfiles
    SET
        Name = p_name,
        SteamId = p_steam_id
    WHERE ProfileId = p_profile_id
      AND UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_cs_match_profiles_delete$$
CREATE PROCEDURE sp_cs_match_profiles_delete
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_profile_id CHAR(36)
)
BEGIN
    DELETE FROM CSMatchProfiles
    WHERE ProfileId = p_profile_id
      AND UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_cs_matches_get$$
CREATE PROCEDURE sp_cs_matches_get
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_profile_id CHAR(36)
)
BEGIN
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
      AND ProfileId <=> p_profile_id
    ORDER BY CreatedUtc DESC;
END$$

DROP PROCEDURE IF EXISTS sp_cs_matches_create$$
CREATE PROCEDURE sp_cs_matches_create
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_match_id CHAR(36),
    IN p_profile_id CHAR(36),
    IN p_start_side VARCHAR(2),
    IN p_map_name VARCHAR(100),
    IN p_game_type VARCHAR(50),
    IN p_team_score SMALLINT UNSIGNED,
    IN p_opponent_score SMALLINT UNSIGNED,
    IN p_overtime_count SMALLINT UNSIGNED,
    IN p_leetify_match_id VARCHAR(64),
    IN p_created_utc DATETIME
)
BEGIN
    INSERT INTO CSMatches
    (
        MatchId,
        UserId,
        ProfileId,
        StartSide,
        MapName,
        GameType,
        TeamScore,
        OpponentScore,
        OvertimeCount,
        LeetifyMatchId,
        CreatedUtc,
        UpdatedUtc
    )
    VALUES
    (
        p_match_id,
        p_user_id,
        p_profile_id,
        p_start_side,
        p_map_name,
        p_game_type,
        p_team_score,
        p_opponent_score,
        p_overtime_count,
        NULLIF(p_leetify_match_id, ''),
        COALESCE(p_created_utc, UTC_TIMESTAMP()),
        UTC_TIMESTAMP()
    );
END$$

DROP PROCEDURE IF EXISTS sp_cs_matches_update$$
CREATE PROCEDURE sp_cs_matches_update
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_match_id CHAR(36),
    IN p_start_side VARCHAR(2),
    IN p_map_name VARCHAR(100),
    IN p_game_type VARCHAR(50),
    IN p_team_score SMALLINT UNSIGNED,
    IN p_opponent_score SMALLINT UNSIGNED,
    IN p_overtime_count SMALLINT UNSIGNED
)
BEGIN
    UPDATE CSMatches
    SET
        StartSide = p_start_side,
        MapName = p_map_name,
        GameType = p_game_type,
        TeamScore = p_team_score,
        OpponentScore = p_opponent_score,
        OvertimeCount = p_overtime_count,
        UpdatedUtc = UTC_TIMESTAMP()
    WHERE MatchId = p_match_id
      AND UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_cs_matches_delete$$
CREATE PROCEDURE sp_cs_matches_delete
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_match_id CHAR(36)
)
BEGIN
    DELETE FROM CSMatches
    WHERE MatchId = p_match_id
      AND UserId = p_user_id;
END$$

DROP PROCEDURE IF EXISTS sp_cs_matches_delete_all$$
CREATE PROCEDURE sp_cs_matches_delete_all
(
    IN p_user_id BIGINT UNSIGNED,
    IN p_profile_id CHAR(36)
)
BEGIN
    DELETE FROM CSMatches
    WHERE UserId = p_user_id
      AND ProfileId <=> p_profile_id;
END$$

DELIMITER ;

/*
    Optional verification after running this script:

    SHOW TABLES LIKE 'CSMatches';
    SHOW TABLES LIKE 'CSMatchProfiles';
    SHOW PROCEDURE STATUS
    WHERE Db = DATABASE()
      AND Name IN
      (
          'sp_cs_matches_get',
          'sp_cs_matches_create',
          'sp_cs_matches_update',
          'sp_cs_matches_delete',
          'sp_cs_matches_delete_all',
          'sp_cs_match_profiles_get',
          'sp_cs_match_profiles_create',
          'sp_cs_match_profiles_update',
          'sp_cs_match_profiles_delete'
      );
*/
