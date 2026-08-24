USE PersonalTools;

ALTER TABLE CaseOpeningCaseSettings
    ADD COLUMN IF NOT EXISTS PurchaseCostStars INT NOT NULL DEFAULT 1 AFTER UnlockCostStars;

UPDATE CaseOpeningCaseSettings
SET PurchaseCostStars = GREATEST(1, CEILING(UnlockCostStars / 10))
WHERE PurchaseCostStars < 1;

ALTER TABLE CaseOpeningGameSettings
    ADD COLUMN IF NOT EXISTS StorageContainerBaseCostStars INT NOT NULL DEFAULT 500 AFTER BotCostGrowthRate,
    ADD COLUMN IF NOT EXISTS StorageContainerCostIncrementStars INT NOT NULL DEFAULT 250 AFTER StorageContainerBaseCostStars,
    ADD COLUMN IF NOT EXISTS StorageContainerSlots INT NOT NULL DEFAULT 1000 AFTER StorageContainerCostIncrementStars,
    ADD COLUMN IF NOT EXISTS MaximumStorageContainers INT NOT NULL DEFAULT 10 AFTER StorageContainerSlots;

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_game_settings_get//
CREATE PROCEDURE sp_case_opening_game_settings_get()
BEGIN
    SELECT XpPerCaseOpen,SkipAnimationCostStars,SkipAnimationXpRequirement,MultiOpenCostStars,MultiOpenXpRequirement,
           MaximumMultiOpenLevel,MaximumOpenQuantity,BotOpeningIntervalSeconds,BotServerBaseCostStars,
           BotServerCostIncrementStars,BotBaseCostStars,BotCostGrowthRate,StorageContainerBaseCostStars,
           StorageContainerCostIncrementStars,StorageContainerSlots,MaximumStorageContainers
    FROM CaseOpeningGameSettings
    WHERE Id=1;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_game_settings_set//
CREATE PROCEDURE sp_case_opening_game_settings_set(
    IN p_xp_per_case_open INT, IN p_skip_animation_cost_stars INT, IN p_skip_animation_xp_requirement INT,
    IN p_multi_open_cost_stars INT, IN p_multi_open_xp_requirement INT, IN p_maximum_multi_open_level TINYINT UNSIGNED,
    IN p_maximum_open_quantity TINYINT UNSIGNED, IN p_bot_opening_interval_seconds INT, IN p_bot_server_base_cost_stars INT,
    IN p_bot_server_cost_increment_stars INT, IN p_bot_base_cost_stars INT, IN p_bot_cost_growth_rate DECIMAL(5,3),
    IN p_storage_container_base_cost_stars INT, IN p_storage_container_cost_increment_stars INT,
    IN p_storage_container_slots INT, IN p_maximum_storage_containers INT)
BEGIN
    UPDATE CaseOpeningGameSettings
    SET XpPerCaseOpen=p_xp_per_case_open,SkipAnimationCostStars=p_skip_animation_cost_stars,
        SkipAnimationXpRequirement=p_skip_animation_xp_requirement,MultiOpenCostStars=p_multi_open_cost_stars,
        MultiOpenXpRequirement=p_multi_open_xp_requirement,MaximumMultiOpenLevel=p_maximum_multi_open_level,
        MaximumOpenQuantity=p_maximum_open_quantity,BotOpeningIntervalSeconds=p_bot_opening_interval_seconds,
        BotServerBaseCostStars=p_bot_server_base_cost_stars,BotServerCostIncrementStars=p_bot_server_cost_increment_stars,
        BotBaseCostStars=p_bot_base_cost_stars,BotCostGrowthRate=p_bot_cost_growth_rate,
        StorageContainerBaseCostStars=p_storage_container_base_cost_stars,
        StorageContainerCostIncrementStars=p_storage_container_cost_increment_stars,
        StorageContainerSlots=p_storage_container_slots,MaximumStorageContainers=p_maximum_storage_containers,
        UpdatedUtc=UTC_TIMESTAMP()
    WHERE Id=1;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_case_settings_get_all//
CREATE PROCEDURE sp_case_opening_case_settings_get_all()
BEGIN
    SELECT CaseKey,UnlockCostStars,PurchaseCostStars,XpRequirement
    FROM CaseOpeningCaseSettings
    ORDER BY UnlockCostStars,CaseKey;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_case_settings_set//
