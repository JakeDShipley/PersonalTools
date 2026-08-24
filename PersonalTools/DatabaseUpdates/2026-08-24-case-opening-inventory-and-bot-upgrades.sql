CREATE TABLE IF NOT EXISTS CaseOpeningUpgradeDefinitions (
    UpgradeKey VARCHAR(50) NOT NULL,
    Name VARCHAR(100) NOT NULL,
    Description VARCHAR(300) NOT NULL,
    Category VARCHAR(30) NOT NULL,
    CostStars INT NOT NULL,
    RequiredLevel INT NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (UpgradeKey)
) COLLATE='utf8mb4_unicode_ci' ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CaseOpeningUserInventoryUpgrades (
    UserId CHAR(36) NOT NULL,
    BulkSellLimit INT NOT NULL DEFAULT 100,
    AutoSellCovertUnlocked TINYINT(1) NOT NULL DEFAULT 0,
    AutoSellCovertEnabled TINYINT(1) NOT NULL DEFAULT 0,
    AutoSellClassifiedUnlocked TINYINT(1) NOT NULL DEFAULT 0,
    AutoSellClassifiedEnabled TINYINT(1) NOT NULL DEFAULT 0,
    AutoSellRestrictedUnlocked TINYINT(1) NOT NULL DEFAULT 0,
    AutoSellRestrictedEnabled TINYINT(1) NOT NULL DEFAULT 0,
    AutoSellMilSpecUnlocked TINYINT(1) NOT NULL DEFAULT 0,
    AutoSellMilSpecEnabled TINYINT(1) NOT NULL DEFAULT 0,
    PreserveStatTrak TINYINT(1) NOT NULL DEFAULT 1,
    UpdatedUtc DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    PRIMARY KEY (UserId),
    CONSTRAINT FK_CaseOpeningUserInventoryUpgrades_Users FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
) COLLATE='utf8mb4_unicode_ci' ENGINE=InnoDB;

INSERT INTO CaseOpeningUpgradeDefinitions (UpgradeKey,Name,Description,Category,CostStars,RequiredLevel,SortOrder,IsActive) VALUES
('bulk-sell-200','Bulk sale: 200','Raise the maximum items in one confirmed sale to 200.','bulk-sale',750,3,10,1),
('bulk-sell-300','Bulk sale: 300','Raise the maximum items in one confirmed sale to 300.','bulk-sale',1500,5,20,1),
('bulk-sell-400','Bulk sale: 400','Raise the maximum items in one confirmed sale to 400.','bulk-sale',2750,7,30,1),
('bulk-sell-500','Bulk sale: 500','Raise the maximum items in one confirmed sale to 500.','bulk-sale',4500,9,40,1),
('auto-sell-covert','Auto-sell Covert','Automatically convert red drops into Stars.','auto-sell',600,3,100,1),
('auto-sell-classified','Auto-sell Classified','Automatically convert pink drops into Stars.','auto-sell',1250,5,110,1),
('auto-sell-restricted','Auto-sell Restricted','Automatically convert purple drops into Stars.','auto-sell',2500,7,120,1),
('auto-sell-mil-spec','Auto-sell Mil-Spec','Automatically convert the most common blue drops into Stars.','auto-sell',5000,10,130,1)
ON DUPLICATE KEY UPDATE Name=VALUES(Name),Description=VALUES(Description),Category=VALUES(Category),CostStars=VALUES(CostStars),RequiredLevel=VALUES(RequiredLevel),SortOrder=VALUES(SortOrder),IsActive=VALUES(IsActive);

ALTER TABLE CaseOpeningBotServers ADD COLUMN IF NOT EXISTS SpeedLevel TINYINT UNSIGNED NOT NULL DEFAULT 0 AFTER UserId;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_collection_item_exists//
CREATE PROCEDURE sp_case_opening_collection_item_exists(IN p_user_id CHAR(36),IN p_case_key VARCHAR(80),IN p_source_item_id VARCHAR(160))
BEGIN
    SELECT COUNT(*) FROM CaseOpeningCollection WHERE UserId=p_user_id AND CaseKey=p_case_key AND SourceItemId=p_source_item_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_inventory_upgrades_get//
