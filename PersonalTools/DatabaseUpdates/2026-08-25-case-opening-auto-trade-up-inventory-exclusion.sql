-- Feature 3 continued: hides held auto trade-up items from normal inventory. One join in
-- sp_case_opening_history_get covers the inventory tab, manual trade-up input selection, selling,
-- and auto trade-up input matching simultaneously, since they all read through GetCaseOpeningHistory.
-- sp_case_opening_inventory_capacity_get's three CaseOpeningHistory subqueries get the same
-- exclusion so held items don't count against capacity. sp_case_opening_trade_up_execute gains two
-- nullable params so the same transaction that inserts a recipe-fired output row also holds it,
-- meaning a fresh auto trade-up output is never briefly visible in normal inventory.
-- Kept in its own file, isolated from the recipe CRUD migration, since this touches the two
-- highest-blast-radius inventory queries in the app.

DELIMITER //

DROP PROCEDURE IF EXISTS sp_case_opening_history_get//
CREATE PROCEDURE sp_case_opening_history_get(IN p_user_id CHAR(36))
BEGIN
    SELECT h.OpeningId,h.UserId,h.CaseKey,h.SourceItemId,h.ItemName,h.MarketHashName,h.ImageUrl,h.Description,h.WeaponName,
        h.PatternName,h.PaintIndex,h.Phase,h.RarityKey,h.RarityName,h.RarityColor,h.Wear,h.IsStatTrak,h.IsRareSpecial,
        h.SupportsStatTrak,h.MinFloat,h.MaxFloat,h.FloatValue,h.PatternSeed,h.EstimatedPrice,h.OpenedUtc
    FROM CaseOpeningHistory h
    LEFT JOIN CaseOpeningTradeUpRecipeHoldings ho ON ho.OpeningId=h.OpeningId
    WHERE h.UserId=p_user_id AND ho.HoldingId IS NULL
    ORDER BY h.OpenedUtc DESC,h.OpeningId DESC;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_inventory_capacity_get//
CREATE PROCEDURE sp_case_opening_inventory_capacity_get(IN p_user_id CHAR(36))
BEGIN
    INSERT IGNORE INTO CaseOpeningInventoryCapacity(UserId,BaseCapacity,UpdatedUtc)
    VALUES(p_user_id,1000,UTC_TIMESTAMP(6));

    INSERT IGNORE INTO CaseOpeningUserInventoryUpgrades(UserId,UpdatedUtc)
    VALUES(p_user_id,UTC_TIMESTAMP(6));

    SELECT
        (SELECT COUNT(*) FROM CaseOpeningHistory h WHERE h.UserId=p_user_id AND NOT EXISTS (SELECT 1 FROM CaseOpeningTradeUpRecipeHoldings ho WHERE ho.OpeningId=h.OpeningId)) AS SkinSlots,
        (SELECT COALESCE(SUM(Quantity),0) FROM CaseOpeningOwnedCases WHERE UserId=p_user_id) AS CaseSlots,
        (SELECT COUNT(*) FROM CaseOpeningHistory h WHERE h.UserId=p_user_id AND NOT EXISTS (SELECT 1 FROM CaseOpeningTradeUpRecipeHoldings ho WHERE ho.OpeningId=h.OpeningId))+(SELECT COALESCE(SUM(Quantity),0) FROM CaseOpeningOwnedCases WHERE UserId=p_user_id) AS UsedSlots,
        c.BaseCapacity,
        (SELECT COUNT(*) FROM CaseOpeningStorageContainers WHERE UserId=p_user_id) AS StorageContainerCount,
        (SELECT COALESCE(SUM(AddedSlots),0) FROM CaseOpeningStorageContainers WHERE UserId=p_user_id) AS StorageSlots,
        u.BonusInventorySlots AS UpgradeSlots,
        c.BaseCapacity+(SELECT COALESCE(SUM(AddedSlots),0) FROM CaseOpeningStorageContainers WHERE UserId=p_user_id)+u.BonusInventorySlots AS TotalCapacity,
        GREATEST(c.BaseCapacity+(SELECT COALESCE(SUM(AddedSlots),0) FROM CaseOpeningStorageContainers WHERE UserId=p_user_id)+u.BonusInventorySlots-(SELECT COUNT(*) FROM CaseOpeningHistory h WHERE h.UserId=p_user_id AND NOT EXISTS (SELECT 1 FROM CaseOpeningTradeUpRecipeHoldings ho WHERE ho.OpeningId=h.OpeningId))-(SELECT COALESCE(SUM(Quantity),0) FROM CaseOpeningOwnedCases WHERE UserId=p_user_id),0) AS AvailableSlots
    FROM CaseOpeningInventoryCapacity c
    INNER JOIN CaseOpeningUserInventoryUpgrades u ON u.UserId=c.UserId
    WHERE c.UserId=p_user_id;
