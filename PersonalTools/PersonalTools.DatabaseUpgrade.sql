-- PersonalTools database upgrade: GUID-safe CS Match Tracker profiles + Active Duty map pool.
-- Run this complete file once in HeidiSQL against the existing PersonalTools database.
-- It does not delete users, sessions, notes, skins, matches, or other application data.

USE PersonalTools;

CREATE TABLE IF NOT EXISTS CSMatchProfiles (
    ProfileId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    Name VARCHAR(100) NOT NULL,
    SteamId CHAR(17) NOT NULL,
    AvatarUrl VARCHAR(2048) NULL,
    CreatedUtc DATETIME NOT NULL,
    PRIMARY KEY (ProfileId),
    KEY IX_CSMatchProfiles_UserId (UserId),
    CONSTRAINT FK_CSMatchProfiles_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

-- Adds the avatar column for profiles created before this feature existed - safe to re-run.
ALTER TABLE CSMatchProfiles ADD COLUMN IF NOT EXISTS AvatarUrl VARCHAR(2048) NULL AFTER SteamId;

ALTER TABLE CSMatches ADD COLUMN IF NOT EXISTS ProfileId CHAR(36) NULL AFTER UserId;
ALTER TABLE CSMatches ADD INDEX IF NOT EXISTS IX_CSMatches_UserId_ProfileId (UserId, ProfileId);

-- Remove every historical unique index involving LeetifyMatchId, regardless of
-- the name used when it was created, before adding the correctly scoped index.
SELECT GROUP_CONCAT(CONCAT('DROP INDEX `', REPLACE(IndexName, '`', '``'), '`') SEPARATOR ', ')
INTO @LeetifyIndexDrops
FROM (
    SELECT DISTINCT INDEX_NAME AS IndexName
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'CSMatches'
      AND COLUMN_NAME = 'LeetifyMatchId'
      AND NON_UNIQUE = 0
      AND INDEX_NAME <> 'PRIMARY'
) AS ExistingLeetifyIndexes;
SET @LeetifyIndexSql = IFNULL(CONCAT('ALTER TABLE CSMatches ', @LeetifyIndexDrops), 'SELECT 1');
PREPARE LeetifyIndexStatement FROM @LeetifyIndexSql;
EXECUTE LeetifyIndexStatement;
DEALLOCATE PREPARE LeetifyIndexStatement;

ALTER TABLE CSMatches ADD UNIQUE INDEX IF NOT EXISTS UX_CSMatches_UserId_ProfileId_LeetifyMatchId (UserId, ProfileId, LeetifyMatchId);

CREATE TABLE IF NOT EXISTS CSActiveDutyMaps (
    MapPoolId CHAR(36) NOT NULL,
    MapName VARCHAR(80) NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (MapPoolId),
    UNIQUE KEY UX_CSActiveDutyMaps_MapName (MapName)
);

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_cs_match_profiles_get$$
DROP PROCEDURE IF EXISTS sp_cs_match_profiles_create$$
DROP PROCEDURE IF EXISTS sp_cs_match_profiles_update$$
DROP PROCEDURE IF EXISTS sp_cs_match_profiles_delete$$
DROP PROCEDURE IF EXISTS sp_cs_matches_get$$
DROP PROCEDURE IF EXISTS sp_cs_matches_create$$
DROP PROCEDURE IF EXISTS sp_cs_matches_update$$
DROP PROCEDURE IF EXISTS sp_cs_matches_delete$$
DROP PROCEDURE IF EXISTS sp_cs_matches_delete_all$$
DROP PROCEDURE IF EXISTS sp_cs_active_duty_maps_get$$
DROP PROCEDURE IF EXISTS sp_cs_active_duty_maps_set$$
DROP PROCEDURE IF EXISTS sp_monitor_database_snapshot$$

CREATE PROCEDURE sp_cs_match_profiles_get(IN p_user_id CHAR(36))
SELECT ProfileId, Name, SteamId, AvatarUrl, CreatedUtc FROM CSMatchProfiles WHERE UserId = p_user_id ORDER BY CreatedUtc$$
CREATE PROCEDURE sp_cs_match_profiles_create(IN p_user_id CHAR(36), IN p_profile_id CHAR(36), IN p_name VARCHAR(100), IN p_steam_id CHAR(17), IN p_avatar_url VARCHAR(2048))
INSERT INTO CSMatchProfiles(ProfileId, UserId, Name, SteamId, AvatarUrl, CreatedUtc) VALUES(p_profile_id, p_user_id, p_name, p_steam_id, NULLIF(p_avatar_url, ''), UTC_TIMESTAMP())$$
CREATE PROCEDURE sp_cs_match_profiles_update(IN p_user_id CHAR(36), IN p_profile_id CHAR(36), IN p_name VARCHAR(100), IN p_steam_id CHAR(17), IN p_avatar_url VARCHAR(2048))
UPDATE CSMatchProfiles SET Name = p_name, SteamId = p_steam_id, AvatarUrl = NULLIF(p_avatar_url, '') WHERE ProfileId = p_profile_id AND UserId = p_user_id$$
CREATE PROCEDURE sp_cs_match_profiles_delete(IN p_user_id CHAR(36), IN p_profile_id CHAR(36))
DELETE FROM CSMatchProfiles WHERE ProfileId = p_profile_id AND UserId = p_user_id$$

CREATE PROCEDURE sp_cs_matches_get(IN p_user_id CHAR(36), IN p_profile_id CHAR(36))
SELECT MatchId, StartSide, MapName, GameType, TeamScore, OpponentScore, OvertimeCount, LeetifyMatchId, CreatedUtc, UpdatedUtc FROM CSMatches WHERE UserId = p_user_id AND ProfileId <=> p_profile_id ORDER BY CreatedUtc DESC$$
CREATE PROCEDURE sp_cs_matches_create(IN p_user_id CHAR(36), IN p_match_id CHAR(36), IN p_profile_id CHAR(36), IN p_start_side VARCHAR(2), IN p_map_name VARCHAR(100), IN p_game_type VARCHAR(100), IN p_team_score INT, IN p_opponent_score INT, IN p_overtime_count INT, IN p_leetify_match_id VARCHAR(100), IN p_created_utc DATETIME)
INSERT IGNORE INTO CSMatches(MatchId, UserId, ProfileId, StartSide, MapName, GameType, TeamScore, OpponentScore, OvertimeCount, LeetifyMatchId, PlayedUtc, CreatedUtc, UpdatedUtc) VALUES(p_match_id, p_user_id, p_profile_id, p_start_side, p_map_name, p_game_type, p_team_score, p_opponent_score, p_overtime_count, NULLIF(p_leetify_match_id, ''), COALESCE(p_created_utc, UTC_TIMESTAMP()), COALESCE(p_created_utc, UTC_TIMESTAMP()), UTC_TIMESTAMP())$$
CREATE PROCEDURE sp_cs_matches_update(IN p_user_id CHAR(36), IN p_match_id CHAR(36), IN p_start_side VARCHAR(2), IN p_map_name VARCHAR(100), IN p_game_type VARCHAR(100), IN p_team_score INT, IN p_opponent_score INT, IN p_overtime_count INT)
UPDATE CSMatches SET StartSide = p_start_side, MapName = p_map_name, GameType = p_game_type, TeamScore = p_team_score, OpponentScore = p_opponent_score, OvertimeCount = p_overtime_count, UpdatedUtc = UTC_TIMESTAMP() WHERE MatchId = p_match_id AND UserId = p_user_id$$
CREATE PROCEDURE sp_cs_matches_delete(IN p_user_id CHAR(36), IN p_match_id CHAR(36))
DELETE FROM CSMatches WHERE MatchId = p_match_id AND UserId = p_user_id$$
CREATE PROCEDURE sp_cs_matches_delete_all(IN p_user_id CHAR(36), IN p_profile_id CHAR(36))
DELETE FROM CSMatches WHERE UserId = p_user_id AND ProfileId <=> p_profile_id$$

CREATE PROCEDURE sp_cs_active_duty_maps_get()
SELECT MapName FROM CSActiveDutyMaps ORDER BY MapName$$
CREATE PROCEDURE sp_cs_active_duty_maps_set(IN p_map_names JSON)
BEGIN
    DELETE FROM CSActiveDutyMaps;
    INSERT INTO CSActiveDutyMaps(MapPoolId, MapName, UpdatedUtc)
    SELECT UUID(), selected.MapName, UTC_TIMESTAMP()
    FROM JSON_TABLE(p_map_names, '$[*]' COLUMNS(MapName VARCHAR(80) PATH '$')) AS selected
    WHERE selected.MapName IS NOT NULL AND CHAR_LENGTH(TRIM(selected.MapName)) > 0;
END$$

CREATE PROCEDURE sp_monitor_database_snapshot()
BEGIN
    SELECT CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='UPTIME'), 0) AS UNSIGNED) AS UptimeSeconds,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='THREADS_CONNECTED'), 0) AS UNSIGNED) AS ThreadsConnected,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='THREADS_RUNNING'), 0) AS UNSIGNED) AS ThreadsRunning,
           CAST(@@max_connections AS UNSIGNED) AS MaxConnections,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='QUESTIONS'), 0) AS UNSIGNED) AS Questions,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='SLOW_QUERIES'), 0) AS UNSIGNED) AS SlowQueries,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='ABORTED_CONNECTS'), 0) AS UNSIGNED) AS AbortedConnects,
           (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('Users','UserSessions','QuickLinks','Notes','TrackedSkins','DashboardWidgetOrders','DashboardWeatherLocations','CSMatches','CSMatchProfiles','CSPlayerReports','AppSettings','CSActiveDutyMaps')) AS RequiredStructuresAvailable,
           12 AS RequiredStructuresTotal;
END$$

DELIMITER ;