CREATE PROCEDURE sp_case_opening_inventory_upgrades_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades (UserId) VALUES (p_user_id);
    SELECT * FROM CaseOpeningUserInventoryUpgrades WHERE UserId=p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrade_definitions_get//
CREATE PROCEDURE sp_case_opening_upgrade_definitions_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades (UserId) VALUES (p_user_id);
    SELECT d.UpgradeKey,d.Name,d.Description,d.Category,d.CostStars,d.RequiredLevel,d.SortOrder,
        CASE d.UpgradeKey
            WHEN 'bulk-sell-200' THEN u.BulkSellLimit>=200 WHEN 'bulk-sell-300' THEN u.BulkSellLimit>=300
            WHEN 'bulk-sell-400' THEN u.BulkSellLimit>=400 WHEN 'bulk-sell-500' THEN u.BulkSellLimit>=500
            WHEN 'auto-sell-covert' THEN u.AutoSellCovertUnlocked WHEN 'auto-sell-classified' THEN u.AutoSellClassifiedUnlocked
            WHEN 'auto-sell-restricted' THEN u.AutoSellRestrictedUnlocked WHEN 'auto-sell-mil-spec' THEN u.AutoSellMilSpecUnlocked ELSE 0 END AS IsUnlocked
    FROM CaseOpeningUpgradeDefinitions d CROSS JOIN CaseOpeningUserInventoryUpgrades u
    WHERE u.UserId=p_user_id AND d.IsActive=1 ORDER BY d.SortOrder;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_inventory_upgrade_unlock//
CREATE PROCEDURE sp_case_opening_inventory_upgrade_unlock(IN p_user_id CHAR(36),IN p_upgrade_key VARCHAR(50),IN p_cost INT)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;
    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades (UserId) VALUES (p_user_id);
    UPDATE CaseOpeningProgress SET Stars=Stars-p_cost,UpdatedUtc=UTC_TIMESTAMP() WHERE UserId=p_user_id AND Stars>=p_cost;
    IF ROW_COUNT()=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='There are not enough Stars to purchase this upgrade.'; END IF;
    UPDATE CaseOpeningUserInventoryUpgrades SET
        BulkSellLimit=CASE p_upgrade_key WHEN 'bulk-sell-200' THEN GREATEST(BulkSellLimit,200) WHEN 'bulk-sell-300' THEN GREATEST(BulkSellLimit,300) WHEN 'bulk-sell-400' THEN GREATEST(BulkSellLimit,400) WHEN 'bulk-sell-500' THEN GREATEST(BulkSellLimit,500) ELSE BulkSellLimit END,
        AutoSellCovertUnlocked=IF(p_upgrade_key='auto-sell-covert',1,AutoSellCovertUnlocked),
        AutoSellClassifiedUnlocked=IF(p_upgrade_key='auto-sell-classified',1,AutoSellClassifiedUnlocked),
        AutoSellRestrictedUnlocked=IF(p_upgrade_key='auto-sell-restricted',1,AutoSellRestrictedUnlocked),
        AutoSellMilSpecUnlocked=IF(p_upgrade_key='auto-sell-mil-spec',1,AutoSellMilSpecUnlocked),UpdatedUtc=UTC_TIMESTAMP(6)
    WHERE UserId=p_user_id;
    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_auto_sell_set//
CREATE PROCEDURE sp_case_opening_auto_sell_set(IN p_user_id CHAR(36),IN p_rarity_key VARCHAR(30),IN p_enabled TINYINT,IN p_preserve_stat_trak TINYINT)
BEGIN
    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades (UserId) VALUES (p_user_id);
    UPDATE CaseOpeningUserInventoryUpgrades SET
        AutoSellCovertEnabled=IF(p_rarity_key='covert' AND AutoSellCovertUnlocked=1,p_enabled,AutoSellCovertEnabled),
        AutoSellClassifiedEnabled=IF(p_rarity_key='classified' AND AutoSellClassifiedUnlocked=1,p_enabled,AutoSellClassifiedEnabled),
        AutoSellRestrictedEnabled=IF(p_rarity_key='restricted' AND AutoSellRestrictedUnlocked=1,p_enabled,AutoSellRestrictedEnabled),
        AutoSellMilSpecEnabled=IF(p_rarity_key='mil-spec' AND AutoSellMilSpecUnlocked=1,p_enabled,AutoSellMilSpecEnabled),
        PreserveStatTrak=p_preserve_stat_trak,UpdatedUtc=UTC_TIMESTAMP(6) WHERE UserId=p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_bot_servers_get//