END//

DROP PROCEDURE IF EXISTS sp_case_opening_trade_up_execute//
CREATE PROCEDURE sp_case_opening_trade_up_execute(
    IN p_user_id CHAR(36),IN p_trade_up_id CHAR(36),IN p_opening_ids JSON,IN p_input_rarity_key VARCHAR(30),
    IN p_output_rarity_key VARCHAR(30),IN p_output_opening_id CHAR(36),IN p_output_case_key VARCHAR(80),
    IN p_output_source_item_id VARCHAR(160),IN p_output_item_name VARCHAR(255),IN p_output_market_hash_name VARCHAR(300),
    IN p_output_image_url VARCHAR(2048),IN p_output_description TEXT,IN p_output_weapon_name VARCHAR(100),
    IN p_output_pattern_name VARCHAR(150),IN p_output_paint_index VARCHAR(20),IN p_output_phase VARCHAR(50),
    IN p_output_rarity_name VARCHAR(80),IN p_output_rarity_color CHAR(7),IN p_output_wear VARCHAR(40),
    IN p_output_is_stat_trak TINYINT(1),IN p_output_is_rare_special TINYINT(1),IN p_output_supports_stat_trak TINYINT(1),
    IN p_output_min_float DECIMAL(9,6),IN p_output_max_float DECIMAL(9,6),IN p_output_float_value DECIMAL(9,6),
    IN p_output_pattern_seed INT,IN p_output_estimated_price DECIMAL(12,2),IN p_average_input_float DECIMAL(9,6),
    IN p_recipe_id CHAR(36),IN p_is_match TINYINT(1))