CREATE PROCEDURE sp_case_opening_case_settings_set(IN p_case_key VARCHAR(80), IN p_unlock_cost_stars INT, IN p_purchase_cost_stars INT, IN p_xp_requirement INT)
BEGIN
    INSERT INTO CaseOpeningCaseSettings(CaseKey,UnlockCostStars,PurchaseCostStars,XpRequirement,UpdatedUtc)
    VALUES(p_case_key,p_unlock_cost_stars,p_purchase_cost_stars,p_xp_requirement,UTC_TIMESTAMP())
    ON DUPLICATE KEY UPDATE UnlockCostStars=VALUES(UnlockCostStars),PurchaseCostStars=VALUES(PurchaseCostStars),
        XpRequirement=VALUES(XpRequirement),UpdatedUtc=UTC_TIMESTAMP();
END//

DROP PROCEDURE IF EXISTS sp_case_opening_cases_purchase//
CREATE PROCEDURE sp_case_opening_cases_purchase(IN p_user_id CHAR(36), IN p_case_key VARCHAR(80), IN p_quantity INT, IN p_purchase_cost_stars INT)
BEGIN
    DECLARE v_total_cost INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    SET v_total_cost=p_quantity*p_purchase_cost_stars;
    START TRANSACTION;
    IF p_quantity<1 OR p_quantity>500 OR p_purchase_cost_stars<0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Buy between 1 and 500 cases at a time.'; END IF;
    IF NOT EXISTS(SELECT 1 FROM CaseOpeningUnlockedCases WHERE UserId=p_user_id AND CaseKey=p_case_key) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unlock this case before buying copies from the Shop.';
    END IF;
    INSERT IGNORE INTO CaseOpeningProgress(UserId,Stars,Xp,SkipAnimationUnlocked,MultiOpenLevel,UpdatedUtc) VALUES(p_user_id,0,0,0,0,UTC_TIMESTAMP());
    UPDATE CaseOpeningProgress SET Stars=Stars-v_total_cost,UpdatedUtc=UTC_TIMESTAMP() WHERE UserId=p_user_id AND Stars>=v_total_cost;
    IF ROW_COUNT()=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='There are not enough Stars to buy these cases.'; END IF;
    INSERT INTO CaseOpeningOwnedCases(UserId,CaseKey,Quantity,UpdatedUtc) VALUES(p_user_id,p_case_key,p_quantity,UTC_TIMESTAMP(6))
    ON DUPLICATE KEY UPDATE Quantity=Quantity+VALUES(Quantity),UpdatedUtc=UTC_TIMESTAMP(6);
    COMMIT;
    SELECT p_case_key AS CaseKey,p_quantity AS PurchasedQuantity,Quantity AS OwnedQuantity,v_total_cost AS StarsSpent,
           (SELECT Stars FROM CaseOpeningProgress WHERE UserId=p_user_id) AS StarsBalance
    FROM CaseOpeningOwnedCases WHERE UserId=p_user_id AND CaseKey=p_case_key;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_storage_container_purchase//
CREATE PROCEDURE sp_case_opening_storage_container_purchase(IN p_user_id CHAR(36), IN p_storage_container_id CHAR(36), IN p_cost INT, IN p_slots INT, IN p_maximum_containers INT)
BEGIN
    DECLARE v_count INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;
    IF p_cost<0 OR p_slots<1 OR p_maximum_containers<0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='The storage configuration is not valid.'; END IF;
    INSERT IGNORE INTO CaseOpeningInventoryCapacity(UserId,BaseCapacity,UpdatedUtc) VALUES(p_user_id,1000,UTC_TIMESTAMP(6));
    SELECT COUNT(*) INTO v_count FROM CaseOpeningStorageContainers WHERE UserId=p_user_id;
    IF v_count>=p_maximum_containers THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='You already own the maximum number of storage containers.'; END IF;
    INSERT IGNORE INTO CaseOpeningProgress(UserId,Stars,Xp,SkipAnimationUnlocked,MultiOpenLevel,UpdatedUtc) VALUES(p_user_id,0,0,0,0,UTC_TIMESTAMP());
    UPDATE CaseOpeningProgress SET Stars=Stars-p_cost,UpdatedUtc=UTC_TIMESTAMP() WHERE UserId=p_user_id AND Stars>=p_cost;
    IF ROW_COUNT()=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='There are not enough Stars to purchase this storage container.'; END IF;
    INSERT INTO CaseOpeningStorageContainers(StorageContainerId,UserId,AddedSlots,AcquiredUtc) VALUES(p_storage_container_id,p_user_id,p_slots,UTC_TIMESTAMP(6));
    COMMIT;
    SELECT v_count+1 AS StorageContainerCount,p_slots AS AddedSlots,
           (SELECT BaseCapacity FROM CaseOpeningInventoryCapacity WHERE UserId=p_user_id)+(v_count+1)*p_slots AS TotalCapacity,
           p_cost AS StarsSpent,(SELECT Stars FROM CaseOpeningProgress WHERE UserId=p_user_id) AS StarsBalance;
END//

DELIMITER ;
