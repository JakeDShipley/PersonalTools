-- CS2 Demo Library: metadata cache only.
-- This script never stores demo files. ReplayUrl is a short-lived source URL returned by Leetify
-- and is cleared whenever it is no longer returned by the provider.

CREATE TABLE IF NOT EXISTS CSDemoCatalog
(
    DemoId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    Steam64Id CHAR(17) NOT NULL,
    LeetifyMatchId VARCHAR(100) NOT NULL,
    MapName VARCHAR(100) NOT NULL,
    GameType VARCHAR(100) NOT NULL,
    TeamScore INT NOT NULL,
    OpponentScore INT NOT NULL,
    IsWin TINYINT(1) NOT NULL,
    ReplayUrl VARCHAR(2048) NULL,
    IsAvailable TINYINT(1) NOT NULL DEFAULT 0,
    PlayedAtUtc DATETIME NOT NULL,
    RefreshedUtc DATETIME NOT NULL,
    PRIMARY KEY (DemoId),
    UNIQUE KEY UX_CSDemoCatalog_User_Steam_Match (UserId, Steam64Id, LeetifyMatchId),
    KEY IX_CSDemoCatalog_User_Steam_Played (UserId, Steam64Id, PlayedAtUtc),
    CONSTRAINT FK_CSDemoCatalog_Users
        FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

DROP PROCEDURE IF EXISTS sp_cs_demo_catalog_get;

DELIMITER //

CREATE PROCEDURE sp_cs_demo_catalog_get(
    IN p_user_id CHAR(36),
    IN p_steam64_id CHAR(17)
)
BEGIN
    -- The user filter is intentional: cached searches and replay links remain private.
    SELECT
        DemoId,
        Steam64Id,
        LeetifyMatchId,
        MapName,
        GameType,
        TeamScore,
        OpponentScore,
        IsWin,
        ReplayUrl,
        IsAvailable,
        PlayedAtUtc,
        RefreshedUtc
    FROM CSDemoCatalog
    WHERE UserId = p_user_id
      AND Steam64Id = p_steam64_id
    ORDER BY PlayedAtUtc DESC;
END//

DELIMITER ;

DROP PROCEDURE IF EXISTS sp_cs_demo_catalog_refresh;

DELIMITER //

CREATE PROCEDURE sp_cs_demo_catalog_refresh(
    IN p_user_id CHAR(36),
    IN p_steam64_id CHAR(17),
    IN p_demos JSON
)
BEGIN
    -- Links can expire at Valve/FACEIT. Mark every existing item unavailable first, then the
    -- current provider response restores only the matches whose source still supplied a link.
    UPDATE CSDemoCatalog
    SET
        IsAvailable = 0,
        ReplayUrl = NULL,
        RefreshedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Steam64Id = p_steam64_id;

    -- A JSON payload keeps the refresh to one database round trip even for a large match list.
    INSERT INTO CSDemoCatalog
    (
        DemoId,
        UserId,
        Steam64Id,
        LeetifyMatchId,
        MapName,
        GameType,
        TeamScore,
        OpponentScore,
        IsWin,
        ReplayUrl,
        IsAvailable,
        PlayedAtUtc,
        RefreshedUtc
    )
    SELECT
        selected.DemoId,
        p_user_id,
        p_steam64_id,
        selected.LeetifyMatchId,
        selected.MapName,
        selected.GameType,
        selected.TeamScore,
        selected.OpponentScore,
        selected.IsWin,
        NULLIF(selected.ReplayUrl, ''),
        selected.IsAvailable,
        selected.PlayedAtUtc,
        UTC_TIMESTAMP()
    FROM JSON_TABLE(
        p_demos,
        '$[*]' COLUMNS
        (
            DemoId CHAR(36) PATH '$.DemoId',
            LeetifyMatchId VARCHAR(100) PATH '$.LeetifyMatchId',
            MapName VARCHAR(100) PATH '$.MapName',
            GameType VARCHAR(100) PATH '$.GameType',
            TeamScore INT PATH '$.TeamScore',
            OpponentScore INT PATH '$.OpponentScore',
            IsWin TINYINT PATH '$.IsWin',
            ReplayUrl VARCHAR(2048) PATH '$.ReplayUrl',
            IsAvailable TINYINT PATH '$.IsAvailable',
            PlayedAtUtc DATETIME PATH '$.PlayedAtUtc'
        )
    ) AS selected
    ON DUPLICATE KEY UPDATE
        MapName = VALUES(MapName),
        GameType = VALUES(GameType),
        TeamScore = VALUES(TeamScore),
        OpponentScore = VALUES(OpponentScore),
        IsWin = VALUES(IsWin),
        ReplayUrl = VALUES(ReplayUrl),
        IsAvailable = VALUES(IsAvailable),
        PlayedAtUtc = VALUES(PlayedAtUtc),
        RefreshedUtc = UTC_TIMESTAMP();
END//

DELIMITER ;