BEGIN
    DECLARE v_selected_count INT DEFAULT 0;
    DECLARE v_rarity_count INT DEFAULT 0;
    DECLARE v_actual_rarity_key VARCHAR(30) DEFAULT '';
    DECLARE v_stat_trak_count INT DEFAULT 0;
    DECLARE v_rare_special_count INT DEFAULT 0;
    DECLARE v_deleted_count INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

    START TRANSACTION;
    SELECT COUNT(*),COUNT(DISTINCT h.RarityKey),COALESCE(MAX(h.RarityKey),''),COUNT(DISTINCT h.IsStatTrak),COALESCE(SUM(h.IsRareSpecial),0)
    INTO v_selected_count,v_rarity_count,v_actual_rarity_key,v_stat_trak_count,v_rare_special_count
    FROM CaseOpeningHistory h
    INNER JOIN (SELECT DISTINCT OpeningId FROM JSON_TABLE(p_opening_ids,'$[*]' COLUMNS(OpeningId CHAR(36) PATH '$')) AS selectedIds) selectedIds
        ON BINARY selectedIds.OpeningId=BINARY h.OpeningId
    WHERE BINARY h.UserId=BINARY p_user_id;

    IF v_selected_count<>10 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Select exactly 10 inventory skins for a Trade Up Contract.';
    END IF;
    IF v_rarity_count<>1 OR v_actual_rarity_key<>p_input_rarity_key OR v_rare_special_count<>0 OR v_stat_trak_count<>1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='The selected skins are not a valid Trade Up Contract.';
    END IF;
    IF (p_input_rarity_key='mil-spec' AND p_output_rarity_key<>'restricted')
        OR (p_input_rarity_key='restricted' AND p_output_rarity_key<>'classified')
        OR (p_input_rarity_key='classified' AND p_output_rarity_key<>'covert')
        OR p_input_rarity_key NOT IN ('mil-spec','restricted','classified')
        OR p_output_is_rare_special<>0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='The selected Trade Up Contract rarity is not valid.';
    END IF;

    INSERT INTO CaseOpeningTradeUps(TradeUpId,UserId,InputRarityKey,OutputRarityKey,OutputOpeningId,OutputCaseKey,AverageInputFloat,CreatedUtc)
    VALUES(p_trade_up_id,p_user_id,p_input_rarity_key,p_output_rarity_key,p_output_opening_id,p_output_case_key,p_average_input_float,UTC_TIMESTAMP(6));

    INSERT INTO CaseOpeningTradeUpInputs(TradeUpInputId,TradeUpId,InputOpeningId,CaseKey,SourceItemId,RarityKey,FloatValue,IsStatTrak)
    SELECT UUID(),p_trade_up_id,h.OpeningId,h.CaseKey,h.SourceItemId,h.RarityKey,h.FloatValue,h.IsStatTrak
    FROM CaseOpeningHistory h
    INNER JOIN (SELECT DISTINCT OpeningId FROM JSON_TABLE(p_opening_ids,'$[*]' COLUMNS(OpeningId CHAR(36) PATH '$')) AS selectedIds) selectedIds
        ON BINARY selectedIds.OpeningId=BINARY h.OpeningId
    WHERE BINARY h.UserId=BINARY p_user_id;

    DELETE h FROM CaseOpeningHistory h
    INNER JOIN (SELECT DISTINCT OpeningId FROM JSON_TABLE(p_opening_ids,'$[*]' COLUMNS(OpeningId CHAR(36) PATH '$')) AS selectedIds) selectedIds
        ON BINARY selectedIds.OpeningId=BINARY h.OpeningId
    WHERE BINARY h.UserId=BINARY p_user_id;
    SET v_deleted_count=ROW_COUNT();
    IF v_deleted_count<>10 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='The selected inventory changed before the Trade Up Contract could finish.';
    END IF;

    INSERT INTO CaseOpeningHistory(OpeningId,UserId,CaseKey,SourceItemId,ItemName,MarketHashName,ImageUrl,Description,WeaponName,PatternName,PaintIndex,Phase,RarityKey,RarityName,RarityColor,Wear,IsStatTrak,IsRareSpecial,SupportsStatTrak,MinFloat,MaxFloat,FloatValue,PatternSeed,EstimatedPrice,OpenedUtc)
    VALUES(p_output_opening_id,p_user_id,p_output_case_key,p_output_source_item_id,p_output_item_name,p_output_market_hash_name,p_output_image_url,p_output_description,p_output_weapon_name,p_output_pattern_name,p_output_paint_index,p_output_phase,p_output_rarity_key,p_output_rarity_name,p_output_rarity_color,p_output_wear,p_output_is_stat_trak,p_output_is_rare_special,p_output_supports_stat_trak,p_output_min_float,p_output_max_float,p_output_float_value,p_output_pattern_seed,p_output_estimated_price,UTC_TIMESTAMP(6));

    INSERT IGNORE INTO CaseOpeningCollection(CollectionId,UserId,CaseKey,SourceItemId,FirstObtainedUtc)
    VALUES(UUID(),p_user_id,p_output_case_key,p_output_source_item_id,UTC_TIMESTAMP(6));

    -- Firing from a recipe holds the output in the same transaction that creates it, so it never
    -- briefly appears in normal inventory - not a race between two separate calls.
    IF p_recipe_id IS NOT NULL THEN
        INSERT INTO CaseOpeningTradeUpRecipeHoldings(HoldingId,RecipeId,UserId,OpeningId,IsMatch,CreatedUtc)
        VALUES(UUID(),p_recipe_id,p_user_id,p_output_opening_id,COALESCE(p_is_match,0),UTC_TIMESTAMP(6));
    END IF;

    COMMIT;
END//

DELIMITER ;
