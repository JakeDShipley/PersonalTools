-- Fixes "reset as new player" leaving inventory-upgrade state behind: CaseOpeningUserInventoryUpgrades
-- (auto-buy unlock/rule slots, bonus inventory slots, bulk-sell limit, auto-sell settings) was never
-- touched by sp_case_opening_reset_dev, so all of it survived a reset. Deleting the row is enough -
-- every read path already does INSERT IGNORE ... VALUES (UserId) first, which repopulates it at
-- column defaults on next access.

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_reset_dev//
CREATE PROCEDURE sp_case_opening_reset_dev(IN p_user_id CHAR(36))
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;
    DELETE FROM CaseOpeningUserAchievements WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningCompletedRarities WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningCompletedCollections WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningPlayerStats WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningBots WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningBotServers WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningTradeUps WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningAutoBuyRules WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningUserInventoryUpgrades WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningCollection WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningHistory WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningStorageContainers WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningInventoryCapacity WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningOwnedCases WHERE UserId = p_user_id;
    DELETE FROM CaseOpeningUnlockedCases WHERE UserId = p_user_id;

    INSERT INTO CaseOpeningUnlockedCases(UserId, CaseKey, UnlockedUtc)
    VALUES(p_user_id, 'kilowatt', UTC_TIMESTAMP());

    INSERT INTO CaseOpeningOwnedCases(UserId, CaseKey, Quantity, UpdatedUtc)
    VALUES(p_user_id, 'kilowatt', 25, UTC_TIMESTAMP(6));

    INSERT INTO CaseOpeningInventoryCapacity(UserId, BaseCapacity, UpdatedUtc)
    VALUES(p_user_id, 1000, UTC_TIMESTAMP(6));

    INSERT INTO CaseOpeningProgress(UserId, Stars, Xp, SkipAnimationUnlocked, MultiOpenLevel, OpenSpeedLevel, UpdatedUtc)
    VALUES(p_user_id, 0, 0, 0, 0, 0, UTC_TIMESTAMP())
    ON DUPLICATE KEY UPDATE
        Stars = 0,
        Xp = 0,
        SkipAnimationUnlocked = 0,
        MultiOpenLevel = 0,
        OpenSpeedLevel = 0,
        UpdatedUtc = UTC_TIMESTAMP();

    COMMIT;
END//

DELIMITER ;
