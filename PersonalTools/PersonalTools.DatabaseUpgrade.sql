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

-- Bug/Feature Tracker: shared across every account (it tracks Personal Tools itself, not
-- personal data), so CreatedByUserId is attribution only - not used to scope any query - and
-- is nulled rather than cascade-deleted if that account is later removed.
CREATE TABLE IF NOT EXISTS TrackerItems (
    ItemId CHAR(36) NOT NULL,
    Type VARCHAR(10) NOT NULL,
    Title VARCHAR(200) NOT NULL,
    Description TEXT NOT NULL,
    Area VARCHAR(50) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Open',
    SortOrder INT NOT NULL DEFAULT 0,
    CreatedByUserId CHAR(36) NULL,
    AssignedToUserId CHAR(36) NULL,
    ResolvedUtc DATETIME NULL,
    ShowOnDashboard TINYINT(1) NOT NULL DEFAULT 0,
    CreatedUtc DATETIME NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (ItemId),
    KEY IX_TrackerItems_Status_SortOrder (Status, SortOrder),
    CONSTRAINT FK_TrackerItems_Users FOREIGN KEY (CreatedByUserId) REFERENCES Users(UserId) ON DELETE SET NULL,
    CONSTRAINT FK_TrackerItems_AssignedTo FOREIGN KEY (AssignedToUserId) REFERENCES Users(UserId) ON DELETE SET NULL
);

-- Adds columns for items/installs created before these features existed - safe to re-run.
ALTER TABLE TrackerItems ADD COLUMN IF NOT EXISTS AssignedToUserId CHAR(36) NULL AFTER CreatedByUserId;
ALTER TABLE TrackerItems ADD COLUMN IF NOT EXISTS ResolvedUtc DATETIME NULL AFTER AssignedToUserId;
ALTER TABLE TrackerItems ADD COLUMN IF NOT EXISTS ShowOnDashboard TINYINT(1) NOT NULL DEFAULT 0 AFTER ResolvedUtc;

-- Singleton config row (Id is always 1) for Tracker-wide settings - shared across every account,
-- same as TrackerItems itself.
CREATE TABLE IF NOT EXISTS TrackerSettings (
    Id TINYINT NOT NULL,
    AutoCloseAfterDays INT NOT NULL DEFAULT 5,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (Id)
);
INSERT IGNORE INTO TrackerSettings (Id, AutoCloseAfterDays, UpdatedUtc) VALUES (1, 5, UTC_TIMESTAMP());

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_cs_match_profiles_get$$
DROP PROCEDURE IF EXISTS sp_cs_match_profiles_create$$
DROP PROCEDURE IF EXISTS sp_cs_match_profiles_update$$
DROP PROCEDURE IF EXISTS sp_cs_match_profiles_delete$$
DROP PROCEDURE IF EXISTS sp_cs_matches_get$$
DROP PROCEDURE IF EXISTS sp_cs_matches_get_range$$
DROP PROCEDURE IF EXISTS sp_cs_matches_create$$
DROP PROCEDURE IF EXISTS sp_cs_matches_update$$
DROP PROCEDURE IF EXISTS sp_cs_matches_delete$$
DROP PROCEDURE IF EXISTS sp_cs_matches_delete_all$$
DROP PROCEDURE IF EXISTS sp_cs_active_duty_maps_get$$
DROP PROCEDURE IF EXISTS sp_cs_active_duty_maps_set$$
DROP PROCEDURE IF EXISTS sp_tracker_items_get$$
DROP PROCEDURE IF EXISTS sp_tracker_items_create$$
DROP PROCEDURE IF EXISTS sp_tracker_items_update$$
DROP PROCEDURE IF EXISTS sp_tracker_items_move$$
DROP PROCEDURE IF EXISTS sp_tracker_items_set_status$$
DROP PROCEDURE IF EXISTS sp_tracker_items_delete$$
DROP PROCEDURE IF EXISTS sp_tracker_items_auto_close$$
DROP PROCEDURE IF EXISTS sp_tracker_items_get_closed$$
DROP PROCEDURE IF EXISTS sp_tracker_assignees_get$$
DROP PROCEDURE IF EXISTS sp_tracker_settings_get$$
DROP PROCEDURE IF EXISTS sp_tracker_settings_set$$
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
CREATE PROCEDURE sp_cs_matches_get_range(IN p_user_id CHAR(36), IN p_start_utc DATETIME, IN p_end_utc DATETIME)
SELECT MatchId, StartSide, MapName, GameType, TeamScore, OpponentScore, OvertimeCount, LeetifyMatchId, CreatedUtc, UpdatedUtc FROM CSMatches WHERE UserId = p_user_id AND CreatedUtc >= p_start_utc AND CreatedUtc < p_end_utc ORDER BY CreatedUtc DESC$$
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