CREATE PROCEDURE sp_case_opening_bot_servers_get(IN p_user_id CHAR(36))
BEGIN SELECT ServerId,UserId,SpeedLevel,CreatedUtc FROM CaseOpeningBotServers WHERE UserId=p_user_id ORDER BY CreatedUtc,ServerId; END//

DROP PROCEDURE IF EXISTS sp_case_opening_bot_server_speed_upgrade//
CREATE PROCEDURE sp_case_opening_bot_server_speed_upgrade(IN p_user_id CHAR(36),IN p_server_id CHAR(36),IN p_cost INT,IN p_maximum_level INT)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;
    UPDATE CaseOpeningBotServers SET SpeedLevel=SpeedLevel+1 WHERE ServerId=p_server_id AND UserId=p_user_id AND SpeedLevel<p_maximum_level;
    IF ROW_COUNT()=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='The bot server is already at maximum speed or could not be found.'; END IF;
    UPDATE CaseOpeningProgress SET Stars=Stars-p_cost,UpdatedUtc=UTC_TIMESTAMP() WHERE UserId=p_user_id AND Stars>=p_cost;
    IF ROW_COUNT()=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='There are not enough Stars for this speed upgrade.'; END IF;
    COMMIT;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_bot_cycle_claim//
CREATE PROCEDURE sp_case_opening_bot_cycle_claim(IN p_user_id CHAR(36),IN p_bot_id CHAR(36))
BEGIN
    DECLARE v_interval INT DEFAULT 12; DECLARE v_level INT DEFAULT 0; DECLARE v_effective INT DEFAULT 12;
    SELECT g.BotOpeningIntervalSeconds,s.SpeedLevel INTO v_interval,v_level FROM CaseOpeningBots b INNER JOIN CaseOpeningBotServers s ON s.ServerId=b.ServerId CROSS JOIN CaseOpeningGameSettings g WHERE g.Id=1 AND b.BotId=p_bot_id AND b.UserId=p_user_id;
    SET v_effective=GREATEST(1,CEILING(v_interval*(0.5/(0.5+(LEAST(v_level,20)*0.025)))));
    UPDATE CaseOpeningBots SET LastOpenedUtc=UTC_TIMESTAMP(6) WHERE BotId=p_bot_id AND UserId=p_user_id AND (LastOpenedUtc IS NULL OR LastOpenedUtc<=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL GREATEST(1,v_effective-1) SECOND));
    SELECT ROW_COUNT();
END//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrade_settings_get//
CREATE PROCEDURE sp_case_opening_upgrade_settings_get()
BEGIN
    SELECT UpgradeKey,Name,Description,Category,CostStars,RequiredLevel,SortOrder,0 AS IsUnlocked
    FROM CaseOpeningUpgradeDefinitions
    WHERE IsActive=1
    ORDER BY SortOrder,UpgradeKey;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_upgrade_settings_set//
CREATE PROCEDURE sp_case_opening_upgrade_settings_set(IN p_upgrade_key VARCHAR(50),IN p_cost_stars INT,IN p_required_level INT)
BEGIN
    UPDATE CaseOpeningUpgradeDefinitions
    SET CostStars=GREATEST(0,p_cost_stars),RequiredLevel=GREATEST(0,p_required_level)
    WHERE UpgradeKey=p_upgrade_key AND IsActive=1;

    IF ROW_COUNT()=0 AND NOT EXISTS(SELECT 1 FROM CaseOpeningUpgradeDefinitions WHERE UpgradeKey=p_upgrade_key AND IsActive=1) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='The inventory upgrade could not be found.';
    END IF;
END//

DELIMITER ;
