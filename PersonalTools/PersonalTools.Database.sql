CREATE DATABASE IF NOT EXISTS PersonalTools CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE PersonalTools;

CREATE TABLE IF NOT EXISTS Users (
    UserId BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    Email VARCHAR(254) NOT NULL,
    DisplayName VARCHAR(100) NOT NULL,
    PasswordHash VARCHAR(512) NOT NULL,
    SteamId CHAR(17) NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedUtc DATETIME NOT NULL,
    PRIMARY KEY (UserId), UNIQUE KEY UX_Users_Email (Email), UNIQUE KEY UX_Users_SteamId (SteamId)
);
CREATE TABLE IF NOT EXISTS UserSessions (
    SessionId CHAR(32) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    TokenHash CHAR(64) NOT NULL,
    ExpiresUtc DATETIME NOT NULL,
    UserAgent VARCHAR(512) NOT NULL,
    CreatedUtc DATETIME NOT NULL,
    PRIMARY KEY (SessionId), KEY IX_UserSessions_UserId (UserId), KEY IX_UserSessions_ExpiresUtc (ExpiresUtc),
    CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS QuickLinks (
    QuickLinkId BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    UserId BIGINT UNSIGNED NOT NULL,
    Title VARCHAR(100) NOT NULL,
    Url VARCHAR(2048) NOT NULL,
    IconClass VARCHAR(100) NULL,
    CreatedUtc DATETIME NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (QuickLinkId), KEY IX_QuickLinks_UserId (UserId),
    CONSTRAINT FK_QuickLinks_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS Notes (
    NoteId CHAR(36) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    Title VARCHAR(200) NOT NULL,
    Body MEDIUMTEXT NOT NULL,
    SortOrder INT NOT NULL DEFAULT 0,
    CreatedUtc DATETIME NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (NoteId), KEY IX_Notes_UserId_SortOrder (UserId, SortOrder),
    CONSTRAINT FK_Notes_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS TrackedSkins (
    SkinId CHAR(36) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    Name VARCHAR(200) NOT NULL,
    Weapon VARCHAR(100) NOT NULL,
    Exterior VARCHAR(100) NOT NULL,
    MarketHashName VARCHAR(255) NOT NULL,
    ExternalImageUrl VARCHAR(2048) NOT NULL,
    PurchasePrice DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    CurrentPrice DECIMAL(12,2) NULL,
    PurchaseDate DATE NULL,
    Notes TEXT NOT NULL,
    CreatedUtc DATETIME NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (SkinId), KEY IX_TrackedSkins_UserId_UpdatedUtc (UserId, UpdatedUtc),
    CONSTRAINT FK_TrackedSkins_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
DELIMITER $$
DROP PROCEDURE IF EXISTS sp_auth_user_count$$
CREATE PROCEDURE sp_auth_user_count() SELECT COUNT(*) FROM Users$$
DROP PROCEDURE IF EXISTS sp_auth_user_get_by_email$$
CREATE PROCEDURE sp_auth_user_get_by_email(IN p_email VARCHAR(254)) SELECT UserId,Email,DisplayName,PasswordHash,IsActive,SteamId FROM Users WHERE Email=p_email LIMIT 1$$
DROP PROCEDURE IF EXISTS sp_auth_user_get_by_id$$
CREATE PROCEDURE sp_auth_user_get_by_id(IN p_user_id BIGINT) SELECT UserId,Email,DisplayName,PasswordHash,IsActive,SteamId FROM Users WHERE UserId=p_user_id LIMIT 1$$
DROP PROCEDURE IF EXISTS sp_auth_owner_create$$
CREATE PROCEDURE sp_auth_owner_create(IN p_email VARCHAR(254),IN p_display_name VARCHAR(100),IN p_password_hash VARCHAR(512)) BEGIN INSERT INTO Users(Email,DisplayName,PasswordHash,CreatedUtc) VALUES(p_email,p_display_name,p_password_hash,UTC_TIMESTAMP()); SELECT LAST_INSERT_ID(); END$$
DROP PROCEDURE IF EXISTS sp_auth_session_create$$
CREATE PROCEDURE sp_auth_session_create(IN p_session_id CHAR(32),IN p_user_id BIGINT,IN p_token_hash CHAR(64),IN p_expires_utc DATETIME,IN p_user_agent VARCHAR(512)) INSERT INTO UserSessions(SessionId,UserId,TokenHash,ExpiresUtc,UserAgent,CreatedUtc) VALUES(p_session_id,p_user_id,p_token_hash,p_expires_utc,p_user_agent,UTC_TIMESTAMP())$$
DROP PROCEDURE IF EXISTS sp_auth_session_valid$$
CREATE PROCEDURE sp_auth_session_valid(IN p_session_id CHAR(32),IN p_user_id BIGINT) SELECT EXISTS(SELECT 1 FROM UserSessions WHERE SessionId=p_session_id AND UserId=p_user_id AND ExpiresUtc>UTC_TIMESTAMP())$$
DROP PROCEDURE IF EXISTS sp_auth_session_delete$$
CREATE PROCEDURE sp_auth_session_delete(IN p_session_id CHAR(32)) DELETE FROM UserSessions WHERE SessionId=p_session_id$$
DROP PROCEDURE IF EXISTS sp_auth_user_set_steam_id$$
CREATE PROCEDURE sp_auth_user_set_steam_id(IN p_user_id BIGINT,IN p_steam_id CHAR(17)) UPDATE Users SET SteamId=p_steam_id WHERE UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_auth_user_clear_steam_id$$
CREATE PROCEDURE sp_auth_user_clear_steam_id(IN p_user_id BIGINT) UPDATE Users SET SteamId=NULL WHERE UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_quick_links_get$$
CREATE PROCEDURE sp_quick_links_get(IN p_user_id BIGINT) SELECT QuickLinkId,Title,Url,IconClass,UpdatedUtc FROM QuickLinks WHERE UserId=p_user_id ORDER BY Title$$
DROP PROCEDURE IF EXISTS sp_quick_links_create$$
CREATE PROCEDURE sp_quick_links_create(IN p_user_id BIGINT,IN p_title VARCHAR(100),IN p_url VARCHAR(2048),IN p_icon_class VARCHAR(100)) BEGIN INSERT INTO QuickLinks(UserId,Title,Url,IconClass,CreatedUtc,UpdatedUtc) VALUES(p_user_id,p_title,p_url,NULLIF(p_icon_class,''),UTC_TIMESTAMP(),UTC_TIMESTAMP()); SELECT LAST_INSERT_ID(); END$$
DROP PROCEDURE IF EXISTS sp_quick_links_update$$
CREATE PROCEDURE sp_quick_links_update(IN p_user_id BIGINT,IN p_quick_link_id BIGINT,IN p_title VARCHAR(100),IN p_url VARCHAR(2048),IN p_icon_class VARCHAR(100)) UPDATE QuickLinks SET Title=p_title,Url=p_url,IconClass=NULLIF(p_icon_class,''),UpdatedUtc=UTC_TIMESTAMP() WHERE QuickLinkId=p_quick_link_id AND UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_quick_links_delete$$
CREATE PROCEDURE sp_quick_links_delete(IN p_user_id BIGINT,IN p_quick_link_id BIGINT) DELETE FROM QuickLinks WHERE QuickLinkId=p_quick_link_id AND UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_notes_get$$
CREATE PROCEDURE sp_notes_get(IN p_user_id BIGINT) SELECT NoteId,Title,Body,SortOrder,CreatedUtc,UpdatedUtc FROM Notes WHERE UserId=p_user_id ORDER BY SortOrder,UpdatedUtc DESC$$
DROP PROCEDURE IF EXISTS sp_notes_create$$
CREATE PROCEDURE sp_notes_create(IN p_user_id BIGINT,IN p_note_id CHAR(36),IN p_title VARCHAR(200),IN p_body MEDIUMTEXT) INSERT INTO Notes(NoteId,UserId,Title,Body,SortOrder,CreatedUtc,UpdatedUtc) SELECT p_note_id,p_user_id,p_title,p_body,COALESCE(MAX(SortOrder)+1,0),UTC_TIMESTAMP(),UTC_TIMESTAMP() FROM Notes WHERE UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_notes_update$$
CREATE PROCEDURE sp_notes_update(IN p_user_id BIGINT,IN p_note_id CHAR(36),IN p_title VARCHAR(200),IN p_body MEDIUMTEXT) UPDATE Notes SET Title=p_title,Body=p_body,UpdatedUtc=UTC_TIMESTAMP() WHERE NoteId=p_note_id AND UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_notes_delete$$
CREATE PROCEDURE sp_notes_delete(IN p_user_id BIGINT,IN p_note_id CHAR(36)) DELETE FROM Notes WHERE NoteId=p_note_id AND UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_notes_set_order$$
CREATE PROCEDURE sp_notes_set_order(IN p_user_id BIGINT,IN p_note_id CHAR(36),IN p_sort_order INT) UPDATE Notes SET SortOrder=p_sort_order WHERE NoteId=p_note_id AND UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_tracked_skins_get$$
CREATE PROCEDURE sp_tracked_skins_get(IN p_user_id BIGINT) SELECT SkinId,Name,Weapon,Exterior,MarketHashName,ExternalImageUrl,PurchasePrice,CurrentPrice,PurchaseDate,Notes,CreatedUtc,UpdatedUtc FROM TrackedSkins WHERE UserId=p_user_id ORDER BY UpdatedUtc DESC$$
DROP PROCEDURE IF EXISTS sp_tracked_skins_create$$
CREATE PROCEDURE sp_tracked_skins_create(IN p_user_id BIGINT,IN p_skin_id CHAR(36),IN p_name VARCHAR(200),IN p_weapon VARCHAR(100),IN p_exterior VARCHAR(100),IN p_market_hash_name VARCHAR(255),IN p_external_image_url VARCHAR(2048),IN p_purchase_price DECIMAL(12,2),IN p_current_price DECIMAL(12,2),IN p_purchase_date DATE,IN p_notes TEXT) INSERT INTO TrackedSkins(SkinId,UserId,Name,Weapon,Exterior,MarketHashName,ExternalImageUrl,PurchasePrice,CurrentPrice,PurchaseDate,Notes,CreatedUtc,UpdatedUtc) VALUES(p_skin_id,p_user_id,p_name,p_weapon,p_exterior,p_market_hash_name,p_external_image_url,p_purchase_price,p_current_price,p_purchase_date,p_notes,UTC_TIMESTAMP(),UTC_TIMESTAMP())$$
DROP PROCEDURE IF EXISTS sp_tracked_skins_update$$
CREATE PROCEDURE sp_tracked_skins_update(IN p_user_id BIGINT,IN p_skin_id CHAR(36),IN p_name VARCHAR(200),IN p_weapon VARCHAR(100),IN p_exterior VARCHAR(100),IN p_market_hash_name VARCHAR(255),IN p_external_image_url VARCHAR(2048),IN p_purchase_price DECIMAL(12,2),IN p_current_price DECIMAL(12,2),IN p_purchase_date DATE,IN p_notes TEXT) UPDATE TrackedSkins SET Name=p_name,Weapon=p_weapon,Exterior=p_exterior,MarketHashName=p_market_hash_name,ExternalImageUrl=p_external_image_url,PurchasePrice=p_purchase_price,CurrentPrice=p_current_price,PurchaseDate=p_purchase_date,Notes=p_notes,UpdatedUtc=UTC_TIMESTAMP() WHERE SkinId=p_skin_id AND UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_tracked_skins_delete$$
CREATE PROCEDURE sp_tracked_skins_delete(IN p_user_id BIGINT,IN p_skin_id CHAR(36)) DELETE FROM TrackedSkins WHERE SkinId=p_skin_id AND UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_monitor_database_snapshot$$
CREATE PROCEDURE sp_monitor_database_snapshot()
BEGIN
    SELECT
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='UPTIME'),0) AS UNSIGNED) AS UptimeSeconds,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='THREADS_CONNECTED'),0) AS UNSIGNED) AS ThreadsConnected,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='THREADS_RUNNING'),0) AS UNSIGNED) AS ThreadsRunning,
        CAST(@@max_connections AS UNSIGNED) AS MaxConnections,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='QUESTIONS'),0) AS UNSIGNED) AS Questions,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='SLOW_QUERIES'),0) AS UNSIGNED) AS SlowQueries,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME='ABORTED_CONNECTS'),0) AS UNSIGNED) AS AbortedConnects,
        (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('Users','UserSessions','QuickLinks','Notes','TrackedSkins')) AS RequiredStructuresAvailable;
