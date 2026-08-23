USE PersonalTools;

CREATE TABLE IF NOT EXISTS CaseOpeningBotServers
(
    ServerId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    CreatedUtc DATETIME(6) NOT NULL,
    PRIMARY KEY (ServerId),
    KEY IX_CaseOpeningBotServers_User (UserId, CreatedUtc),
    CONSTRAINT FK_CaseOpeningBotServers_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CaseOpeningBots
(
    BotId CHAR(36) NOT NULL,
    ServerId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    CreatedUtc DATETIME(6) NOT NULL,
    LastOpenedUtc DATETIME(6) NULL,
    PRIMARY KEY (BotId),
    KEY IX_CaseOpeningBots_UserServer (UserId, ServerId),
    KEY IX_CaseOpeningBots_Cycle (BotId, UserId, LastOpenedUtc),
    CONSTRAINT FK_CaseOpeningBots_Servers
        FOREIGN KEY (ServerId) REFERENCES CaseOpeningBotServers (ServerId) ON DELETE CASCADE,
    CONSTRAINT FK_CaseOpeningBots_Users
        FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
)
COLLATE='utf8mb4_unicode_ci'
ENGINE=InnoDB;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_bot_servers_get//
CREATE PROCEDURE sp_case_opening_bot_servers_get(IN p_user_id CHAR(36))
BEGIN
    SELECT ServerId, UserId, CreatedUtc
    FROM CaseOpeningBotServers
    WHERE UserId = p_user_id
    ORDER BY CreatedUtc, ServerId;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_bots_get//
CREATE PROCEDURE sp_case_opening_bots_get(IN p_user_id CHAR(36))
BEGIN
    SELECT BotId, ServerId, UserId, CreatedUtc, LastOpenedUtc
    FROM CaseOpeningBots
    WHERE UserId = p_user_id
    ORDER BY CreatedUtc, BotId;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_bot_server_purchase//
CREATE PROCEDURE sp_case_opening_bot_server_purchase
(
    IN p_user_id CHAR(36),
    IN p_server_id CHAR(36),
    IN p_cost INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    INSERT IGNORE INTO CaseOpeningProgress
    (
        UserId, Stars, SkipAnimationUnlocked, MultiOpenUnlocked, MultiOpenLevel, UpdatedUtc
    )
    VALUES
    (
        p_user_id, 0, 0, 0, 0, UTC_TIMESTAMP()
    );

    UPDATE CaseOpeningProgress
    SET Stars = Stars - p_cost, UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost;

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'There are not enough Stars to purchase this bot server.';
    END IF;

    INSERT INTO CaseOpeningBotServers (ServerId, UserId, CreatedUtc)
    VALUES (p_server_id, p_user_id, UTC_TIMESTAMP(6));

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_bot_purchase//
CREATE PROCEDURE sp_case_opening_bot_purchase
(
    IN p_user_id CHAR(36),
    IN p_server_id CHAR(36),
    IN p_bot_id CHAR(36),
    IN p_cost INT
)
BEGIN
    DECLARE v_bot_count INT DEFAULT 0;
    DECLARE v_server_found CHAR(36) DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    SELECT ServerId
    INTO v_server_found
    FROM CaseOpeningBotServers
    WHERE ServerId = p_server_id
      AND UserId = p_user_id
    FOR UPDATE;

    IF v_server_found IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The selected bot server could not be found.';
    END IF;

    SELECT COUNT(*)
    INTO v_bot_count
    FROM CaseOpeningBots
    WHERE ServerId = p_server_id
      AND UserId = p_user_id;

    IF v_bot_count >= 4 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'This bot server is full. Purchase another server to add a bot.';
    END IF;

    UPDATE CaseOpeningProgress
    SET Stars = Stars - p_cost, UpdatedUtc = UTC_TIMESTAMP()
    WHERE UserId = p_user_id
      AND Stars >= p_cost;

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'There are not enough Stars to purchase this bot.';
    END IF;

    INSERT INTO CaseOpeningBots
    (
        BotId, ServerId, UserId, CreatedUtc, LastOpenedUtc
    )
    VALUES
    (
        p_bot_id, p_server_id, p_user_id, UTC_TIMESTAMP(6), NULL
    );

    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_bot_cycle_claim//
CREATE PROCEDURE sp_case_opening_bot_cycle_claim
(
    IN p_user_id CHAR(36),
    IN p_bot_id CHAR(36)
)
BEGIN
    UPDATE CaseOpeningBots
    SET LastOpenedUtc = UTC_TIMESTAMP(6)
    WHERE BotId = p_bot_id
      AND UserId = p_user_id
      AND
      (
          LastOpenedUtc IS NULL
          OR LastOpenedUtc <= DATE_SUB(UTC_TIMESTAMP(6), INTERVAL 12 SECOND)
      );

    SELECT ROW_COUNT();
END//

DELIMITER ;
