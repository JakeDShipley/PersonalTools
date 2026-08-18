USE PersonalTools;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_monitor_database_snapshot//

CREATE PROCEDURE sp_monitor_database_snapshot()
BEGIN
    SELECT
        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'UPTIME' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS UptimeSeconds,
        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'THREADS_CONNECTED' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS ThreadsConnected,
        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'THREADS_RUNNING' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS ThreadsRunning,
        CAST(@@GLOBAL.max_connections AS UNSIGNED) AS MaxConnections,

        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'QUESTIONS' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS Questions,
        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'SLOW_QUERIES' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS SlowQueries,

        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'ABORTED_CONNECTS' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS AbortedConnects,
        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'CONNECTIONS' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS Connections,

        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'CREATED_TMP_TABLES' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS CreatedTmpTables,
        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'CREATED_TMP_DISK_TABLES' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS CreatedTmpDiskTables,

        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'THREADS_CREATED' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS ThreadsCreated,

        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'INNODB_BUFFER_POOL_READ_REQUESTS' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS InnodbBufferPoolReadRequests,
        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'INNODB_BUFFER_POOL_READS' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS InnodbBufferPoolReads,

        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'INNODB_ROW_LOCK_CURRENT_WAITS' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS InnodbRowLockCurrentWaits,

        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'BYTES_RECEIVED' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS BytesReceived,
        CAST(COALESCE(MAX(CASE WHEN VARIABLE_NAME = 'BYTES_SENT' THEN VARIABLE_VALUE END), 0) AS UNSIGNED) AS BytesSent,

        (
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN
              (
                  'Users',
                  'UserSessions',
                  'QuickLinks',
                  'Notes',
                  'TrackedSkins',
                  'DashboardWidgetOrders',
                  'DashboardWeatherLocations',
                  'CSMatches',
                  'CSMatchProfiles',
                  'CSPlayerReports',
                  'AppSettings',
                  'CSActiveDutyMaps',
                  'TrackerItems',
                  'TrackerSettings'
              )
        ) AS RequiredStructuresAvailable,

        14 AS RequiredStructuresTotal

    FROM information_schema.GLOBAL_STATUS
    WHERE VARIABLE_NAME IN
    (
        'UPTIME',
        'THREADS_CONNECTED',
        'THREADS_RUNNING',
        'QUESTIONS',
        'SLOW_QUERIES',
        'ABORTED_CONNECTS',
        'CONNECTIONS',
        'CREATED_TMP_TABLES',
        'CREATED_TMP_DISK_TABLES',
        'THREADS_CREATED',
        'INNODB_BUFFER_POOL_READ_REQUESTS',
        'INNODB_BUFFER_POOL_READS',
        'INNODB_ROW_LOCK_CURRENT_WAITS',
        'BYTES_RECEIVED',
        'BYTES_SENT'
    );
END//

DELIMITER ;