END$$
DELIMITER ;

/*
CS MATCH TRACKER: STORED-PROCEDURE TUTORIAL FOR BLITS
=====================================================

This section is intentionally a guide, not executable tracker SQL. Notes and TrackedSkins above are the
finished patterns to follow.

1. Add a CSMatches table before DELIMITER $$. Give it:
   - MatchId CHAR(36) primary key;
   - UserId BIGINT UNSIGNED NOT NULL;
   - the match fields already present in CSMatchObj;
   - CreatedUtc and UpdatedUtc DATETIME NOT NULL;
   - an index beginning with UserId;
   - a foreign key to Users(UserId) with ON DELETE CASCADE.

2. Inside the DELIMITER $$ section, add DROP PROCEDURE IF EXISTS before every CREATE PROCEDURE.

3. Implement these procedures:
   - sp_cs_matches_get(p_user_id)
   - sp_cs_matches_create(p_user_id, p_match_id, ...)
   - sp_cs_matches_update(p_user_id, p_match_id, ...)
   - sp_cs_matches_delete(p_user_id, p_match_id)

4. The security-critical predicates are:

       SELECT ... FROM CSMatches WHERE UserId = p_user_id;
       UPDATE CSMatches SET ... WHERE MatchId = p_match_id AND UserId = p_user_id;
       DELETE FROM CSMatches WHERE MatchId = p_match_id AND UserId = p_user_id;

5. Write UTC_TIMESTAMP() during create/update, as this MariaDB version does not accept UTC_TIMESTAMP()
   as the column DEFAULT expression.

6. Rerun this whole script in HeidiSQL after adding the executable table/procedures. The existing
   CREATE TABLE IF NOT EXISTS and DROP PROCEDURE IF EXISTS pattern makes deployment repeatable.
*/