CREATE PROCEDURE sp_tracker_items_get()
SELECT t.ItemId, t.Type, t.Title, t.Description, t.Area, t.Status, t.SortOrder,
       creator.DisplayName AS CreatedByDisplayName,
       t.AssignedToUserId, assignee.DisplayName AS AssignedToDisplayName,
       t.ShowOnDashboard, t.CreatedUtc, t.UpdatedUtc
FROM TrackerItems t
LEFT JOIN Users creator ON creator.UserId = t.CreatedByUserId
LEFT JOIN Users assignee ON assignee.UserId = t.AssignedToUserId
WHERE t.Status <> 'Closed'
ORDER BY t.Status, t.SortOrder, t.CreatedUtc$$
CREATE PROCEDURE sp_tracker_items_create(IN p_item_id CHAR(36), IN p_type VARCHAR(10), IN p_title VARCHAR(200), IN p_description TEXT, IN p_area VARCHAR(50), IN p_created_by_user_id CHAR(36), IN p_assigned_to_user_id CHAR(36), IN p_show_on_dashboard TINYINT(1))
INSERT INTO TrackerItems(ItemId, Type, Title, Description, Area, Status, SortOrder, CreatedByUserId, AssignedToUserId, ShowOnDashboard, CreatedUtc, UpdatedUtc)
SELECT p_item_id, p_type, p_title, p_description, p_area, 'Open', COALESCE(MAX(SortOrder) + 1, 0), p_created_by_user_id, NULLIF(p_assigned_to_user_id, ''), p_show_on_dashboard, UTC_TIMESTAMP(), UTC_TIMESTAMP()
FROM TrackerItems WHERE Status = 'Open'$$
CREATE PROCEDURE sp_tracker_items_update(IN p_item_id CHAR(36), IN p_type VARCHAR(10), IN p_title VARCHAR(200), IN p_description TEXT, IN p_area VARCHAR(50), IN p_status VARCHAR(20), IN p_assigned_to_user_id CHAR(36), IN p_show_on_dashboard TINYINT(1))
BEGIN
    -- ResolvedUtc marks when an item most recently became Resolved, so the auto-close job can
    -- measure "days since resolved" rather than "days since last touched" (which a drag reorder
    -- or an unrelated field edit would otherwise incorrectly reset).
    UPDATE TrackerItems t
    SET t.ResolvedUtc = CASE WHEN p_status = 'Resolved' AND t.Status <> 'Resolved' THEN UTC_TIMESTAMP()
                              WHEN p_status <> 'Resolved' THEN NULL
                              ELSE t.ResolvedUtc END,
        t.Type = p_type, t.Title = p_title, t.Description = p_description, t.Area = p_area,
        t.SortOrder = IF(t.Status = p_status, t.SortOrder, COALESCE((SELECT MAX(o.SortOrder) + 1 FROM (SELECT SortOrder FROM TrackerItems WHERE Status = p_status) AS o), 0)),
        t.Status = p_status, t.AssignedToUserId = NULLIF(p_assigned_to_user_id, ''), t.ShowOnDashboard = p_show_on_dashboard, t.UpdatedUtc = UTC_TIMESTAMP()
    WHERE t.ItemId = p_item_id;
END$$
CREATE PROCEDURE sp_tracker_items_move(IN p_item_id CHAR(36), IN p_status VARCHAR(20), IN p_item_ids JSON)
BEGIN
    UPDATE TrackerItems
    SET ResolvedUtc = CASE WHEN p_status = 'Resolved' AND Status <> 'Resolved' THEN UTC_TIMESTAMP()
                            WHEN p_status <> 'Resolved' THEN NULL
                            ELSE ResolvedUtc END,
        Status = p_status, UpdatedUtc = UTC_TIMESTAMP()
    WHERE ItemId = p_item_id;
    UPDATE TrackerItems t
    INNER JOIN JSON_TABLE(p_item_ids, '$[*]' COLUMNS(SortOrder FOR ORDINALITY, ItemId CHAR(36) PATH '$')) p ON p.ItemId = t.ItemId
    SET t.SortOrder = p.SortOrder;
