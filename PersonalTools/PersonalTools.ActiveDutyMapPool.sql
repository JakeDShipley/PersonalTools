-- PersonalTools: Active Duty map-pool migration
-- Run this once in HeidiSQL against the PersonalTools database.
-- This replaces the deployed JSON file with MariaDB-backed shared map-pool storage.

CREATE TABLE IF NOT EXISTS CSActiveDutyMaps
(
    MapPoolId CHAR(36) NOT NULL,
    MapName VARCHAR(80) NOT NULL,
    UpdatedUtc DATETIME NOT NULL,
    PRIMARY KEY (MapPoolId),
    UNIQUE KEY UX_CSActiveDutyMaps_MapName (MapName)
);

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_cs_active_duty_maps_get$$
DROP PROCEDURE IF EXISTS sp_cs_active_duty_maps_set$$
DROP PROCEDURE IF EXISTS sp_monitor_database_snapshot$$

CREATE PROCEDURE sp_cs_active_duty_maps_get()
BEGIN
    SELECT MapName
    FROM CSActiveDutyMaps
    ORDER BY MapName;
END$$

CREATE PROCEDURE sp_cs_active_duty_maps_set(IN p_map_names JSON)
BEGIN
    DELETE FROM CSActiveDutyMaps;

    INSERT INTO CSActiveDutyMaps (MapPoolId, MapName, UpdatedUtc)
    SELECT UUID(), selected.MapName, UTC_TIMESTAMP()
    FROM JSON_TABLE(
        p_map_names,
        '$[*]' COLUMNS (MapName VARCHAR(80) PATH '$')
    ) AS selected
    WHERE selected.MapName IS NOT NULL
      AND CHAR_LENGTH(TRIM(selected.MapName)) > 0;
END$$

CREATE PROCEDURE sp_monitor_database_snapshot()
BEGIN
    SELECT
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME = 'UPTIME'), 0) AS UNSIGNED) AS UptimeSeconds,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME = 'THREADS_CONNECTED'), 0) AS UNSIGNED) AS ThreadsConnected,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME = 'THREADS_RUNNING'), 0) AS UNSIGNED) AS ThreadsRunning,
        CAST(@@max_connections AS UNSIGNED) AS MaxConnections,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME = 'QUESTIONS'), 0) AS UNSIGNED) AS Questions,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME = 'SLOW_QUERIES'), 0) AS UNSIGNED) AS SlowQueries,
        CAST(COALESCE((SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME = 'ABORTED_CONNECTS'), 0) AS UNSIGNED) AS AbortedConnects,
        (
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN ('Users', 'UserSessions', 'QuickLinks', 'Notes', 'TrackedSkins', 'DashboardWidgetOrders', 'DashboardWeatherLocations', 'CSMatches', 'CSPlayerReports', 'AppSettings', 'CSActiveDutyMaps')
        ) AS RequiredStructuresAvailable;
END$$

DELIMITER ;
