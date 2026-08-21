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

DELIMITER ;