END$$
CREATE PROCEDURE sp_tracker_items_set_status(IN p_item_id CHAR(36), IN p_status VARCHAR(20))
BEGIN
    -- Lightweight status-only move for the dashboard's compact board, which only ever has a
    -- partial (most-recent-first) view of a column - it can't safely submit a full ordered id
    -- list the way the full board's drag-and-drop does, so this just appends to the end instead.
    UPDATE TrackerItems t
    SET t.ResolvedUtc = CASE WHEN p_status = 'Resolved' AND t.Status <> 'Resolved' THEN UTC_TIMESTAMP()
                              WHEN p_status <> 'Resolved' THEN NULL
                              ELSE t.ResolvedUtc END,
        t.SortOrder = IF(t.Status = p_status, t.SortOrder, COALESCE((SELECT MAX(o.SortOrder) + 1 FROM (SELECT SortOrder FROM TrackerItems WHERE Status = p_status) AS o), 0)),
        t.Status = p_status, t.UpdatedUtc = UTC_TIMESTAMP()
    WHERE t.ItemId = p_item_id;
END$$
CREATE PROCEDURE sp_tracker_items_delete(IN p_item_id CHAR(36))
DELETE FROM TrackerItems WHERE ItemId = p_item_id$$
CREATE PROCEDURE sp_tracker_items_auto_close(IN p_days INT)
UPDATE TrackerItems SET Status = 'Closed', UpdatedUtc = UTC_TIMESTAMP()
WHERE Status = 'Resolved' AND ResolvedUtc IS NOT NULL AND ResolvedUtc <= UTC_TIMESTAMP() - INTERVAL p_days DAY$$
CREATE PROCEDURE sp_tracker_items_get_closed()
SELECT t.ItemId, t.Type, t.Title, t.Description, t.Area, t.Status, t.SortOrder,
       creator.DisplayName AS CreatedByDisplayName,
       t.AssignedToUserId, assignee.DisplayName AS AssignedToDisplayName,
       t.ShowOnDashboard, t.CreatedUtc, t.UpdatedUtc
FROM TrackerItems t
LEFT JOIN Users creator ON creator.UserId = t.CreatedByUserId
LEFT JOIN Users assignee ON assignee.UserId = t.AssignedToUserId
WHERE t.Status = 'Closed'
ORDER BY t.UpdatedUtc DESC$$
CREATE PROCEDURE sp_tracker_assignees_get()
SELECT UserId, DisplayName FROM Users WHERE IsActive = 1 ORDER BY DisplayName$$
CREATE PROCEDURE sp_tracker_settings_get()
SELECT AutoCloseAfterDays FROM TrackerSettings WHERE Id = 1$$
CREATE PROCEDURE sp_tracker_settings_set(IN p_auto_close_after_days INT)
UPDATE TrackerSettings SET AutoCloseAfterDays = p_auto_close_after_days, UpdatedUtc = UTC_TIMESTAMP() WHERE Id = 1$$

CREATE PROCEDURE sp_monitor_database_snapshot()
BEGIN
    SELECT CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='UPTIME'), 0) AS UNSIGNED) AS UptimeSeconds,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='THREADS_CONNECTED'), 0) AS UNSIGNED) AS ThreadsConnected,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='THREADS_RUNNING'), 0) AS UNSIGNED) AS ThreadsRunning,
           CAST(@@max_connections AS UNSIGNED) AS MaxConnections,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='QUESTIONS'), 0) AS UNSIGNED) AS Questions,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='SLOW_QUERIES'), 0) AS UNSIGNED) AS SlowQueries,
           CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='ABORTED_CONNECTS'), 0) AS UNSIGNED) AS AbortedConnects,
           (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('Users','UserSessions','QuickLinks','Notes','TrackedSkins','DashboardWidgetOrders','DashboardWeatherLocations','CSMatches','CSMatchProfiles','CSPlayerReports','AppSettings','CSActiveDutyMaps','TrackerItems','TrackerSettings')) AS RequiredStructuresAvailable,
           14 AS RequiredStructuresTotal;
END$$

DELIMITER ;
