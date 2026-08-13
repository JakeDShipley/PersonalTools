CREATE DATABASE IF NOT EXISTS PersonalTools CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE PersonalTools;

CREATE TABLE IF NOT EXISTS Users (
    UserId BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    Email VARCHAR(254) NOT NULL,
    DisplayName VARCHAR(100) NOT NULL,
    PasswordHash VARCHAR(512) NOT NULL,
    SteamId CHAR(17) NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedUtc DATETIME NOT NULL DEFAULT UTC_TIMESTAMP(),
    PRIMARY KEY (UserId), UNIQUE KEY UX_Users_Email (Email), UNIQUE KEY UX_Users_SteamId (SteamId)
);
CREATE TABLE IF NOT EXISTS UserSessions (
    SessionId CHAR(32) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    TokenHash CHAR(64) NOT NULL,
    ExpiresUtc DATETIME NOT NULL,
    UserAgent VARCHAR(512) NOT NULL,
    CreatedUtc DATETIME NOT NULL DEFAULT UTC_TIMESTAMP(),
    PRIMARY KEY (SessionId), KEY IX_UserSessions_UserId (UserId), KEY IX_UserSessions_ExpiresUtc (ExpiresUtc),
    CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS QuickLinks (
    QuickLinkId BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    UserId BIGINT UNSIGNED NOT NULL,
    Title VARCHAR(100) NOT NULL,
    Url VARCHAR(2048) NOT NULL,
    IconClass VARCHAR(100) NULL,
    CreatedUtc DATETIME NOT NULL DEFAULT UTC_TIMESTAMP(),
    UpdatedUtc DATETIME NOT NULL DEFAULT UTC_TIMESTAMP() ON UPDATE UTC_TIMESTAMP(),
    PRIMARY KEY (QuickLinkId), KEY IX_QuickLinks_UserId (UserId),
    CONSTRAINT FK_QuickLinks_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);
DELIMITER $$
DROP PROCEDURE IF EXISTS sp_auth_user_count$$
CREATE PROCEDURE sp_auth_user_count() SELECT COUNT(*) FROM Users$$
DROP PROCEDURE IF EXISTS sp_auth_user_get_by_email$$
CREATE PROCEDURE sp_auth_user_get_by_email(IN p_email VARCHAR(254)) SELECT UserId,Email,DisplayName,PasswordHash,IsActive,SteamId FROM Users WHERE Email=p_email LIMIT 1$$
DROP PROCEDURE IF EXISTS sp_auth_user_get_by_id$$
CREATE PROCEDURE sp_auth_user_get_by_id(IN p_user_id BIGINT) SELECT UserId,Email,DisplayName,PasswordHash,IsActive,SteamId FROM Users WHERE UserId=p_user_id LIMIT 1$$
DROP PROCEDURE IF EXISTS sp_auth_owner_create$$
CREATE PROCEDURE sp_auth_owner_create(IN p_email VARCHAR(254),IN p_display_name VARCHAR(100),IN p_password_hash VARCHAR(512)) BEGIN INSERT INTO Users(Email,DisplayName,PasswordHash) VALUES(p_email,p_display_name,p_password_hash); SELECT LAST_INSERT_ID(); END$$
DROP PROCEDURE IF EXISTS sp_auth_session_create$$
CREATE PROCEDURE sp_auth_session_create(IN p_session_id CHAR(32),IN p_user_id BIGINT,IN p_token_hash CHAR(64),IN p_expires_utc DATETIME,IN p_user_agent VARCHAR(512)) INSERT INTO UserSessions(SessionId,UserId,TokenHash,ExpiresUtc,UserAgent) VALUES(p_session_id,p_user_id,p_token_hash,p_expires_utc,p_user_agent)$$
DROP PROCEDURE IF EXISTS sp_auth_session_valid$$
CREATE PROCEDURE sp_auth_session_valid(IN p_session_id CHAR(32),IN p_user_id BIGINT) SELECT EXISTS(SELECT 1 FROM UserSessions WHERE SessionId=p_session_id AND UserId=p_user_id AND ExpiresUtc>UTC_TIMESTAMP())$$
DROP PROCEDURE IF EXISTS sp_auth_session_delete$$
CREATE PROCEDURE sp_auth_session_delete(IN p_session_id CHAR(32)) DELETE FROM UserSessions WHERE SessionId=p_session_id$$
DROP PROCEDURE IF EXISTS sp_auth_user_set_steam_id$$
CREATE PROCEDURE sp_auth_user_set_steam_id(IN p_user_id BIGINT,IN p_steam_id CHAR(17)) UPDATE Users SET SteamId=p_steam_id WHERE UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_quick_links_get$$
CREATE PROCEDURE sp_quick_links_get(IN p_user_id BIGINT) SELECT QuickLinkId,Title,Url,IconClass,UpdatedUtc FROM QuickLinks WHERE UserId=p_user_id ORDER BY Title$$
DROP PROCEDURE IF EXISTS sp_quick_links_create$$
CREATE PROCEDURE sp_quick_links_create(IN p_user_id BIGINT,IN p_title VARCHAR(100),IN p_url VARCHAR(2048),IN p_icon_class VARCHAR(100)) BEGIN INSERT INTO QuickLinks(UserId,Title,Url,IconClass) VALUES(p_user_id,p_title,p_url,NULLIF(p_icon_class,'')); SELECT LAST_INSERT_ID(); END$$
DROP PROCEDURE IF EXISTS sp_quick_links_update$$
CREATE PROCEDURE sp_quick_links_update(IN p_user_id BIGINT,IN p_quick_link_id BIGINT,IN p_title VARCHAR(100),IN p_url VARCHAR(2048),IN p_icon_class VARCHAR(100)) UPDATE QuickLinks SET Title=p_title,Url=p_url,IconClass=NULLIF(p_icon_class,'') WHERE QuickLinkId=p_quick_link_id AND UserId=p_user_id$$
DROP PROCEDURE IF EXISTS sp_quick_links_delete$$
CREATE PROCEDURE sp_quick_links_delete(IN p_user_id BIGINT,IN p_quick_link_id BIGINT) DELETE FROM QuickLinks WHERE QuickLinkId=p_quick_link_id AND UserId=p_user_id$$
DELIMITER ;
