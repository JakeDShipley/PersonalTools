-- DESTRUCTIVE: wipes PersonalTools accounts, sessions and user-owned tool data.
-- Run this complete file in HeidiSQL only after deploying the matching GUID build.
USE PersonalTools;
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS CSMatches;
DROP TABLE IF EXISTS CSPlayerReports;
DROP TABLE IF EXISTS AppSettings;
DROP TABLE IF EXISTS DashboardWeatherLocations;
DROP TABLE IF EXISTS DashboardWidgetOrders;
DROP TABLE IF EXISTS TrackedSkins;
DROP TABLE IF EXISTS Notes;
DROP TABLE IF EXISTS QuickLinks;
DROP TABLE IF EXISTS UserSessions;
DROP TABLE IF EXISTS Users;
SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE Users (UserId CHAR(36) NOT NULL, Email VARCHAR(254) NOT NULL, DisplayName VARCHAR(100) NOT NULL, PasswordHash VARCHAR(512) NOT NULL, SteamId CHAR(17) NULL, IsActive TINYINT(1) NOT NULL DEFAULT 1, CreatedUtc DATETIME NOT NULL, PRIMARY KEY (UserId), UNIQUE KEY UX_Users_Email (Email), UNIQUE KEY UX_Users_SteamId (SteamId));
CREATE TABLE UserSessions (SessionId CHAR(36) NOT NULL, UserId CHAR(36) NOT NULL, TokenHash CHAR(64) NOT NULL, ExpiresUtc DATETIME NOT NULL, UserAgent VARCHAR(512) NOT NULL, CreatedUtc DATETIME NOT NULL, PRIMARY KEY (SessionId), KEY IX_UserSessions_UserId (UserId), KEY IX_UserSessions_ExpiresUtc (ExpiresUtc), CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
CREATE TABLE QuickLinks (QuickLinkId CHAR(36) NOT NULL, UserId CHAR(36) NOT NULL, Title VARCHAR(100) NOT NULL, Url VARCHAR(2048) NOT NULL, IconClass VARCHAR(100) NULL, SortOrder INT NOT NULL DEFAULT 0, CreatedUtc DATETIME NOT NULL, UpdatedUtc DATETIME NOT NULL, PRIMARY KEY (QuickLinkId), KEY IX_QuickLinks_UserId_SortOrder (UserId, SortOrder), CONSTRAINT FK_QuickLinks_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
CREATE TABLE Notes (NoteId CHAR(36) NOT NULL, UserId CHAR(36) NOT NULL, Title VARCHAR(200) NOT NULL, Body MEDIUMTEXT NOT NULL, SortOrder INT NOT NULL DEFAULT 0, CreatedUtc DATETIME NOT NULL, UpdatedUtc DATETIME NOT NULL, PRIMARY KEY (NoteId), KEY IX_Notes_UserId_SortOrder (UserId, SortOrder), CONSTRAINT FK_Notes_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
CREATE TABLE DashboardWidgetOrders (UserId CHAR(36) NOT NULL, WidgetKey VARCHAR(50) NOT NULL, SortOrder INT NOT NULL, UpdatedUtc DATETIME NOT NULL, PRIMARY KEY (UserId, WidgetKey), KEY IX_DashboardWidgetOrders_UserId_SortOrder (UserId, SortOrder), CONSTRAINT FK_DashboardWidgetOrders_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
CREATE TABLE DashboardWeatherLocations (WeatherLocationId CHAR(36) NOT NULL, UserId CHAR(36) NOT NULL, DisplayName VARCHAR(100) NOT NULL, Latitude DECIMAL(9,6) NOT NULL, Longitude DECIMAL(9,6) NOT NULL, CreatedUtc DATETIME NOT NULL, PRIMARY KEY (WeatherLocationId), KEY IX_DashboardWeatherLocations_UserId_CreatedUtc (UserId, CreatedUtc), CONSTRAINT FK_DashboardWeatherLocations_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
CREATE TABLE CSMatches (MatchId CHAR(36) NOT NULL, UserId CHAR(36) NOT NULL, StartSide VARCHAR(2) NOT NULL, MapName VARCHAR(100) NOT NULL, GameType VARCHAR(100) NOT NULL, TeamScore INT NOT NULL, OpponentScore INT NOT NULL, OvertimeCount INT NOT NULL DEFAULT 0, LeetifyMatchId VARCHAR(100) NULL, PlayedUtc DATETIME NOT NULL, CreatedUtc DATETIME NOT NULL, UpdatedUtc DATETIME NOT NULL, PRIMARY KEY (MatchId), KEY IX_CSMatches_UserId_PlayedUtc (UserId, PlayedUtc), UNIQUE KEY UX_CSMatches_UserId_LeetifyMatchId (UserId, LeetifyMatchId), CONSTRAINT FK_CSMatches_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
CREATE TABLE TrackedSkins (SkinId CHAR(36) NOT NULL, UserId CHAR(36) NOT NULL, Name VARCHAR(200) NOT NULL, Weapon VARCHAR(100) NOT NULL, Exterior VARCHAR(100) NOT NULL, MarketHashName VARCHAR(255) NOT NULL, ExternalImageUrl VARCHAR(2048) NOT NULL, PurchasePrice DECIMAL(12,2) NOT NULL DEFAULT 0.00, CurrentPrice DECIMAL(12,2) NULL, PurchaseDate DATE NULL, Notes TEXT NOT NULL, CreatedUtc DATETIME NOT NULL, UpdatedUtc DATETIME NOT NULL, PRIMARY KEY (SkinId), KEY IX_TrackedSkins_UserId_UpdatedUtc (UserId, UpdatedUtc), CONSTRAINT FK_TrackedSkins_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
CREATE TABLE CSPlayerReports (ReportId CHAR(36) NOT NULL, UserId CHAR(36) NOT NULL, Steam64Id CHAR(17) NOT NULL, CreatedUtc DATETIME NOT NULL, PRIMARY KEY (ReportId), UNIQUE KEY UX_CSPlayerReports_UserId_Steam64Id (UserId, Steam64Id), KEY IX_CSPlayerReports_Steam64Id (Steam64Id), CONSTRAINT FK_CSPlayerReports_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
CREATE TABLE AppSettings (UserId CHAR(36) NOT NULL, SettingKey VARCHAR(80) NOT NULL, SettingValue MEDIUMTEXT NOT NULL, UpdatedUtc DATETIME NOT NULL, PRIMARY KEY (UserId, SettingKey), CONSTRAINT FK_AppSettings_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE);

DELIMITER $$
DROP PROCEDURE IF EXISTS sp_auth_user_count$$
DROP PROCEDURE IF EXISTS sp_auth_user_get_by_email$$
DROP PROCEDURE IF EXISTS sp_auth_user_get_by_id$$
DROP PROCEDURE IF EXISTS sp_auth_owner_create$$
DROP PROCEDURE IF EXISTS sp_auth_session_create$$
DROP PROCEDURE IF EXISTS sp_auth_session_valid$$
DROP PROCEDURE IF EXISTS sp_auth_session_delete$$
DROP PROCEDURE IF EXISTS sp_auth_user_set_steam_id$$
DROP PROCEDURE IF EXISTS sp_auth_user_clear_steam_id$$
DROP PROCEDURE IF EXISTS sp_auth_user_change_password$$
CREATE PROCEDURE sp_auth_user_count() SELECT COUNT(*) FROM Users$$
CREATE PROCEDURE sp_auth_user_get_by_email(IN p_email VARCHAR(254)) SELECT UserId,Email,DisplayName,PasswordHash,IsActive,SteamId FROM Users WHERE Email=p_email LIMIT 1$$
CREATE PROCEDURE sp_auth_user_get_by_id(IN p_user_id CHAR(36)) SELECT UserId,Email,DisplayName,PasswordHash,IsActive,SteamId FROM Users WHERE UserId=p_user_id LIMIT 1$$
CREATE PROCEDURE sp_auth_owner_create(IN p_user_id CHAR(36),IN p_email VARCHAR(254),IN p_display_name VARCHAR(100),IN p_password_hash VARCHAR(512)) INSERT INTO Users(UserId,Email,DisplayName,PasswordHash,CreatedUtc) VALUES(p_user_id,p_email,p_display_name,p_password_hash,UTC_TIMESTAMP())$$
CREATE PROCEDURE sp_auth_session_create(IN p_session_id CHAR(36),IN p_user_id CHAR(36),IN p_token_hash CHAR(64),IN p_expires_utc DATETIME,IN p_user_agent VARCHAR(512)) INSERT INTO UserSessions(SessionId,UserId,TokenHash,ExpiresUtc,UserAgent,CreatedUtc) VALUES(p_session_id,p_user_id,p_token_hash,p_expires_utc,p_user_agent,UTC_TIMESTAMP())$$
CREATE PROCEDURE sp_auth_session_valid(IN p_session_id CHAR(36),IN p_user_id CHAR(36)) SELECT EXISTS(SELECT 1 FROM UserSessions WHERE SessionId=p_session_id AND UserId=p_user_id AND ExpiresUtc>UTC_TIMESTAMP())$$
CREATE PROCEDURE sp_auth_session_delete(IN p_session_id CHAR(36)) DELETE FROM UserSessions WHERE SessionId=p_session_id$$
CREATE PROCEDURE sp_auth_user_set_steam_id(IN p_user_id CHAR(36),IN p_steam_id CHAR(17)) UPDATE Users SET SteamId=p_steam_id WHERE UserId=p_user_id$$
CREATE PROCEDURE sp_auth_user_clear_steam_id(IN p_user_id CHAR(36)) UPDATE Users SET SteamId=NULL WHERE UserId=p_user_id$$
CREATE PROCEDURE sp_auth_user_change_password(IN p_user_id CHAR(36),IN p_session_id CHAR(36),IN p_password_hash VARCHAR(512)) BEGIN UPDATE Users SET PasswordHash=p_password_hash WHERE UserId=p_user_id AND IsActive=1; IF ROW_COUNT()=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Account is unavailable'; END IF; DELETE FROM UserSessions WHERE UserId=p_user_id AND SessionId<>p_session_id; END$$

DROP PROCEDURE IF EXISTS sp_app_settings_get$$
DROP PROCEDURE IF EXISTS sp_app_settings_set$$
CREATE PROCEDURE sp_app_settings_get(IN p_user_id CHAR(36)) SELECT SettingKey,SettingValue FROM AppSettings WHERE UserId=p_user_id$$
CREATE PROCEDURE sp_app_settings_set(IN p_user_id CHAR(36),IN p_setting_key VARCHAR(80),IN p_setting_value MEDIUMTEXT) INSERT INTO AppSettings(UserId,SettingKey,SettingValue,UpdatedUtc) VALUES(p_user_id,p_setting_key,p_setting_value,UTC_TIMESTAMP()) ON DUPLICATE KEY UPDATE SettingValue=VALUES(SettingValue),UpdatedUtc=VALUES(UpdatedUtc)$$

DROP PROCEDURE IF EXISTS sp_quick_links_get$$ DROP PROCEDURE IF EXISTS sp_quick_links_create$$ DROP PROCEDURE IF EXISTS sp_quick_links_update$$ DROP PROCEDURE IF EXISTS sp_quick_links_delete$$ DROP PROCEDURE IF EXISTS sp_quick_links_set_order_bulk$$
CREATE PROCEDURE sp_quick_links_get(IN p_user_id CHAR(36)) SELECT QuickLinkId,Title,Url,IconClass,SortOrder,UpdatedUtc FROM QuickLinks WHERE UserId=p_user_id ORDER BY SortOrder,CreatedUtc,QuickLinkId$$
CREATE PROCEDURE sp_quick_links_create(IN p_user_id CHAR(36),IN p_quick_link_id CHAR(36),IN p_title VARCHAR(100),IN p_url VARCHAR(2048),IN p_icon_class VARCHAR(100)) INSERT INTO QuickLinks(QuickLinkId,UserId,Title,Url,IconClass,SortOrder,CreatedUtc,UpdatedUtc) SELECT p_quick_link_id,p_user_id,p_title,p_url,NULLIF(p_icon_class,''),COALESCE(MAX(SortOrder)+1,0),UTC_TIMESTAMP(),UTC_TIMESTAMP() FROM QuickLinks WHERE UserId=p_user_id$$
CREATE PROCEDURE sp_quick_links_update(IN p_user_id CHAR(36),IN p_quick_link_id CHAR(36),IN p_title VARCHAR(100),IN p_url VARCHAR(2048),IN p_icon_class VARCHAR(100)) UPDATE QuickLinks SET Title=p_title,Url=p_url,IconClass=NULLIF(p_icon_class,''),UpdatedUtc=UTC_TIMESTAMP() WHERE QuickLinkId=p_quick_link_id AND UserId=p_user_id$$
CREATE PROCEDURE sp_quick_links_delete(IN p_user_id CHAR(36),IN p_quick_link_id CHAR(36)) DELETE FROM QuickLinks WHERE QuickLinkId=p_quick_link_id AND UserId=p_user_id$$
CREATE PROCEDURE sp_quick_links_set_order_bulk(IN p_user_id CHAR(36),IN p_quick_link_ids JSON) BEGIN UPDATE QuickLinks l INNER JOIN JSON_TABLE(p_quick_link_ids,'$[*]' COLUMNS(SortOrder FOR ORDINALITY,QuickLinkId CHAR(36) PATH '$')) p ON p.QuickLinkId=l.QuickLinkId SET l.SortOrder=p.SortOrder WHERE l.UserId=p_user_id; END$$

DROP PROCEDURE IF EXISTS sp_notes_get$$ DROP PROCEDURE IF EXISTS sp_notes_create$$ DROP PROCEDURE IF EXISTS sp_notes_update$$ DROP PROCEDURE IF EXISTS sp_notes_delete$$ DROP PROCEDURE IF EXISTS sp_notes_set_order_bulk$$
CREATE PROCEDURE sp_notes_get(IN p_user_id CHAR(36)) SELECT NoteId,Title,Body,SortOrder,CreatedUtc,UpdatedUtc FROM Notes WHERE UserId=p_user_id ORDER BY SortOrder,UpdatedUtc DESC$$
CREATE PROCEDURE sp_notes_create(IN p_user_id CHAR(36),IN p_note_id CHAR(36),IN p_title VARCHAR(200),IN p_body MEDIUMTEXT) INSERT INTO Notes(NoteId,UserId,Title,Body,SortOrder,CreatedUtc,UpdatedUtc) SELECT p_note_id,p_user_id,p_title,p_body,COALESCE(MAX(SortOrder)+1,0),UTC_TIMESTAMP(),UTC_TIMESTAMP() FROM Notes WHERE UserId=p_user_id$$
CREATE PROCEDURE sp_notes_update(IN p_user_id CHAR(36),IN p_note_id CHAR(36),IN p_title VARCHAR(200),IN p_body MEDIUMTEXT) UPDATE Notes SET Title=p_title,Body=p_body,UpdatedUtc=UTC_TIMESTAMP() WHERE NoteId=p_note_id AND UserId=p_user_id$$
CREATE PROCEDURE sp_notes_delete(IN p_user_id CHAR(36),IN p_note_id CHAR(36)) DELETE FROM Notes WHERE NoteId=p_note_id AND UserId=p_user_id$$
CREATE PROCEDURE sp_notes_set_order_bulk(IN p_user_id CHAR(36),IN p_note_ids JSON) BEGIN UPDATE Notes n INNER JOIN JSON_TABLE(p_note_ids,'$[*]' COLUMNS(SortOrder FOR ORDINALITY,NoteId CHAR(36) PATH '$')) p ON p.NoteId=n.NoteId SET n.SortOrder=p.SortOrder WHERE n.UserId=p_user_id; END$$

DROP PROCEDURE IF EXISTS sp_tracked_skins_get$$ DROP PROCEDURE IF EXISTS sp_tracked_skins_create$$ DROP PROCEDURE IF EXISTS sp_tracked_skins_update$$ DROP PROCEDURE IF EXISTS sp_tracked_skins_delete$$
CREATE PROCEDURE sp_tracked_skins_get(IN p_user_id CHAR(36)) SELECT SkinId,Name,Weapon,Exterior,MarketHashName,ExternalImageUrl,PurchasePrice,CurrentPrice,PurchaseDate,Notes,CreatedUtc,UpdatedUtc FROM TrackedSkins WHERE UserId=p_user_id ORDER BY UpdatedUtc DESC$$
CREATE PROCEDURE sp_tracked_skins_create(IN p_user_id CHAR(36),IN p_skin_id CHAR(36),IN p_name VARCHAR(200),IN p_weapon VARCHAR(100),IN p_exterior VARCHAR(100),IN p_market_hash_name VARCHAR(255),IN p_external_image_url VARCHAR(2048),IN p_purchase_price DECIMAL(12,2),IN p_current_price DECIMAL(12,2),IN p_purchase_date DATE,IN p_notes TEXT) INSERT INTO TrackedSkins(SkinId,UserId,Name,Weapon,Exterior,MarketHashName,ExternalImageUrl,PurchasePrice,CurrentPrice,PurchaseDate,Notes,CreatedUtc,UpdatedUtc) VALUES(p_skin_id,p_user_id,p_name,p_weapon,p_exterior,p_market_hash_name,p_external_image_url,p_purchase_price,p_current_price,p_purchase_date,p_notes,UTC_TIMESTAMP(),UTC_TIMESTAMP())$$
CREATE PROCEDURE sp_tracked_skins_update(IN p_user_id CHAR(36),IN p_skin_id CHAR(36),IN p_name VARCHAR(200),IN p_weapon VARCHAR(100),IN p_exterior VARCHAR(100),IN p_market_hash_name VARCHAR(255),IN p_external_image_url VARCHAR(2048),IN p_purchase_price DECIMAL(12,2),IN p_current_price DECIMAL(12,2),IN p_purchase_date DATE,IN p_notes TEXT) UPDATE TrackedSkins SET Name=p_name,Weapon=p_weapon,Exterior=p_exterior,MarketHashName=p_market_hash_name,ExternalImageUrl=p_external_image_url,PurchasePrice=p_purchase_price,CurrentPrice=p_current_price,PurchaseDate=p_purchase_date,Notes=p_notes,UpdatedUtc=UTC_TIMESTAMP() WHERE SkinId=p_skin_id AND UserId=p_user_id$$
CREATE PROCEDURE sp_tracked_skins_delete(IN p_user_id CHAR(36),IN p_skin_id CHAR(36)) DELETE FROM TrackedSkins WHERE SkinId=p_skin_id AND UserId=p_user_id$$

DROP PROCEDURE IF EXISTS sp_dashboard_widget_order_get$$ DROP PROCEDURE IF EXISTS sp_dashboard_widget_order_set_bulk$$ DROP PROCEDURE IF EXISTS sp_dashboard_weather_locations_get$$ DROP PROCEDURE IF EXISTS sp_dashboard_weather_locations_create$$ DROP PROCEDURE IF EXISTS sp_dashboard_weather_locations_delete$$
CREATE PROCEDURE sp_dashboard_widget_order_get(IN p_user_id CHAR(36)) SELECT WidgetKey FROM DashboardWidgetOrders WHERE UserId=p_user_id ORDER BY SortOrder,WidgetKey$$
CREATE PROCEDURE sp_dashboard_widget_order_set_bulk(IN p_user_id CHAR(36),IN p_widget_keys JSON) BEGIN INSERT INTO DashboardWidgetOrders(UserId,WidgetKey,SortOrder,UpdatedUtc) SELECT p_user_id,p.WidgetKey,p.SortOrder,UTC_TIMESTAMP() FROM JSON_TABLE(p_widget_keys,'$[*]' COLUMNS(SortOrder FOR ORDINALITY,WidgetKey VARCHAR(50) PATH '$')) p ON DUPLICATE KEY UPDATE SortOrder=VALUES(SortOrder),UpdatedUtc=VALUES(UpdatedUtc); END$$
CREATE PROCEDURE sp_dashboard_weather_locations_get(IN p_user_id CHAR(36)) SELECT WeatherLocationId,DisplayName,Latitude,Longitude,CreatedUtc FROM DashboardWeatherLocations WHERE UserId=p_user_id ORDER BY CreatedUtc DESC$$
CREATE PROCEDURE sp_dashboard_weather_locations_create(IN p_user_id CHAR(36),IN p_weather_location_id CHAR(36),IN p_display_name VARCHAR(100),IN p_latitude DECIMAL(9,6),IN p_longitude DECIMAL(9,6)) INSERT INTO DashboardWeatherLocations(WeatherLocationId,UserId,DisplayName,Latitude,Longitude,CreatedUtc) VALUES(p_weather_location_id,p_user_id,p_display_name,p_latitude,p_longitude,UTC_TIMESTAMP())$$
CREATE PROCEDURE sp_dashboard_weather_locations_delete(IN p_user_id CHAR(36),IN p_weather_location_id CHAR(36)) DELETE FROM DashboardWeatherLocations WHERE WeatherLocationId=p_weather_location_id AND UserId=p_user_id$$

DROP PROCEDURE IF EXISTS sp_cs_matches_get$$ DROP PROCEDURE IF EXISTS sp_cs_matches_get_range$$ DROP PROCEDURE IF EXISTS sp_cs_matches_create$$ DROP PROCEDURE IF EXISTS sp_cs_matches_update$$ DROP PROCEDURE IF EXISTS sp_cs_matches_delete$$ DROP PROCEDURE IF EXISTS sp_cs_matches_delete_all$$
CREATE PROCEDURE sp_cs_matches_get(IN p_user_id CHAR(36)) SELECT MatchId,StartSide,MapName,GameType,TeamScore,OpponentScore,OvertimeCount,LeetifyMatchId,PlayedUtc AS Created,UpdatedUtc AS Updated FROM CSMatches WHERE UserId=p_user_id ORDER BY PlayedUtc DESC,CreatedUtc DESC$$
CREATE PROCEDURE sp_cs_matches_get_range(IN p_user_id CHAR(36),IN p_start_utc DATETIME,IN p_end_utc DATETIME) SELECT MatchId,StartSide,MapName,GameType,TeamScore,OpponentScore,OvertimeCount,LeetifyMatchId,PlayedUtc AS Created,UpdatedUtc AS Updated FROM CSMatches WHERE UserId=p_user_id AND PlayedUtc>=p_start_utc AND PlayedUtc<p_end_utc ORDER BY PlayedUtc DESC$$
CREATE PROCEDURE sp_cs_matches_create(IN p_user_id CHAR(36),IN p_match_id CHAR(36),IN p_start_side VARCHAR(2),IN p_map_name VARCHAR(100),IN p_game_type VARCHAR(100),IN p_team_score INT,IN p_opponent_score INT,IN p_overtime_count INT,IN p_leetify_match_id VARCHAR(100),IN p_played_utc DATETIME) INSERT IGNORE INTO CSMatches(MatchId,UserId,StartSide,MapName,GameType,TeamScore,OpponentScore,OvertimeCount,LeetifyMatchId,PlayedUtc,CreatedUtc,UpdatedUtc) VALUES(p_match_id,p_user_id,p_start_side,p_map_name,p_game_type,p_team_score,p_opponent_score,p_overtime_count,NULLIF(p_leetify_match_id,''),p_played_utc,UTC_TIMESTAMP(),UTC_TIMESTAMP())$$
CREATE PROCEDURE sp_cs_matches_update(IN p_user_id CHAR(36),IN p_match_id CHAR(36),IN p_start_side VARCHAR(2),IN p_map_name VARCHAR(100),IN p_game_type VARCHAR(100),IN p_team_score INT,IN p_opponent_score INT,IN p_overtime_count INT) UPDATE CSMatches SET StartSide=p_start_side,MapName=p_map_name,GameType=p_game_type,TeamScore=p_team_score,OpponentScore=p_opponent_score,OvertimeCount=p_overtime_count,UpdatedUtc=UTC_TIMESTAMP() WHERE MatchId=p_match_id AND UserId=p_user_id$$
CREATE PROCEDURE sp_cs_matches_delete(IN p_user_id CHAR(36),IN p_match_id CHAR(36)) DELETE FROM CSMatches WHERE MatchId=p_match_id AND UserId=p_user_id$$
CREATE PROCEDURE sp_cs_matches_delete_all(IN p_user_id CHAR(36)) DELETE FROM CSMatches WHERE UserId=p_user_id$$

DROP PROCEDURE IF EXISTS sp_cs_player_reports_count$$
DROP PROCEDURE IF EXISTS sp_cs_player_reports_create$$
CREATE PROCEDURE sp_cs_player_reports_count(IN p_steam64_id CHAR(17)) SELECT COUNT(*) FROM CSPlayerReports WHERE Steam64Id=p_steam64_id$$
CREATE PROCEDURE sp_cs_player_reports_create(IN p_report_id CHAR(36),IN p_user_id CHAR(36),IN p_steam64_id CHAR(17)) BEGIN INSERT IGNORE INTO CSPlayerReports(ReportId,UserId,Steam64Id,CreatedUtc) VALUES(p_report_id,p_user_id,p_steam64_id,UTC_TIMESTAMP()); SELECT ROW_COUNT(); END$$

DELIMITER ;
