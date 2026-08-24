USE PersonalTools;

CREATE TABLE IF NOT EXISTS ApplicationLogs
(
    LogId CHAR(36) NOT NULL,
    CapturedUtc DATETIME(6) NOT NULL,
    LogLevel TINYINT UNSIGNED NOT NULL,
    EventId INT NOT NULL DEFAULT 0,
    EventName VARCHAR(250) NULL,
    Category VARCHAR(500) NOT NULL,
    Message TEXT NOT NULL,
    ExceptionText MEDIUMTEXT NULL,
    PRIMARY KEY (LogId),
    KEY IX_ApplicationLogs_CapturedUtc (CapturedUtc),
    KEY IX_ApplicationLogs_Level_CapturedUtc (LogLevel, CapturedUtc)
)
COLLATE='utf8mb4_general_ci'
ENGINE=InnoDB;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_application_logs_write_bulk//
CREATE PROCEDURE sp_application_logs_write_bulk(IN p_logs JSON)
BEGIN
    INSERT IGNORE INTO ApplicationLogs(LogId,CapturedUtc,LogLevel,EventId,EventName,Category,Message,ExceptionText)
    SELECT selected.LogId,selected.CapturedUtc,selected.LogLevel,selected.EventId,NULLIF(selected.EventName,''),selected.Category,selected.Message,NULLIF(selected.ExceptionText,'')
    FROM JSON_TABLE(p_logs,'$[*]' COLUMNS(LogId CHAR(36) PATH '$.LogId',CapturedUtc DATETIME(6) PATH '$.CapturedUtc',LogLevel TINYINT PATH '$.Level',EventId INT PATH '$.EventId',EventName VARCHAR(250) PATH '$.EventName' NULL ON EMPTY,Category VARCHAR(500) PATH '$.Category',Message VARCHAR(10000) PATH '$.Message',ExceptionText TEXT PATH '$.Exception' NULL ON EMPTY)) AS selected;
    DELETE FROM ApplicationLogs WHERE CapturedUtc < UTC_TIMESTAMP(6) - INTERVAL 30 DAY;
END//

DROP PROCEDURE IF EXISTS sp_application_logs_get_page//
CREATE PROCEDURE sp_application_logs_get_page(IN p_minimum_level TINYINT,IN p_search VARCHAR(500),IN p_offset INT,IN p_page_size INT)
BEGIN
    SELECT LogId,CapturedUtc,LogLevel AS `Level`,EventId,EventName,Category,Message,ExceptionText AS `Exception`
    FROM ApplicationLogs
    WHERE LogLevel >= p_minimum_level AND (COALESCE(TRIM(p_search),'')='' OR Category LIKE CONCAT('%',TRIM(p_search) COLLATE utf8mb4_general_ci,'%') OR Message LIKE CONCAT('%',TRIM(p_search) COLLATE utf8mb4_general_ci,'%') OR ExceptionText LIKE CONCAT('%',TRIM(p_search) COLLATE utf8mb4_general_ci,'%'))
    ORDER BY CapturedUtc DESC,LogId DESC
    LIMIT p_offset,p_page_size;
END//

DROP PROCEDURE IF EXISTS sp_application_logs_get_summary//
CREATE PROCEDURE sp_application_logs_get_summary(IN p_minimum_level TINYINT,IN p_search VARCHAR(500))
BEGIN
    SELECT MIN(CapturedUtc) AS CaptureStartedUtc,COUNT(*) AS RetainedCount,COALESCE(SUM(CASE WHEN LogLevel=3 THEN 1 ELSE 0 END),0) AS WarningCount,COALESCE(SUM(CASE WHEN LogLevel=4 THEN 1 ELSE 0 END),0) AS ErrorCount,COALESCE(SUM(CASE WHEN LogLevel=5 THEN 1 ELSE 0 END),0) AS CriticalCount,COALESCE(SUM(CASE WHEN LogLevel>=p_minimum_level AND (COALESCE(TRIM(p_search),'')='' OR Category LIKE CONCAT('%',TRIM(p_search) COLLATE utf8mb4_general_ci,'%') OR Message LIKE CONCAT('%',TRIM(p_search) COLLATE utf8mb4_general_ci,'%') OR ExceptionText LIKE CONCAT('%',TRIM(p_search) COLLATE utf8mb4_general_ci,'%')) THEN 1 ELSE 0 END),0) AS FilteredCount
    FROM ApplicationLogs;
END//

DELIMITER ;
