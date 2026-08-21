USE PersonalTools;

CREATE TABLE IF NOT EXISTS DashboardWeatherLocations (
    WeatherLocationId CHAR(36) NOT NULL,
    UserId BIGINT UNSIGNED NOT NULL,
    DisplayName VARCHAR(100) NOT NULL,
    Latitude DECIMAL(9,6) NOT NULL,
    Longitude DECIMAL(9,6) NOT NULL,
    CreatedUtc DATETIME NOT NULL,
    PRIMARY KEY (WeatherLocationId),
    KEY IX_DashboardWeatherLocations_UserId_CreatedUtc (UserId, CreatedUtc),
    CONSTRAINT FK_DashboardWeatherLocations_Users FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_dashboard_weather_locations_get$$
CREATE PROCEDURE sp_dashboard_weather_locations_get(IN p_user_id BIGINT)
SELECT WeatherLocationId, DisplayName, Latitude, Longitude, CreatedUtc
FROM DashboardWeatherLocations
WHERE UserId = p_user_id
ORDER BY CreatedUtc DESC$$

DROP PROCEDURE IF EXISTS sp_dashboard_weather_locations_create$$
CREATE PROCEDURE sp_dashboard_weather_locations_create(IN p_user_id BIGINT, IN p_weather_location_id CHAR(36), IN p_display_name VARCHAR(100), IN p_latitude DECIMAL(9,6), IN p_longitude DECIMAL(9,6))
INSERT INTO DashboardWeatherLocations(WeatherLocationId, UserId, DisplayName, Latitude, Longitude, CreatedUtc)
VALUES(p_weather_location_id, p_user_id, p_display_name, p_latitude, p_longitude, UTC_TIMESTAMP())$$

DROP PROCEDURE IF EXISTS sp_dashboard_weather_locations_delete$$
CREATE PROCEDURE sp_dashboard_weather_locations_delete(IN p_user_id BIGINT, IN p_weather_location_id CHAR(36))
DELETE FROM DashboardWeatherLocations
WHERE WeatherLocationId = p_weather_location_id AND UserId = p_user_id$$

DELIMITER ;
