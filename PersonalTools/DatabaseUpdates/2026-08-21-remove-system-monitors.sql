USE PersonalTools;

-- The server and database monitor features have been removed. Dropping the dedicated procedure
-- ensures an upgraded database matches a clean installation of the current application.
DROP PROCEDURE IF EXISTS sp_monitor_database_snapshot;
